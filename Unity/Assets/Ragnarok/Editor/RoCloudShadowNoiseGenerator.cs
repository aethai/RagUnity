using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    internal static class RoCloudShadowNoiseGenerator {
        private const int Size = 512;
        private const int Seed = 0x51A7C10D;
        private const string AssetPath = "Assets/Ragnarok/Resources/Weather/RoCloudShadowNoise.png";

        [MenuItem("Ragnarok/Generate Cloud Shadow Noise", false, 300)]
        private static void Generate() {
            string directory = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.R8, mipChain: true, linear: true) {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++) {
                for (int x = 0; x < Size; x++) {
                    float value = SampleFbm((float)x / Size, (float)y / Size);
                    byte gray = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
                    pixels[y * Size + x] = new Color32(gray, gray, gray, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            File.WriteAllBytes(AssetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureImporter() {
            if (AssetImporter.GetAtPath(AssetPath) is not TextureImporter importer) {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = "DefaultTexturePlatform",
                overridden = true,
                maxTextureSize = Size,
                format = TextureImporterFormat.R8,
                textureCompression = TextureImporterCompression.Uncompressed
            });
        }

        private static float SampleFbm(float u, float v) {
            float amplitude = 0.55f;
            float total = 0f;
            float weight = 0f;
            int period = 4;
            for (int octave = 0; octave < 6; octave++) {
                total += TileableValueNoise(u, v, period, Seed + octave * 131) * amplitude;
                weight += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            float normalized = total / Mathf.Max(weight, 0.0001f);
            return Mathf.SmoothStep(0f, 1f, normalized);
        }

        private static float TileableValueNoise(float u, float v, int period, int seed) {
            float x = u * period;
            float y = v * period;
            int x0 = Mathf.FloorToInt(x) % period;
            int y0 = Mathf.FloorToInt(y) % period;
            int x1 = (x0 + 1) % period;
            int y1 = (y0 + 1) % period;
            float tx = Fade(x - Mathf.Floor(x));
            float ty = Fade(y - Mathf.Floor(y));

            float a = Hash01(x0, y0, seed);
            float b = Hash01(x1, y0, seed);
            float c = Hash01(x0, y1, seed);
            float d = Hash01(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Fade(float t) {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float Hash01(int x, int y, int seed) {
            unchecked {
                uint h = (uint)seed;
                h ^= (uint)x * 0x9E3779B9u;
                h ^= (uint)y * 0x85EBCA6Bu;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (h & 0x00FFFFFFu) / 16777215f;
            }
        }
    }
}
