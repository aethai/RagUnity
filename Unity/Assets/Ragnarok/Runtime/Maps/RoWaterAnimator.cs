using System.Collections.Generic;
using Ragnarok.Assets;
using ROIO.Models.FileTypes;
using UnityEngine;

namespace Ragnarok.Maps {
    [ExecuteAlways]
    public sealed class RoWaterAnimator : MonoBehaviour {
        public const int WaterTextureFrameCount = 32;
        public const float WaterFramesPerSecond = 60f;
        public const float DefaultWaterOpacity = 0.6f;
        public const float OpaqueWaterOpacity = 1f;

        [SerializeField] private RSW.WaterInfo waterInfo;
        [SerializeField] private Material material;
        [SerializeField] private Texture2D[] textures;

        private int currentTextureId = -1;
        private bool ownsTextures = true;

        public void Initialize(RSW.WaterInfo info, Material waterMaterial, RoAssetDatabase assets) {
            DestroyRuntimeTextures();
            waterInfo = info;
            material = waterMaterial;
            if (assets != null) {
                textures = RoRuntimeMapAssetCache.GetOrBuildWaterTextures(
                    assets,
                    info,
                    () => LoadTextures(info, assets));
                ownsTextures = false;
            } else {
                textures = LoadTextures(info, assets);
                ownsTextures = true;
            }

            currentTextureId = -1;
            ApplyWaterProperties();
            ApplyTexture(0);
        }

        private void OnDestroy() {
            DestroyRuntimeTextures();
        }

        private void Update() {
            if (!Application.isPlaying || waterInfo == null || material == null || textures == null || textures.Length == 0) {
                return;
            }

            int textureId = GetTextureFrameIndex(Time.time, waterInfo.animSpeed, textures.Length);

            SetFloatIfPropertyExists(material, "_WaterOffset", GetWaveOffset(Time.time, waterInfo.waveSpeed));
            ApplyTexture(textureId);
        }

        public static int GetTextureFrameIndex(float timeSeconds, int animSpeed, int textureCount) {
            if (textureCount <= 0) {
                return 0;
            }

            float frame = timeSeconds * WaterFramesPerSecond;
            int speed = Mathf.Max(1, animSpeed);
            return Mathf.FloorToInt(frame / speed) % textureCount;
        }

        public static float GetWaveOffset(float timeSeconds, float waveSpeed) {
            float frame = timeSeconds * WaterFramesPerSecond;
            return Mathf.Repeat(frame * waveSpeed, 360f) - 180f;
        }

        public static float GetWaterOpacity(RSW.WaterInfo info) {
            if (info != null && (info.type == 4 || info.type == 6)) {
                return OpaqueWaterOpacity;
            }

            return DefaultWaterOpacity;
        }

        private void ApplyWaterProperties() {
            if (material == null || waterInfo == null) {
                return;
            }

            SetFloatIfPropertyExists(material, "Wave Height", waterInfo.waveHeight);
            SetFloatIfPropertyExists(material, "Wave Pitch", waterInfo.wavePitch);
            SetFloatIfPropertyExists(material, "_WaveHeight", waterInfo.waveHeight);
            SetFloatIfPropertyExists(material, "_WavePitch", waterInfo.wavePitch);
            SetFloatIfPropertyExists(material, "_WaterOffset", GetWaveOffset(0f, waterInfo.waveSpeed));
            SetFloatIfPropertyExists(material, "_WaterOpacity", GetWaterOpacity(waterInfo));
        }

        private void ApplyTexture(int textureId) {
            if (textureId == currentTextureId || material == null || textures == null || textures.Length == 0) {
                return;
            }

            Texture2D texture = textures[Mathf.Clamp(textureId, 0, textures.Length - 1)];
            if (texture == null) {
                return;
            }

            material.mainTexture = texture;
            SetTextureIfPropertyExists(material, "_BaseMap", texture);
            SetTextureIfPropertyExists(material, "_MainTex", texture);
            currentTextureId = textureId;
        }

        private static Texture2D[] LoadTextures(RSW.WaterInfo info, RoAssetDatabase assets) {
            var loaded = new Texture2D[WaterTextureFrameCount];
            if (info == null || info.images == null || assets == null) {
                return loaded;
            }

            for (int i = 0; i < loaded.Length && i < info.images.Length; i++) {
                string path = info.images[i];
                if (string.IsNullOrWhiteSpace(path)) {
                    continue;
                }

                Texture2D texture = assets.LoadTexture(path);
                if (texture == null) {
                    continue;
                }

                loaded[i] = CreateRuntimeWaterTexture(texture);
            }

            Texture2D fallback = null;
            for (int i = 0; i < loaded.Length; i++) {
                if (loaded[i] != null) {
                    fallback = loaded[i];
                    break;
                }
            }

            if (fallback == null) {
                return loaded;
            }

            for (int i = 0; i < loaded.Length; i++) {
                loaded[i] ??= fallback;
            }

            return loaded;
        }

        private static Texture2D CreateRuntimeWaterTexture(Texture2D source) {
            if (source == null) {
                return null;
            }

            try {
                var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true) {
                    name = source.name + " (RO Water Runtime)",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 2,
                    hideFlags = HideFlags.DontSave
                };

                texture.SetPixels32(source.GetPixels32());
                texture.Apply(true, false);
                return texture;
            } catch (System.Exception ex) {
                Debug.LogWarning($"Could not clone water texture '{source.name}' with mipmaps: {ex.Message}");
                source.wrapMode = TextureWrapMode.Repeat;
                source.filterMode = FilterMode.Bilinear;
                return source;
            }
        }

        private void DestroyRuntimeTextures() {
            if (textures == null) {
                return;
            }

            if (!ownsTextures) {
                textures = null;
                ownsTextures = true;
                return;
            }

            var destroyed = new HashSet<Texture2D>();
            for (int i = 0; i < textures.Length; i++) {
                Texture2D texture = textures[i];
                textures[i] = null;
                if (texture == null || !destroyed.Add(texture)) {
                    continue;
                }

                if ((texture.hideFlags & HideFlags.DontSave) == 0) {
                    continue;
                }

                if (Application.isPlaying) {
                    Destroy(texture);
                } else {
                    DestroyImmediate(texture);
                }
            }
        }

        private static void SetFloatIfPropertyExists(Material target, string property, float value) {
            if (target.HasProperty(property)) {
                target.SetFloat(property, value);
            }
        }

        private static void SetTextureIfPropertyExists(Material target, string property, Texture texture) {
            if (target.HasProperty(property)) {
                target.SetTexture(property, texture);
            }
        }
    }
}
