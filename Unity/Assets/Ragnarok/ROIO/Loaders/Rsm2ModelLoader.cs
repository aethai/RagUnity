using ROIO.Models.FileTypes;
using ROIO.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROIO.Loaders {
    /// <summary>
    /// RSM2 models (".rsm2") as the client's C3dModelResV2 (vtable 0x00f83388) reads them. Load (vf05 0x6cbe30)
    /// checks "GRSM", reads the major byte (+0x110, not checked) and the minor byte, and hands the rest to
    /// FUN_006d38b0 (x.1), FUN_006d4500 (x.2) or FUN_006d52c0 (x.3); any other minor reads nothing (an empty
    /// model, refused here). Strings are an int length and that many bytes; all reads go through FUN_0055a960,
    /// which reads nothing past the end of the data (the trailing volume box count is often missing).
    ///
    /// <code>
    /// int animLen (frames, +0x160), int shadeType (+0x14c), byte alpha (+0x150), float fps (+0x15c)
    /// x.1 / x.2: int textureCount, textureCount x string          (model texture table)
    /// int rootCount, rootCount x string                          (+0x11c)
    /// int nodeCount, nodeCount x node (C3dNodeResV2, 0xb4 bytes):
    ///   string name; int length, the parent name only when length > 0
    ///   int textureCount; x.1 / x.2: textureCount x int (table index); x.3: textureCount x string (GetTexture)
    ///   float[12] matrix (3x3 rows, translation; the node's model-space transform, +0x58)
    ///   int vertexCount, vertexCount x float[3]
    ///   int tvertexCount, tvertexCount x (uint color, float u, float v)
    ///   int faceCount, faceCount x face: x.1 24 bytes; x.2+ an int length, then that many bytes:
    ///     ushort vertex[3], ushort tvertex[3], ushort texture slot, ushort padding, int twoSided,
    ///     (length - 20) / 4 smoothing groups
    ///   int n, n x (int frame, float x, y, z, float unused)       scale keys (FUN_0051f290, +0x88)
    ///   int n, n x (int frame, float x, y, z, w)                  rotation keys (FUN_0051f190, +0x94)
    ///   int n, n x (int frame, float x, y, z, float unused)       position keys (FUN_0051f0a0, +0xa0)
    ///   x.3: int n, n x (int texture slot, FUN_0051f310: int typeCount, typeCount x
    ///        (int type, int keyCount, keyCount x (int frame, float value)))
    /// int volumeBoxCount (optional), volumeBoxCount x 0xe8 bytes
    /// </code>
    ///
    /// Parents are linked by name to the first node of that name (FUN_006d2ee0); the actor instantiates the
    /// root names' nodes and their children (FUN_006d3070, C3dNodeV2::vf01 0x9e81e0), so other nodes are not
    /// drawn and are left out here.
    /// </summary>
    public static class Rsm2ModelLoader {
        public const string Extension = "rsm2";

        public static RSM Load(MemoryStreamReader data) {
            var header = RoLoaderValidation.ReadBinaryString(data, 4, "Rsm2ModelLoader.Load header");
            if (header != RSM.Header) {
                throw new Exception("Rsm2ModelLoader.Load: Header (" + header + ") is not \"GRSM\"");
            }

            byte major = RoLoaderValidation.ReadByte(data, "Rsm2ModelLoader.Load major version");
            byte minor = RoLoaderValidation.ReadByte(data, "Rsm2ModelLoader.Load minor version");
            if (minor < 1 || minor > 3) {
                throw new Exception($"Rsm2ModelLoader.Load: Version {major}.{minor} is not read by the client (C3dModelResV2 reads x.1, x.2 and x.3).");
            }

            var rsm = new RSM {
                version = major + "." + minor,
                isRsm2 = true,
                instances = new List<RSW.ModelDescriptor>(),
                volumeBoxes = new RSM.VolumeBox[0],
                posKeyframes = new RSM.PositionKeyframe[0],
                box = new RSM.Box()
            };

            rsm.animLen = ReadInt(data);
            rsm.shadeType = (RSM.SHADING) ReadInt(data);
            rsm.alpha = ReadByte(data) / 255f;
            rsm.fps = ReadFloat(data);

            var textures = new List<string>();
            var textureIndices = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] tableTextures = null;
            if (minor <= 2) {
                int count = ReadCount(data, "texture");
                tableTextures = new string[count];
                for (int i = 0; i < count; i++) {
                    tableTextures[i] = ReadString(data, "texture name");
                }
            }

            int rootCount = ReadCount(data, "root node");
            rsm.rootNames = new string[rootCount];
            for (int i = 0; i < rootCount; i++) {
                rsm.rootNames[i] = ReadString(data, "root node name");
            }

            int nodeCount = RoLoaderValidation.RequireCount(
                ReadInt(data),
                RoLoaderValidation.MaxModelNodeCount,
                "node",
                "Rsm2ModelLoader.Load");
            var nodes = new RSM.Node[nodeCount];
            for (int i = 0; i < nodeCount; i++) {
                nodes[i] = LoadNode(rsm, data, minor, tableTextures, textures, textureIndices);
            }

            // Volume boxes: 0xe8 raw bytes each; the count is often missing (then it stays 0).
            if (data.Position + 4 <= data.Length) {
                int volumeBoxCount = ReadCount(data, "volume box");
                RoLoaderValidation.RequireRemaining(data, (long) volumeBoxCount * 0xe8, "Rsm2ModelLoader.Load volume boxes");
                data.Seek((long) volumeBoxCount * 0xe8, System.IO.SeekOrigin.Current);
            }

            rsm.textures = textures.ToArray();
            LinkNodes(rsm, nodes);
            CalcFramePose(rsm);
            return rsm;
        }

        public static bool TryLoad(MemoryStreamReader data, out RSM rsm, out string error) {
            try {
                rsm = Load(data);
                error = string.Empty;
                return true;
            } catch (Exception ex) {
                rsm = null;
                error = ex.Message;
                return false;
            }
        }

        private static RSM.Node LoadNode(
            RSM rsm,
            MemoryStreamReader data,
            byte minor,
            string[] tableTextures,
            List<string> textures,
            Dictionary<string, int> textureIndices) {
            var node = new RSM.Node {
                model = rsm,
                name = ReadString(data, "node name")
            };

            int parentLength = ReadInt(data);
            node.parentName = parentLength > 0 ? ReadString(data, parentLength, "parent name") : string.Empty;
            string context = $"Rsm2ModelLoader.LoadNode {node.name}";

            int textureCount = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "node texture", context);
            node.textures = new long[textureCount];
            for (int i = 0; i < textureCount; i++) {
                string textureName;
                if (minor <= 2) {
                    int index = ReadInt(data);
                    if (index < 0 || index >= tableTextures.Length) {
                        throw new Exception($"{context}: Texture slot {i} references model texture {index}, but only {tableTextures.Length} exist.");
                    }

                    textureName = tableTextures[index];
                } else {
                    textureName = ReadString(data, "node texture name");
                }

                if (!textureIndices.TryGetValue(textureName, out int textureIndex)) {
                    textureIndex = textures.Count;
                    textures.Add(textureName);
                    textureIndices.Add(textureName, textureIndex);
                }

                node.textures[i] = textureIndex;
            }

            RoLoaderValidation.RequireRemaining(data, 48, context + " matrix");
            node.rsm2Local = new float[Rsm2Pose.MatrixLength];
            for (int i = 0; i < Rsm2Pose.MatrixLength; i++) {
                node.rsm2Local[i] = data.ReadFloat();
            }

            // The RSM1 transform fields stay neutral (Node.Compile does not use them for RSM2).
            node.mat3 = new[] { new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1) };
            node.offset = Vector3.zero;
            node.pos = new Vector3(node.rsm2Local[9], node.rsm2Local[10], node.rsm2Local[11]);
            node.rotAxis = Vector3.zero;
            node.scale = Vector3.one;

            int vertexCount = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "vertex", context);
            RoLoaderValidation.RequireRemaining(data, (long) vertexCount * 12, context + " vertices");
            node.vertices = new List<Vector3>(vertexCount);
            for (int i = 0; i < vertexCount; i++) {
                node.vertices.Add(new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat()));
            }

            // Texture vertices: color, u, v. The client draws u, v as they are (scaled to the texture's
            // power-of-two surface, FUN_009ec8f0), without RSM1's 0.98 / 0.01 inset.
            int tvertexCount = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "texture vertex", context);
            RoLoaderValidation.RequireRemaining(data, (long) tvertexCount * 12, context + " texture vertices");
            node.tVertices = new float[checked(tvertexCount * 6)];
            for (int i = 0; i < tvertexCount; i++) {
                node.tVertices[i * 6 + 0] = data.ReadByte() / 255f;
                node.tVertices[i * 6 + 1] = data.ReadByte() / 255f;
                node.tVertices[i * 6 + 2] = data.ReadByte() / 255f;
                node.tVertices[i * 6 + 3] = data.ReadByte() / 255f;
                node.tVertices[i * 6 + 4] = data.ReadFloat();
                node.tVertices[i * 6 + 5] = data.ReadFloat();
            }

            int faceCount = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "face", context);
            node.faces = new RSM.Face[faceCount];
            for (int i = 0; i < faceCount; i++) {
                int length = minor == 1 ? 24 : ReadInt(data);
                if (length < 20) {
                    throw new Exception($"{context}: Face {i} is {length} bytes; a face has at least 20.");
                }

                RoLoaderValidation.RequireRemaining(data, length, context + " face");
                long faceEnd = data.Position + length;
                var face = new RSM.Face {
                    vertidx = new Vector3Int(data.ReadUShort(), data.ReadUShort(), data.ReadUShort()),
                    tvertidx = new Vector3Int(data.ReadUShort(), data.ReadUShort(), data.ReadUShort()),
                    texid = data.ReadUShort(),
                    padding = data.ReadUShort(),
                    // C3dNodeV2::vf03 draws a face from both sides when this int is exactly 1.
                    twoSided = data.ReadInt() == 1 ? 1 : 0,
                    smoothGroup = length >= 24 ? data.ReadInt() : 0
                };
                data.Position = faceEnd;
                ValidateFace(node, face, i, vertexCount, tvertexCount, textureCount, context);
                // The client picks the face's texture as slot % textureCount (C3dNodeV2::vf03).
                face.texid = (ushort) (face.texid % textureCount);
                node.faces[i] = face;
            }

            node.rsm2ScaleKeys = ReadVectorKeys(data, context + " scale keys");
            node.rsm2RotationKeys = ReadRotationKeys(data, context + " rotation keys");
            node.rsm2PositionKeys = ReadVectorKeys(data, context + " position keys");
            node.rsm2TextureAnimations = minor >= 3 ? ReadTextureAnimations(data, context) : new RSM.Rsm2TextureAnimation[0];

            // The sorted RSM1 views (first key of a frame wins), so HasAnimation and diagnostics see the keys.
            foreach (RSM.Rsm2VectorKey key in node.rsm2ScaleKeys) {
                if (!node.scaleKeyframes.ContainsKey(key.frame)) {
                    node.scaleKeyframes.Add(key.frame, key.value);
                }
            }

            foreach (RSM.Rsm2RotationKey key in node.rsm2RotationKeys) {
                if (!node.rotKeyframes.ContainsKey(key.frame)) {
                    node.rotKeyframes.Add(key.frame, key.q);
                }
            }

            foreach (RSM.Rsm2VectorKey key in node.rsm2PositionKeys) {
                if (!node.posKeyframes.ContainsKey(key.frame)) {
                    node.posKeyframes.Add(key.frame, key.value);
                }
            }

            node.box = new RSM.Box();
            return node;
        }

        private static RSM.Rsm2VectorKey[] ReadVectorKeys(MemoryStreamReader data, string context) {
            int count = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "keyframe", context);
            RoLoaderValidation.RequireRemaining(data, (long) count * 20, context);
            var keys = new RSM.Rsm2VectorKey[count];
            for (int i = 0; i < count; i++) {
                keys[i].frame = data.ReadInt();
                keys[i].value = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
                data.ReadFloat();
            }

            return keys;
        }

        private static RSM.Rsm2RotationKey[] ReadRotationKeys(MemoryStreamReader data, string context) {
            int count = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "keyframe", context);
            RoLoaderValidation.RequireRemaining(data, (long) count * 20, context);
            var keys = new RSM.Rsm2RotationKey[count];
            for (int i = 0; i < count; i++) {
                keys[i].frame = data.ReadInt();
                keys[i].q = new Quaternion(data.ReadFloat(), data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
            }

            return keys;
        }

        /// <summary>
        /// FUN_006d52c0 / FUN_0051f310: per animated texture slot a map type -> keys; a slot read again replaces
        /// its map (FUN_006ce8c0 then the map assignment), a type read again within a slot appends its keys.
        /// </summary>
        private static RSM.Rsm2TextureAnimation[] ReadTextureAnimations(MemoryStreamReader data, string context) {
            int slotCount = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "texture animation", context);
            var bySlot = new SortedDictionary<int, SortedDictionary<int, List<RSM.Rsm2FloatKey>>>();
            for (int i = 0; i < slotCount; i++) {
                int slot = ReadInt(data);
                var byType = new SortedDictionary<int, List<RSM.Rsm2FloatKey>>();
                int typeCount = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "texture animation type", context);
                for (int j = 0; j < typeCount; j++) {
                    int type = ReadInt(data);
                    int keyCount = RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, "texture animation key", context);
                    RoLoaderValidation.RequireRemaining(data, (long) keyCount * 8, context + " texture animation keys");
                    if (!byType.TryGetValue(type, out List<RSM.Rsm2FloatKey> keys)) {
                        keys = new List<RSM.Rsm2FloatKey>(keyCount);
                        byType.Add(type, keys);
                    }

                    for (int k = 0; k < keyCount; k++) {
                        keys.Add(new RSM.Rsm2FloatKey { frame = data.ReadInt(), value = data.ReadFloat() });
                    }
                }

                bySlot[slot] = byType;
            }

            var animations = new List<RSM.Rsm2TextureAnimation>();
            foreach (KeyValuePair<int, SortedDictionary<int, List<RSM.Rsm2FloatKey>>> slot in bySlot) {
                foreach (KeyValuePair<int, List<RSM.Rsm2FloatKey>> type in slot.Value) {
                    animations.Add(new RSM.Rsm2TextureAnimation {
                        textureSlot = slot.Key,
                        type = type.Key,
                        keys = type.Value.ToArray()
                    });
                }
            }

            return animations.ToArray();
        }

        /// <summary>
        /// FUN_006d52c0's link loop (first node of the parent's name, FUN_006d2ee0) and the actor's node tree
        /// (FUN_006d3070: each root name's node, then C3dNodeV2::vf01's children). Keeps the instantiated nodes,
        /// in file order, and their rest matrices (local * inverse(parent local), C3dNodeV2::vf01 0x9e81e0).
        /// </summary>
        private static void LinkNodes(RSM rsm, RSM.Node[] nodes) {
            var byName = new Dictionary<string, RSM.Node>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Length; i++) {
                if (!byName.ContainsKey(nodes[i].name)) {
                    byName.Add(nodes[i].name, nodes[i]);
                }
            }

            for (int i = 0; i < nodes.Length; i++) {
                RSM.Node node = nodes[i];
                if (node.parentName.Length > 0 && byName.TryGetValue(node.parentName, out RSM.Node parent) && parent != node) {
                    node.parent = parent;
                    parent.children.Add(node);
                }
            }

            var reached = new HashSet<RSM.Node>();
            var stack = new Stack<RSM.Node>();
            for (int i = 0; i < rsm.rootNames.Length; i++) {
                if (!byName.TryGetValue(rsm.rootNames[i], out RSM.Node root)) {
                    continue;
                }

                if (rsm.mainNode == null) {
                    rsm.mainNode = root;
                    rsm.name = root.name;
                }

                stack.Push(root);
                while (stack.Count > 0) {
                    RSM.Node node = stack.Pop();
                    if (!reached.Add(node)) {
                        continue;
                    }

                    for (int c = 0; c < node.children.Count; c++) {
                        stack.Push(node.children[c]);
                    }
                }
            }

            var kept = new List<RSM.Node>(reached.Count);
            for (int i = 0; i < nodes.Length; i++) {
                if (!reached.Contains(nodes[i])) {
                    continue;
                }

                RSM.Node node = nodes[i];
                node.rsm2Rest = new float[Rsm2Pose.MatrixLength];
                if (node.parent != null) {
                    Rsm2Pose.Invert(node.rsm2Rest, node.parent.rsm2Local);
                    Rsm2Pose.Multiply(node.rsm2Rest, node.rsm2Local, node.rsm2Rest);
                } else {
                    Array.Copy(node.rsm2Local, node.rsm2Rest, Rsm2Pose.MatrixLength);
                }

                kept.Add(node);
            }

            if (kept.Count == 0) {
                throw new Exception("Rsm2ModelLoader.Load: No root node of the model is in its node list; the client draws nothing.");
            }

            rsm.nodes = kept.ToArray();
            for (int i = 0; i < rsm.nodes.Length; i++) {
                rsm.nodes[i].isOnly = rsm.nodes.Length == 1;
            }
        }

        /// <summary>
        /// The frame-0 pose the actor shows after AssignModel (C3dActorV2::vf01 0x9e7d40 calls SetFrame(0)):
        /// each node's world matrix M(0) * world(parent) (<see cref="RSM.Node.rsm2World"/>), baked when no node
        /// of its chain has keys, else its <see cref="RSM.Node.matrix"/> (Unity form), and the model and node
        /// bounding boxes of the flipped vertices.
        /// </summary>
        private static void CalcFramePose(RSM rsm) {
            var worlds = new Dictionary<RSM.Node, float[]>(rsm.nodes.Length);
            RSM.Box box = rsm.box;
            for (int i = 0; i < rsm.nodes.Length; i++) {
                ComputeWorld(rsm.nodes[i], worlds);
            }

            for (int i = 0; i < rsm.nodes.Length; i++) {
                RSM.Node node = rsm.nodes[i];
                float[] world = worlds[node];
                node.rsm2World = world;
                node.rsm2Baked = !HasKeysOnChain(node);
                node.matrix = new Mat4();
                if (!node.rsm2Baked) {
                    Rsm2Pose.ToUnityMat4(world, node.matrix);
                }

                for (int v = 0; v < node.vertices.Count; v++) {
                    Vector3 p = Rsm2Pose.FlipY(Rsm2Pose.TransformPoint(world, node.vertices[v]));
                    for (int axis = 0; axis < 3; axis++) {
                        node.box.min[axis] = Math.Min(node.box.min[axis], p[axis]);
                        node.box.max[axis] = Math.Max(node.box.max[axis], p[axis]);
                        box.min[axis] = Math.Min(box.min[axis], p[axis]);
                        box.max[axis] = Math.Max(box.max[axis], p[axis]);
                    }
                }

                FinishBox(node.box);
            }

            FinishBox(box);
        }

        private static bool HasKeysOnChain(RSM.Node node) {
            for (RSM.Node current = node; current != null; current = current.parent) {
                if ((current.rsm2ScaleKeys?.Length ?? 0) > 0 ||
                    (current.rsm2RotationKeys?.Length ?? 0) > 0 ||
                    (current.rsm2PositionKeys?.Length ?? 0) > 0) {
                    return true;
                }
            }

            return false;
        }

        private static float[] ComputeWorld(RSM.Node node, Dictionary<RSM.Node, float[]> worlds) {
            if (worlds.TryGetValue(node, out float[] world)) {
                return world;
            }

            world = new float[Rsm2Pose.MatrixLength];
            float[] parentWorld = node.parent != null ? ComputeWorld(node.parent, worlds) : null;
            Rsm2Pose.EvaluateLocal(node, 0f, world);
            if (parentWorld != null) {
                Rsm2Pose.Multiply(world, world, parentWorld);
            }

            worlds.Add(node, world);
            return world;
        }

        private static void FinishBox(RSM.Box box) {
            if (box.min.x > box.max.x) {
                box.min = Vector3.zero;
                box.max = Vector3.zero;
            }

            for (int i = 0; i < 3; i++) {
                box.offset[i] = (box.max[i] + box.min[i]) / 2.0f;
                box.range[i] = (box.max[i] - box.min[i]) / 2.0f;
                box.center[i] = box.min[i] + box.range[i];
            }
        }

        private static void ValidateFace(RSM.Node node, RSM.Face face, int faceIndex, int vertexCount, int textureVertexCount, int textureCount, string context) {
            for (int i = 0; i < 3; i++) {
                if (face.vertidx[i] >= vertexCount) {
                    throw new Exception($"{context}: Face {faceIndex} references vertex {face.vertidx[i]}, but only {vertexCount} vertex/vertices exist.");
                }

                if (face.tvertidx[i] >= textureVertexCount) {
                    throw new Exception($"{context}: Face {faceIndex} references texture vertex {face.tvertidx[i]}, but only {textureVertexCount} texture vertex/vertices exist.");
                }
            }

            if (textureCount == 0) {
                throw new Exception($"{context}: Face {faceIndex} has no texture (the node has no texture slot).");
            }
        }

        private static int ReadCount(MemoryStreamReader data, string name) {
            return RoLoaderValidation.RequireCount(ReadInt(data), RoLoaderValidation.MaxModelElementCount, name, "Rsm2ModelLoader.Load");
        }

        private static string ReadString(MemoryStreamReader data, string name) {
            return ReadString(data, ReadInt(data), name);
        }

        private static string ReadString(MemoryStreamReader data, int length, string name) {
            if (length < 0 || length > RoLoaderValidation.MaxTexturePathLength) {
                throw new Exception($"Rsm2ModelLoader.Load: Invalid {name} length {length}.");
            }

            return RoLoaderValidation.ReadBinaryString(data, length, "Rsm2ModelLoader.Load " + name);
        }

        private static int ReadInt(MemoryStreamReader data) {
            return RoLoaderValidation.ReadInt(data, "Rsm2ModelLoader.Load");
        }

        private static float ReadFloat(MemoryStreamReader data) {
            return RoLoaderValidation.ReadFloat(data, "Rsm2ModelLoader.Load");
        }

        private static byte ReadByte(MemoryStreamReader data) {
            return RoLoaderValidation.ReadByte(data, "Rsm2ModelLoader.Load");
        }
    }
}
