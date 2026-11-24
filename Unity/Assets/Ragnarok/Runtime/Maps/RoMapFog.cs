using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.Maps {
    public readonly struct RoMapFogSettings {
        public RoMapFogSettings(bool exists, float near, float far, Color color, float factor) {
            Exists = exists;
            Near = near;
            Far = far;
            Color = color;
            Factor = factor;
        }

        public bool Exists { get; }
        public float Near { get; }
        public float Far { get; }
        public Color Color { get; }
        public float Factor { get; }
        public bool IsValid => Exists && Far > Near;
    }

    public static class RoMapFog {
        public const string FogTablePath = "data/fogparametertable.txt";
        // roBrowser multiplies raw fogparametertable values by 240 before shader upload.
        // Keep this scale in shader depth space; do not divide it by the map tile scale.
        public const float RobrowserFogDistanceScale = 240f;

        private static readonly int UseId = Shader.PropertyToID("_RoFogUse");
        private static readonly int NearId = Shader.PropertyToID("_RoFogNear");
        private static readonly int FarId = Shader.PropertyToID("_RoFogFar");
        private static readonly int ColorId = Shader.PropertyToID("_RoFogColor");
        private static readonly int FactorId = Shader.PropertyToID("_RoFogFactor");

        public static bool TryLoadSettings(RoAssetDatabase assets, string mapName, out RoMapFogSettings settings) {
            settings = default;
            if (assets == null || !assets.IsInitialized) {
                return false;
            }

            if (!assets.Exists(FogTablePath)) {
                return false;
            }

            string table = assets.ReadText(FogTablePath);
            return TryParseTable(table, mapName, out settings);
        }

        public static bool TryParseTable(string table, string mapName, out RoMapFogSettings settings) {
            settings = default;
            if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(mapName)) {
                return false;
            }

            string requestedKey = NormalizeMapKey(mapName);

            // Real fogparametertable.txt format: each field on its own line ending with '#',
            // with optional // comment lines preceding map name entries in the same '#'-block.
            // Strategy: split entire file by '#', then per segment take the last non-empty
            // non-comment line as the token value. Process tokens in groups of 5:
            //   [mapname, near, far, color, factor]
            string[] segments = table.Split('#');
            var tokens = new List<string>(segments.Length);
            foreach (string segment in segments) {
                string value = null;
                int start = 0;
                while (start <= segment.Length) {
                    int end = segment.IndexOf('\n', start);
                    string line;
                    if (end < 0) {
                        line = segment.Substring(start).Trim().TrimEnd('\r');
                        start = segment.Length + 1;
                    } else {
                        line = segment.Substring(start, end - start).Trim().TrimEnd('\r');
                        start = end + 1;
                    }

                    if (line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal)) {
                        value = line;
                    }
                }

                if (value != null) {
                    tokens.Add(value);
                }
            }

            for (int i = 0; i + 4 < tokens.Count; i += 5) {
                string key = NormalizeMapKey(tokens[i]);
                if (!string.Equals(key, requestedKey, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (!TryParseFloat(tokens[i + 1], out float near) ||
                    !TryParseFloat(tokens[i + 2], out float far) ||
                    !TryParseColor(tokens[i + 3], out Color color) ||
                    !TryParseFloat(tokens[i + 4], out float factor)) {
                    continue;
                }

                settings = new RoMapFogSettings(
                    exists: true,
                    near: near * RobrowserFogDistanceScale,
                    far: far * RobrowserFogDistanceScale,
                    color: color,
                    factor: factor
                );
                return true;
            }

            return false;
        }

        public static void ApplyGlobals(RoMapFogSettings settings) {
            if (!settings.IsValid) {
                ApplyDisabledGlobals();
                return;
            }

            Shader.SetGlobalFloat(UseId, 1f);
            Shader.SetGlobalFloat(NearId, settings.Near);
            Shader.SetGlobalFloat(FarId, settings.Far);
            Shader.SetGlobalColor(ColorId, settings.Color);
            Shader.SetGlobalFloat(FactorId, Mathf.Clamp01(settings.Factor));
        }

        public static void ApplyDisabledGlobals() {
            Shader.SetGlobalFloat(UseId, 0f);
            Shader.SetGlobalFloat(NearId, 0f);
            Shader.SetGlobalFloat(FarId, 1f);
            Shader.SetGlobalColor(ColorId, Color.white);
            Shader.SetGlobalFloat(FactorId, 0f);
        }

        public static float EvaluateFogBlendForDepth(RoMapFogSettings settings, float fogDepth) {
            if (!settings.IsValid) {
                return 0f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(settings.Near, settings.Far, fogDepth));
        }

        public static string BuildAudit(string mapName, RoMapFogSettings settings) {
            if (!settings.IsValid) {
                return $"RO map fog: disabled for '{mapName}'.";
            }

            return
                $"RO map fog for '{mapName}': near={settings.Near:0.###}, far={settings.Far:0.###}, " +
                $"color=({settings.Color.r:0.###},{settings.Color.g:0.###},{settings.Color.b:0.###}), factor={settings.Factor:0.###}";
        }

        private static string NormalizeMapKey(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            string normalized = value.Trim().Replace('\\', '/');
            normalized = Path.GetFileNameWithoutExtension(normalized);
            return normalized.ToLowerInvariant();
        }

        private static bool TryParseFloat(string value, out float result) {
            return float.TryParse(
                value?.Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result
            );
        }

        private static bool TryParseColor(string value, out Color color) {
            color = Color.white;
            string hex = value?.Trim() ?? string.Empty;
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                hex = hex.Substring(2);
            }

            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint packed)) {
                return false;
            }

            color = new Color(
                ((packed >> 16) & 0xff) / 255f,
                ((packed >> 8) & 0xff) / 255f,
                (packed & 0xff) / 255f,
                1f
            );
            return true;
        }
    }
}
