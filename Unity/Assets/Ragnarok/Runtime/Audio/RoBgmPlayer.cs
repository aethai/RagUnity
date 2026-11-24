using System;
using System.Collections;
using Ragnarok.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;

namespace Ragnarok.Audio {
    public sealed class RoBgmPlayer : MonoBehaviour {
        /// <summary>Serialized so a domain reload during Play keeps it (a fresh one would leave the old track playing).</summary>
        [SerializeField] private AudioSource source;
        private Coroutine loadCoroutine;
        private Coroutine fadeCoroutine;
        private RoAudioSettings settings;

        public string CurrentPath { get; private set; }

        public void Configure(RoAudioSettings audioSettings) {
            settings = audioSettings ?? new RoAudioSettings();
            EnsureSource();
            source.volume = settings.GetEffectiveVolume(RoAudioBus.Bgm);
            source.outputAudioMixerGroup = settings.GetMixerGroup(RoAudioBus.Bgm);
        }

        public void PlayLocalFile(string fullPath, RoAudioSettings audioSettings, UnityEngine.Object logContext = null, float fadeSeconds = 0.75f, bool restart = false) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return;
            }

            Configure(audioSettings);
            if (!restart && string.Equals(CurrentPath, fullPath, StringComparison.OrdinalIgnoreCase) && source.isPlaying) {
                return;
            }

            if (loadCoroutine != null) {
                StopCoroutine(loadCoroutine);
                loadCoroutine = null;
            }

            loadCoroutine = StartCoroutine(LoadAndPlay(fullPath, logContext, fadeSeconds));
        }

        public void Stop(float fadeSeconds = 0.35f) {
            if (source == null || !source.isPlaying) {
                return;
            }

            StartFade(0f, fadeSeconds, true);
        }

        private IEnumerator LoadAndPlay(string fullPath, UnityEngine.Object logContext, float fadeSeconds) {
            CurrentPath = fullPath;
            string uri = new Uri(fullPath).AbsoluteUri;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(uri, GetAudioType(fullPath))) {
                var handler = request.downloadHandler as DownloadHandlerAudioClip;
                if (handler != null) {
                    handler.streamAudio = true;
                }

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogWarning($"RO BGM load failed: {fullPath} ({request.error})", logContext);
                    loadCoroutine = null;
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null) {
                    Debug.LogWarning($"RO BGM load returned no AudioClip: {fullPath}", logContext);
                    loadCoroutine = null;
                    yield break;
                }

                EnsureSource();
                // every track is its own streamed clip (decoder + open file): the one it replaces goes
                AudioClip previous = source.clip;
                source.clip = clip;
                DestroyClip(previous, clip);
                source.loop = true;
                source.volume = 0f;
                source.Play();
                StartFade(settings.GetEffectiveVolume(RoAudioBus.Bgm), fadeSeconds, false);
                if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                    RoRuntimeDiagnostics.IncrementDebugLogLine();
                    Debug.Log($"RO BGM playing: {PathDisplay(fullPath)}", logContext);
                }
            }

            loadCoroutine = null;
        }

        private static void DestroyClip(AudioClip clip, AudioClip keep) {
            if (clip != null && clip != keep) {
                Destroy(clip);
            }
        }

        private void StartFade(float targetVolume, float seconds, bool stopWhenSilent) {
            if (fadeCoroutine != null) {
                StopCoroutine(fadeCoroutine);
            }

            fadeCoroutine = StartCoroutine(FadeTo(targetVolume, Mathf.Max(0.01f, seconds), stopWhenSilent));
        }

        private IEnumerator FadeTo(float targetVolume, float seconds, bool stopWhenSilent) {
            float start = source.volume;
            float elapsed = 0f;
            while (elapsed < seconds) {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, targetVolume, elapsed / seconds);
                yield return null;
            }

            source.volume = targetVolume;
            if (stopWhenSilent) {
                source.Stop();
                AudioClip stopped = source.clip;
                source.clip = null;
                DestroyClip(stopped, null);
                CurrentPath = null;
            }

            fadeCoroutine = null;
        }

        private void EnsureSource() {
            if (source != null) {
                return;
            }

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.priority = 0;
        }

        private static AudioType GetAudioType(string path) {
            string extension = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            switch (extension) {
                case "mp3":
                    return AudioType.MPEG;
                case "ogg":
                    return AudioType.OGGVORBIS;
                case "wav":
                    return AudioType.WAV;
                default:
                    return AudioType.UNKNOWN;
            }
        }

        private static string PathDisplay(string fullPath) {
            return string.IsNullOrWhiteSpace(fullPath) ? string.Empty : System.IO.Path.GetFileName(fullPath);
        }
    }
}
