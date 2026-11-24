using System;
using System.IO;
using UnityEngine;

namespace Ragnarok.Editor {
    /// <summary>
    /// HD texture detail transfer: Real-ESRGAN gives clean, sharp edges but wipes the grain off RO's textures (cobbles
    /// turn to soap). The final HD texture is the upscaled one plus the original's own fine detail - the source
    /// upsampled bilinearly minus its blur - so the stones keep their edges and get their grit and dark joints back.
    /// Used by Tools/HdTextures (up-x4plus/ + src/ -> hd/).
    /// </summary>
    public static class RoHdDetailTransfer {
        /// <param name="upscaledPath">the Real-ESRGAN result</param>
        /// <param name="sourcePath">the exported original (src/)</param>
        /// <param name="outputPath">the HD pack file (hd/)</param>
        /// <param name="strength">how much of the original's detail goes back on (1 = all of it)</param>
        /// <param name="radius">the detail's scale, in output pixels (a box blur run twice)</param>
        public static void Apply(string upscaledPath, string sourcePath, string outputPath, float strength = 1f, int radius = 3) {
            Texture2D up = Load(upscaledPath), src = Load(sourcePath);
            try {
                int w = up.width, h = up.height;
                Color32[] upPixels = up.GetPixels32();
                // the original at the HD size, bilinear (GetPixelBilinear runs bottom-up like GetPixels32)
                var grain = new float[w * h * 3];
                for (int y = 0; y < h; y++) {
                    for (int x = 0; x < w; x++) {
                        Color c = src.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                        int i = (y * w + x) * 3;
                        grain[i] = c.r;
                        grain[i + 1] = c.g;
                        grain[i + 2] = c.b;
                    }
                }

                float[] blurred = Blur(Blur(grain, w, h, radius), w, h, radius);
                var output = new Color32[w * h];
                for (int p = 0; p < w * h; p++) {
                    Color32 o = upPixels[p];
                    int i = p * 3;
                    output[p] = new Color32(
                        Add(o.r, grain[i] - blurred[i], strength),
                        Add(o.g, grain[i + 1] - blurred[i + 1], strength),
                        Add(o.b, grain[i + 2] - blurred[i + 2], strength),
                        o.a);
                }

                var result = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
                result.SetPixels32(output);
                result.Apply(false);
                File.WriteAllBytes(outputPath, result.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(result);
            } finally {
                UnityEngine.Object.DestroyImmediate(up);
                UnityEngine.Object.DestroyImmediate(src);
            }
        }

        static byte Add(byte value, float detail, float strength) {
            return (byte) Mathf.Clamp(Mathf.RoundToInt(value + detail * strength * 255f), 0, 255);
        }

        static Texture2D Load(string path) {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            if (!texture.LoadImage(File.ReadAllBytes(path), false)) {
                throw new InvalidDataException("not an image: " + path);
            }

            return texture;
        }

        // a separable box blur with wrap-around (the textures tile)
        static float[] Blur(float[] pixels, int w, int h, int radius) {
            var horizontal = new float[pixels.Length];
            var vertical = new float[pixels.Length];
            float norm = 1f / (radius * 2 + 1);
            for (int y = 0; y < h; y++) {
                for (int c = 0; c < 3; c++) {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += pixels[(y * w + Wrap(k, w)) * 3 + c];
                    for (int x = 0; x < w; x++) {
                        horizontal[(y * w + x) * 3 + c] = sum * norm;
                        sum += pixels[(y * w + Wrap(x + radius + 1, w)) * 3 + c] - pixels[(y * w + Wrap(x - radius, w)) * 3 + c];
                    }
                }
            }

            for (int x = 0; x < w; x++) {
                for (int c = 0; c < 3; c++) {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += horizontal[(Wrap(k, h) * w + x) * 3 + c];
                    for (int y = 0; y < h; y++) {
                        vertical[(y * w + x) * 3 + c] = sum * norm;
                        sum += horizontal[(Wrap(y + radius + 1, h) * w + x) * 3 + c] - horizontal[(Wrap(y - radius, h) * w + x) * 3 + c];
                    }
                }
            }

            return vertical;
        }

        static int Wrap(int value, int size) => ((value % size) + size) % size;
    }
}
