using System.Threading;
using Ragnarok.Assets;
using UnityEngine;

namespace Ragnarok.Audio {
    /// <summary>
    /// The client's sound: one player for effect, UI and BGM sounds that lives from the login screen on (not per map), the
    /// settings it plays with, and the original client's distance rule. Effects are not 3D: like the client's default 2D
    /// mode (PlaySound3D 0x5e5070 → 0x5e4d30) a sound plays without panning, skipped at the maximum distance from the local
    /// player or beyond, at full volume within the minimum distance and at volume · min / distance in between (250 / 40 RO
    /// units = 50 / 8 cells for effects and hits).
    /// </summary>
    public static class RoAudioSystem {
        public const float DefaultMaxDistance = 250f;
        public const float DefaultMinDistance = 40f;
        /// <summary>RO world units per Unity unit (5 per cell).</summary>
        public const float RoUnitsPerUnity = 5f;

        static RoAudioPlayer player;

        /// <summary>Volumes and mutes (client settings write here).</summary>
        public static RoAudioSettings Settings { get; } = new RoAudioSettings();

        /// <summary>Where sounds load from: the first initialised asset database (content service, map).</summary>
        public static RoAssetDatabase Assets { get; private set; }

        /// <summary>The local player: distances are measured from it (no anchor: full volume, e.g. the login screen).</summary>
        public static Transform ListenerAnchor { get; set; }

        /// <summary>Offers an asset database to load sounds from (any thread): an initialised one replaces one that is not.</summary>
        public static void UseAssets(RoAssetDatabase assets) {
            if (assets == null) {
                return;
            }

            if (Assets == null || !Assets.IsInitialized || assets.IsInitialized) {
                Assets = assets;
            }
        }

        /// <summary>
        /// The original attenuation: false (not played) at <paramref name="maxDistance"/> or beyond; otherwise the gain,
        /// 1 within <paramref name="minDistance"/>, min / distance past it.
        /// </summary>
        public static bool Attenuate(float distance, float maxDistance, float minDistance, out float gain) {
            if (distance >= maxDistance) {
                gain = 0f;
                return false;
            }

            gain = distance > minDistance && distance > 0f ? minDistance / distance : 1f;
            return true;
        }

        /// <summary>Distance from the local player to a world position in RO units, height included like the original's
        /// 2D mode (0 without a player).</summary>
        public static float DistanceFromListener(Vector3 worldPosition) {
            Transform anchor = ListenerAnchor;
            if (anchor == null) {
                return 0f;
            }

            return Vector3.Distance(worldPosition, anchor.position) * RoUnitsPerUnity;
        }

        static int mainThreadId = -1;
        static int loginBgmPending, settingsPending, loginMode;

        /// <summary>Unity's audio may only be touched from the main thread: calls from others (the login session's network
        /// thread, settings loaders) are recorded and carried out by the player's next Update.</summary>
        internal static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EnsurePlayer();
        }

        /// <summary>Plays a BGM file (a loose file path); the same track keeps playing across maps. Main thread.</summary>
        public static void PlayBgm(string fullPath) => EnsurePlayer()?.PlayMapBgm(fullPath);

        /// <summary>
        /// The client's login mode (login, character select): BGM/01.mp3 plays and a map's BGM (the scene's backdrop map) waits
        /// until the game starts. Any thread.
        /// </summary>
        public static void SetLoginMode(bool on) => Interlocked.Exchange(ref loginMode, on ? 1 : 0);

        internal static bool InLoginMode => Volatile.Read(ref loginMode) != 0;

        /// <summary>Diagnostics: the BGM playing and the login mode.</summary>
        public static string DescribeBgm() => (player != null ? player.DescribeBgm() : "no player") + " login=" + InLoginMode;

        /// <summary>The login and character select BGM (BGM/01.mp3), restarted on every entry like the client. Any thread.</summary>
        public static void PlayLoginBgm() => Interlocked.Exchange(ref loginBgmPending, 1);

        /// <summary>Applies changed <see cref="Settings"/> to what is playing (BGM volume). Any thread.</summary>
        public static void ApplySettings() => Interlocked.Exchange(ref settingsPending, 1);

        internal static bool TakeLoginBgmRequest() => Interlocked.Exchange(ref loginBgmPending, 0) != 0;

        internal static bool TakeSettingsChange() => Interlocked.Exchange(ref settingsPending, 0) != 0;

        public static void StopBgm() {
            if (IsMainThread && player != null) {
                player.StopBgm();
            }
        }

        /// <summary>
        /// A player becoming active (OnEnable runs on the main thread): after a domain reload during Play the statics are
        /// gone but the player object lives on, so it is taken back instead of a second one being created.
        /// </summary>
        internal static void Adopt(RoAudioPlayer active) {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (player == null) {
                player = active;
            }
        }

        /// <summary>The player, created at startup and on first use; null off the main thread or outside play mode.</summary>
        internal static RoAudioPlayer EnsurePlayer() {
            if (!IsMainThread || !Application.isPlaying) {
                return null;
            }

            if (player != null) {
                return player;
            }

            // Unity's default of 32 real voices is below the pool; more virtual voices keep culled ones tracked
            AudioConfiguration config = AudioSettings.GetConfiguration();
            if (config.numRealVoices < RoAudioPlayer.MaxVoices + 16) {
                config.numRealVoices = RoAudioPlayer.MaxVoices + 16;
                config.numVirtualVoices = Mathf.Max(config.numVirtualVoices, 512);
                AudioSettings.Reset(config);
            }

            var go = new GameObject("RO Audio");
            Object.DontDestroyOnLoad(go);
            player = go.AddComponent<RoAudioPlayer>();
            return player;
        }
    }
}
