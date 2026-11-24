using System;
using UnityEngine;

namespace Ragnarok.Audio {
    /// <summary>
    /// A sound request. Positional effects carry either a world position (the distance to the local player is measured
    /// when it plays) or an offset from the local player in RO units (the classic engine's PlaySound3D arguments).
    /// </summary>
    public readonly struct RoAudioEvent {
        public RoAudioEvent(string path, RoAudioBus bus, float volume, Vector3 position, bool hasPosition)
            : this(path, bus, volume, position, hasPosition, false, 0f, 0f, 0f, RoAudioSystem.DefaultMaxDistance, RoAudioSystem.DefaultMinDistance) {
        }

        public RoAudioEvent(string path, RoAudioBus bus, float volume, Vector3 position, bool hasPosition,
            bool hasRoOffset, float roDx, float roDy, float roDz, float maxDistance, float minDistance) {
            Path = path;
            Bus = bus;
            Volume = volume;
            Position = position;
            HasPosition = hasPosition;
            HasRoOffset = hasRoOffset;
            RoDx = roDx;
            RoDy = roDy;
            RoDz = roDz;
            MaxDistance = maxDistance;
            MinDistance = minDistance;
        }

        public string Path { get; }
        public RoAudioBus Bus { get; }
        public float Volume { get; }
        public Vector3 Position { get; }
        public bool HasPosition { get; }
        /// <summary>The position is (<see cref="RoDx"/>, <see cref="RoDy"/>, <see cref="RoDz"/>) RO units from the local player.</summary>
        public bool HasRoOffset { get; }
        public float RoDx { get; }
        /// <summary>Height offset: about 35 of the client's sounds pass one (quieter, the distance is 3D).</summary>
        public float RoDy { get; }
        public float RoDz { get; }
        /// <summary>RO units: not played at or beyond.</summary>
        public float MaxDistance { get; }
        /// <summary>RO units: full volume within, then volume · min / distance.</summary>
        public float MinDistance { get; }
    }

    public static class RoAudioEvents {
        private static readonly string[] recent = new string[16];
        // the bus of each recent path, joined only when the console asks (a string per played sound before)
        private static readonly RoAudioBus[] recentBus = new RoAudioBus[16];
        private static int recentNext;
        // the client's wav names are a small fixed set: each one is normalised once (sounds play many times a second in a fight)
        private static readonly System.Collections.Generic.Dictionary<string, string> normalized =
            new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The last requested sounds, oldest first (the sweep console's !sound).</summary>
        public static string DescribeRecent() {
            lock (recent) {
                var s = new System.Text.StringBuilder();
                for (int i = 0; i < recent.Length; i++) {
                    int k = (recentNext + i) % recent.Length;
                    if (recent[k] != null) s.Append(s.Length > 0 ? " | " : string.Empty).Append(recentBus[k]).Append(' ').Append(recent[k]);
                }

                return s.ToString();
            }
        }

        public static event Action<RoAudioEvent> Requested;

        /// <summary>An ACT frame sound (attack swing, step) of an actor at a world position.</summary>
        public static void PlayActSound(string wavFileOrPath, Vector3 position, float volume = 1f) {
            Request(NormalizeWavPath(wavFileOrPath), RoAudioBus.Effects, volume, position, true);
        }

        /// <summary>An effect sound at a world position (the original's PlaySound3D with distances 250 / 40).</summary>
        public static void PlayEffect(string wavFileOrPath, Vector3 position, float volume = 1f) {
            Request(NormalizeWavPath(wavFileOrPath), RoAudioBus.Effects, volume, position, true);
        }

        /// <summary>
        /// PlaySound3D as the client calls it (0x5e5070): (<paramref name="roDx"/>, <paramref name="roDy"/>,
        /// <paramref name="roDz"/>) RO units from the local player, not played at <paramref name="maxDistance"/> or beyond,
        /// attenuated past <paramref name="minDistance"/>, times <paramref name="factor"/>.
        /// </summary>
        public static void PlayEffectRelative(string wavFileOrPath, float roDx, float roDy, float roDz, float maxDistance, float minDistance, float factor = 1f) {
            string path = NormalizeWavPath(wavFileOrPath);
            if (string.IsNullOrWhiteSpace(path)) {
                return;
            }

            Remember(RoAudioBus.Effects, path);
            RoAudioSystem.EnsurePlayer();
            Requested?.Invoke(new RoAudioEvent(path, RoAudioBus.Effects, factor, default, true, true, roDx, roDy, roDz, maxDistance, minDistance));
        }

        /// <summary>버튼소리.wav, the client's one UI sound (login / server / character OK, notices, the whisper window).</summary>
        public const string ButtonSoundWav = "\ubc84\ud2bc\uc18c\ub9ac.wav";

        /// <summary>The button sound at its call sites in the client (packages/sound §4.1).</summary>
        public static void PlayButtonSound() => PlayUiSound(ButtonSoundWav);

        public static void PlayUiSound(string wavFileOrPath, float volume = 1f) {
            Request(NormalizeWavPath(wavFileOrPath), RoAudioBus.Ui, volume, default, false);
        }

        public static void Request(string path, RoAudioBus bus, float volume = 1f) {
            Request(path, bus, volume, default, false);
        }

        public static void Request(string path, RoAudioBus bus, float volume, Vector3 position, bool hasPosition) {
            if (string.IsNullOrWhiteSpace(path)) {
                return;
            }

            Remember(bus, path);
            RoAudioSystem.EnsurePlayer();
            Requested?.Invoke(new RoAudioEvent(path, bus, volume, position, hasPosition));
        }

        // the diagnostics console's view of what was asked for (a ring of the last ones; strings are already made)
        private static void Remember(RoAudioBus bus, string path) {
            lock (recent) {
                recent[recentNext] = path;
                recentBus[recentNext] = bus;
                recentNext = (recentNext + 1) % recent.Length;
            }
        }

        /// <summary>
        /// The client's wav names are relative to data\wav\ ("effect\ef_hit2.wav", "_hit_sword.wav"): the result is
        /// "data/wav/..." (".wav" added when missing), so the asset lookup is a direct hit and never a suffix scan.
        /// Paths that already start with "data/" are kept.
        /// </summary>
        public static string NormalizeWavPath(string wavFileOrPath) {
            if (string.IsNullOrWhiteSpace(wavFileOrPath)) {
                return null;
            }

            lock (normalized) {
                if (normalized.TryGetValue(wavFileOrPath, out string cached)) {
                    return cached;
                }
            }

            string path = wavFileOrPath.Replace('\\', '/').Trim().TrimStart('/');
            if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) {
                path += ".wav";
            }

            path = path.StartsWith("data/", StringComparison.OrdinalIgnoreCase) ? path : "data/wav/" + path;
            lock (normalized) {
                if (normalized.Count < 4096) normalized[wavFileOrPath] = path;
            }

            return path;
        }
    }
}
