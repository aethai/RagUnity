using System;
using UnityEngine;

namespace Ragnarok.Sprites {
    public sealed class RoSpriteAtlas : IDisposable {
        public RoSpriteAtlas(Texture2D texture, Sprite[] sprites, Rect[] rects, Vector2Int[] sizes, int rgbaFrameOffset = 0,
            bool hasPartialAlpha = true) {
            Texture = texture;
            Sprites = sprites ?? Array.Empty<Sprite>();
            Rects = rects ?? Array.Empty<Rect>();
            Sizes = sizes ?? Array.Empty<Vector2Int>();
            RgbaFrameOffset = Math.Max(0, rgbaFrameOffset);
            HasPartialAlpha = hasPartialAlpha;
        }

        public Texture2D Texture { get; }
        /// <summary>
        /// Some texel's alpha is neither 0 nor 255 (an RGBA frame with soft alpha); true unless the builder checked it.
        /// Only atlases without it may draw their layers in one pass (RoActSpriteAnimator.ApplyDepthMode).
        /// </summary>
        public bool HasPartialAlpha { get; }
        public Sprite[] Sprites { get; }
        public Rect[] Rects { get; }
        public Vector2Int[] Sizes { get; }
        public int RgbaFrameOffset { get; }

        public void Dispose() {
            DestroyObject(Texture);
            for (int i = 0; i < Sprites.Length; i++) {
                DestroyObject(Sprites[i]);
            }
        }

        private static void DestroyObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                UnityEngine.Object.Destroy(target);
            } else {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
