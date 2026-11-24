using ROIO.Models.FileTypes;
using ROIO.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ROIO.Loaders {
    public class EffectLoader {
        private const int StrReservedHeaderBytes = 16;
        private const int StrTextureNameBytes = 128;
        private const int StrAnimationRecordBytes = 124;

        private static Dictionary<string, int> textureIdLookup = new Dictionary<string, int>();
        private static List<string> textureNames = new List<string>();
        private static List<Texture2D> textures = new List<Texture2D>();
        private static Texture2D missingTexture;

        public static STR Load(MemoryStreamReader data, string path) {
            return Load(data, path, null);
        }

        public static STR Load(MemoryStreamReader data, string path, Func<string, Texture2D> textureResolver) {
            var header = RoLoaderValidation.ReadBinaryString(data, 4, "EffectLoader.Load header");

            if (!header.Equals(STR.Header)) {
                throw new Exception("EffectLoader.Load: Header (" + header + ") is not \"STRM\"");
            }

            var version = RoLoaderValidation.ReadUInt(data, "EffectLoader.Load version");
            if (version != 0x94) {
                throw new Exception("EffectLoader.Load: Unsupported STR version (v" + version + ")");
            }

            textureIdLookup.Clear();
            textureNames.Clear();
            textures.Clear();
            var diagnostics = new List<STR.TextureLoadDiagnostic>();

            STR str = ScriptableObject.CreateInstance<STR>();
            str.name = path;
            str.version = version;
            str.fps = RoLoaderValidation.ReadUInt(data, "EffectLoader.Load fps");
            str.maxKey = RoLoaderValidation.ReadUInt(data, "EffectLoader.Load max key");
            var layerCount = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadUInt(data, "EffectLoader.Load layer count"), RoLoaderValidation.MaxCollectionCount, "layer", "EffectLoader.Load");
            RoLoaderValidation.RequireRemaining(data, StrReservedHeaderBytes, "EffectLoader.Load reserved header data");
            data.Seek(StrReservedHeaderBytes, System.IO.SeekOrigin.Current);

            //read layers
            str.layers = new STR.Layer[layerCount];
            for (int i = 0; i < layerCount; i++) {
                STR.Layer layer = str.layers[i] = new STR.Layer();

                //read texture filenames
                var textureCount = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadInt(data, $"EffectLoader.Load layer {i} texture count"), RoLoaderValidation.MaxCollectionCount, "texture", $"EffectLoader.Load layer {i}");
                RoLoaderValidation.RequireRemaining(data, checked((long) textureCount * StrTextureNameBytes), $"EffectLoader.Load layer {i} texture name data");
                layer.textures = new Texture2D[textureCount];
                layer.texturesIds = new List<int>();
                for (int j = 0; j < textureCount; j++) {
                    var tex = data.ReadBinaryString(StrTextureNameBytes);
                    IReadOnlyList<string> candidates = BuildTextureLookupCandidates(path, tex);
                    TextureLoadResult loadResult = LoadTexture(candidates, textureResolver);
                    var texture = loadResult.Texture;
                    layer.textures[j] = texture;
                    diagnostics.Add(new STR.TextureLoadDiagnostic {
                        layerIndex = (int) i,
                        textureIndex = j,
                        textureName = tex ?? string.Empty,
                        requestedPath = candidates.Count > 0 ? candidates[0] : string.Empty,
                        resolvedPath = loadResult.ResolvedPath,
                        candidatePaths = ToArray(candidates),
                        usedTransparentFallback = loadResult.UsedTransparentFallback
                    });

                    if (!textureNames.Contains(tex)) {
                        layer.texturesIds.Add(textureNames.Count);
                        textureIdLookup.Add(tex, textureNames.Count);
                        textureNames.Add(tex);
                        textures.Add(texture);
                    } else {
                        layer.texturesIds.Add(textureIdLookup[tex]);
                    }
                }

                //read animations
                var animCount = RoLoaderValidation.RequireCount(RoLoaderValidation.ReadInt(data, $"EffectLoader.Load layer {i} animation count"), RoLoaderValidation.MaxCollectionCount, "animation", $"EffectLoader.Load layer {i}");
                RoLoaderValidation.RequireRemaining(data, checked((long) animCount * StrAnimationRecordBytes), $"EffectLoader.Load layer {i} animation records");
                layer.animations = new STR.Animation[animCount];
                for (int j = 0; j < animCount; j++) {

                    var entry = new STR.Animation() {
                        frame = data.ReadInt(),
                        type = data.ReadUInt(),
                        position = new Vector2(data.ReadFloat(), data.ReadFloat())
                    };

                    var uv = new float[] {
                        data.ReadFloat(), data.ReadFloat(), data.ReadFloat(), data.ReadFloat(),
                        data.ReadFloat(), data.ReadFloat(), data.ReadFloat(), data.ReadFloat()
                    };
                    var xy = new float[] {
                        data.ReadFloat(), data.ReadFloat(), data.ReadFloat(), data.ReadFloat(),
                        data.ReadFloat(), data.ReadFloat(), data.ReadFloat(), data.ReadFloat()
                    };

                    entry.uv = new Vector2[4];
                    entry.uv[0] = new Vector2(uv[0], uv[1]);
                    entry.uv[1] = new Vector2(uv[2], uv[3]);
                    entry.uv[2] = new Vector2(uv[4], uv[5]);
                    entry.uv[3] = new Vector2(uv[6], uv[7]);

                    entry.xy = new Vector2[4];
                    entry.xy[0] = new Vector2(xy[0], -xy[4]);
                    entry.xy[1] = new Vector2(xy[1], -xy[5]);
                    entry.xy[2] = new Vector2(xy[3], -xy[7]);
                    entry.xy[3] = new Vector2(xy[2], -xy[6]);

                    entry.animFrame = data.ReadFloat();
                    entry.animType = data.ReadUInt();
                    entry.delay = data.ReadFloat();
                    entry.angle = data.ReadFloat() / (1024f / 360f);
                    entry.color = new Color(data.ReadFloat() / 255, data.ReadFloat() / 255, data.ReadFloat() / 255, data.ReadFloat() / 255);
                    entry.srcAlpha = data.ReadUInt();
                    entry.destAlpha = data.ReadUInt();
                    entry.mtPreset = data.ReadUInt();

                    layer.animations[j] = entry;
                }
            }

            str.textureDiagnostics = diagnostics.ToArray();
            return str;
        }

        public static bool TryLoad(MemoryStreamReader data, string path, out STR str, out string error) {
            try {
                str = Load(data, path);
                error = string.Empty;
                return true;
            } catch (Exception ex) {
                str = null;
                error = ex.Message;
                return false;
            }
        }

        public static IReadOnlyList<string> BuildTextureLookupCandidates(string path, string textureName) {
            var candidates = new List<string>();
            string exact = BuildTexturePath(path, textureName);
            AddUnique(candidates, exact);

            string extension = Path.GetExtension(exact);
            if (string.IsNullOrWhiteSpace(extension)) {
                AddUnique(candidates, exact + ".tga");
                AddUnique(candidates, exact + ".png");
                AddUnique(candidates, exact + ".bmp");
            } else {
                AddUnique(candidates, Path.ChangeExtension(exact, ".tga")?.Replace("\\", "/"));
                AddUnique(candidates, Path.ChangeExtension(exact, ".png")?.Replace("\\", "/"));
                AddUnique(candidates, Path.ChangeExtension(exact, ".bmp")?.Replace("\\", "/"));
            }

            return candidates.AsReadOnly();
        }

        public static string BuildTexturePath(string path, string textureName) {
            string basePath = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : NormalizeBasePath(path);

            string normalizedTexture = string.IsNullOrWhiteSpace(textureName)
                ? string.Empty
                : textureName.Trim().Replace("\\", "/").TrimStart('/');

            if (normalizedTexture.StartsWith("data/", StringComparison.OrdinalIgnoreCase)) {
                return normalizedTexture;
            }

            return string.IsNullOrEmpty(basePath) ? normalizedTexture : basePath + "/" + normalizedTexture;
        }

        private static TextureLoadResult LoadTexture(IReadOnlyList<string> addresses, Func<string, Texture2D> textureResolver) {
            if (addresses == null || addresses.Count == 0) {
                return TextureLoadResult.Missing(GetMissingTexture());
            }

            for (int i = 0; i < addresses.Count; i++) {
                string address = addresses[i];
                try {
                    Texture2D texture = textureResolver != null
                        ? textureResolver(address)
                        : FileManager.Load(address) as Texture2D;
                    if (texture != null) {
                        return TextureLoadResult.Found(texture, address);
                    }
                } catch (Exception ex) {
                    Debug.LogWarning($"Could not load STR texture '{address}': {ex.Message}");
                }
            }

            return TextureLoadResult.Missing(GetMissingTexture());
        }

        private static string NormalizeBasePath(string path) {
            string basePath = path.Trim().Replace("\\", "/").TrimEnd('/');
            if (basePath.EndsWith(".str", StringComparison.OrdinalIgnoreCase)) {
                return Path.GetDirectoryName(basePath)?.Replace("\\", "/") ?? string.Empty;
            }

            return basePath;
        }

        private static void AddUnique(List<string> candidates, string candidate) {
            if (!string.IsNullOrWhiteSpace(candidate) && !candidates.Contains(candidate)) {
                candidates.Add(candidate);
            }
        }

        private static string[] ToArray(IReadOnlyList<string> values) {
            if (values == null || values.Count == 0) {
                return Array.Empty<string>();
            }

            var copy = new string[values.Count];
            for (int i = 0; i < values.Count; i++) {
                copy[i] = values[i];
            }

            return copy;
        }

        private static Texture2D GetMissingTexture() {
            if (missingTexture != null) {
                return missingTexture;
            }

            missingTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
                name = "RO Missing STR Texture",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            missingTexture.SetPixel(0, 0, Color.clear);
            missingTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return missingTexture;
        }

        private readonly struct TextureLoadResult {
            private TextureLoadResult(Texture2D texture, string resolvedPath, bool usedTransparentFallback) {
                Texture = texture;
                ResolvedPath = resolvedPath ?? string.Empty;
                UsedTransparentFallback = usedTransparentFallback;
            }

            public Texture2D Texture { get; }
            public string ResolvedPath { get; }
            public bool UsedTransparentFallback { get; }

            public static TextureLoadResult Found(Texture2D texture, string resolvedPath) =>
                new TextureLoadResult(texture, resolvedPath, false);

            public static TextureLoadResult Missing(Texture2D fallback) =>
                new TextureLoadResult(fallback, string.Empty, true);
        }
    }
}
