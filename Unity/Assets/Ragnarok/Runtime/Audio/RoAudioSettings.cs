using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Ragnarok.Audio {
    [Serializable]
    public sealed class RoAudioSettings {
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.8f;
        [Range(0f, 1f)] public float ambientVolume = 0.8f;
        [Range(0f, 1f)] public float effectsVolume = 1f;
        [Range(0f, 1f)] public float uiVolume = 1f;
        public bool mute;
        /// <summary>The game options' BGM off.</summary>
        public bool bgmMuted;
        /// <summary>The game options' sound off: effects, ambient and interface sounds.</summary>
        public bool effectsMuted;
        public AudioMixerGroup bgmMixerGroup;
        public AudioMixerGroup ambientMixerGroup;
        public AudioMixerGroup effectsMixerGroup;
        public AudioMixerGroup uiMixerGroup;

        public float GetEffectiveVolume(RoAudioBus bus, float sourceVolume = 1f) {
            if (mute) {
                return 0f;
            }

            return Mathf.Clamp01(sourceVolume) * Mathf.Clamp01(masterVolume) * GetBusVolume(bus);
        }

        public AudioMixerGroup GetMixerGroup(RoAudioBus bus) {
            switch (bus) {
                case RoAudioBus.Bgm:
                    return bgmMixerGroup;
                case RoAudioBus.Ambient:
                    return ambientMixerGroup;
                case RoAudioBus.Effects:
                    return effectsMixerGroup;
                case RoAudioBus.Ui:
                    return uiMixerGroup;
                default:
                    return null;
            }
        }

        private float GetBusVolume(RoAudioBus bus) {
            if (bus == RoAudioBus.Bgm ? bgmMuted : effectsMuted) {
                return 0f;
            }

            switch (bus) {
                case RoAudioBus.Bgm:
                    return Mathf.Clamp01(bgmVolume);
                case RoAudioBus.Ambient:
                    return Mathf.Clamp01(ambientVolume);
                case RoAudioBus.Effects:
                    return Mathf.Clamp01(effectsVolume);
                case RoAudioBus.Ui:
                    return Mathf.Clamp01(uiVolume);
                default:
                    return 1f;
            }
        }
    }
}
