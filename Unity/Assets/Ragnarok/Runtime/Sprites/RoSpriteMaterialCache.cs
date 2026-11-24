using System;
using System.Collections.Generic;
using Ragnarok.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Sprites {
    internal static class RoSpriteMaterialCache {
        private const int DefaultRenderQueue = (int) RenderQueue.Transparent;

        private static readonly Dictionary<CacheKey, CacheEntry> Entries = new Dictionary<CacheKey, CacheEntry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnDomainReload() {
            Clear();
        }

        public static RoSpriteMaterialLease Acquire(Texture texture, RoShadowRenderMode mode, int renderQueue = DefaultRenderQueue) {
            Shader shader = ResolveShader(mode);
            int shaderId = shader != null ? shader.GetInstanceID() : 0;
            int textureId = texture != null ? texture.GetInstanceID() : 0;
            var key = new CacheKey(shaderId, textureId, mode, renderQueue);
            if (Entries.TryGetValue(key, out CacheEntry entry) && entry.Material != null) {
                RoRuntimePerfCounters.IncrementSpriteMaterialCacheHit();
                entry.ReferenceCount++;
                return new RoSpriteMaterialLease(key, entry.Material);
            }

            RoRuntimePerfCounters.IncrementSpriteMaterialCacheMiss();
            Material material = CreateMaterial(shader, texture, mode, renderQueue);
            Entries[key] = new CacheEntry(material, 1);
            return new RoSpriteMaterialLease(key, material);
        }

        public static void Release(CacheKey key) {
            if (!Entries.TryGetValue(key, out CacheEntry entry)) {
                return;
            }

            entry.ReferenceCount--;
            if (entry.ReferenceCount > 0) {
                return;
            }

            DestroyRuntimeObject(entry.Material);
            Entries.Remove(key);
        }

        public static void Clear() {
            foreach (CacheEntry entry in Entries.Values) {
                DestroyRuntimeObject(entry.Material);
            }

            Entries.Clear();
        }

        private static Shader ResolveShader(RoShadowRenderMode mode) {
            Shader shader = Shader.Find(mode == RoShadowRenderMode.Enhanced
                ? "Ragnarok/RO Sprite Enhanced"
                : "Ragnarok/RO Sprite Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            return shader;
        }

        private static Material CreateMaterial(Shader shader, Texture texture, RoShadowRenderMode mode, int renderQueue) {
            var material = new Material(shader) {
                name = "RO Sprite Material Shared",
                renderQueue = renderQueue,
                hideFlags = HideFlags.DontSave
            };

            if (texture != null) {
                material.mainTexture = texture;
            }

            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Off);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_ZTest", (float) RoActSpriteAnimator.SpriteDepthTest);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            SetFloatIfPropertyExists(material, "_Cutoff", 0.01f);
            SetFloatIfPropertyExists(material, "_DepthCorrection", 1f);
            SetFloatIfPropertyExists(material, "_RoSpriteDepthBias", RoActSpriteAnimator.SpriteDepthBias);
            SetFloatIfPropertyExists(material, "_RoDirectionalLightInfluence", RoEnhancedShadowSettings.DefaultActorDirectionalLightInfluence);
            SetFloatIfPropertyExists(material, "_RoEmissionLightInfluence", RoEnhancedShadowSettings.DefaultMapEmissionLightInfluence);
            RoActorLightingPolicy materialPolicy = RoActorLightingPolicy.Resolve(mode, null);
            SetFloatIfPropertyExists(material, "_RoReceiveRealtimeShadows", materialPolicy.ReceiveRealtimeShadows ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_RoRealtimeShadowStrength", materialPolicy.RealtimeShadowStrength);
            SetFloatIfPropertyExists(material, "_RoUseAdditionalLights", materialPolicy.UseAdditionalLights ? 1f : 0f);
            SetFloatIfPropertyExists(material, "_RoShadowCasterYOffset", 0f);
            SetFloatIfPropertyExists(material, "_RoShadowCasterPullDistance", RoEnhancedShadowSettings.DefaultActorShadowCasterPullDistance);
            SetFloatIfPropertyExists(material, "_RoShadowCasterGroundClearance", RoEnhancedShadowSettings.DefaultActorShadowCasterGroundClearance);
            SetFloatIfPropertyExists(material, "_RoShadowCasterLiftPullScale", RoEnhancedShadowSettings.DefaultActorShadowCasterLiftPullScale);
            SetFloatIfPropertyExists(material, "_RoShadowCasterBottomLift", 0f);
            SetFloatIfPropertyExists(material, "_RoUseSharedActorShadowCard", 0f);
            SetVectorIfPropertyExists(material, "_RoActorShadowCardCenterOS", Vector4.zero);
            SetFloatIfPropertyExists(material, "_RoActorShadowCardBottomLift", 0f);
            SetTextureIfPropertyExists(material, "_BaseMap", texture);
            SetTextureIfPropertyExists(material, "_MainTex", texture);
            return material;
        }

        private static void SetFloatIfPropertyExists(Material target, string property, float value) {
            if (target != null && target.HasProperty(property)) {
                target.SetFloat(property, value);
            }
        }

        private static void SetVectorIfPropertyExists(Material target, string property, Vector4 value) {
            if (target != null && target.HasProperty(property)) {
                target.SetVector(property, value);
            }
        }

        private static void SetTextureIfPropertyExists(Material target, string property, Texture texture) {
            if (target != null && target.HasProperty(property)) {
                target.SetTexture(property, texture);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                UnityEngine.Object.Destroy(target);
            } else {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        internal readonly struct CacheKey : IEquatable<CacheKey> {
            private readonly int shaderId;
            private readonly int textureId;
            private readonly RoShadowRenderMode mode;
            private readonly int renderQueue;

            public CacheKey(int shaderId, int textureId, RoShadowRenderMode mode, int renderQueue) {
                this.shaderId = shaderId;
                this.textureId = textureId;
                this.mode = mode;
                this.renderQueue = renderQueue;
            }

            public bool Equals(CacheKey other) {
                return shaderId == other.shaderId &&
                    textureId == other.textureId &&
                    mode == other.mode &&
                    renderQueue == other.renderQueue;
            }

            public override bool Equals(object obj) {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    int hash = shaderId;
                    hash = (hash * 397) ^ textureId;
                    hash = (hash * 397) ^ (int) mode;
                    hash = (hash * 397) ^ renderQueue;
                    return hash;
                }
            }
        }

        private sealed class CacheEntry {
            public CacheEntry(Material material, int referenceCount) {
                Material = material;
                ReferenceCount = referenceCount;
            }

            public Material Material { get; }
            public int ReferenceCount { get; set; }
        }
    }

    internal sealed class RoSpriteMaterialLease : IDisposable {
        private readonly RoSpriteMaterialCache.CacheKey key;
        private bool disposed;

        public RoSpriteMaterialLease(RoSpriteMaterialCache.CacheKey key, Material material) {
            this.key = key;
            Material = material;
        }

        public Material Material { get; private set; }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            RoSpriteMaterialCache.Release(key);
            Material = null;
        }
    }
}
