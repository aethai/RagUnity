using System;
using Ragnarok.Assets;
using Ragnarok.Diagnostics;
using Ragnarok.Entities;
using Ragnarok.Sprites;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.Effects {
    public sealed class RoActorOverlayRenderer : MonoBehaviour {
        private RoActorOverlaySpec spec;
        private Transform followTarget;
        private Vector3 followLocalOffset;
        private Vector3 worldPosition;
        private UnityEngine.Camera renderCamera;
        private float durationSeconds;
        private float elapsedSeconds;
        private bool initialized;
        private bool finished;
        private bool usingSprite;

        private GameObject spriteObject;
        private GameObject textObject;
        private RoActSpriteAnimator spriteAnimator;
        private TextMesh textMesh;
        private MeshRenderer textRenderer;
        private Material spriteMaterial;
        private Color baseTextColor;
        private bool hasLastRotation;
        private bool hasLastPosition;
        private bool hasLastColor;
        private Quaternion lastRotation;
        private Vector3 lastPosition;
        private Color lastColor;

        public event Action<RoActorOverlayRenderer> Finished;

        public bool IsInitialized => initialized;
        public bool IsFinished => finished;
        public bool IsUsingSprite => usingSprite;
        public string CurrentText => !usingSprite && textMesh != null ? textMesh.text : string.Empty;
        public RoActorOverlayKind CurrentKind => spec.Kind;
        public int CurrentSpriteActionIndex => spec.EffectiveSpriteActionIndex;

        private void Update() {
            if (!initialized || finished) {
                return;
            }

            long updateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            using (RoProfilerMarkers.ActorOverlayUpdate.Auto()) {
                try {
                    elapsedSeconds += Time.deltaTime;
                    ApplyFrame();
                    if (elapsedSeconds >= durationSeconds) {
                        Stop();
                    }
                } finally {
                    RoRuntimePerfCounters.AddActorOverlayUpdateTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(updateStartTicks));
                }
            }
        }

        private void OnDestroy() {
            if (spriteMaterial != null) {
                DestroyRuntimeObject(spriteMaterial);
                spriteMaterial = null;
            }
        }

        public void Initialize(
            RoActorOverlaySpec overlaySpec,
            Vector3 spawnWorldPosition,
            RoAssetDatabase assetDatabase = null,
            Transform target = null,
            Vector3 targetOffset = default,
            UnityEngine.Camera camera = null
        ) {
            spec = overlaySpec;
            worldPosition = spawnWorldPosition;
            followTarget = target;
            followLocalOffset = targetOffset;
            renderCamera = camera;
            durationSeconds = spec.EffectiveDurationSeconds;
            elapsedSeconds = 0f;
            finished = false;
            initialized = true;
            hasLastRotation = false;
            hasLastPosition = false;
            hasLastColor = false;
            gameObject.SetActive(true);

            usingSprite = TryInitializeSprite(assetDatabase);
            if (!usingSprite) {
                InitializeTextFallback();
            }

            ApplyFrame();
        }

        public void Stop() {
            if (finished) {
                return;
            }

            finished = true;
            gameObject.SetActive(false);
            Finished?.Invoke(this);
        }

        private bool TryInitializeSprite(RoAssetDatabase assetDatabase) {
            SetTextVisible(false);
            SetSpriteVisible(false);

            if (assetDatabase == null ||
                spec.EffectiveSpriteActionIndex < 0 ||
                string.IsNullOrWhiteSpace(spec.SpriteBasePath)) {
                return false;
            }

            try {
                string basePath = RoActorSpriteVisual.NormalizeBasePath(spec.SpriteBasePath);
                if (!TryResolveCachedSprite(assetDatabase, basePath, out RoSpriteAtlasCacheEntry cachedEntry)) {
                    return false;
                }

                EnsureSpriteComponents();
                if (spriteMaterial == null) {
                    spriteMaterial = CreateSpriteMaterial();
                }

                ConfigureSpriteMaterial(spriteMaterial, spec.UseDepthTest, spec.EffectiveRenderQueue);

                spriteAnimator.Initialize(
                    cachedEntry.Sprite,
                    cachedEntry.Act,
                    cachedEntry.Atlas,
                    spec.EffectiveSpriteActionIndex,
                    spriteMaterial);
                spriteAnimator.SetLoop(spec.LoopSprite);
                spriteAnimator.SetMaterialRenderQueue(spec.EffectiveRenderQueue);
                if (!spec.LoopSprite &&
                    spec.DurationSeconds <= 0f &&
                    spriteAnimator.TryGetActionDurationSeconds(spec.EffectiveSpriteActionIndex, out float spriteDuration)) {
                    durationSeconds = Mathf.Max(0.1f, spriteDuration);
                }

                spriteObject.transform.localPosition = Vector3.zero;
                spriteObject.transform.localScale = Vector3.one * spec.EffectiveScale;
                SetSpriteVisible(true);
                return true;
            } catch (Exception ex) {
                Debug.LogWarning($"Could not initialize actor overlay sprite '{spec.SpriteBasePath}': {ex.Message}", this);
                return false;
            }
        }

        private static bool TryResolveCachedSprite(
            RoAssetDatabase assetDatabase,
            string basePath,
            out RoSpriteAtlasCacheEntry cachedEntry
        ) {
            cachedEntry = null;
            if (assetDatabase == null || string.IsNullOrWhiteSpace(basePath)) {
                return false;
            }

            if (RoSpriteAtlasCache.TryGet(assetDatabase, basePath, out cachedEntry)) {
                RoRuntimePerfCounters.IncrementActorOverlaySpriteCacheHit();
                return cachedEntry != null;
            }

            RoRuntimePerfCounters.IncrementActorOverlaySpriteCacheMiss();
            if (RoSpriteAtlasCache.TryGetOrBuild(assetDatabase, basePath, out cachedEntry) && cachedEntry != null) {
                return true;
            }

            RoRuntimePerfCounters.IncrementActorOverlaySpriteCacheBuildFailure();
            return false;
        }

        private void InitializeTextFallback() {
            EnsureTextComponents();
            SetSpriteVisible(false);
            SetTextVisible(true);

            textMesh.text = spec.BuildFallbackText();
            textMesh.characterSize = 0.16f * spec.EffectiveScale;
            baseTextColor = spec.ResolveColor();
            textMesh.color = baseTextColor;
            lastColor = baseTextColor;
            hasLastColor = true;
        }

        private void EnsureSpriteComponents() {
            if (spriteObject == null) {
                spriteObject = new GameObject("Sprite");
                spriteObject.transform.SetParent(transform, false);
            }

            spriteAnimator = spriteObject.GetComponent<RoActSpriteAnimator>();
            if (spriteAnimator == null) {
                spriteAnimator = spriteObject.AddComponent<RoActSpriteAnimator>();
            }
        }

        private void EnsureTextComponents() {
            if (textObject == null) {
                textObject = new GameObject("Text");
                textObject.transform.SetParent(transform, false);
            }

            textMesh = textObject.GetComponent<TextMesh>();
            if (textMesh == null) {
                textMesh = textObject.AddComponent<TextMesh>();
            }

            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 48;

            textRenderer = textObject.GetComponent<MeshRenderer>();
            if (textRenderer == null) {
                textRenderer = textObject.AddComponent<MeshRenderer>();
            }

            textRenderer.shadowCastingMode = ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
            textRenderer.sortingOrder = 30000;
        }

        private void ApplyFrame() {
            long applyFrameStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            using (RoProfilerMarkers.ActorOverlayApplyFrame.Auto()) {
                try {
                    UnityEngine.Camera camera = ResolveCamera();
                    if (camera != null) {
                        SetRotationIfChanged(camera.transform.rotation);
                    }

                    Vector3 basePosition = followTarget != null
                        ? followTarget.TransformPoint(followLocalOffset)
                        : worldPosition;
                    float bob = spec.EffectiveBobAmplitude > 0f
                        ? Mathf.Sin(elapsedSeconds * spec.EffectiveBobFrequency) * spec.EffectiveBobAmplitude
                        : 0f;
                    SetPositionIfChanged(basePosition + Vector3.up * (spec.EffectiveHeightOffset + bob));

                    if (!usingSprite && textMesh != null) {
                        Color color = baseTextColor;
                        float fadeStart = durationSeconds * 0.7f;
                        if (elapsedSeconds > fadeStart && durationSeconds > fadeStart) {
                            color.a *= Mathf.InverseLerp(durationSeconds, fadeStart, elapsedSeconds);
                        }

                        SetTextColorIfChanged(color);
                    }
                } finally {
                    RoRuntimePerfCounters.AddActorOverlayApplyFrameTicks(
                        RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(applyFrameStartTicks));
                }
            }
        }

        private UnityEngine.Camera ResolveCamera() {
            return RoEffectCameraCache.Resolve(renderCamera);
        }

        private void SetRotationIfChanged(Quaternion rotation) {
            if (hasLastRotation && lastRotation == rotation) {
                return;
            }

            transform.rotation = rotation;
            lastRotation = rotation;
            hasLastRotation = true;
        }

        private void SetPositionIfChanged(Vector3 position) {
            if (hasLastPosition && lastPosition == position) {
                return;
            }

            transform.position = position;
            lastPosition = position;
            hasLastPosition = true;
        }

        private void SetTextColorIfChanged(Color color) {
            if (hasLastColor && lastColor == color) {
                return;
            }

            textMesh.color = color;
            lastColor = color;
            hasLastColor = true;
        }

        private void SetSpriteVisible(bool visible) {
            if (spriteObject != null) {
                spriteObject.SetActive(visible);
            }
        }

        private void SetTextVisible(bool visible) {
            if (textObject != null) {
                textObject.SetActive(visible);
            }
        }

        private static Material CreateSpriteMaterial() {
            Shader shader = Shader.Find("Ragnarok/RO Sprite Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader) {
                name = "RO Actor Overlay Sprite",
                hideFlags = HideFlags.DontSave
            };
            return material;
        }

        private static void ConfigureSpriteMaterial(Material material, bool useDepthTest, int renderQueue) {
            if (material == null) {
                return;
            }

            material.renderQueue = renderQueue;
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Off);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_ZTest", (float) (useDepthTest ? CompareFunction.LessEqual : CompareFunction.Always));
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            SetFloatIfPropertyExists(material, "_Cutoff", 0.01f);
        }

        private static void SetFloatIfPropertyExists(Material material, string property, float value) {
            if (material != null && material.HasProperty(property)) {
                material.SetFloat(property, value);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object obj) {
            if (obj == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(obj);
            } else {
                DestroyImmediate(obj);
            }
        }
    }
}
