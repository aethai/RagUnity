using System;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Maps {
    public static class RoWaterMeshBuilder {
        private const int LegacyWaterStride = 5;
        private const int LegacyPositionOffset = 0;
        private const int LegacyUvOffset = 3;

        public static Mesh Build(GND.Mesh source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            if (source.waterMesh == null || source.waterVertCount <= 0) {
                return null;
            }

            int vertexCount = source.waterVertCount;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            for (int i = 0; i < vertexCount; i++) {
                int offset = i * LegacyWaterStride;
                vertices[i] = new Vector3(
                    source.waterMesh[offset + LegacyPositionOffset],
                    source.waterMesh[offset + LegacyPositionOffset + 1],
                    source.waterMesh[offset + LegacyPositionOffset + 2]
                );

                uvs[i] = new Vector2(
                    source.waterMesh[offset + LegacyUvOffset],
                    source.waterMesh[offset + LegacyUvOffset + 1]
                );
            }

            int quadCount = vertexCount / 4;
            var triangles = new int[quadCount * 6];
            for (int quad = 0; quad < quadCount; quad++) {
                int vertex = quad * 4;
                int triangle = quad * 6;

                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 3;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 1;
                triangles[triangle + 5] = vertex + 2;
            }

            var mesh = new Mesh {
                name = "RO Water",
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
