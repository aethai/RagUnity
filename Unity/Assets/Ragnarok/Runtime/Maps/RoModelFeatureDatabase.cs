using System;
using System.Collections.Generic;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Maps {
    [CreateAssetMenu(fileName = "RoModelFeatureDatabase", menuName = "Ragnarok/Map Model Feature Database")]
    public sealed class RoModelFeatureDatabase : ScriptableObject {
        public List<ModelPointLightEntry> pointLights = new List<ModelPointLightEntry> {
            ModelPointLightEntry.CreateDefaultLampGlass()
        };

        public IReadOnlyList<ModelPointLightEntry> PointLights => pointLights;

        private void OnValidate() {
            EnsureEntryDefaults();
        }

        public void EnsureEntryDefaults() {
            pointLights ??= new List<ModelPointLightEntry>();

            for (int i = 0; i < pointLights.Count; i++) {
                pointLights[i]?.EnsureInspectorDefaults();
            }
        }

        public bool TryFindPointLightEntry(RoModelSurfaceInfo surface, out ModelPointLightEntry entry) {
            entry = null;
            return TryFindEntry(pointLights, surface, out entry);
        }

        private static bool TryFindEntry<TEntry>(List<TEntry> entries, RoModelSurfaceInfo surface, out TEntry entry)
            where TEntry : ModelFeatureEntry {
            entry = null;
            if (surface == null || entries == null) {
                return false;
            }

            for (int i = 0; i < entries.Count; i++) {
                TEntry candidate = entries[i];
                if (candidate == null || !candidate.Matches(surface)) {
                    continue;
                }

                entry = candidate;
                return true;
            }

            return false;
        }

        [Serializable]
        public sealed class ModelSurfaceMatch {
            public string modelFile;
            public string nodeName;
            public string textureName;

            public bool IsEmpty =>
                string.IsNullOrWhiteSpace(modelFile) &&
                string.IsNullOrWhiteSpace(nodeName) &&
                string.IsNullOrWhiteSpace(textureName);

            public bool Matches(RoModelSurfaceInfo surface) {
                if (surface == null) {
                    return false;
                }

                return FieldMatches(modelFile, surface.ModelFile, compareFileNameWhenPlain: true) &&
                    FieldMatches(nodeName, surface.NodeName, compareFileNameWhenPlain: false) &&
                    FieldMatches(textureName, surface.TextureName, compareFileNameWhenPlain: true);
            }

            private static bool FieldMatches(string expected, string actual, bool compareFileNameWhenPlain) {
                if (string.IsNullOrWhiteSpace(expected)) {
                    return true;
                }

                string normalizedExpected = NormalizePath(expected.Trim());
                string normalizedActual = NormalizePath(actual);
                bool plainExpected = normalizedExpected.IndexOf('/') < 0;
                if (compareFileNameWhenPlain && plainExpected) {
                    normalizedActual = GetFileName(normalizedActual);
                }

                if (normalizedExpected.IndexOf('*') >= 0) {
                    return WildcardMatches(normalizedExpected, normalizedActual);
                }

                if (string.Equals(normalizedExpected, normalizedActual, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                return !plainExpected &&
                    normalizedActual.EndsWith(normalizedExpected, StringComparison.OrdinalIgnoreCase);
            }

            private static bool WildcardMatches(string pattern, string value) {
                if (pattern == "*") {
                    return true;
                }

                string[] parts = pattern.Split('*');
                int valueIndex = 0;
                bool mustStartWithFirstPart = !pattern.StartsWith("*", StringComparison.Ordinal);
                bool mustEndWithLastPart = !pattern.EndsWith("*", StringComparison.Ordinal);
                string lastNonEmptyPart = null;

                for (int i = 0; i < parts.Length; i++) {
                    string part = parts[i];
                    if (string.IsNullOrEmpty(part)) {
                        continue;
                    }

                    int found = value.IndexOf(part, valueIndex, StringComparison.OrdinalIgnoreCase);
                    if (found < 0) {
                        return false;
                    }

                    if (mustStartWithFirstPart && lastNonEmptyPart == null && found != 0) {
                        return false;
                    }

                    valueIndex = found + part.Length;
                    lastNonEmptyPart = part;
                }

                return !mustEndWithLastPart ||
                    string.IsNullOrEmpty(lastNonEmptyPart) ||
                    value.EndsWith(lastNonEmptyPart, StringComparison.OrdinalIgnoreCase);
            }

            private static string NormalizePath(string value) {
                return string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\\', '/');
            }

            private static string GetFileName(string value) {
                if (string.IsNullOrEmpty(value)) {
                    return string.Empty;
                }

                int slash = value.LastIndexOf('/');
                return slash >= 0 ? value.Substring(slash + 1) : value;
            }
        }

        [Serializable]
        public abstract class ModelFeatureEntry {
            public string displayName;
            public bool enabled = true;
            public ModelSurfaceMatch match = new ModelSurfaceMatch();

            protected bool HasEmptyIdentity =>
                string.IsNullOrWhiteSpace(displayName) &&
                (match == null || match.IsEmpty);

            protected void EnsureCommonInspectorDefaults() {
                match ??= new ModelSurfaceMatch();
                if (!enabled && HasEmptyIdentity) {
                    enabled = true;
                }
            }

            public bool Matches(RoModelSurfaceInfo surface) {
                return match != null && match.Matches(surface);
            }
        }

        [Serializable]
        public sealed class ModelPointLightEntry : ModelFeatureEntry {
            public Color color = RoEnhancedShadowSettings.DefaultModelLampPointLightColor;
            [Min(0f)] public float range = RoEnhancedShadowSettings.DefaultModelLampPointLightRange;
            [Min(0f)] public float intensity = RoEnhancedShadowSettings.DefaultModelLampPointLightIntensity;
            public float verticalOffset = RoEnhancedShadowSettings.DefaultModelLampPointLightVerticalOffset;
            [Min(0f)] public float dayIntensityScale = RoEnhancedShadowSettings.DefaultModelLampPointLightDayIntensityScale;
            [Min(0f)] public float nightIntensityScale = RoEnhancedShadowSettings.DefaultModelLampPointLightNightIntensityScale;
            public bool castShadows = RoEnhancedShadowSettings.DefaultModelLampPointLightShadows;
            [Range(0f, 1f)] public float shadowStrength = RoEnhancedShadowSettings.DefaultModelLampPointLightShadowStrength;
            [Min(0f)] public float shadowBias = RoEnhancedShadowSettings.DefaultModelLampPointLightShadowBias;
            [Min(0f)] public float shadowNormalBias = RoEnhancedShadowSettings.DefaultModelLampPointLightShadowNormalBias;
            [Min(0.01f)] public float shadowNearPlane = RoEnhancedShadowSettings.DefaultModelLampPointLightShadowNearPlane;
            public bool matchedSurfaceCastsShadows = RoEnhancedShadowSettings.DefaultModelLampGlassCastsShadows;

            public static ModelPointLightEntry CreateDefaultLampGlass() {
                return new ModelPointLightEntry {
                    displayName = "Lamp glass",
                    match = new ModelSurfaceMatch {
                        nodeName = "Box04",
                        textureName = "d-side.bmp"
                    }
                };
            }

            public void EnsureInspectorDefaults() {
                bool zeroInitialized = HasEmptyIdentity &&
                    !enabled &&
                    color == default &&
                    Approximately(range, 0f) &&
                    Approximately(intensity, 0f) &&
                    Approximately(verticalOffset, 0f) &&
                    Approximately(dayIntensityScale, 0f) &&
                    Approximately(nightIntensityScale, 0f) &&
                    !castShadows &&
                    Approximately(shadowStrength, 0f) &&
                    Approximately(shadowBias, 0f) &&
                    Approximately(shadowNormalBias, 0f) &&
                    Approximately(shadowNearPlane, 0f) &&
                    !matchedSurfaceCastsShadows;

                EnsureCommonInspectorDefaults();
                if (!zeroInitialized) {
                    return;
                }

                color = RoEnhancedShadowSettings.DefaultModelLampPointLightColor;
                range = RoEnhancedShadowSettings.DefaultModelLampPointLightRange;
                intensity = RoEnhancedShadowSettings.DefaultModelLampPointLightIntensity;
                verticalOffset = RoEnhancedShadowSettings.DefaultModelLampPointLightVerticalOffset;
                dayIntensityScale = RoEnhancedShadowSettings.DefaultModelLampPointLightDayIntensityScale;
                nightIntensityScale = RoEnhancedShadowSettings.DefaultModelLampPointLightNightIntensityScale;
                castShadows = RoEnhancedShadowSettings.DefaultModelLampPointLightShadows;
                shadowStrength = RoEnhancedShadowSettings.DefaultModelLampPointLightShadowStrength;
                shadowBias = RoEnhancedShadowSettings.DefaultModelLampPointLightShadowBias;
                shadowNormalBias = RoEnhancedShadowSettings.DefaultModelLampPointLightShadowNormalBias;
                shadowNearPlane = RoEnhancedShadowSettings.DefaultModelLampPointLightShadowNearPlane;
                matchedSurfaceCastsShadows = RoEnhancedShadowSettings.DefaultModelLampGlassCastsShadows;
            }
        }

        private static bool Approximately(float a, float b) {
            return Mathf.Abs(a - b) <= 0.0001f;
        }
    }
}
