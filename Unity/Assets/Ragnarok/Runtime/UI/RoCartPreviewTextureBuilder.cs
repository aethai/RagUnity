using System;
using System.Collections.Generic;
using System.Globalization;
using Ragnarok.Assets;
using Ragnarok.Network;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>Composites a cart sprite's first frame into pixels for the cart previews (UISelectCartWnd).</summary>
    internal static class RoCartPreviewTextureBuilder {
        /// <summary>The middle frame of action 0 composed on a transparent canvas around (originX, originY), rows top-down.</summary>
        public static bool TryBuildPixels(SPR spr, ACT act, int canvasWidth, int canvasHeight, int originX, int originY, out Color32[] topDown) {
            topDown = null;
            if (spr == null || act?.actions == null || act.actions.Length == 0 || canvasWidth <= 0 || canvasHeight <= 0) {
                return false;
            }

            RoSpriteAtlas atlas = null;
            try {
                atlas = RoSpriteAtlasBuilder.Build(spr);
                ACT.Frame frame = PickPreviewFrame(act);
                if (frame?.layers == null || frame.layers.Length == 0) {
                    return false;
                }

                var layers = new List<CompositedLayer>(frame.layers.Length);
                for (int i = 0; i < frame.layers.Length; i++) {
                    if (TryCreateLayer(spr, atlas.RgbaFrameOffset, frame.layers[i], out CompositedLayer layer)) {
                        layers.Add(layer);
                    }
                }

                byte[] pixels = new byte[checked(canvasWidth * canvasHeight * 4)];
                if (layers.Count == 0 || !CompositeLayers(pixels, canvasWidth, canvasHeight, layers, originX, originY)) {
                    return false;
                }

                // the canvas is stored bottom-up (texture rows)
                topDown = new Color32[canvasWidth * canvasHeight];
                for (int y = 0; y < canvasHeight; y++) {
                    int src = (canvasHeight - 1 - y) * canvasWidth * 4;
                    for (int x = 0; x < canvasWidth; x++) {
                        int o = src + x * 4;
                        byte a = pixels[o + 3];
                        topDown[y * canvasWidth + x] = a < 128 ? default : new Color32(pixels[o], pixels[o + 1], pixels[o + 2], 255);
                    }
                }

                return true;
            } catch (Exception ex) {
                Debug.LogWarning($"Could not build a cart preview: {ex.Message}");
                return false;
            } finally {
                atlas?.Dispose();
            }
        }

        private const int RobrowserBottomOriginPaddingPixels = 10;
        private const float RobrowserMiddleOfCellPixels = 17.5f;

        public static bool TryBuild(
            SPR spr,
            ACT act,
            int canvasWidth,
            int canvasHeight,
            string textureName,
            out Texture2D texture
        ) {
            texture = null;
            if (spr == null || act?.actions == null || act.actions.Length == 0 ||
                canvasWidth <= 0 || canvasHeight <= 0) {
                return false;
            }

            RoSpriteAtlas atlas = null;
            try {
                atlas = RoSpriteAtlasBuilder.Build(spr);
                ACT.Frame frame = PickPreviewFrame(act);
                if (frame?.layers == null || frame.layers.Length == 0) {
                    return false;
                }

                var layers = new List<CompositedLayer>(frame.layers.Length);
                for (int i = 0; i < frame.layers.Length; i++) {
                    if (TryCreateLayer(spr, atlas.RgbaFrameOffset, frame.layers[i], out CompositedLayer layer)) {
                        layers.Add(layer);
                    }
                }

                if (layers.Count == 0) {
                    return false;
                }

                byte[] pixels = new byte[checked(canvasWidth * canvasHeight * 4)];
                bool painted = CompositeLayers(
                    pixels,
                    canvasWidth,
                    canvasHeight,
                    layers,
                    canvasWidth / 2,
                    canvasHeight + RobrowserBottomOriginPaddingPixels);

                int outputWidth = canvasWidth;
                int outputHeight = canvasHeight;
                if (!painted) {
                    CompositedFrameLayout layout = BuildLayoutFromLayers(layers);
                    outputWidth = layout.Width;
                    outputHeight = layout.Height;
                    pixels = new byte[checked(outputWidth * outputHeight * 4)];
                    painted = CompositeLayers(
                        pixels,
                        outputWidth,
                        outputHeight,
                        layers,
                        layout.CanvasOffsetX,
                        layout.CanvasOffsetY);
                }

                if (!painted) {
                    return false;
                }

                texture = new Texture2D(outputWidth, outputHeight, TextureFormat.RGBA32, false, linear: false) {
                    name = textureName,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0
                };
                texture.LoadRawTextureData(pixels);
                texture.Apply(false, false);
                return true;
            } catch (Exception ex) {
                Debug.LogWarning($"Could not build Change Cart preview '{textureName}': {ex.Message}");
                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(texture);
                } else {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                texture = null;
                return false;
            } finally {
                atlas?.Dispose();
            }
        }

        private static ACT.Frame PickPreviewFrame(ACT act) {
            ACT.Action action = act.actions[0];
            if (action?.frames == null || action.frames.Length == 0) {
                return null;
            }

            return action.frames[action.frames.Length / 2];
        }

        private static bool TryCreateLayer(
            SPR spr,
            int rgbaFrameOffset,
            ACT.Layer layer,
            out CompositedLayer compositedLayer
        ) {
            compositedLayer = default;
            int spriteIndex = ResolveSpriteFrameIndex(layer, rgbaFrameOffset);
            if (spriteIndex < 0 || spriteIndex >= spr.frames.Length) {
                return false;
            }

            SPR.Frame sourceFrame = spr.frames[spriteIndex];
            if (sourceFrame == null || sourceFrame.type != SPR.TYPE_RGBA || sourceFrame.data == null) {
                return false;
            }

            int sourceWidth = sourceFrame.width;
            int sourceHeight = sourceFrame.height;
            if (sourceWidth <= 0 || sourceHeight <= 0) {
                return false;
            }

            float scaleX = ResolveLayerScale(layer.scale.x);
            float scaleY = ResolveLayerScale(layer.scale.y);
            int scaledWidth = Math.Max(1, Mathf.RoundToInt(sourceWidth * scaleX));
            int scaledHeight = Math.Max(1, Mathf.RoundToInt(sourceHeight * scaleY));
            int left = Mathf.RoundToInt(layer.pos.x - scaledWidth * 0.5f);
            int top = Mathf.RoundToInt(layer.pos.y - RobrowserMiddleOfCellPixels - scaledHeight * 0.5f);
            bool mirrored = layer.isMirror ^ (layer.scale.x < 0f);
            compositedLayer = new CompositedLayer(
                sourceFrame,
                sourceWidth,
                sourceHeight,
                scaledWidth,
                scaledHeight,
                left,
                top,
                scaleX,
                scaleY,
                mirrored,
                ResolveLayerTint(layer));
            return true;
        }

        private static int ResolveSpriteFrameIndex(ACT.Layer layer, int rgbaFrameOffset) {
            if (layer == null || layer.index < 0) {
                return -1;
            }

            return layer.sprType == SPR.TYPE_RGBA
                ? layer.index + Mathf.Max(0, rgbaFrameOffset)
                : layer.index;
        }

        private static float ResolveLayerScale(float scale) {
            float absolute = Mathf.Abs(scale);
            return absolute <= 0.0001f ? 1f : Mathf.Max(0.01f, absolute);
        }

        private static Color ResolveLayerTint(ACT.Layer layer) {
            Color color = layer != null ? layer.color : Color.clear;
            if (Mathf.Approximately(color.r, 0f) &&
                Mathf.Approximately(color.g, 0f) &&
                Mathf.Approximately(color.b, 0f) &&
                Mathf.Approximately(color.a, 0f)) {
                return Color.white;
            }

            return color;
        }

        private static bool CompositeLayers(
            byte[] canvas,
            int canvasWidth,
            int canvasHeight,
            IReadOnlyList<CompositedLayer> layers,
            int originX,
            int originY
        ) {
            bool painted = false;
            for (int i = 0; i < layers.Count; i++) {
                painted |= CompositeLayer(canvas, canvasWidth, canvasHeight, layers[i], originX, originY);
            }

            return painted;
        }

        private static bool CompositeLayer(
            byte[] canvas,
            int canvasWidth,
            int canvasHeight,
            CompositedLayer layer,
            int originX,
            int originY
        ) {
            bool painted = false;
            for (int y = 0; y < layer.ScaledHeight; y++) {
                int targetY = y + layer.Top + originY;
                if (targetY < 0 || targetY >= canvasHeight) {
                    continue;
                }

                int sourceY = Mathf.Clamp((int) (y / layer.ScaleY), 0, layer.SourceHeight - 1);
                for (int x = 0; x < layer.ScaledWidth; x++) {
                    int targetX = x + layer.Left + originX;
                    if (targetX < 0 || targetX >= canvasWidth) {
                        continue;
                    }

                    int sourceX = Mathf.Clamp((int) (x / layer.ScaleX), 0, layer.SourceWidth - 1);
                    if (layer.Mirrored) {
                        sourceX = layer.SourceWidth - 1 - sourceX;
                    }

                    int sourceOffset = GetCompiledFramePixelOffset(layer.Frame, layer.SourceWidth, layer.SourceHeight, sourceX, sourceY);
                    byte sourceAlphaByte = layer.Frame.data[sourceOffset + 3];
                    if (sourceAlphaByte == 0) {
                        continue;
                    }

                    float sourceR = (layer.Frame.data[sourceOffset] / 255f) * layer.Tint.r;
                    float sourceG = (layer.Frame.data[sourceOffset + 1] / 255f) * layer.Tint.g;
                    float sourceB = (layer.Frame.data[sourceOffset + 2] / 255f) * layer.Tint.b;
                    float sourceA = (sourceAlphaByte / 255f) * layer.Tint.a;
                    if (sourceA <= 0f) {
                        continue;
                    }

                    int canvasY = canvasHeight - 1 - targetY;
                    int targetOffset = ((canvasY * canvasWidth) + targetX) * 4;
                    float destinationR = canvas[targetOffset] / 255f;
                    float destinationG = canvas[targetOffset + 1] / 255f;
                    float destinationB = canvas[targetOffset + 2] / 255f;
                    float destinationA = canvas[targetOffset + 3] / 255f;

                    float outA = sourceA + destinationA * (1f - sourceA);
                    if (outA <= 0.0001f) {
                        continue;
                    }

                    float outR = (sourceR * sourceA + destinationR * destinationA * (1f - sourceA)) / outA;
                    float outG = (sourceG * sourceA + destinationG * destinationA * (1f - sourceA)) / outA;
                    float outB = (sourceB * sourceA + destinationB * destinationA * (1f - sourceA)) / outA;

                    canvas[targetOffset] = (byte) Mathf.Clamp(Mathf.RoundToInt(outR * 255f), 0, 255);
                    canvas[targetOffset + 1] = (byte) Mathf.Clamp(Mathf.RoundToInt(outG * 255f), 0, 255);
                    canvas[targetOffset + 2] = (byte) Mathf.Clamp(Mathf.RoundToInt(outB * 255f), 0, 255);
                    canvas[targetOffset + 3] = (byte) Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255);
                    painted = true;
                }
            }

            return painted;
        }

        private static int GetCompiledFramePixelOffset(SPR.Frame frame, int sourceWidth, int sourceHeight, int x, int yFromTop) {
            int rawY = sourceHeight - 1 - yFromTop;
            int rawX = x;
            return ((rawY * frame.width) + rawX) * 4;
        }

        private static CompositedFrameLayout BuildLayoutFromLayers(IReadOnlyList<CompositedLayer> layers) {
            int minLeft = int.MaxValue;
            int minTop = int.MaxValue;
            int maxRight = int.MinValue;
            int maxBottom = int.MinValue;

            for (int i = 0; i < layers.Count; i++) {
                CompositedLayer layer = layers[i];
                minLeft = Math.Min(minLeft, layer.Left);
                minTop = Math.Min(minTop, layer.Top);
                maxRight = Math.Max(maxRight, layer.Right);
                maxBottom = Math.Max(maxBottom, layer.Bottom);
            }

            int halfWidth = Math.Max(Math.Abs(minLeft), Math.Abs(maxRight));
            int width = Math.Max(1, halfWidth * 2 + 1);
            int contentHeight = Math.Max(1, maxBottom - minTop + 1);
            int topPadding = contentHeight % 2 == 0 ? 1 : 0;
            int height = Math.Max(1, contentHeight + topPadding);
            return new CompositedFrameLayout(width, height, halfWidth, topPadding - minTop);
        }

        private readonly struct CompositedLayer {
            public CompositedLayer(
                SPR.Frame frame,
                int sourceWidth,
                int sourceHeight,
                int scaledWidth,
                int scaledHeight,
                int left,
                int top,
                float scaleX,
                float scaleY,
                bool mirrored,
                Color tint
            ) {
                Frame = frame;
                SourceWidth = sourceWidth;
                SourceHeight = sourceHeight;
                ScaledWidth = scaledWidth;
                ScaledHeight = scaledHeight;
                Left = left;
                Top = top;
                ScaleX = scaleX;
                ScaleY = scaleY;
                Mirrored = mirrored;
                Tint = tint;
            }

            public SPR.Frame Frame { get; }
            public int SourceWidth { get; }
            public int SourceHeight { get; }
            public int ScaledWidth { get; }
            public int ScaledHeight { get; }
            public int Left { get; }
            public int Top { get; }
            public int Right => Left + ScaledWidth - 1;
            public int Bottom => Top + ScaledHeight - 1;
            public float ScaleX { get; }
            public float ScaleY { get; }
            public bool Mirrored { get; }
            public Color Tint { get; }
        }

        private readonly struct CompositedFrameLayout {
            public CompositedFrameLayout(int width, int height, int canvasOffsetX, int canvasOffsetY) {
                Width = width;
                Height = height;
                CanvasOffsetX = canvasOffsetX;
                CanvasOffsetY = canvasOffsetY;
            }

            public int Width { get; }
            public int Height { get; }
            public int CanvasOffsetX { get; }
            public int CanvasOffsetY { get; }
        }
    }
}
