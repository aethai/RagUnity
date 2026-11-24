using Ragnarok.Assets;
using Ragnarok.Entities;
using Ragnarok.Input;
using Ragnarok.Sprites;
using ROIO.Models.FileTypes;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ragnarok.World {
    public sealed class RoSelectedTargetLockIndicator : MonoBehaviour {
        public const string CursorSpritePath = RoCursorSpriteVisual.CursorSpritePath;
        public const string CursorActionPath = RoCursorSpriteVisual.CursorActionPath;
        public const int CursorLockActionIndex = RoCursorSpriteVisual.LockAction;
        public const int DefaultRenderQueue = (int) RenderQueue.Overlay;
        public const int DefaultSortingOrder = 30160;

        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 fallbackWorldPosition;
        [SerializeField] private Vector3 worldOffset = Vector3.zero;
        [SerializeField] private UnityEngine.Camera renderCamera;
        [SerializeField] private bool isShowing;

        private GameObject spriteObject;
        private RoActSpriteAnimator animator;
        private Material spriteMaterial;
        private RoAssetDatabase initializedAssets;
        private bool initialized;
        private bool warnedInitializationFailure;
        private Quaternion lastRotation;
        private Vector3 lastPosition;
        private bool hasLastRotation;
        private bool hasLastPosition;

        public bool IsShowing => isShowing && spriteObject != null && spriteObject.activeSelf;
        public Transform FollowTarget => followTarget;
        public Vector3 FallbackWorldPosition => fallbackWorldPosition;
        public int ActionIndex => animator != null ? animator.ActionIndex : -1;

        public bool Configure(
            RoAssetDatabase assets,
            Transform target,
            Vector3 worldPosition,
            UnityEngine.Camera camera = null
        ) {
            if (assets == null || !assets.IsInitialized) {
                Hide();
                return false;
            }

            if (!EnsureInitialized(assets)) {
                Hide();
                return false;
            }

            followTarget = target;
            fallbackWorldPosition = worldPosition;
            renderCamera = camera != null ? camera : renderCamera;
            isShowing = true;
            hasLastPosition = false;
            hasLastRotation = false;
            SetSpriteVisible(true);
            ApplyTransform();
            return true;
        }

        public void Hide() {
            isShowing = false;
            followTarget = null;
            SetSpriteVisible(false);
            hasLastPosition = false;
            hasLastRotation = false;
        }

        private void LateUpdate() {
            if (!isShowing) {
                return;
            }

            ApplyTransform();
        }

        private void OnDisable() {
            isShowing = false;
            followTarget = null;
            SetSpriteVisible(false);
        }

        private void OnDestroy() {
            if (spriteMaterial != null) {
                DestroyRuntimeObject(spriteMaterial);
                spriteMaterial = null;
            }
        }

        private bool EnsureInitialized(RoAssetDatabase assets) {
            if (initialized && initializedAssets == assets && animator != null) {
                return true;
            }

            if (!TryLoadCursorAssets(assets, out RoSpriteAtlasCacheEntry cachedEntry)) {
                WarnInitializationFailure("could not load cursor lock SPR/ACT assets");
                return false;
            }

            ACT act = cachedEntry.Act;
            if (act.actions == null || CursorLockActionIndex < 0 || CursorLockActionIndex >= act.actions.Length) {
                WarnInitializationFailure($"cursor ACT does not contain lock action {CursorLockActionIndex}");
                return false;
            }

            EnsureSpriteComponents();
            EnsureSpriteMaterial();
            animator.Initialize(cachedEntry.Sprite, act, cachedEntry.Atlas, CursorLockActionIndex, spriteMaterial);
            animator.SetLoop(true);
            animator.SetPlayFrameSounds(false);
            animator.SetMaterialRenderQueue(DefaultRenderQueue);
            animator.SetRendererSortingOrder(DefaultSortingOrder);
            spriteObject.transform.localPosition = Vector3.zero;
            spriteObject.transform.localScale = Vector3.one;

            initialized = true;
            initializedAssets = assets;
            warnedInitializationFailure = false;
            return true;
        }

        private static bool TryLoadCursorAssets(RoAssetDatabase assets, out RoSpriteAtlasCacheEntry cachedEntry) {
            cachedEntry = null;
            return assets != null &&
                RoSpriteAtlasCache.TryGetOrBuild(assets, RoCursorSpriteVisual.CursorBasePath, out cachedEntry) &&
                cachedEntry != null &&
                cachedEntry.Sprite != null &&
                cachedEntry.Act != null &&
                cachedEntry.Atlas != null;
        }

        private void EnsureSpriteComponents() {
            if (spriteObject == null) {
                spriteObject = new GameObject("RO Selected Target Lock Sprite");
                spriteObject.transform.SetParent(transform, false);
            }

            animator = spriteObject.FindComponent<RoActSpriteAnimator>();
            if (animator == null) {
                animator = spriteObject.AddComponent<RoActSpriteAnimator>();
            }
        }

        private void EnsureSpriteMaterial() {
            if (spriteMaterial != null) {
                ConfigureSpriteMaterial(spriteMaterial);
                return;
            }

            Shader shader = Shader.Find("Ragnarok/RO Sprite Unlit");
            if (shader == null) {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null) {
                shader = Shader.Find("Sprites/Default");
            }

            spriteMaterial = new Material(shader) {
                name = "RO Selected Target Lock",
                hideFlags = HideFlags.DontSave
            };
            ConfigureSpriteMaterial(spriteMaterial);
        }

        private static void ConfigureSpriteMaterial(Material material) {
            if (material == null) {
                return;
            }

            material.renderQueue = DefaultRenderQueue;
            SetFloatIfPropertyExists(material, "_Cull", (float) CullMode.Off);
            SetFloatIfPropertyExists(material, "_ZWrite", 0f);
            SetFloatIfPropertyExists(material, "_ZTest", (float) CompareFunction.Always);
            SetFloatIfPropertyExists(material, "_SrcBlend", (float) BlendMode.SrcAlpha);
            SetFloatIfPropertyExists(material, "_DstBlend", (float) BlendMode.OneMinusSrcAlpha);
            SetFloatIfPropertyExists(material, "_Cutoff", 0.01f);
            SetFloatIfPropertyExists(material, "_DepthCorrection", 0f);
        }

        private void ApplyTransform() {
            UnityEngine.Camera camera = ResolveCamera();
            if (camera != null) {
                SetRotationIfChanged(camera.transform.rotation);
            }

            Vector3 basePosition = followTarget != null ? followTarget.position : fallbackWorldPosition;
            SetPositionIfChanged(basePosition + worldOffset);
        }

        private UnityEngine.Camera ResolveCamera() {
            if (renderCamera != null) {
                return renderCamera;
            }

            renderCamera = UnityEngine.Camera.main;
            return renderCamera;
        }

        private void SetSpriteVisible(bool visible) {
            if (spriteObject != null && spriteObject.activeSelf != visible) {
                spriteObject.SetActive(visible);
            }
        }

        private void SetPositionIfChanged(Vector3 position) {
            if (hasLastPosition && lastPosition == position) {
                return;
            }

            transform.position = position;
            lastPosition = position;
            hasLastPosition = true;
        }

        private void SetRotationIfChanged(Quaternion rotation) {
            if (hasLastRotation && lastRotation == rotation) {
                return;
            }

            transform.rotation = rotation;
            lastRotation = rotation;
            hasLastRotation = true;
        }

        private void WarnInitializationFailure(string reason) {
            initialized = false;
            initializedAssets = null;
            if (warnedInitializationFailure) {
                return;
            }

            warnedInitializationFailure = true;
            Debug.LogWarning($"Selected target lock indicator is disabled: {reason}.", this);
        }

        private static void SetFloatIfPropertyExists(Material material, string property, float value) {
            if (material != null && material.HasProperty(property)) {
                material.SetFloat(property, value);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target) {
            if (target == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(target);
            } else {
                DestroyImmediate(target);
            }
        }
    }
}
