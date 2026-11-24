using ROIO.Models.FileTypes;
using ROIO.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ROIO.Loaders {
    public class ModelLoader {
        //returns a collection of nodes and its meshes generated from an RSM.
        public static RSM.CompiledModel Compile(RSM rsm) {
            var nodesData = new Dictionary<long, RSM.NodeMeshData>[rsm.nodes.Length];

            for (int i = 0; i < rsm.nodes.Length; ++i) {
                //mesh = union of nodes meshes
                nodesData[i] = rsm.nodes[i].Compile();
            }

            return new RSM.CompiledModel() {
                nodesData = nodesData,
                rsm = rsm,
            };
        }

        public static RSM Load(MemoryStreamReader data) {
            var header = RoLoaderValidation.ReadBinaryString(data, 4, "ModelLoader.Load header");

            if (header != RSM.Header) {
                throw new Exception("ModelLoader.Load: Header (" + header + ") is not \"GRSM\"");
            }

            RSM rsm = new RSM();

            //read infos
            RoLoaderValidation.RequireRemaining(data, 2, "ModelLoader.Load version");
            RoFileVersion version = RoFileVersion.ReadMajorMinor(data);
            rsm.version = version.Text;
            rsm.animLen = RoLoaderValidation.ReadInt(data, "ModelLoader.Load animation length");
            rsm.shadeType = (RSM.SHADING) RoLoaderValidation.ReadInt(data, "ModelLoader.Load shade type");

            rsm.alpha = version.IsAtLeast(1, 4) ? RoLoaderValidation.ReadByte(data, "ModelLoader.Load alpha") / 255f : 1;
            RoLoaderValidation.RequireRemaining(data, 16, "ModelLoader.Load reserved header data");
            data.Seek(16, System.IO.SeekOrigin.Current);

            //read textures
            int textureCount = RoLoaderValidation.RequireCount(
                RoLoaderValidation.ReadInt(data, "ModelLoader.Load texture count"),
                RoLoaderValidation.MaxModelElementCount,
                "texture",
                "ModelLoader.Load");
            RoLoaderValidation.RequireRemaining(data, (long) textureCount * 40, "ModelLoader.Load texture names");
            rsm.textures = new string[textureCount];
            for (int i = 0; i < textureCount; ++i) {
                rsm.textures[i] = data.ReadBinaryString(40);
            }

            //read nodes (meshes)
            rsm.name = RoLoaderValidation.ReadBinaryString(data, 40, "ModelLoader.Load root node name");
            int nodeCount = RoLoaderValidation.RequirePositiveCount(
                RoLoaderValidation.ReadInt(data, "ModelLoader.Load node count"),
                RoLoaderValidation.MaxModelNodeCount,
                "node",
                "ModelLoader.Load");
            rsm.nodes = new RSM.Node[nodeCount];

            for (int i = 0; i < nodeCount; ++i) {
                var node = rsm.nodes[i] = LoadNode(rsm, data, version);
                if (string.Equals(node.name, rsm.name)) {
                    rsm.mainNode = node;
                }
            }

            //fallback for non defined main node
            if (rsm.mainNode == null) {
                rsm.mainNode = rsm.nodes[0];
            }

            // roBrowser reads model-level position keyframes for pre-1.6 RSMs.
            if (version.IsLessThan(1, 6)) {
                int count = RoLoaderValidation.RequireCount(
                    RoLoaderValidation.ReadInt(data, "ModelLoader.Load position keyframe count"),
                    RoLoaderValidation.MaxModelElementCount,
                    "position keyframe",
                    "ModelLoader.Load");
                RoLoaderValidation.RequireRemaining(data, (long) count * 20, "ModelLoader.Load position keyframes");
                rsm.posKeyframes = new RSM.PositionKeyframe[count];
                for (int i = 0; i < count; ++i) {
                    rsm.posKeyframes[i] = new RSM.PositionKeyframe() {
                        frame = data.ReadInt(),
                        p = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat())
                    };
                    data.ReadFloat();
                }
            } else {
                rsm.posKeyframes = new RSM.PositionKeyframe[0];
            }

            //read volume box
            var vbCount = RoLoaderValidation.RequireCount(
                RoLoaderValidation.ReadInt(data, "ModelLoader.Load volume box count"),
                RoLoaderValidation.MaxModelElementCount,
                "volume box",
                "ModelLoader.Load");
            RoLoaderValidation.RequireRemaining(data, (long) vbCount * (version.IsAtLeast(1, 3) ? 40 : 36), "ModelLoader.Load volume boxes");
            rsm.volumeBoxes = new RSM.VolumeBox[vbCount];

            for (int i = 0; i < vbCount; ++i) {
                rsm.volumeBoxes[i] = new RSM.VolumeBox() {
                    size = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat()),
                    pos = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat()),
                    rot = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat()),
                    flag = version.IsAtLeast(1, 3) ? data.ReadInt() : 0
                };
            }

            rsm.instances = new List<RSW.ModelDescriptor>();
            rsm.box = new RSM.Box();

            CalcBoundingBox(rsm);

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

        private static void CalcBoundingBox(RSM rsm) {
            var matrix = Mat4.Identity;
            var count = rsm.nodes.Length;
            var box = rsm.box;

            CalcNodeBoundingBox(rsm.mainNode, matrix);

            for (int i = 0; i < 3; ++i) {
                for (int j = 0; j < count; ++j) {
                    box.max[i] = Math.Max(box.max[i], rsm.nodes[j].box.max[i]);
                    box.min[i] = Math.Min(box.min[i], rsm.nodes[j].box.min[i]);
                }
                box.offset[i] = (box.max[i] + box.min[i]) / 2.0f;
                box.range[i] = (box.max[i] - box.min[i]) / 2.0f;
                box.center[i] = box.min[i] + box.range[i];
            }
        }

        private static void CalcNodeBoundingBox(RSM.Node node, Mat4 _matrix) {
            var v = new Vector3();
            var box = node.box;
            var nodes = node.model.nodes;
            var vertices = node.vertices;
            float x, y, z;

            //find position
            node.matrix = _matrix.Clone();

            Mat4.Translate(node.matrix, node.matrix, node.pos);

            //dynamic or static model
            if (node.rotKeyframes.Count == 0) {
                Mat4.Rotate(node.matrix, node.matrix, node.rotAngle, node.rotAxis);
            }

            Mat4.Scale(node.matrix, node.matrix, node.scale);

            Mat4 matrix = node.matrix.Clone();

            if (!node.isOnly) {
                Mat4.Translate(matrix, matrix, node.offset);
            }

            Mat4.Multiply(matrix, matrix, Mat4.FromMat3(node.mat3, null));

            for (int i = 0, count = vertices.Count; i < count; ++i) {
                x = vertices[i][0];
                y = vertices[i][1];
                z = vertices[i][2];

                v[0] = matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12];
                v[1] = matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13];
                v[2] = matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14];

                for (int j = 0; j < 3; j++) {
                    box.min[j] = Math.Min(v[j], box.min[j]);
                    box.max[j] = Math.Max(v[j], box.max[j]);
                }
            }

            for (int i = 0; i < 3; ++i) {
                box.offset[i] = (box.max[i] + box.min[i]) / 2.0f;
                box.range[i] = (box.max[i] - box.min[i]) / 2.0f;
                box.center[i] = box.min[i] + box.range[i];
            }

            for (int i = 0, count = nodes.Length; i < count; ++i) {
                if (string.Equals(nodes[i].parentName, node.name) && !string.Equals(node.name, node.parentName)) {
                    nodes[i].parent = node;
                    node.children.Add(nodes[i]);
                    CalcNodeBoundingBox(nodes[i], node.matrix);
                }
            }
        }

        private static RSM.Node LoadNode(RSM rsm, MemoryStreamReader data, RoFileVersion version) {
            RSM.Node node = new RSM.Node();

            node.model = rsm;
            node.isOnly = rsm.nodes.Length == 1;

            node.name = RoLoaderValidation.ReadBinaryString(data, 40, "ModelLoader.LoadNode node name");
            node.parentName = RoLoaderValidation.ReadBinaryString(data, 40, $"ModelLoader.LoadNode parent name for {node.name}");

            //read textures
            int textureCount = RoLoaderValidation.RequireCount(
                RoLoaderValidation.ReadInt(data, $"ModelLoader.LoadNode texture count for {node.name}"),
                RoLoaderValidation.MaxModelElementCount,
                "node texture",
                $"ModelLoader.LoadNode {node.name}");
            RoLoaderValidation.RequireRemaining(data, (long) textureCount * 4, $"ModelLoader.LoadNode texture ids for {node.name}");
            node.textures = new long[textureCount];

            for (int i = 0; i < textureCount; ++i) {
                node.textures[i] = data.ReadInt();
            }

            //read options
            RoLoaderValidation.RequireRemaining(data, 88, $"ModelLoader.LoadNode transform data for {node.name}");
            node.mat3 = new Vector3[] {
            new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat()),
            new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat()),
            new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat())
        };

            node.offset = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
            node.pos = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
            node.rotAngle = data.ReadFloat();
            node.rotAxis = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
            node.scale = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat());

            //read vertices
            int verticeCount = RoLoaderValidation.RequireCount(
                RoLoaderValidation.ReadInt(data, $"ModelLoader.LoadNode vertex count for {node.name}"),
                RoLoaderValidation.MaxModelElementCount,
                "vertex",
                $"ModelLoader.LoadNode {node.name}");
            RoLoaderValidation.RequireRemaining(data, (long) verticeCount * 12, $"ModelLoader.LoadNode vertices for {node.name}");
            node.vertices = new List<Vector3>();
            for (int i = 0; i < verticeCount; ++i) {
                node.vertices.Add(new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat()));
            }

            //read textures vertices
            int tverticeCount = RoLoaderValidation.RequireCount(
                RoLoaderValidation.ReadInt(data, $"ModelLoader.LoadNode texture vertex count for {node.name}"),
                RoLoaderValidation.MaxModelElementCount,
                "texture vertex",
                $"ModelLoader.LoadNode {node.name}");
            RoLoaderValidation.RequireRemaining(data, (long) tverticeCount * (version.IsAtLeast(1, 2) ? 12 : 8), $"ModelLoader.LoadNode texture vertices for {node.name}");
            node.tVertices = new float[checked(tverticeCount * 6)];
            for (int i = 0; i < tverticeCount; ++i) {
                if (version.IsAtLeast(1, 2)) {
                    node.tVertices[i * 6 + 0] = data.ReadByte() / 255f;
                    node.tVertices[i * 6 + 1] = data.ReadByte() / 255f;
                    node.tVertices[i * 6 + 2] = data.ReadByte() / 255f;
                    node.tVertices[i * 6 + 3] = data.ReadByte() / 255f;
                }
                node.tVertices[i * 6 + 4] = data.ReadFloat() * 0.98f + 0.01f;
                node.tVertices[i * 6 + 5] = data.ReadFloat() * 0.98f + 0.01f;
            }

            //read faces
            int faceCount = RoLoaderValidation.RequireCount(
                RoLoaderValidation.ReadInt(data, $"ModelLoader.LoadNode face count for {node.name}"),
                RoLoaderValidation.MaxModelElementCount,
                "face",
                $"ModelLoader.LoadNode {node.name}");
            RoLoaderValidation.RequireRemaining(data, (long) faceCount * (version.IsAtLeast(1, 2) ? 24 : 20), $"ModelLoader.LoadNode faces for {node.name}");
            node.faces = new RSM.Face[faceCount];
            for (int i = 0; i < faceCount; ++i) {
                node.faces[i] = new RSM.Face() {
                    vertidx = new Vector3Int(data.ReadUShort(), data.ReadUShort(), data.ReadUShort()),
                    tvertidx = new Vector3Int(data.ReadUShort(), data.ReadUShort(), data.ReadUShort()),
                    texid = data.ReadUShort(),
                    padding = data.ReadUShort(),
                    twoSided = data.ReadInt(),
                    smoothGroup = version.IsAtLeast(1, 2) ? data.ReadInt() : 0
                };
                ValidateFaceReferences(node, node.faces[i], i, verticeCount, tverticeCount, textureCount);
            }

            // roBrowser reads node scale keyframes before rotation for RSM 1.6+.
            if (version.IsAtLeast(1, 6)) {
                int scaleCount = RoLoaderValidation.RequireCount(
                    RoLoaderValidation.ReadInt(data, $"ModelLoader.LoadNode scale keyframe count for {node.name}"),
                    RoLoaderValidation.MaxModelElementCount,
                    "node scale keyframe",
                    $"ModelLoader.LoadNode {node.name}");
                RoLoaderValidation.RequireRemaining(data, (long) scaleCount * 20, $"ModelLoader.LoadNode scale keyframes for {node.name}");
                for (int i = 0; i < scaleCount; ++i) {
                    int key = data.ReadInt();
                    Vector3 scale = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
                    data.ReadFloat();

                    if (!node.scaleKeyframes.ContainsKey(key)) {
                        node.scaleKeyframes.Add(key, scale);
                    }
                }
            }

            //read rotation keyframes
            int rkfCount = RoLoaderValidation.RequireCount(
                RoLoaderValidation.ReadInt(data, $"ModelLoader.LoadNode rotation keyframe count for {node.name}"),
                RoLoaderValidation.MaxModelElementCount,
                "rotation keyframe",
                $"ModelLoader.LoadNode {node.name}");
            RoLoaderValidation.RequireRemaining(data, (long) rkfCount * 20, $"ModelLoader.LoadNode rotation keyframes for {node.name}");
            for (int i = 0; i < rkfCount; ++i) {
                int time = data.ReadInt();
                Quaternion quat = new Quaternion(data.ReadFloat(), data.ReadFloat(), data.ReadFloat(), data.ReadFloat());

                if (!node.rotKeyframes.ContainsKey(time)) {
                    //some models have multiple keyframes with the
                    //same timestamp, here we just keep the first one
                    //and throw out the rest.
                    node.rotKeyframes.Add(time, quat);
                }
            }

            //read position keyframes
            if (version.IsAtLeast(2, 2)) {
                int pkfCount = RoLoaderValidation.RequireCount(
                    RoLoaderValidation.ReadInt(data, $"ModelLoader.LoadNode position keyframe count for {node.name}"),
                    RoLoaderValidation.MaxModelElementCount,
                    "node position keyframe",
                    $"ModelLoader.LoadNode {node.name}");
                RoLoaderValidation.RequireRemaining(data, (long) pkfCount * 20, $"ModelLoader.LoadNode position keyframes for {node.name}");
                for (int i = 0; i < pkfCount; ++i) {
                    var key = data.ReadInt();
                    Vector3 position = new Vector3(data.ReadFloat(), data.ReadFloat(), data.ReadFloat());
                    data.ReadInt();

                    if (!node.posKeyframes.ContainsKey(key)) {
                        node.posKeyframes.Add(key, position);
                    }
                }
            }

            node.box = new RSM.Box();

            return node;
        }

        private static void ValidateFaceReferences(
            RSM.Node node,
            RSM.Face face,
            int faceIndex,
            int vertexCount,
            int textureVertexCount,
            int textureCount) {
            for (int i = 0; i < 3; i++) {
                if (face.vertidx[i] >= vertexCount) {
                    throw new Exception($"ModelLoader.LoadNode {node.name}: Face {faceIndex} references vertex {face.vertidx[i]}, but only {vertexCount} vertex/vertices exist.");
                }

                if (face.tvertidx[i] >= textureVertexCount) {
                    throw new Exception($"ModelLoader.LoadNode {node.name}: Face {faceIndex} references texture vertex {face.tvertidx[i]}, but only {textureVertexCount} texture vertex/vertices exist.");
                }
            }

            if (face.texid >= textureCount) {
                throw new Exception($"ModelLoader.LoadNode {node.name}: Face {faceIndex} references node texture {face.texid}, but only {textureCount} node texture slot(s) exist.");
            }
        }
    }
}
