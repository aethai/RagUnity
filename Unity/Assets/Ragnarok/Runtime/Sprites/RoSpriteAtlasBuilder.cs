using System;
using System.Collections.Generic;
using ROIO.Models.FileTypes;
using Unity.Collections;
using UnityEngine;

namespace Ragnarok.Sprites {
    public static class RoSpriteAtlasBuilder {
        static readonly Unity.Profiling.ProfilerMarker CompileMarker = new Unity.Profiling.ProfilerMarker("SpriteAtlas.CompileRgba");
        static readonly Unity.Profiling.ProfilerMarker TextureMarker = new Unity.Profiling.ProfilerMarker("SpriteAtlas.Texture");
        static readonly Unity.Profiling.ProfilerMarker SpritesMarker = new Unity.Profiling.ProfilerMarker("SpriteAtlas.Sprites");

        public const int AtlasPadding = 2;
        public const int MaxAtlasSize = 2048;

        public static RoSpriteAtlas Build(SPR source, byte[] paletteOverride = null, int maxAtlasSize = MaxAtlasSize) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            using (CompileMarker.Auto()) {
                EnsureRgbaCompiled(source, paletteOverride);
            }

            var sizes = new Vector2Int[source.frames.Length];
            var frames = new List<FramePackInfo>(source.frames.Length);
            for (int i = 0; i < source.frames.Length; i++) {
                SPR.Frame frame = source.frames[i];
                if (frame == null || frame.type != SPR.TYPE_RGBA) {
                    throw new InvalidOperationException($"SPR frame {i} is not available as RGBA data.");
                }

                if (frame.width <= 0 || frame.height <= 0 || frame.data == null ||
                    frame.data.Length < frame.width * frame.height * 4) {
                    throw new InvalidOperationException($"SPR frame {i} has invalid RGBA dimensions or data.");
                }

                sizes[i] = ResolveSourceFrameSize(frame);
                frames.Add(new FramePackInfo(i, frame.width, frame.height));
            }

            if (!TryPackFrames(frames, maxAtlasSize, out Vector2Int atlasSize, out RectInt[] placements)) {
                throw new InvalidOperationException(
                    $"SPR '{source.filename ?? "sprite"}' does not fit within a {maxAtlasSize}x{maxAtlasSize} atlas.");
            }

            TextureMarker.Begin();
            var atlas = new Texture2D(atlasSize.x, atlasSize.y, TextureFormat.RGBA32, false) {
                name = $"{source.filename ?? "sprite"}:atlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            // the frames go straight into the texture's memory (a managed atlas array per sprite was most of a new
            // sprite's garbage); the gaps between frames are cleared first
            NativeArray<byte> pixels = atlas.GetRawTextureData<byte>();
            ClearPixels(pixels);
            for (int i = 0; i < source.frames.Length; i++) {
                CopyFramePixels(source.frames[i], pixels, atlasSize.x, placements[i]);
            }

            atlas.Apply(false, false);
            TextureMarker.End();
            using var spritesMarker = SpritesMarker.Auto();

            var rects = new Rect[source.frames.Length];
            var sprites = new Sprite[source.frames.Length];
            for (int i = 0; i < placements.Length; i++) {
                RectInt placement = placements[i];
                SPR.Frame frame = source.frames[i];
                Vector2Int sourceSize = sizes[i];
                int sourceX = placement.x + Math.Max(0, (frame.width - sourceSize.x) / 2);
                int sourceY = placement.y + Math.Max(0, (frame.height - sourceSize.y) / 2);
                rects[i] = new Rect(
                    sourceX / (float) atlas.width,
                    sourceY / (float) atlas.height,
                    sourceSize.x / (float) atlas.width,
                    sourceSize.y / (float) atlas.height);

                var spriteRect = new Rect(sourceX, sourceY, sourceSize.x, sourceSize.y);
                sprites[i] = Sprite.Create(atlas, spriteRect, new Vector2(0.5f, 0.5f), SPR.PIXELS_PER_UNIT, 0, SpriteMeshType.FullRect);
                sprites[i].name = $"{source.filename ?? "sprite"}:{i}";
            }

            return new RoSpriteAtlas(atlas, sprites, rects, sizes, source.oldRgbaIndex, HasPartialAlpha(source));
        }

        /// <summary>
        /// Any texel alpha other than 0 and 255? Palette frames (before oldRgbaIndex) compile to 0 / 255 only, so just the
        /// RGBA frames are read.
        /// </summary>
        private static bool HasPartialAlpha(SPR source) {
            for (int i = Math.Max(0, (int) source.oldRgbaIndex); i < source.frames.Length; i++) {
                byte[] data = source.frames[i]?.data;
                if (data == null) {
                    continue;
                }

                for (int a = 3; a < data.Length; a += 4) {
                    byte alpha = data[a];
                    if (alpha != 0 && alpha != 255) {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Vector2Int ResolveSourceFrameSize(SPR.Frame frame) {
            int width = frame.originalWidth > 0 && frame.originalWidth <= frame.width
                ? frame.originalWidth
                : frame.width;
            int height = frame.originalHeight > 0 && frame.originalHeight <= frame.height
                ? frame.originalHeight
                : frame.height;
            return new Vector2Int(width, height);
        }

        private static void EnsureRgbaCompiled(SPR source, byte[] paletteOverride) {
            if (source.frames == null) {
                throw new InvalidOperationException("SPR has no frame data.");
            }

            if (!source.compiled) {
                if (source.indexedCount > 0) {
                    if ((paletteOverride == null || paletteOverride.Length == 0) && source.palette == null) {
                        throw new InvalidOperationException("Indexed SPR frames require an embedded or override palette.");
                    }
                }

                source.CompileRgba(paletteOverride ?? Array.Empty<byte>());
            }
        }

        private static bool TryPackFrames(
            List<FramePackInfo> frames,
            int maxAtlasSize,
            out Vector2Int atlasSize,
            out RectInt[] placements
        ) {
            maxAtlasSize = Mathf.Max(1, maxAtlasSize);
            placements = new RectInt[frames.Count];
            atlasSize = default;
            if (frames.Count == 0) {
                return false;
            }

            int minimumSize = 1;
            for (int i = 0; i < frames.Count; i++) {
                minimumSize = Math.Max(minimumSize, frames[i].Width + AtlasPadding * 2);
                minimumSize = Math.Max(minimumSize, frames[i].Height + AtlasPadding * 2);
            }

            int size = Mathf.NextPowerOfTwo(minimumSize);
            for (; size <= maxAtlasSize; size *= 2) {
                if (TryPackFramesIntoSquare(frames, size, placements)) {
                    atlasSize = new Vector2Int(size, size);
                    return true;
                }
            }

            return false;
        }

        private static bool TryPackFramesIntoSquare(List<FramePackInfo> frames, int atlasSize, RectInt[] placements) {
            var sorted = new List<FramePackInfo>(frames);
            sorted.Sort((left, right) => {
                int heightCompare = right.Height.CompareTo(left.Height);
                return heightCompare != 0 ? heightCompare : right.Width.CompareTo(left.Width);
            });

            int x = AtlasPadding;
            int y = AtlasPadding;
            int contentLimit = atlasSize - AtlasPadding;
            int shelfHeight = 0;
            for (int i = 0; i < sorted.Count; i++) {
                FramePackInfo frame = sorted[i];
                if (frame.Width > atlasSize - AtlasPadding * 2 ||
                    frame.Height > atlasSize - AtlasPadding * 2) {
                    return false;
                }

                if (x + frame.Width > contentLimit) {
                    x = AtlasPadding;
                    y += shelfHeight + AtlasPadding;
                    shelfHeight = 0;
                }

                if (y + frame.Height > contentLimit) {
                    return false;
                }

                placements[frame.Index] = new RectInt(
                    x,
                    y,
                    frame.Width,
                    frame.Height);
                x += frame.Width + AtlasPadding;
                shelfHeight = Math.Max(shelfHeight, frame.Height);
            }

            return true;
        }

        private static void CopyFramePixels(SPR.Frame frame, NativeArray<byte> atlasPixels, int atlasWidth, RectInt placement) {
            int rowBytes = frame.width * 4;
            for (int row = 0; row < frame.height; row++) {
                int sourceOffset = row * rowBytes;
                int targetOffset = ((placement.y + row) * atlasWidth + placement.x) * 4;
                NativeArray<byte>.Copy(frame.data, sourceOffset, atlasPixels, targetOffset, rowBytes);
            }
        }

        static readonly byte[] Zeros = new byte[64 * 1024];

        private static void ClearPixels(NativeArray<byte> pixels) {
            for (int offset = 0; offset < pixels.Length; offset += Zeros.Length) {
                NativeArray<byte>.Copy(Zeros, 0, pixels, offset, Math.Min(Zeros.Length, pixels.Length - offset));
            }
        }

        private readonly struct FramePackInfo {
            public FramePackInfo(int index, int width, int height) {
                Index = index;
                Width = width;
                Height = height;
            }

            public int Index { get; }
            public int Width { get; }
            public int Height { get; }
        }
    }
}
