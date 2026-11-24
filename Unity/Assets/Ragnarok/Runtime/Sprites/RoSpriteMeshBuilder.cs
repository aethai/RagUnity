using System;
using System.Collections.Generic;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Sprites {
    public static class RoSpriteMeshBuilder {
        public static Mesh Build(ACT.Frame frame, Sprite[] sprites) {
            return Build(frame, sprites, 0);
        }

        public static Mesh Build(ACT.Frame frame, RoSpriteAtlas atlas) {
            if (atlas == null) {
                throw new ArgumentNullException(nameof(atlas));
            }

            return Build(frame, atlas.Sprites, atlas.RgbaFrameOffset);
        }

        private static readonly List<Vector3> ScratchVertices = new List<Vector3>(64);
        private static readonly List<Vector3> ScratchNormals = new List<Vector3>(64);
        private static readonly List<Vector2> ScratchUvs = new List<Vector2>(64);
        private static readonly List<Vector2> ScratchClips = new List<Vector2>(64);
        private static readonly List<Color> ScratchColors = new List<Color>(64);
        private static readonly List<int> ScratchTriangles = new List<int>(96);

        private static Mesh Build(ACT.Frame frame, Sprite[] sprites, int rgbaFrameOffset) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (sprites == null) {
                throw new ArgumentNullException(nameof(sprites));
            }

            // scratch lists shared by every build (a Mesh is made on the main thread only): new sprites in a fight build
            // frames all the time, five lists each before
            List<Vector3> vertices = ScratchVertices, normals = ScratchNormals;
            List<Vector2> uvs = ScratchUvs, clips = ScratchClips;
            List<Color> colors = ScratchColors;
            List<int> triangles = ScratchTriangles;
            vertices.Clear();
            normals.Clear();
            uvs.Clear();
            clips.Clear();
            colors.Clear();
            triangles.Clear();

            for (int i = 0; i < frame.layers.Length; i++) {
                AddLayer(frame.layers[i], sprites, rgbaFrameOffset, vertices, normals, uvs, colors, triangles);
                // the clip's index in the frame (UV1.x): RoSpriteEnhanced keys later clips nearer, as the client does
                while (clips.Count < vertices.Count) clips.Add(new Vector2(i, 0f));
            }

            var mesh = new Mesh {
                name = "RO ACT Frame"
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, clips);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddLayer(
            ACT.Layer layer,
            Sprite[] sprites,
            int rgbaFrameOffset,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<Color> colors,
            List<int> triangles
        ) {
            int spriteIndex = ResolveSpriteIndex(layer, rgbaFrameOffset);
            if (spriteIndex < 0 || spriteIndex >= sprites.Length || sprites[spriteIndex] == null) {
                return;
            }

            Sprite sprite = sprites[spriteIndex];
            Vector2[] spriteVertices = sprite.vertices;
            Vector2[] spriteUvs = sprite.uv;
            if (spriteVertices.Length != 4 || spriteUvs.Length != 4) {
                return;
            }

            int start = vertices.Count;
            Quaternion rotation = Quaternion.Euler(0f, 0f, -layer.angle);
            var scale = new Vector3(layer.scale.x * (layer.isMirror ? -1f : 1f), layer.scale.y, 1f);
            float offsetX = Mathf.RoundToInt(sprite.rect.width) % 2 == 1 ? 0.5f : 0f;
            float offsetY = Mathf.RoundToInt(sprite.rect.height) % 2 == 1 ? 0.5f : 0f;
            Vector3 layerOffset = new Vector3(layer.pos.x - offsetX, -layer.pos.y + offsetY, 0f) / SPR.PIXELS_PER_UNIT;

            for (int i = 0; i < 4; i++) {
                Vector3 vertex = rotation * Vector3.Scale(spriteVertices[i], scale);
                vertices.Add(vertex + layerOffset);
                normals.Add(Vector3.back);
                uvs.Add(spriteUvs[i]);
                colors.Add(layer.color);
            }

            if (layer.isMirror) {
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
                triangles.Add(start + 1);
            } else {
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }
        }

        private static int ResolveSpriteIndex(ACT.Layer layer, int rgbaFrameOffset) {
            if (layer == null || layer.index < 0) {
                return -1;
            }

            return layer.sprType == SPR.TYPE_RGBA ? layer.index + Mathf.Max(0, rgbaFrameOffset) : layer.index;
        }
    }
}
