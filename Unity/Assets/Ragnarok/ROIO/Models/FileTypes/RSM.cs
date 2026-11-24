using ROIO.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace ROIO.Models.FileTypes
{
    public class RSM
    {
        public static string Header = "GRSM";

        public enum SHADING
        {
            NONE,
            FLAT,
            SMOOTH
        }

        public string version;
        public string name;

        public List<RSW.ModelDescriptor> instances;

        public string[] textures;
        public SHADING shadeType;
        public float alpha;

        public long animLen;

        public Node[] nodes;
        public Node mainNode;

        public Box box;
        public VolumeBox[] volumeBoxes;
        public PositionKeyframe[] posKeyframes;

        /// <summary>
        /// An RSM2 model (".rsm2", C3dModelResV2 0x6cbe30): nodes carry their model-space matrix and
        /// keyframes (<see cref="Node.rsm2Local"/>), vertices are node-local, the actor does not centre the model on
        /// its bounding box (C3dActorV2::vf02 0x9ec760) and the keyframe time is frames at <see cref="fps"/>.
        /// </summary>
        public bool isRsm2;
        /// <summary>RSM2 header: keyframes per second (+0x15c); <see cref="animLen"/> is in frames.</summary>
        public float fps;
        /// <summary>RSM2 root node names (+0x11c), the nodes the actor instantiates (FUN_006d3070).</summary>
        public string[] rootNames;

        public class NodeMeshData
        {
            public List<Vector3> vertices = new List<Vector3>();
            public List<int> triangles = new List<int>();
            public List<Vector3> normals = new List<Vector3>();
            public List<Vector2> uv = new List<Vector2>();
            public bool twoSided;
            public Node node;
        }

        public class Instance
        {
            public int id;
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 scale;
        }

        public class VolumeBox
        {
            public Vector3 pos;
            public Vector3 size;
            public Vector3 rot;
            public long flag;
        }

        public class Box
        {
            public Vector3 max = Vector3.negativeInfinity,
                min = Vector3.positiveInfinity,
                offset = new Vector3(),
                range = new Vector3(),
                center = new Vector3();
        }

        public class Face
        {
            public Vector3Int vertidx;
            public Vector3Int tvertidx;
            public ushort texid;
            public ushort padding;
            public long twoSided;
            public long smoothGroup;
        }

        public class PositionKeyframe
        {
            public long frame;
            public Vector3 p;
        }

        public class RotationKeyframe
        {
            public long frame;
            public Quaternion q;
        }

        /// <summary>RSM2 scale or position key (20 bytes: frame, x, y, z, an unused float).</summary>
        public struct Rsm2VectorKey
        {
            public int frame;
            public Vector3 value;
        }

        /// <summary>RSM2 rotation key (20 bytes: frame, quaternion x, y, z, w).</summary>
        public struct Rsm2RotationKey
        {
            public int frame;
            public Quaternion q;
        }

        /// <summary>RSM2 texture animation key (8 bytes: frame, value).</summary>
        public struct Rsm2FloatKey
        {
            public int frame;
            public float value;
        }

        /// <summary>
        /// RSM2 2.3 texture animation of one node texture slot (FUN_0051f310): the keys of one UV value
        /// (FUN_0051ede0): type 0 = U offset, 1 = V offset, 2 = U scale, 3 = V scale, 4 = rotation (radians).
        /// </summary>
        public class Rsm2TextureAnimation
        {
            public int textureSlot;
            public int type;
            public Rsm2FloatKey[] keys;
        }

        public class Node
        {
            public string FullName
            {
                get { return name + "[" + instanceNumber + "]"; }
            }
            public string name;
            public string parentName;
            public Node parent;
            public List<Node> children = new List<Node>();
            public RSM model;
            public bool isOnly;
            public Vector3[] mat3;
            public Vector3 offset, pos, rotAxis, scale;
            public float rotAngle;
            public long[] textures;
            public List<Vector3> vertices;
            public float[] tVertices; //texture vertices
            public Face[] faces;
            public SortedList<int, Quaternion> rotKeyframes = new SortedList<int, Quaternion>();
            public SortedList<int, Vector3> posKeyframes = new SortedList<int, Vector3>();
            public SortedList<int, Vector3> scaleKeyframes = new SortedList<int, Vector3>();

            public int instanceNumber;
            public Box box;

            public Mat4 matrix = Mat4.Identity;

            /// <summary>RSM2: the file's 4x3 matrix (12 floats, rows then translation; v' = v * M), the node's
            /// model-space transform (C3dNodeResV2 +0x58).</summary>
            public float[] rsm2Local;
            /// <summary>RSM2: local * inverse(parent local) (C3dNodeV2::vf01 0x9e81e0, +0xbc), the pose a node
            /// keeps relative to its parent where it has no rotation / position keys.</summary>
            public float[] rsm2Rest;
            public Rsm2VectorKey[] rsm2ScaleKeys;
            public Rsm2RotationKey[] rsm2RotationKeys;
            public Rsm2VectorKey[] rsm2PositionKeys;
            public Rsm2TextureAnimation[] rsm2TextureAnimations;
            /// <summary>RSM2: the frame-0 world matrix (M(0) * world(parent), client form).</summary>
            public float[] rsm2World;
            /// <summary>
            /// RSM2: neither the node nor a parent has scale / rotation / position keys, so its world matrix never
            /// changes: it is baked into the mesh and the node transform is the identity (a Unity transform cannot
            /// hold the skewed or mirrored matrices some nodes have).
            /// </summary>
            public bool rsm2Baked;

            public Matrix4x4 GetPositionMatrix()
            {
                if (model != null && model.isRsm2)
                {
                    // C3dActorV2::vf02 (0x9ec760) does not offset the model by its bounding box; matrix is the
                    // frame-0 pose with the actor's Y flip folded in (Rsm2Pose.ToUnityMatrix), or the identity for
                    // a baked node.
                    return this.matrix.ToMatrix4x4();
                }

                //calculate matrix
                var matrix = Mat4.Identity;
                Mat4.Translate(matrix, matrix, new Vector3(-model.box.center[0], -model.box.max[1], -model.box.center[2]));
                Mat4.Multiply(matrix, matrix, this.matrix);

                return matrix.ToMatrix4x4();
            }

            public Dictionary<long, NodeMeshData> Compile()
            {
                Dictionary<long, NodeMeshData> mesh = new Dictionary<long, NodeMeshData>();

                if (model != null && model.isRsm2)
                {
                    CompileRsm2(mesh);
                    return mesh;
                }

                //calculate offset and rotation matrix
                var matrix = Mat4.Identity;

                if (parent != null || children.Count > 0)
                {
                    Mat4.Translate(matrix, matrix, offset);
                }

                Mat4.Multiply(matrix, matrix, Mat4.FromMat3(mat3, null));

                //generate new vertices
                for (int i = 0; i < vertices.Count; i++)
                {
                    float x = vertices[i].x;
                    float y = vertices[i].y;
                    float z = vertices[i].z;

                    //(vec3)vert = (mat4)modelViewMat * (vec3)vertices[i];
                    Vector3 transformed = new Vector3
                    {
                        x = matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12],
                        y = matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13],
                        z = matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14]
                    };

                    vertices[i] = transformed;
                }

                //initialize buffer
                for (int i = 0; i < textures.Length; i++)
                {
                    //initialize mesh for texture i
                    mesh[textures[i]] = new NodeMeshData
                    {
                        node = this
                    };
                }

                GenerateMesh(vertices, mesh);

                return mesh;
            }

            /// <summary>
            /// RSM2 vertices are node-local (C3dNodeV2::vf03 0x9e92a0 projects them with the node's matrix); the
            /// actor's Y flip goes into them here (see Rsm2Pose), and a baked node's frame-0 world matrix too.
            /// The client culls by screen-space winding only (and the instance's mirror flag), so a node whose
            /// world matrix mirrors (determinant below 0) shows its faces' back sides; a baked node gets that from
            /// its transformed positions, a moving one from reversed triangles (Unity flips the culling of a
            /// mirrored transform).
            /// </summary>
            private void CompileRsm2(Dictionary<long, NodeMeshData> mesh)
            {
                var vert = new List<Vector3>(vertices.Count);
                for (int i = 0; i < vertices.Count; i++)
                {
                    vert.Add(Rsm2Pose.FlipY(rsm2Baked && rsm2World != null ? Rsm2Pose.TransformPoint(rsm2World, vertices[i]) : vertices[i]));
                }

                bool reverse = !rsm2Baked && rsm2World != null && Rsm2Pose.Determinant(rsm2World) < 0f;
                for (int i = 0; i < textures.Length; i++)
                {
                    mesh[textures[i]] = new NodeMeshData
                    {
                        node = this
                    };
                }

                for (int i = 0; i < faces.Length; ++i)
                {
                    var face = faces[i];
                    var _out = mesh[textures[face.texid]];
                    _out.twoSided = face.twoSided != 0;

                    for (int j = 0; j < 3; j++)
                    {
                        int corner = reverse ? (3 - j) % 3 : j;
                        _out.vertices.Add(vert[face.vertidx[corner]]);
                        int b = face.tvertidx[corner] * 6;
                        _out.uv.Add(new Vector2(tVertices[b + 4], -tVertices[b + 5]));
                    }
                }
            }

            private void GenerateMesh(List<Vector3> vert, Dictionary<long, NodeMeshData> mesh)
            {
                for (int i = 0; i < faces.Length; ++i)
                {
                    var face = faces[i];
                    var idx = face.vertidx;
                    var tidx = face.tvertidx;
                    var t = textures[face.texid];
                    var _out = mesh[t];
                    _out.twoSided = face.twoSided != 0;

                    for (int j = 0; j < 3; j++)
                    {
                        var a = idx[j];
                        var b = tidx[j] * 6;
                        /* vec3 positions  */
                        _out.vertices.Add(vert[a]);
                        /* vec2 textCoords */
                        _out.uv.Add(new Vector2(tVertices[b + 4], -tVertices[b + 5]));
                    }
                }
            }
        }

        public class CompiledModel
        {
            public RSM rsm;
            public Dictionary<long, NodeMeshData>[] nodesData;
        }

        public void CreateInstance(RSW.ModelDescriptor model)
        {
            instances.Add(model);
        }

        //utility vars
        public string filename;
    }
}
