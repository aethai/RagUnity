using System;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    public static class RoGroundMeshBuilder {
        private const int LegacyGroundStride = 12;
        private const int LegacyPositionOffset = 0;
        private const int LegacyNormalOffset = 3;
        private const int LegacyUvOffset = 6;
        private const int LegacyLightUvOffset = 8;
        private const int LegacyTileUvOffset = 10;
        private const float WireframeLift = 0.08f;

        public enum GroundTopDiagonalMode {
            Default,
            Alternate
        }

        public static GroundTopDiagonalMode TopDiagonalMode { get; set; }

        public static Mesh Build(GND.Mesh source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.mesh == null || source.meshVertCount <= 0) {
                throw new ArgumentException("Ground mesh has no vertices.", nameof(source));
            }

            int vertexCount = source.meshVertCount;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var lightUvs = new Vector2[vertexCount];
            var tileUvs = new Vector2[vertexCount];

            for (int i = 0; i < vertexCount; i++) {
                int offset = i * LegacyGroundStride;
                vertices[i] = new Vector3(
                    source.mesh[offset + LegacyPositionOffset],
                    source.mesh[offset + LegacyPositionOffset + 1],
                    source.mesh[offset + LegacyPositionOffset + 2]
                );

                normals[i] = new Vector3(
                    source.mesh[offset + LegacyNormalOffset],
                    source.mesh[offset + LegacyNormalOffset + 1],
                    source.mesh[offset + LegacyNormalOffset + 2]
                );

                uvs[i] = new Vector2(
                    source.mesh[offset + LegacyUvOffset],
                    source.mesh[offset + LegacyUvOffset + 1]
                );

                lightUvs[i] = new Vector2(
                    source.mesh[offset + LegacyLightUvOffset],
                    source.mesh[offset + LegacyLightUvOffset + 1]
                );

                tileUvs[i] = new Vector2(
                    source.mesh[offset + LegacyTileUvOffset],
                    source.mesh[offset + LegacyTileUvOffset + 1]
                );
            }

            int quadCount = vertexCount / 4;
            var triangles = new int[quadCount * 6];
            for (int quad = 0; quad < quadCount; quad++) {
                int vertex = quad * 4;
                int triangle = quad * 6;

                Vector3 normal = normals[vertex];
                if (normal.z == 1f) {
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 1;
                    triangles[triangle + 2] = vertex + 2;
                    triangles[triangle + 3] = vertex + 2;
                    triangles[triangle + 4] = vertex + 3;
                    triangles[triangle + 5] = vertex;
                } else if (normal.x == 1f) {
                    triangles[triangle] = vertex;
                    triangles[triangle + 1] = vertex + 2;
                    triangles[triangle + 2] = vertex + 1;
                    triangles[triangle + 3] = vertex + 2;
                    triangles[triangle + 4] = vertex + 3;
                    triangles[triangle + 5] = vertex + 1;
                } else {
                    if (TopDiagonalMode == GroundTopDiagonalMode.Alternate) {
                        triangles[triangle] = vertex;
                        triangles[triangle + 1] = vertex + 1;
                        triangles[triangle + 2] = vertex + 3;
                        triangles[triangle + 3] = vertex + 1;
                        triangles[triangle + 4] = vertex + 2;
                        triangles[triangle + 5] = vertex + 3;
                    } else {
                        triangles[triangle] = vertex;
                        triangles[triangle + 1] = vertex + 2;
                        triangles[triangle + 2] = vertex + 3;
                        triangles[triangle + 3] = vertex;
                        triangles[triangle + 4] = vertex + 1;
                        triangles[triangle + 5] = vertex + 2;
                    }
                }
            }

            var mesh = new Mesh {
                name = "RO Ground",
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(BuildShadingNormals(vertices, normals, quadCount));
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, lightUvs);
            mesh.SetUVs(2, tileUvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Normals for lighting: the compiled GND normals are only wall flags (z or x == 1) and a broken roBrowser copy
        /// (z repeats y), so the enhanced ground shader used a fixed up vector and hills were never lit by the sun.
        /// Top surfaces get the corner average of the adjacent tiles' face normals (corners at the same position only,
        /// so a cliff edge stays sharp); walls keep their own face normal. The sign is the mesh's winding: the shader
        /// turns top surfaces up and walls toward the viewer.
        /// </summary>
        public static Vector3[] BuildShadingNormals(Vector3[] vertices, Vector3[] sourceNormals, int quadCount) {
            var result = new Vector3[vertices.Length];
            var faceNormals = new Vector3[quadCount];
            var isWall = new bool[quadCount];
            var corners = new System.Collections.Generic.Dictionary<Vector3Int, Vector3>(quadCount * 2);
            for (int quad = 0; quad < quadCount; quad++) {
                int v = quad * 4;
                Vector3 flag = sourceNormals[v];
                isWall[quad] = flag.z == 1f || flag.x == 1f;
                Vector3 n = Vector3.Cross(vertices[v + 2] - vertices[v], vertices[v + 3] - vertices[v + 1]);
                faceNormals[quad] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
                if (isWall[quad]) continue;
                // keep the top surfaces facing one way before averaging (the winding is the same for every tile)
                Vector3 top = faceNormals[quad];
                for (int k = 0; k < 4; k++) {
                    Vector3Int key = CornerKey(vertices[v + k]);
                    corners[key] = corners.TryGetValue(key, out Vector3 sum) ? sum + top : top;
                }
            }

            for (int quad = 0; quad < quadCount; quad++) {
                int v = quad * 4;
                for (int k = 0; k < 4; k++) {
                    result[v + k] = !isWall[quad] && corners.TryGetValue(CornerKey(vertices[v + k]), out Vector3 sum) && sum.sqrMagnitude > 1e-12f
                        ? sum.normalized
                        : faceNormals[quad];
                }
            }

            return result;
        }

        private static Vector3Int CornerKey(Vector3 position) {
            return new Vector3Int(Mathf.RoundToInt(position.x * 64f), Mathf.RoundToInt(position.y * 64f), Mathf.RoundToInt(position.z * 64f));
        }

        public static MeshCollider AddCollider(GameObject target, Mesh sharedMesh) {
            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (sharedMesh == null) {
                throw new ArgumentNullException(nameof(sharedMesh));
            }

            var collider = target.AddComponent<MeshCollider>();
            collider.sharedMesh = sharedMesh;
            return collider;
        }

        public static Mesh BuildWireframe(GND.Mesh source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.mesh == null || source.meshVertCount <= 0) {
                throw new ArgumentException("Ground mesh has no vertices.", nameof(source));
            }

            int quadCount = source.meshVertCount / 4;
            var vertices = new Vector3[quadCount * 8];
            var indices = new int[vertices.Length];

            for (int quad = 0; quad < quadCount; quad++) {
                int sourceVertex = quad * 4;
                int targetVertex = quad * 8;

                Vector3 a = ReadPosition(source, sourceVertex);
                Vector3 b = ReadPosition(source, sourceVertex + 1);
                Vector3 c = ReadPosition(source, sourceVertex + 2);
                Vector3 d = ReadPosition(source, sourceVertex + 3);

                a.y -= WireframeLift;
                b.y -= WireframeLift;
                c.y -= WireframeLift;
                d.y -= WireframeLift;

                vertices[targetVertex] = a;
                vertices[targetVertex + 1] = b;
                vertices[targetVertex + 2] = b;
                vertices[targetVertex + 3] = c;
                vertices[targetVertex + 4] = c;
                vertices[targetVertex + 5] = d;
                vertices[targetVertex + 6] = d;
                vertices[targetVertex + 7] = a;
            }

            for (int i = 0; i < indices.Length; i++) {
                indices[i] = i;
            }

            var mesh = new Mesh {
                name = "RO Ground Wireframe",
                indexFormat = vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 ReadPosition(GND.Mesh source, int vertexIndex) {
            int offset = vertexIndex * LegacyGroundStride + LegacyPositionOffset;
            return new Vector3(source.mesh[offset], source.mesh[offset + 1], source.mesh[offset + 2]);
        }
    }
}
