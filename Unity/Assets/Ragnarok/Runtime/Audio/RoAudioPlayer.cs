using System.Collections.Generic;
using Ragnarok.Assets;
using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.Audio {
    /// <summary>
    /// The persistent sound player (<see cref="RoAudioSystem"/>): every effect, hit, skill, ACT event, ambient and UI
    /// sound on one pool of 2D voices like the original CSoundMgr (reference packages/sound), attenuated by distance
    /// from the local player, and the BGM. The pool has the original's largest size (48 sample handles). A full pool in
    /// the original drops the new sound; here it gives its quietest voice (the farthest sound) to a louder new one and
    /// drops only a quieter one, so what is near the player is never lost in a crowd (a deliberate difference).
    /// </summary>
    public sealed class RoAudioPlayer : MonoBehaviour {
        /// <summary>NUMSAMPLETYPE 0 (the original defaults to 16 when the registry value is missing).</summary>
        public const int MaxVoices = 48;

        // serialized so a domain reload during Play keeps the sources this player already owns
        [SerializeField] List<AudioSource> voices = new List<AudioSource>(MaxVoices);
        /// <summary>Each voice's start volume: a full pool hands the quietest one over.</summary>
        readonly float[] voiceVolume = new float[MaxVoices];
        readonly HashSet<string> reportedMissing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        [SerializeField] RoBgmPlayer bgm;
        readonly System.Collections.Concurrent.ConcurrentQueue<RoAudioEvent> offThreadRequests =
            new System.Collections.Concurrent.ConcurrentQueue<RoAudioEvent>();

        void OnEnable() {
            RoAudioSystem.Adopt(this);
            RoAudioEvents.Requested += Play;
            RoActSpriteAnimator.FrameSoundRequested += PlayFrameSound;
        }

        void OnDisable() {
            RoAudioEvents.Requested -= Play;
            RoActSpriteAnimator.FrameSoundRequested -= PlayFrameSound;
        }

        static void PlayFrameSound(string soundName, Vector3 position, float volume) =>
            RoAudioEvents.PlayActSound(soundName, position, volume);

        void Play(RoAudioEvent request) {
            if (!RoAudioSystem.IsMainThread) {
                offThreadRequests.Enqueue(request);   // played by the next Update
                return;
            }

            RoAssetDatabase assets = RoAudioSystem.Assets;
            if (assets == null || !assets.IsInitialized) {
                return;
            }

            float gain = 1f;
            if (request.Bus != RoAudioBus.Ui && request.HasPosition) {
                float distance = request.HasRoOffset
                    ? Mathf.Sqrt(request.RoDx * request.RoDx + request.RoDy * request.RoDy + request.RoDz * request.RoDz)
                    : RoAudioSystem.DistanceFromListener(request.Position);
                if (!RoAudioSystem.Attenuate(distance, request.MaxDistance, request.MinDistance, out gain)) {
                    return;
                }
            }

            float volume = RoAudioSystem.Settings.GetEffectiveVolume(request.Bus, request.Volume * gain);
            if (volume <= 0f) {
                return;
            }

            AudioClip clip = assets.LoadAudioClip(request.Path);
            if (clip == null) {
                if (reportedMissing.Add(request.Path)) {
                    Debug.LogWarning($"RO audio: no sound '{request.Path}'", this);
                }

                return;
            }

            AudioSource voice = TakeVoice(volume);
            if (voice == null) {
                return;
            }

            voice.clip = clip;
            voice.volume = volume;
            voice.outputAudioMixerGroup = RoAudioSystem.Settings.GetMixerGroup(request.Bus);
            voice.Play();
        }

        /// <summary>A free voice, a new one while the pool is not full, else the quietest playing voice if it is quieter
        /// than <paramref name="volume"/>; null when every voice is louder (the new sound is dropped).</summary>
        AudioSource TakeVoice(float volume) {
            int quietest = -1;
            for (int i = 0; i < voices.Count; i++) {
                if (!voices[i].isPlaying) {
                    voiceVolume[i] = volume;
                    return voices[i];
                }

                if (quietest < 0 || voiceVolume[i] < voiceVolume[quietest]) {
                    quietest = i;
                }
            }

            if (voices.Count < MaxVoices) {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                voiceVolume[voices.Count] = volume;
                voices.Add(source);
                return source;
            }

            if (quietest < 0 || voiceVolume[quietest] >= volume) {
                return null;
            }

            voices[quietest].Stop();
            voiceVolume[quietest] = volume;
            return voices[quietest];
        }

        /// <summary>The BGM file playing (diagnostics), and a map BGM held back by the login mode.</summary>
        public string DescribeBgm() => (bgm != null ? bgm.CurrentPath : "-") + (deferredMapBgm != null ? " (map waiting: " + deferredMapBgm + ")" : string.Empty);

        public void PlayBgm(string fullPath, bool restart = false) {
            if (bgm == null) {
                bgm = gameObject.AddComponent<RoBgmPlayer>();
            }

            // the client switches tracks without fades; the same track keeps playing unless restarted
            bgm.PlayLocalFile(fullPath, RoAudioSystem.Settings, this, fadeSeconds: 0f, restart: restart);
        }

        string pendingBgm;
        bool pendingBgmRestart;
        // a map's BGM asked for in the login mode, played when the game starts
        string deferredMapBgm;

        /// <summary>A map's BGM: held back while the login mode plays BGM/01.mp3.</summary>
        public void PlayMapBgm(string fullPath) {
            if (RoAudioSystem.InLoginMode) {
                deferredMapBgm = fullPath;
                return;
            }

            deferredMapBgm = null;
            PlayBgm(fullPath);
        }

        /// <summary>A loose BGM file ("BGM/01.mp3"), started as soon as the content is loaded.</summary>
        public void RequestBgm(string looseRelativePath, bool restart) {
            pendingBgm = looseRelativePath;
            pendingBgmRestart = restart;
            TryStartPendingBgm();
        }

        void Update() {
            if (RoAudioSystem.TakeSettingsChange()) {
                ApplySettings();
            }

            if (RoAudioSystem.TakeLoginBgmRequest()) {
                RequestBgm("01.mp3", restart: true);
            }

            while (offThreadRequests.TryDequeue(out RoAudioEvent request)) {
                Play(request);
            }

            if (pendingBgm != null) {
                TryStartPendingBgm();
            }

            if (deferredMapBgm != null && !RoAudioSystem.InLoginMode) {
                string map = deferredMapBgm;
                deferredMapBgm = null;
                PlayBgm(map);
            }
        }

        void TryStartPendingBgm() {
            RoAssetDatabase assets = RoAudioSystem.Assets;
            if (assets == null || !assets.IsInitialized) {
                return;
            }

            string file = pendingBgm;
            pendingBgm = null;
            foreach (string candidate in RoMapBgmResolver.BuildBgmPathCandidates(file)) {
                if (assets.TryResolveLooseFilePath(candidate, out string fullPath)) {
                    PlayBgm(fullPath, pendingBgmRestart);
                    return;
                }
            }

            Debug.LogWarning($"RO BGM: no loose file for '{file}'", this);
        }

        public void StopBgm() {
            if (bgm != null) {
                bgm.Stop();
            }
        }

        public void ApplySettings() {
            if (bgm != null) {
                bgm.Configure(RoAudioSystem.Settings);
            }
        }
    }
}
