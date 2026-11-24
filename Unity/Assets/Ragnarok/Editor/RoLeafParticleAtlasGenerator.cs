using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ragnarok.Editor {
    internal static class RoLeafParticleAtlasGenerator {
        private const int Size = 64;
        private const string AssetPath = "Assets/Ragnarok/Resources/Weather/RoLeafParticleAtlas.png";

        [MenuItem("Ragnarok/Generate Leaf Particle Atlas", false, 301)]
        private static void Generate() {
            string directory = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true, linear: false) {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color32[] pixels = new Color32[Size * Size];
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = new Color32(0, 0, 0, 0);
            }

            DrawLeaf(pixels, 10f, 32f, 11f, 21f, -18f, new Color32(112, 134, 47, 255), new Color32(162, 178, 76, 255));
            DrawLeaf(pixels, 32f, 32f, 10f, 19f, 12f, new Color32(143, 98, 38, 255), new Color32(186, 137, 57, 255));
            DrawLeaf(pixels, 53f, 32f, 9f, 18f, 28f, new Color32(73, 119, 52, 255), new Color32(126, 156, 67, 255));

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
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
                name = "DefaultTexturePlatform",
                overridden = true,
                maxTextureSize = Size,
                format = TextureImporterFormat.RGBA32,
                textureCompression = TextureImporterCompression.Uncompressed
            });
        }

        private static void DrawLeaf(Color32[] pixels, float centerX, float centerY, float radiusX, float radiusY, float rotationDegrees, Color32 edge, Color32 center) {
            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radiusY - 2f));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(centerX + radiusY + 2f));
            int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radiusY - 2f));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(centerY + radiusY + 2f));

            for (int y = minY; y <= maxY; y++) {
                for (int x = minX; x <= maxX; x++) {
                    float px = x + 0.5f - centerX;
                    float py = y + 0.5f - centerY;
                    float lx = px * cos + py * sin;
                    float ly = -px * sin + py * cos;
                    float nx = lx / Mathf.Max(1f, radiusX);
                    float ny = ly / Mathf.Max(1f, radiusY);
                    float taper = Mathf.Clamp01(1f - Mathf.Abs(ny) * 0.45f);
                    float shape = (nx * nx) / Mathf.Max(0.15f, taper) + ny * ny;
                    if (shape > 1f) {
                        continue;
                    }

                    float vein = 1f - Mathf.SmoothStep(0.02f, 0.11f, Mathf.Abs(nx));
                    float edgeBlend = Mathf.SmoothStep(0.58f, 1f, shape);
                    Color color = Color32.Lerp(center, edge, edgeBlend);
                    color = Color.Lerp(color, new Color(0.9f, 0.82f, 0.42f, 1f), vein * 0.22f);
                    color.a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.78f, 1f, shape));
                    pixels[x + y * Size] = color;
                }
            }
        }
    }
}
