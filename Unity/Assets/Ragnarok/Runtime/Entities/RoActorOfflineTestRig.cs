using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Assets;
using Ragnarok.Combat;
using Ragnarok.Content;
using Ragnarok.Effects;
using Ragnarok.Network;
using Ragnarok.Sprites;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.Entities {
    public enum RoActorOfflinePreviewPreset {
        DefaultNovice,
        BodyPaletteVariant,
        HairPaletteVariant,
        HeadgearTop,
        FullLayerProbe
    }

    public sealed class RoActorOfflineTestRig : MonoBehaviour {
        [SerializeField] private RagnarokContentSettings contentSettings;
        [SerializeField] private bool useSavedContentProfile = true;
        [SerializeField] private bool spawnOnStart = true;
        [SerializeField] private bool autoPopulateDefaultPcLayers = true;
        [SerializeField] private RoActorOfflinePreviewPreset previewPreset = RoActorOfflinePreviewPreset.DefaultNovice;
        [SerializeField, Min(0)] private int bodyPaletteVariantId = 1;
        [SerializeField, Min(0)] private int hairPaletteVariantId = 1;
        [SerializeField] private Vector3 actorLocalOffset = new Vector3(0f, RoActorSpriteVisual.GroundedVisualYOffset, 0f);
        [SerializeField] private RoActorSpriteLayerSpec[] layerSpecs = Array.Empty<RoActorSpriteLayerSpec>();
        [SerializeField] private RoSpriteMotion motion = RoSpriteMotion.Idle;
        [SerializeField] private RoActorDirection facingDirection = RoActorDirection.South;
        [SerializeField, Range(0, 2)] private int headDirectionFrameIndex;
        [SerializeField] private bool showShadow = true;
        [SerializeField] private bool showLabelAnchors = true;
        [SerializeField] private string actorName = "Novice";
        [SerializeField, Min(0)] private int hp = 50;
        [SerializeField, Min(1)] private int maxHp = 50;
        [SerializeField] private bool enableKeyboardPreviewControls = true;
        [SerializeField] private bool cycleMotionPreview;
        [SerializeField, Min(0.1f)] private float motionPreviewSeconds = 1.2f;
        [SerializeField] private bool frameCameraOnSpawn = true;
        [SerializeField, Min(0.1f)] private float previewCameraDistance = 6f;
        [SerializeField, Min(0.1f)] private float previewCameraPadding = 1.35f;
        [SerializeField, Min(0.1f)] private float minimumPreviewOrthographicSize = 0.8f;
        [SerializeField] private bool showDiagnostics = true;
        [SerializeField, TextArea(3, 8)] private string lastSpawnStatus = "Not started.";
        [SerializeField] private RoSpriteMotion[] motionPreviewSequence = {
            RoSpriteMotion.Idle,
            RoSpriteMotion.Walk,
            RoSpriteMotion.Sit,
            RoSpriteMotion.Hit,
            RoSpriteMotion.Dead,
            RoSpriteMotion.Attack1
        };

        private readonly RagnarokContentService content = new RagnarokContentService();
        private GameObject actorObject;
        private GameObject diagnosticsObject;
        private int motionPreviewIndex;
        private float nextMotionPreviewTime;

        public RoActorLayeredVisual Actor { get; private set; }

        private void Start() {
            if (spawnOnStart) {
                SpawnActor();
            }
        }

        private void Update() {
            if (cycleMotionPreview) {
                AdvanceMotionPreview();
            }

            if (enableKeyboardPreviewControls) {
                HandleKeyboardPreviewControls();
            }

            FaceDiagnosticsToCamera();
        }

        private void OnDestroy() {
            ClearActor();
            content.Dispose();
        }

        [ContextMenu("Spawn Actor")]
        public void SpawnActor() {
            ClearActor();
            try {
                SetSpawnStatus("Initializing RO content...", false);
                if (!EnsureContentInitialized()) {
                    FailSpawn("RO content profile not loaded.\nUse saved content profile or assign Content Settings.");
                    return;
                }

                SetSpawnStatus("Resolving actor sprite layers...");
                var resolveWatch = System.Diagnostics.Stopwatch.StartNew();
                RoActorSpriteLayerSpec[] sortedSpecs = ResolveLayerSpecs();
                resolveWatch.Stop();
                string layerSummary = BuildLayerSummary(sortedSpecs);
                SetSpawnStatus($"Resolved {sortedSpecs.Length} actor layer(s) in {resolveWatch.ElapsedMilliseconds} ms.\n{layerSummary}");

                if (sortedSpecs.Length == 0) {
                    FailSpawn("No actor .spr/.act layers resolved.\nCheck content profile and default PC body paths.");
                    return;
                }

                actorObject = new GameObject("RO Offline Actor");
                actorObject.transform.SetParent(transform, false);
                actorObject.transform.localPosition = actorLocalOffset;

                Actor = actorObject.AddComponent<RoActorLayeredVisual>();
                var loadWatch = System.Diagnostics.Stopwatch.StartNew();
                bool initialized = Actor.Initialize(content.Assets, sortedSpecs, motion, facingDirection);
                loadWatch.Stop();
                if (!initialized || Actor.LayerCount == 0) {
                    string message = $"Actor layers resolved but none loaded in {loadWatch.ElapsedMilliseconds} ms.\nCheck Console for missing SPR/ACT details.\n{layerSummary}";
                    ClearActor();
                    FailSpawn(message);
                    return;
                }

                Actor.SetHeadDirectionFrameIndex(headDirectionFrameIndex);
                if (showShadow) {
                    var shadow = actorObject.AddComponent<RoActorShadowVisual>();
                    shadow.Initialize(content.Assets, 0f);
                }

                if (showLabelAnchors) {
                    var labels = actorObject.AddComponent<RoActorLabelAnchorVisual>();
                    labels.Initialize(Actor, actorName, hp, maxHp);
                }

                FrameCameraOnActor();
                SetSpawnStatus($"Spawned actor with {Actor.LayerCount}/{sortedSpecs.Length} loaded layer(s). Resolve={resolveWatch.ElapsedMilliseconds} ms, load={loadWatch.ElapsedMilliseconds} ms.\n{layerSummary}");
                ResetMotionPreviewTimer();
            } catch (Exception ex) {
                Debug.LogException(ex, this);
                ClearActor();
                ShowDiagnostics($"Actor spawn exception:\n{ex.Message}");
            }
        }

        [ContextMenu("Apply Pose")]
        public void ApplyPose() {
            if (Actor == null) {
                return;
            }

            Actor.SetMotionAndFacing(motion, facingDirection);
            Actor.SetHeadDirectionFrameIndex(headDirectionFrameIndex);
        }

        [ContextMenu("Pose Idle")]
        public void PoseIdle() {
            SetPreviewMotion(RoSpriteMotion.Idle);
        }

        [ContextMenu("Pose Walk")]
        public void PoseWalk() {
            SetPreviewMotion(RoSpriteMotion.Walk);
        }

        [ContextMenu("Pose Sit")]
        public void PoseSit() {
            SetPreviewMotion(RoSpriteMotion.Sit);
        }

        [ContextMenu("Pose Hit")]
        public void PoseHit() {
            SetPreviewMotion(RoSpriteMotion.Hit);
        }

        [ContextMenu("Pose Dead")]
        public void PoseDead() {
            SetPreviewMotion(RoSpriteMotion.Dead);
        }

        [ContextMenu("Pose Attack")]
        public void PoseAttack() {
            SetPreviewMotion(RoSpriteMotion.Attack1);
        }

        [ContextMenu("Preset Default Novice")]
        public void PresetDefaultNovice() {
            ApplyPreviewPreset(RoActorOfflinePreviewPreset.DefaultNovice);
        }

        [ContextMenu("Preset Body Palette Variant")]
        public void PresetBodyPaletteVariant() {
            ApplyPreviewPreset(RoActorOfflinePreviewPreset.BodyPaletteVariant);
        }

        [ContextMenu("Preset Hair Palette Variant")]
        public void PresetHairPaletteVariant() {
            ApplyPreviewPreset(RoActorOfflinePreviewPreset.HairPaletteVariant);
        }

        [ContextMenu("Preset Headgear Top")]
        public void PresetHeadgearTop() {
            ApplyPreviewPreset(RoActorOfflinePreviewPreset.HeadgearTop);
        }

        [ContextMenu("Preset Full Layer Probe")]
        public void PresetFullLayerProbe() {
            ApplyPreviewPreset(RoActorOfflinePreviewPreset.FullLayerProbe);
        }

        [ContextMenu("Clear Actor")]
        public void ClearActor() {
            ClearDiagnostics();
            if (actorObject == null) {
                Actor = null;
                return;
            }

            if (Application.isPlaying) {
                Destroy(actorObject);
            } else {
                DestroyImmediate(actorObject);
            }

            actorObject = null;
            Actor = null;
        }

        private RoActorSpriteLayerSpec[] ResolveLayerSpecs() {
            List<RoActorSpriteLayerSpec> specs = CopyUsableLayerSpecs(layerSpecs);
            if (!autoPopulateDefaultPcLayers) {
                return RoActorLayeredVisual.SortSpecsForRendering(specs);
            }

            return RoActorSpritePathResolver.BuildPlayerLayerSpecs(content.Assets, CreateAppearanceRequest(), specs, this);
        }

        private void FailSpawn(string message) {
            Debug.LogWarning($"Offline actor test rig: {message}", this);
            ShowDiagnostics(message);
        }

        private void SetSpawnStatus(string message, bool log = true) {
            lastSpawnStatus = message ?? string.Empty;
            if (log) {
                Debug.Log($"Offline actor test rig: {lastSpawnStatus}", this);
            }
        }

        private static string BuildLayerSummary(IReadOnlyList<RoActorSpriteLayerSpec> specs) {
            if (specs == null || specs.Count == 0) {
                return "No resolved layers.";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < specs.Count; i++) {
                RoActorSpriteLayerSpec spec = specs[i];
                if (spec == null) {
                    continue;
                }

                builder.Append("- ");
                builder.Append(spec.layer);
                builder.Append(": ");
                builder.Append(spec.basePath);
                if (!string.IsNullOrWhiteSpace(spec.palettePath)) {
                    builder.Append(" palette=");
                    builder.Append(spec.palettePath);
                }

                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private void FrameCameraOnActor() {
            if (!frameCameraOnSpawn || actorObject == null) {
                return;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null || !TryCalculateActorRenderBounds(out Bounds bounds)) {
                return;
            }

            camera.orthographic = true;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, previewCameraDistance + 20f);
            camera.transform.rotation = Quaternion.identity;

            Vector3 center = bounds.center;
            camera.transform.position = new Vector3(center.x, center.y, center.z - previewCameraDistance);

            float aspect = Mathf.Max(0.1f, camera.aspect);
            float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect) * previewCameraPadding;
            camera.orthographicSize = Mathf.Max(minimumPreviewOrthographicSize, halfHeight);
        }

        private bool TryCalculateActorRenderBounds(out Bounds bounds) {
            bounds = default;
            if (actorObject == null) {
                return false;
            }

            MeshRenderer[] renderers = actorObject.GetComponentsInChildren<MeshRenderer>();
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++) {
                MeshRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.FindComponent<TextMesh>() != null) {
                    continue;
                }

                if (!hasBounds) {
                    bounds = renderer.bounds;
                    hasBounds = true;
                } else {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private RoActorAppearanceRequest CreateAppearanceRequest() {
            var appearance = RoActorAppearanceRequest.CreateDefaultPlayerProbe();
            appearance.motion = motion;
            appearance.facingDirection = facingDirection;
            appearance.headFacing = (RoActorHeadFacing) Mathf.Clamp(headDirectionFrameIndex, 0, 2);
            ApplyPreviewPreset(appearance, previewPreset);
            return appearance;
        }

        private void ApplyPreviewPreset(RoActorOfflinePreviewPreset preset) {
            previewPreset = preset;
            if (Application.isPlaying && actorObject != null) {
                SpawnActor();
            }
        }

        private void ApplyPreviewPreset(RoActorAppearanceRequest appearance, RoActorOfflinePreviewPreset preset) {
            switch (preset) {
                case RoActorOfflinePreviewPreset.BodyPaletteVariant:
                    appearance.bodyPaletteId = Mathf.Max(0, bodyPaletteVariantId);
                    break;
                case RoActorOfflinePreviewPreset.HairPaletteVariant:
                    appearance.hairColorId = Mathf.Max(0, hairPaletteVariantId);
                    break;
                case RoActorOfflinePreviewPreset.HeadgearTop:
                    appearance.headgearTopId = 1;
                    break;
                case RoActorOfflinePreviewPreset.FullLayerProbe:
                    appearance.bodyPaletteId = Mathf.Max(0, bodyPaletteVariantId);
                    appearance.hairColorId = Mathf.Max(0, hairPaletteVariantId);
                    appearance.headgearTopId = 1;
                    break;
            }
        }

        private void SetPreviewMotion(RoSpriteMotion newMotion) {
            motion = newMotion;
            ResetMotionPreviewTimer();
            ApplyPose();
        }

        private void StepFacing(int directionStep) {
            facingDirection = RoActorLayeredVisual.StepFacingDirection(facingDirection, directionStep);
            ApplyPose();
        }

        private void SetHeadDirection(int frameIndex) {
            headDirectionFrameIndex = Mathf.Clamp(frameIndex, 0, 2);
            ApplyPose();
        }

        private void AdvanceMotionPreview() {
            if (motionPreviewSequence == null || motionPreviewSequence.Length == 0) {
                return;
            }

            if (Time.time < nextMotionPreviewTime) {
                return;
            }

            motionPreviewIndex = (motionPreviewIndex + 1) % motionPreviewSequence.Length;
            SetPreviewMotion(motionPreviewSequence[motionPreviewIndex]);
        }

        private void ResetMotionPreviewTimer() {
            nextMotionPreviewTime = Time.time + Mathf.Max(0.1f, motionPreviewSeconds);
        }

        private void HandleKeyboardPreviewControls() {
            if (WasKeyPressed(Alpha1Key())) {
                SetPreviewMotion(RoSpriteMotion.Idle);
            } else if (WasKeyPressed(Alpha2Key())) {
                SetPreviewMotion(RoSpriteMotion.Walk);
            } else if (WasKeyPressed(Alpha3Key())) {
                SetPreviewMotion(RoSpriteMotion.Sit);
            } else if (WasKeyPressed(Alpha4Key())) {
                SetPreviewMotion(RoSpriteMotion.Hit);
            } else if (WasKeyPressed(Alpha5Key())) {
                SetPreviewMotion(RoSpriteMotion.Dead);
            } else if (WasKeyPressed(Alpha6Key())) {
                SetPreviewMotion(RoSpriteMotion.Attack1);
            }

            if (WasKeyPressed(QKey())) {
                StepFacing(-1);
            } else if (WasKeyPressed(EKey())) {
                StepFacing(1);
            }

            if (WasKeyPressed(ZKey())) {
                SetHeadDirection(2);
            } else if (WasKeyPressed(XKey())) {
                SetHeadDirection(0);
            } else if (WasKeyPressed(CKey())) {
                SetHeadDirection(1);
            }
        }

        private static bool WasKeyPressed(KeyCode legacyKey) {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null) {
                Key key = legacyKey switch {
                    KeyCode.Alpha1 => Key.Digit1,
                    KeyCode.Alpha2 => Key.Digit2,
                    KeyCode.Alpha3 => Key.Digit3,
                    KeyCode.Alpha4 => Key.Digit4,
                    KeyCode.Alpha5 => Key.Digit5,
                    KeyCode.Alpha6 => Key.Digit6,
                    KeyCode.Q => Key.Q,
                    KeyCode.E => Key.E,
                    KeyCode.Z => Key.Z,
                    KeyCode.X => Key.X,
                    KeyCode.C => Key.C,
                    _ => Key.None
                };
                if (key != Key.None && keyboard[key].wasPressedThisFrame) {
                    return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(legacyKey);
#else
            return false;
#endif
        }

        private static KeyCode Alpha1Key() => KeyCode.Alpha1;
        private static KeyCode Alpha2Key() => KeyCode.Alpha2;
        private static KeyCode Alpha3Key() => KeyCode.Alpha3;
        private static KeyCode Alpha4Key() => KeyCode.Alpha4;
        private static KeyCode Alpha5Key() => KeyCode.Alpha5;
        private static KeyCode Alpha6Key() => KeyCode.Alpha6;
        private static KeyCode QKey() => KeyCode.Q;
        private static KeyCode EKey() => KeyCode.E;
        private static KeyCode ZKey() => KeyCode.Z;
        private static KeyCode XKey() => KeyCode.X;
        private static KeyCode CKey() => KeyCode.C;

        private List<RoActorSpriteLayerSpec> CopyUsableLayerSpecs(IReadOnlyList<RoActorSpriteLayerSpec> specs) {
            var usable = new List<RoActorSpriteLayerSpec>();
            if (specs == null) {
                return usable;
            }

            for (int i = 0; i < specs.Count; i++) {
                RoActorSpriteLayerSpec spec = specs[i];
                if (spec == null || string.IsNullOrWhiteSpace(spec.basePath)) {
                    continue;
                }

                string basePath = RoActorSpriteVisual.NormalizeBasePath(spec.basePath);
                if (content.IsInitialized && !SpritePairExists(basePath)) {
                    Debug.LogWarning($"Offline actor test rig layer '{spec.layer}' path '{basePath}' was not found as a .spr/.act pair. Falling back to auto-resolution if enabled.", this);
                    continue;
                }

                usable.Add(new RoActorSpriteLayerSpec(spec.layer, basePath) {
                    palettePath = spec.palettePath,
                    initialActionIndex = spec.initialActionIndex,
                    motionProfile = spec.motionProfile,
                    localOffset = spec.localOffset,
                    scale = spec.scale
                });
            }

            return usable;
        }

        private bool SpritePairExists(string basePath) {
            return content.IsInitialized &&
                content.Assets.Exists(RoSpritePairPath.SpritePath(basePath)) &&
                content.Assets.Exists(RoSpritePairPath.ActionPath(basePath));
        }

        private bool EnsureContentInitialized() {
            if (content.IsInitialized) {
                return true;
            }

            string report = null;
            if (useSavedContentProfile && content.TryInitializeSavedProfile(out report)) {
                Debug.Log(report, this);
                return true;
            }

            if (useSavedContentProfile && !string.IsNullOrWhiteSpace(report)) {
                Debug.LogWarning(report, this);
            }

            if (contentSettings == null) {
                Debug.LogWarning("Offline actor test rig has no content settings and no saved content profile could be loaded.", this);
                return false;
            }

            try {
                content.Initialize(contentSettings);
                Debug.Log($"Offline actor test rig loaded RO content from scene settings '{contentSettings.name}'.", this);
                return true;
            } catch (Exception ex) {
                Debug.LogWarning($"Offline actor test rig could not initialize content: {ex.Message}", this);
                return false;
            }
        }

        private void ShowDiagnostics(string message) {
            lastSpawnStatus = message ?? string.Empty;
            if (!showDiagnostics) {
                return;
            }

            ClearDiagnostics();
            diagnosticsObject = new GameObject("RO Offline Actor Diagnostics");
            diagnosticsObject.transform.SetParent(transform, false);
            diagnosticsObject.transform.localPosition = new Vector3(0f, 1.1f, 0f);

            var text = diagnosticsObject.AddComponent<TextMesh>();
            text.text = message;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.12f;
            text.fontSize = 32;
            text.color = new Color(1f, 0.72f, 0.2f, 1f);

            FaceDiagnosticsToCamera();
        }

        private void ClearDiagnostics() {
            if (diagnosticsObject == null) {
                return;
            }

            if (Application.isPlaying) {
                Destroy(diagnosticsObject);
            } else {
                DestroyImmediate(diagnosticsObject);
            }

            diagnosticsObject = null;
        }

        private void FaceDiagnosticsToCamera() {
            if (diagnosticsObject == null) {
                return;
            }

            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera != null) {
                diagnosticsObject.transform.rotation = camera.transform.rotation;
            }
        }
    }
}
