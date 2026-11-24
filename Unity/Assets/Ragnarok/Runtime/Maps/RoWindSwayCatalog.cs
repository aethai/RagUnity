using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ragnarok.Maps {
    public enum RoWindSwayMode {
        Off = 0,
        Banner = 1,
        Foliage = 2
    }

    public enum RoWindSwayAnchor {
        Top = 0,
        Bottom = 1,
        Radial = 2
    }

    public enum RoWindSwayAxis {
        X = 0,
        Y = 1,
        Z = 2
    }

    public sealed class RoWindSwayCatalog {
        public const int CurrentCatalogVersion = 3;
        public const string ResourcePath = "Weather/RoWindSwayCatalog";
        public const float DefaultBannerAmplitude = 0.10f;
        public const float DefaultBannerFrequency = 1.2f;
        public const float DefaultFoliageAmplitude = 0.06f;
        public const float DefaultFoliageFrequency = 0.5f;
        public const RoWindSwayAxis DefaultAxis = RoWindSwayAxis.Y;
        public const float DefaultAnchorHold = 0f;

        private static RoWindSwayCatalog cached;
        private static readonly RoWindSwayCatalog Empty = new RoWindSwayCatalog(CurrentCatalogVersion, Array.Empty<Profile>());

        private readonly Profile[] entries;

        private RoWindSwayCatalog(int version, Profile[] entries) {
            Version = version > 0 ? version : CurrentCatalogVersion;
            this.entries = entries ?? Array.Empty<Profile>();
            Signature = BuildCatalogSignature(Version, this.entries);
        }

        public int Version { get; }
        public int Signature { get; }
        public IReadOnlyList<Profile> Entries => entries;

        public static RoWindSwayCatalog Load() {
            if (cached != null) {
                return cached;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            cached = asset != null ? FromJson(asset.text) : Empty;
            return cached;
        }

        public static void ClearCacheForTests() {
            cached = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCacheOnRuntimeLoad() {
            cached = null;
        }

        public static RoWindSwayCatalog FromJsonForTests(string json) => FromJson(json);

        public bool TryFindProfile(string textureName, out Profile profile) {
            profile = null;
            if (string.IsNullOrWhiteSpace(textureName) || entries.Length == 0) {
                return false;
            }

            for (int i = 0; i < entries.Length; i++) {
                Profile candidate = entries[i];
                if (candidate != null && candidate.Matches(textureName)) {
                    profile = candidate;
                    return true;
                }
            }

            return false;
        }

        private static RoWindSwayCatalog FromJson(string json) {
            if (string.IsNullOrWhiteSpace(json)) {
                return Empty;
            }

            JsonDocument document = JsonUtility.FromJson<JsonDocument>(StripLineComments(json));
            if (document == null || document.entries == null || document.entries.Length == 0) {
                return new RoWindSwayCatalog(document != null ? document.version : CurrentCatalogVersion, Array.Empty<Profile>());
            }

            var profiles = new List<Profile>(document.entries.Length);
            for (int i = 0; i < document.entries.Length; i++) {
                if (TryCreateProfile(document.entries[i], i, document.version, out Profile profile)) {
                    profiles.Add(profile);
                }
            }

            return new RoWindSwayCatalog(document.version, profiles.ToArray());
        }

        private static bool TryCreateProfile(JsonEntry entry, int index, int catalogVersion, out Profile profile) {
            profile = null;
            if (entry == null || string.IsNullOrWhiteSpace(entry.textureContains)) {
                return false;
            }

            if (!Enum.TryParse(entry.mode, ignoreCase: true, out RoWindSwayMode mode) ||
                mode == RoWindSwayMode.Off) {
                return false;
            }

            RoWindSwayAnchor defaultAnchor = mode == RoWindSwayMode.Foliage
                ? RoWindSwayAnchor.Bottom
                : RoWindSwayAnchor.Top;
            RoWindSwayAnchor anchor = defaultAnchor;
            if (!string.IsNullOrWhiteSpace(entry.anchor)) {
                if (Enum.TryParse(entry.anchor, ignoreCase: true, out RoWindSwayAnchor parsedAnchor)) {
                    anchor = parsedAnchor;
                }
            }

            RoWindSwayAxis axis = DefaultAxis;
            if (!string.IsNullOrWhiteSpace(entry.axis) &&
                Enum.TryParse(entry.axis, ignoreCase: true, out RoWindSwayAxis parsedAxis)) {
                axis = parsedAxis;
            }

            float defaultAmplitude = mode == RoWindSwayMode.Foliage
                ? DefaultFoliageAmplitude
                : DefaultBannerAmplitude;
            float defaultFrequency = mode == RoWindSwayMode.Foliage
                ? DefaultFoliageFrequency
                : DefaultBannerFrequency;
            float amplitude = entry.amplitude > 0f ? entry.amplitude : defaultAmplitude;
            float frequency = entry.frequency > 0f ? entry.frequency : defaultFrequency;
            float anchorHold = Mathf.Clamp(entry.anchorHold, 0f, 0.95f);
            int version = catalogVersion > 0 ? catalogVersion : CurrentCatalogVersion;
            int identity = BuildProfileIdentity(index, version, entry.textureContains, mode, anchor, axis, amplitude, frequency, anchorHold, entry.allowAnimatedNode);
            profile = new Profile(
                index,
                version,
                identity,
                entry.textureContains.Trim(),
                mode,
                Mathf.Max(0f, amplitude),
                Mathf.Max(0f, frequency),
                anchor,
                axis,
                anchorHold,
                entry.allowAnimatedNode);
            return true;
        }

        private static string StripLineComments(string json) {
            var builder = new StringBuilder(json.Length);
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < json.Length; i++) {
                char c = json[i];
                if (inString) {
                    builder.Append(c);
                    if (escaped) {
                        escaped = false;
                    } else if (c == '\\') {
                        escaped = true;
                    } else if (c == '"') {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"') {
                    inString = true;
                    builder.Append(c);
                    continue;
                }

                if (c == '/' && i + 1 < json.Length && json[i + 1] == '/') {
                    while (i < json.Length && json[i] != '\n') {
                        i++;
                    }

                    if (i < json.Length) {
                        builder.Append(json[i]);
                    }

                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        private static int BuildCatalogSignature(int version, IReadOnlyList<Profile> profiles) {
            unchecked {
                uint hash = 2166136261u;
                AddInt(ref hash, version);
                if (profiles != null) {
                    for (int i = 0; i < profiles.Count; i++) {
                        AddInt(ref hash, profiles[i]?.Identity ?? 0);
                    }
                }

                return (int) hash;
            }
        }

        private static int BuildProfileIdentity(
            int index,
            int catalogVersion,
            string textureContains,
            RoWindSwayMode mode,
            RoWindSwayAnchor anchor,
            RoWindSwayAxis axis,
            float amplitude,
            float frequency,
            float anchorHold,
            bool allowAnimatedNode
        ) {
            unchecked {
                uint hash = 2166136261u;
                AddInt(ref hash, catalogVersion);
                AddInt(ref hash, index);
                AddString(ref hash, textureContains);
                AddInt(ref hash, (int) mode);
                AddInt(ref hash, (int) anchor);
                AddInt(ref hash, (int) axis);
                AddInt(ref hash, Mathf.RoundToInt(amplitude * 100000f));
                AddInt(ref hash, Mathf.RoundToInt(frequency * 100000f));
                AddInt(ref hash, Mathf.RoundToInt(anchorHold * 100000f));
                AddInt(ref hash, allowAnimatedNode ? 1 : 0);
                return (int) hash;
            }
        }

        private static void AddString(ref uint hash, string value) {
            if (string.IsNullOrEmpty(value)) {
                return;
            }

            for (int i = 0; i < value.Length; i++) {
                char c = char.ToLowerInvariant(value[i]);
                hash ^= c;
                hash *= 16777619u;
            }
        }

        private static void AddInt(ref uint hash, int value) {
            unchecked {
                uint v = (uint) value;
                for (int i = 0; i < 4; i++) {
                    hash ^= (byte) (v >> (i * 8));
                    hash *= 16777619u;
                }
            }
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class JsonDocument {
            public int version = CurrentCatalogVersion;
            public JsonEntry[] entries;
        }

        [Serializable]
        private sealed class JsonEntry {
            public string textureContains;
            public string mode;
            public float amplitude;
            public float frequency;
            public string anchor;
            public string axis;
            public float anchorHold;
            public bool allowAnimatedNode;
        }
#pragma warning restore 0649

        public sealed class Profile {
            public Profile(
                int index,
                int catalogVersion,
                int identity,
                string textureContains,
                RoWindSwayMode mode,
                float amplitude,
                float frequency,
                RoWindSwayAnchor anchor,
                RoWindSwayAxis axis,
                float anchorHold,
                bool allowAnimatedNode
            ) {
                Index = index;
                CatalogVersion = catalogVersion;
                Identity = identity;
                TextureContains = textureContains ?? string.Empty;
                Mode = mode;
                Amplitude = amplitude;
                Frequency = frequency;
                Anchor = anchor;
                Axis = axis;
                AnchorHold = Mathf.Clamp(anchorHold, 0f, 0.95f);
                AllowAnimatedNode = allowAnimatedNode;
            }

            public int Index { get; }
            public int CatalogVersion { get; }
            public int Identity { get; }
            public string TextureContains { get; }
            public RoWindSwayMode Mode { get; }
            public float Amplitude { get; }
            public float Frequency { get; }
            public RoWindSwayAnchor Anchor { get; }
            public RoWindSwayAxis Axis { get; }
            public float AnchorHold { get; }
            public bool AllowAnimatedNode { get; }

            public bool Matches(string textureName) =>
                !string.IsNullOrWhiteSpace(textureName) &&
                !string.IsNullOrEmpty(TextureContains) &&
                textureName.IndexOf(TextureContains, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public static class RoWindSwayMath {
        public static float GetAxisCoordinate(Vector3 position, RoWindSwayAxis axis) {
            return axis switch {
                RoWindSwayAxis.X => position.x,
                RoWindSwayAxis.Z => position.z,
                _ => position.y
            };
        }

        public static float ComputeAnchorWeight(float coordinate, float min, float max, RoWindSwayAnchor anchor, float anchorHold = 0f) {
            float span = max - min;
            if (span <= 0.0001f) {
                return 0f;
            }

            float raw = anchor == RoWindSwayAnchor.Bottom
                ? (coordinate - min) / span
                : (max - coordinate) / span;
            raw = Mathf.Clamp01(raw);
            anchorHold = Mathf.Clamp(anchorHold, 0f, 0.95f);
            if (anchorHold > 0f) {
                raw = Mathf.Clamp01((raw - anchorHold) / Mathf.Max(1f - anchorHold, 0.0001f));
            }

            return raw * raw;
        }

        public static float ComputeRadialWeight(Vector3 position, Vector3 boundsMin, Vector3 boundsMax) {
            float halfExtentX = Mathf.Max(0f, (boundsMax.x - boundsMin.x) * 0.5f);
            float halfExtentZ = Mathf.Max(0f, (boundsMax.z - boundsMin.z) * 0.5f);
            float maxHalfExtent = Mathf.Max(halfExtentX, halfExtentZ);
            if (maxHalfExtent <= 0.0001f) {
                return 0f;
            }

            float centerX = (boundsMin.x + boundsMax.x) * 0.5f;
            float centerZ = (boundsMin.z + boundsMax.z) * 0.5f;
            float dx = position.x - centerX;
            float dz = position.z - centerZ;
            float normalized = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / maxHalfExtent);
            return Mathf.Pow(normalized, 1.5f);
        }

        public static byte ComputePhaseSeed(Vector3 localPosition) {
            unchecked {
                uint hash = 2166136261u;
                AddQuantizedFloat(ref hash, localPosition.x);
                AddQuantizedFloat(ref hash, localPosition.y);
                AddQuantizedFloat(ref hash, localPosition.z);
                return (byte) (hash & 0xff);
            }
        }

        public static bool HasAnimatedKeyframes(int rotationKeyframes, int positionKeyframes, int scaleKeyframes) =>
            rotationKeyframes > 0 || positionKeyframes > 0 || scaleKeyframes > 0;

        private static void AddQuantizedFloat(ref uint hash, float value) {
            int quantized = Mathf.RoundToInt(value * 1000f);
            unchecked {
                uint v = (uint) quantized;
                for (int i = 0; i < 4; i++) {
                    hash ^= (byte) (v >> (i * 8));
                    hash *= 16777619u;
                }
            }
        }
    }
}
