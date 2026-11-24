using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Ragnarok.Diagnostics;
using Ragnarok.Sprites;

namespace Ragnarok.Camera {
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class RoCameraController : MonoBehaviour {
        // ── Camera zoom constants ──────────────────────────────────────────────────────────────
        // Source: ragnarokresearchlab.github.io/rendering/camera-controls/
        // Scale: 1 GAT tile = 5 kRO world units = 1 normalized unit.
        // At our scene scale (1 GAT tile ≈ 1 Unity unit): 1 normalized unit ≈ 1 Unity unit.
        //
        // Test baseline: match RagnarokRebuildTcp's gameplay camera more closely.
        // Rebuild normal mode uses FOV 15, distance 60, height/pitch 50 and zoom clamp 30..90.
        // We keep roBrowser/RRL-style input semantics and Rebuild-like clip planes. The default zoom
        // is intentionally pulled one notch farther than Rebuild's 60 (user preference) — see the
        // distance constants below.
        //
        // roBrowser camera tilt clamp is angleFinal[0]=190..270 in Camera.processMouseAction().
        // Unity pitch conversion is roBrowser angle - 180; range 10..90.
        // ─────────────────────────────────────────────────────────────────────────────────────
        public const float ClassicGravityZoomStep                    = 5f;    // 5 normalized units per scroll step (RRL)
        public const float ClassicGravityZoomLevels                  = 7f;    // classic client step count (RRL reference; modern range is larger)
        public const float ClassicGravityMinDistance                 = 30f;   // zoom-in clamp (Rebuild normal min).
        // User preference (2026-06-22): default framing pulled one zoom-out notch farther than Rebuild's 60.
        // Default == old max-out (90); max-out extended to keep the same 30u (= 6 scroll steps) of headroom.
        // Keeps the test invariant maxDistance == ClassicGravityDistance + ZoomStep*MaxZoomOutSteps (90+5*6=120).
        public const float ClassicGravityMaxDistance                 = 120f;  // zoom-out clamp (default 90 + 30u headroom).
        public const float ClassicGravityDistance                    = 90f;   // default framing (was 60; pulled out per preference).
        // roBrowser zoom scale cross-check (Camera.js, gravity camera):
        //   roBrowser reference = 300 kRO world units = 300/5 = 60 normalized
        //   Rebuild-like default (60) lines up with that normalized reference.
        public const float RobrowserDefaultZoom                      = 300f;  // roBrowser reference zoom in kRO world units
        public const float ClassicGravityUnityUnitsPerRobrowserZoom  = 1f / 5f; // 1 normalized unit = 5 kRO world units
        public const float ClassicGravityDefaultZoomInSteps          = 0f;   // default (60) == roBrowser normalized reference
        public const float ClassicGravityMaxZoomOutSteps             = 6f;   // default (60) -> max (90): 6 scroll-out steps
        public const float ClassicGravityYaw              = 0f;
        public const float ClassicGravityPitch            = 50f;   // Rebuild normal Height.
        public const float ClassicGravityFieldOfView      = 15f;   // RRL + roBrowser confirmed
        public const float RobrowserMinCameraAngle        = 190f;  // roBrowser Camera.processMouseAction() lower clamp
        public const float RobrowserMaxCameraAngle        = 270f;  // roBrowser Camera.processMouseAction() upper clamp
        public const float ClassicGravityMinPitch         = RobrowserMinCameraAngle - 180f;
        public const float ClassicGravityMaxPitch         = RobrowserMaxCameraAngle - 180f;
        public const float ClassicGravityTiltStep         = 5f;    // exactly 5° per scroll event (RRL confirmed)
        public const float ClassicGravityRotateDegreesPerScreenWidth       = 720f;   // ~1°/px at 720px — roBrowser
        public const float ClassicGravityRightDragTiltDegreesPerScreenHeight = 300f; // roBrowser confirmed
        public const float ClassicGravityRightDragZoomDistancePerPixel     = ClassicGravityZoomStep / 10f;
        public const float ClassicGravityZoomSmoothing    = 12f;
        // ── Camera follow smoothing (MVT-17) ───────────────────────────────────────────────────
        // Rebuild's "turbo smooth" follow eases the look-at toward the player instead of hard-locking
        // to it: Lerp(TargetFollow, playerPos, dt*5) horizontally + Lerp(height, ground, dt*20)
        // vertically (CameraFollower.cs). We use FPS-independent exponential smoothing (SmoothValue)
        // with the same factors so a per-frame sprite jitter no longer shakes the whole screen.
        public const float ClassicGravityFollowSmoothing  = 5f;    // horizontal (XZ) ease factor
        public const float ClassicGravityHeightSmoothing  = 20f;   // vertical (Y) ease — softens GAT height steps on slopes
        public const float ClassicGravityFollowSnapDistance = 3f;  // horizontal jump (cells) above which we snap instead of driving across the map (teleport/warp/respawn/knockback)
        // Yaw (rotation) easing — Rebuild eases Rotation toward TargetRotation with LerpAngle(.,7.5*dt)
        // (CameraFollower.cs:2182). We use the same factor but FPS-independent (exp) so right-drag rotation
        // glides instead of snapping. Only the rotation input is eased; presets/resets set yaw directly.
        public const float ClassicGravityYawSmoothing     = 7.5f;
        public const float ClassicGravityDoubleRightClickSeconds     = 0.5f;
        public const float ClassicGravityDoubleRightClickMaxPixels   = 10f;
        public const float ClassicGravityNearPlane                   = 0.2f; // Rebuild main camera prefab.
        public const float ClassicGravityFarPlane                    = 500f; // Rebuild main camera prefab.
        // roBrowser Camera.js indoor defaults: zoomFinal=125, angleFinal=[230,-40].
        public const float RobrowserIndoorCameraZoom                 = 125f;
        public const float RobrowserIndoorDistance                   = 56f;
        public const float RobrowserIndoorMinDistance                = 65f * 0.5f;
        public const float RobrowserIndoorMaxDistance                = 162.5f * 0.5f;
        public const float RobrowserIndoorYaw                        = 40f;
        public const float RobrowserIndoorYawMin                     = 20f;
        public const float RobrowserIndoorYawMax                     = 55f;
        public const float RobrowserIndoorPitch                      = 50f;
        public const float RobrowserIndoorPitchMin                   = 40f;
        public const float RobrowserIndoorPitchMax                   = 60f;

        [SerializeField] private Vector3 target = Vector3.zero;
        [SerializeField] private Transform followTarget;
        [SerializeField] private Vector3 targetOffset = Vector3.zero;
        [SerializeField] private bool orthographic = false;
        [SerializeField] private float orthographicSize = 12f;
        [SerializeField] private float distance = ClassicGravityDistance;
        [SerializeField] private float yaw = ClassicGravityYaw;
        [SerializeField] private float pitch = ClassicGravityPitch;
        [SerializeField] private float panSpeed = 1.2f;
        [SerializeField] private float rotateDegreesPerScreenWidth = ClassicGravityRotateDegreesPerScreenWidth;
        [SerializeField] private float rightDragTiltDegreesPerScreenHeight = ClassicGravityRightDragTiltDegreesPerScreenHeight;
        [SerializeField] private float rightDragZoomDistancePerPixel = ClassicGravityRightDragZoomDistancePerPixel;
        [SerializeField] private float zoomSpeed = 3.5f;
        [SerializeField] private float perspectiveZoomSpeed = ClassicGravityZoomStep;
        [SerializeField] private float perspectiveZoomSmoothing = ClassicGravityZoomSmoothing;
        [SerializeField] private float perspectiveTiltSpeed = ClassicGravityTiltStep;
        [SerializeField] private float minOrthographicSize = 6f;
        [SerializeField] private float maxOrthographicSize = 44f;
        [SerializeField] private float minDistance  = ClassicGravityMinDistance;
        [SerializeField] private float maxDistance  = ClassicGravityMaxDistance;
        [SerializeField] private float minPitch = ClassicGravityMinPitch;
        [SerializeField] private float maxPitch = ClassicGravityMaxPitch;
        [SerializeField] private float fieldOfView = ClassicGravityFieldOfView;
        [SerializeField] private bool cameraFollowSmoothingEnabled = true;
        [SerializeField] private float cameraFollowSmoothing = ClassicGravityFollowSmoothing;
        [SerializeField] private float cameraHeightSmoothing = ClassicGravityHeightSmoothing;
        [SerializeField] private float cameraFollowSnapDistance = ClassicGravityFollowSnapDistance;
        [SerializeField] private float cameraYawSmoothing = ClassicGravityYawSmoothing;

        private UnityEngine.Camera controlledCamera;
        private Vector3 smoothedFollowTarget;
        private bool hasSmoothedFollowTarget;
        private float targetYaw = ClassicGravityYaw;
        private float targetDistance = ClassicGravityDistance;
        private Vector2 previousMousePosition;
        private float lastRightClickTime = -100f;
        private Vector2 lastRightClickPosition;
        private bool indoorMode = false;
        private bool yawClamped = false;
        private float yawMin = 0f;
        private float yawMax = 360f;

        public static Func<Vector2, bool> PointerOverUiResolver { get; set; }

        /// <summary>
        /// /camera ([0x0153a9a4], registry g_isFixedCamera): CView::Update 0xa093a0 eases the look-at a tenth of the way to
        /// the player every tick while it is off, and puts it on the player while it is on. Set by the game.
        /// </summary>
        public static bool FixedFollow { get; set; }

        /// <summary>
        /// /q1 ([0x01552d58]): the right press in the world (CGameMode 0xc07540, button state 1) uses the shortcut slot of
        /// F9, and the double right click no longer resets the rotation (state 4; the Shift / Ctrl resets stay). Set by
        /// the game.
        /// </summary>
        public static bool QuickSpellRightClick { get; set; }

        /// <summary>/q2 ([0x01552d5c]): the wheel (0xc0b640) uses the slot of F7 (down) or F8 (up) instead of zooming.</summary>
        public static bool QuickSpellWheel { get; set; }

        /// <summary>Uses a shortcut slot (hotkey window msg 0x29: 6 = F7, 7 = F8, 8 = F9); set by the game.</summary>
        public static Action<int> UseQuickSpellSlot { get; set; }

        public RoActorDirection Direction => RoSpriteActionMap.GetCameraDirectionFromYaw(yaw);
        public float Distance => distance;
        public float TargetDistance => targetDistance;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float YawDegrees => yaw;

        /// <summary>Diagnostics (!spin): turns the camera by the given angle at once, without easing.</summary>
        public void TurnForDiagnostics(float degrees) {
            yaw = Mathf.Repeat(yaw + degrees, 360f);
            targetYaw = yaw;
        }
        public float PitchDegrees => pitch;
        public float FieldOfView => fieldOfView;
        public Vector3 TargetOffset => targetOffset;
        public bool IsIndoorMode => indoorMode;

        public void ApplyClassicGravityPreset() {
            targetOffset = Vector3.zero;
            orthographic = false;
            indoorMode = false;
            yawClamped = false;
            yawMin = 0f;
            yawMax = 360f;
            minDistance = ClassicGravityMinDistance;
            maxDistance = ClassicGravityMaxDistance;
            minPitch = ClassicGravityMinPitch;
            maxPitch = ClassicGravityMaxPitch;
            perspectiveZoomSpeed = ClassicGravityZoomStep;
            perspectiveZoomSmoothing = ClassicGravityZoomSmoothing;
            perspectiveTiltSpeed = ClassicGravityTiltStep;
            rotateDegreesPerScreenWidth = ClassicGravityRotateDegreesPerScreenWidth;
            rightDragTiltDegreesPerScreenHeight = ClassicGravityRightDragTiltDegreesPerScreenHeight;
            rightDragZoomDistancePerPixel = ClassicGravityRightDragZoomDistancePerPixel;
            ApplyClassicClipPlanes();
            ResetClassicView();
        }

        public void ResetClassicView() {
            targetDistance = Mathf.Clamp(ClassicGravityDistance, minDistance, maxDistance);
            distance = targetDistance;
            yaw = ClassicGravityYaw;
            targetYaw = yaw;
            pitch = Mathf.Clamp(ClassicGravityPitch, minPitch, maxPitch);
            fieldOfView = ClassicGravityFieldOfView;
            ApplyTransform();
        }

        public void Follow(Transform targetTransform, float initialDistance) {
            followTarget = targetTransform;
            if (followTarget != null) {
                target = followTarget.position + targetOffset;
                smoothedFollowTarget = target;
                hasSmoothedFollowTarget = true; // snap on attach; never drive the camera in from the old look-at
            }

            yaw = ClassicGravityYaw;
            targetYaw = yaw;
            pitch = ClassicGravityPitch;
            targetDistance = Mathf.Clamp(initialDistance, minDistance, maxDistance);
            distance = targetDistance;
            ApplyTransform();
        }

        /// <summary>
        /// Forces the follow look-at onto the target immediately (no smoothing) on the next frame.
        /// Call from teleport / map change / respawn so the camera never eases across the world. The
        /// large-jump auto-snap in <see cref="ResolveSmoothedFollowTarget"/> covers most of these, but
        /// an explicit call is deterministic regardless of distance.
        /// </summary>
        public void SnapFollowTarget() {
            hasSmoothedFollowTarget = false;
        }

        public void FrameBounds(Bounds bounds) {
            target = bounds.center;
            yaw = ClassicGravityYaw;
            targetYaw = yaw;
            pitch = ClassicGravityPitch;
            targetDistance = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.65f, minDistance, maxDistance);
            distance = targetDistance;
            ApplyTransform();
        }

        public void ApplyRobrowserIndoorPreset() {
            indoorMode = true;
            yawClamped = true;
            yawMin = RobrowserIndoorYawMin;
            yawMax = RobrowserIndoorYawMax;
            minDistance = RobrowserIndoorMinDistance;
            maxDistance = RobrowserIndoorMaxDistance;
            minPitch = RobrowserIndoorPitchMin;
            maxPitch = RobrowserIndoorPitchMax;
            targetDistance = Mathf.Clamp(RobrowserIndoorDistance, minDistance, maxDistance);
            distance = targetDistance;
            yaw = Mathf.Clamp(RobrowserIndoorYaw, yawMin, yawMax);
            targetYaw = yaw;
            pitch = Mathf.Clamp(RobrowserIndoorPitch, minPitch, maxPitch);
            fieldOfView = ClassicGravityFieldOfView;
            ApplyClassicClipPlanes();
            ApplyTransform();
        }

        /// <summary>
        /// Applies per-map camera constraints from viewpointtable.txt.
        /// Overrides zoom range, pitch range, and yaw range. Sets initial values from the preset.
        /// Distance values converted from kRO world units (÷5) to normalized/Unity units.
        /// </summary>
        public void ApplyViewpointPreset(ROIO.Models.RoViewpointPreset preset) {
            minDistance = preset.MinDistanceNormalized;
            maxDistance = preset.MaxDistanceNormalized;
            minPitch    = preset.MinPitchDegrees;
            maxPitch    = preset.MaxPitchDegrees;

            // Yaw clamping: only active when rotationFrom != rotationTo
            if (preset.RotationFrom != preset.RotationTo) {
                yawClamped = true;
                yawMin = preset.RotationFrom;
                yawMax = preset.RotationTo;
            } else {
                yawClamped = false;
                yawMin = 0f;
                yawMax = 360f;
            }

            // Apply initial values
            float initialDist = preset.InitialDistanceNormalized;
            targetDistance = Mathf.Clamp(initialDist, minDistance, maxDistance);
            distance = targetDistance;
            yaw   = yawClamped ? Mathf.Clamp(preset.RotationIn, yawMin, yawMax) : preset.RotationIn;
            targetYaw = yaw;
            pitch = Mathf.Clamp(preset.InitialPitchDegrees, minPitch, maxPitch);
            ApplyViewpointClipPlanes();
            ApplyTransform();
        }

        /// <summary>
        /// Enables or disables indoor mode. In indoor mode, right-drag rotation is suppressed
        /// (maps in indoorRswTable.txt have a fixed camera orientation).
        /// </summary>
        public void SetIndoorMode(bool indoor) {
            indoorMode = indoor;
        }

        private void Awake() {
            controlledCamera = GetComponent<UnityEngine.Camera>();
            targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            distance = targetDistance;
            targetYaw = yaw;
            controlledCamera.orthographic = orthographic;
            controlledCamera.fieldOfView = fieldOfView;
            ApplyClassicClipPlanes();
            ApplyTransform();
        }

        private void LateUpdate() {
            long cameraLateUpdateStartTicks = RoRuntimePerfCounters.GetCaptureTimingStartTicks();
            try {
                if (followTarget != null) {
                    Vector3 desiredTarget = followTarget.position + targetOffset;
                    target = Application.isPlaying
                        ? ResolveSmoothedFollowTarget(
                            smoothedFollowTarget,
                            hasSmoothedFollowTarget,
                            desiredTarget,
                            Time.unscaledDeltaTime,
                            cameraFollowSmoothing,
                            cameraHeightSmoothing,
                            cameraFollowSnapDistance,
                            cameraFollowSmoothingEnabled && !FixedFollow)
                        : desiredTarget;
                    smoothedFollowTarget = target;
                    hasSmoothedFollowTarget = true;
                }

                HandleInput();
                SmoothCameraState();
                SmoothYaw();
                ApplyTransform();
            } finally {
                RoRuntimePerfCounters.AddCameraLateUpdateTicks(
                    RoRuntimePerfCounters.GetCaptureTimingElapsedTicks(cameraLateUpdateStartTicks));
            }
        }

        private void SmoothYaw() {
            if (!Application.isPlaying) {
                yaw = targetYaw;
                return;
            }

            yaw = SmoothAngle(yaw, targetYaw, Time.unscaledDeltaTime, cameraYawSmoothing);
        }

        /// <summary>
        /// FPS-independent exponential easing of an angle (degrees) toward a target along the shortest
        /// path. Used for smoothed camera yaw (right-drag rotation) — Rebuild's CameraFollower eases
        /// rotation the same way (LerpAngle 7.5*dt), here made frame-rate independent like SmoothValue.
        /// </summary>
        public static float SmoothAngle(float current, float target, float deltaTime, float smoothing) {
            if (deltaTime <= 0f || smoothing <= 0f) {
                return NormalizeYaw(target);
            }

            float factor = 1f - Mathf.Exp(-smoothing * deltaTime);
            return NormalizeYaw(Mathf.LerpAngle(current, target, factor));
        }

        /// <summary>
        /// FPS-independent exponential follow smoothing (MVT-17). Eases the camera look-at toward the
        /// player so per-frame sprite jitter does not shake the whole screen, while a large horizontal
        /// jump (teleport / warp / respawn / big knockback) snaps instead of driving across the map.
        /// Horizontal (XZ) and vertical (Y) use independent factors; Y softens GAT height steps on slopes.
        /// Exponential smoothing is exactly composable over sub-steps, so the result is frame-rate independent.
        /// </summary>
        public static Vector3 ResolveSmoothedFollowTarget(
            Vector3 currentTarget,
            bool hasCurrentTarget,
            Vector3 desiredTarget,
            float deltaSeconds,
            float followSmoothing,
            float heightSmoothing,
            float snapDistance,
            bool smoothingEnabled
        ) {
            if (!smoothingEnabled || !hasCurrentTarget || deltaSeconds <= 0f) {
                return desiredTarget;
            }

            float dx = desiredTarget.x - currentTarget.x;
            float dz = desiredTarget.z - currentTarget.z;
            float horizontalDelta = Mathf.Sqrt(dx * dx + dz * dz);
            if (snapDistance > 0f && horizontalDelta > snapDistance) {
                return desiredTarget;
            }

            return new Vector3(
                SmoothValue(currentTarget.x, desiredTarget.x, deltaSeconds, followSmoothing),
                SmoothValue(currentTarget.y, desiredTarget.y, deltaSeconds, heightSmoothing),
                SmoothValue(currentTarget.z, desiredTarget.z, deltaSeconds, followSmoothing));
        }

        private void HandleInput() {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            Vector2 mousePosition = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            float scroll = mouse?.scroll.ReadValue().y ?? 0f;
            float normalizedScroll = NormalizeMouseWheelDelta(scroll);
            if (Mathf.Abs(normalizedScroll) > 0.01f && !IsPointerOverUi(mousePosition)) {
                if (!orthographic && IsTiltModifierPressed(keyboard)) {
                    pitch = CalculatePitchFromTiltScroll(pitch, normalizedScroll, perspectiveTiltSpeed, minPitch, maxPitch);
                } else if (QuickSpellWheel && UseQuickSpellSlot != null) {
                    // /q2 (0xc0b640): delta < 1 → hotkey msg 0x29 slot 6 (F7), else slot 7 (F8); no zoom
                    UseQuickSpellSlot(normalizedScroll < 0f ? 6 : 7);
                } else if (orthographic) {
                    orthographicSize = Mathf.Clamp(orthographicSize - normalizedScroll * zoomSpeed, minOrthographicSize, maxOrthographicSize);
                } else {
                    targetDistance = CalculateZoomDistance(targetDistance, normalizedScroll, perspectiveZoomSpeed, minDistance, maxDistance);
                }
            }

            if (mouse != null) {
                if (mouse.rightButton.wasPressedThisFrame) {
                    if (IsDoubleRightClick(mousePosition)) {
                        ResetClassicViewForModifier(keyboard);
                    } else if (QuickSpellRightClick && UseQuickSpellSlot != null && !IsPointerOverUi(mousePosition)) {
                        // /q1 (0xc07540, state 1): hotkey msg 0x29 slot 8 (F9)
                        UseQuickSpellSlot(8);
                    }

                    lastRightClickTime = Time.unscaledTime;
                    lastRightClickPosition = mousePosition;
                    previousMousePosition = mousePosition;
                } else if (mouse.rightButton.isPressed) {
                    Vector2 delta = mousePosition - previousMousePosition;
                    float browserStyleVerticalDelta = -delta.y;
                    if (!orthographic && IsShiftPressed(keyboard)) {
                        pitch = CalculatePitchFromVerticalDrag(
                            pitch,
                            browserStyleVerticalDelta,
                            Screen.height,
                            rightDragTiltDegreesPerScreenHeight,
                            minPitch,
                            maxPitch
                        );
                    } else if (!orthographic && IsCtrlPressed(keyboard)) {
                        targetDistance = CalculateZoomDistanceFromBrowserVerticalDrag(
                            targetDistance,
                            browserStyleVerticalDelta,
                            rightDragZoomDistancePerPixel,
                            minDistance,
                            maxDistance
                        );
                    } else if (yawClamped) {
                        targetYaw = CalculateClampedYawFromHorizontalDrag(
                            targetYaw,
                            delta.x,
                            Screen.width,
                            rotateDegreesPerScreenWidth,
                            yawMin,
                            yawMax
                        );
                    } else if (!indoorMode) {
                        targetYaw = CalculateYawFromHorizontalDrag(targetYaw, delta.x, Screen.width, rotateDegreesPerScreenWidth);
                    }
                }

                previousMousePosition = mousePosition;
            }

            if (keyboard == null || followTarget != null) {
                return;
            }

            Vector3 pan = Vector3.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) {
                pan.x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) {
                pan.x += 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) {
                pan.z += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) {
                pan.z -= 1f;
            }

            if (pan.sqrMagnitude > 0f) {
                Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 right = yawRotation * Vector3.right;
                Vector3 forward = yawRotation * Vector3.forward;
                float panScale = orthographic ? orthographicSize : Mathf.Sqrt(distance);
                target += (right * pan.x + forward * pan.z) * panSpeed * panScale * Time.deltaTime;
            }
        }

        private void SmoothCameraState() {
            if (orthographic) {
                return;
            }

            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            if (!Application.isPlaying) {
                distance = targetDistance;
                return;
            }

            distance = SmoothValue(distance, targetDistance, Time.unscaledDeltaTime, perspectiveZoomSmoothing);
        }

        public static float NormalizeMouseWheelDelta(float scroll) {
            if (Mathf.Abs(scroll) <= 0.01f) {
                return 0f;
            }

            return Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
        }

        private static bool IsPointerOverUi(Vector2 screenPosition) {
            Func<Vector2, bool> resolver = PointerOverUiResolver;
            return resolver != null && resolver(screenPosition);
        }

        public static float CalculateZoomDistance(float currentDistance, float normalizedScroll, float step, float min, float max) {
            if (Mathf.Abs(normalizedScroll) <= 0.01f) {
                return Mathf.Clamp(currentDistance, min, max);
            }

            return Mathf.Clamp(currentDistance + normalizedScroll * step, min, max);
        }

        public static float CalculatePitchFromTiltScroll(float currentPitch, float normalizedScroll, float step, float min, float max) {
            if (Mathf.Abs(normalizedScroll) <= 0.01f) {
                return Mathf.Clamp(currentPitch, min, max);
            }

            return Mathf.Clamp(currentPitch + normalizedScroll * step, min, max);
        }

        public static float CalculatePitchFromVerticalDrag(float currentPitch, float dragPixelsY, float screenHeight, float degreesPerScreenHeight, float min, float max) {
            float height = Mathf.Max(1f, screenHeight);
            float pitchDelta = dragPixelsY / height * degreesPerScreenHeight;
            return Mathf.Clamp(currentPitch + pitchDelta, min, max);
        }

        public static float CalculateZoomDistanceFromVerticalDrag(float currentDistance, float dragPixelsY, float screenHeight, float distancePerScreenHeight, float min, float max) {
            float height = Mathf.Max(1f, screenHeight);
            float distanceDelta = dragPixelsY / height * distancePerScreenHeight;
            return Mathf.Clamp(currentDistance - distanceDelta, min, max);
        }

        public static float CalculateZoomDistanceFromBrowserVerticalDrag(float currentDistance, float browserDragPixelsY, float distancePerPixel, float min, float max) {
            return Mathf.Clamp(currentDistance - browserDragPixelsY * distancePerPixel, min, max);
        }

        public static float SmoothValue(float current, float target, float deltaTime, float smoothing) {
            if (deltaTime <= 0f || smoothing <= 0f) {
                return target;
            }

            float factor = 1f - Mathf.Exp(-smoothing * deltaTime);
            return Mathf.Lerp(current, target, factor);
        }

        public static float CalculateYawFromHorizontalDrag(float currentYaw, float dragPixels, float screenWidth, float degreesPerScreenWidth) {
            float width = Mathf.Max(1f, screenWidth);
            float yawDelta = dragPixels / width * degreesPerScreenWidth;
            return NormalizeYaw(currentYaw + yawDelta);
        }

        public static float CalculateClampedYawFromHorizontalDrag(float currentYaw, float dragPixels, float screenWidth, float degreesPerScreenWidth, float min, float max) {
            float width = Mathf.Max(1f, screenWidth);
            float yawDelta = dragPixels / width * degreesPerScreenWidth;
            return Mathf.Clamp(currentYaw + yawDelta, min, max);
        }

        private void ResetClassicViewForModifier(Keyboard keyboard) {
            bool shift = IsShiftPressed(keyboard);
            bool ctrl = IsCtrlPressed(keyboard);

            if (!shift && !ctrl) {
                // /q1 on (0xc07540 state 4): the rotation stays
                if (QuickSpellRightClick) return;
                // roBrowser: double right-click alone resets only yaw (rotation)
                yaw = indoorMode && yawClamped ? yawMax : ClassicGravityYaw;
                targetYaw = yaw;
            } else {
                if (shift) {
                    // roBrowser: +SHIFT resets pitch to default (angleFinal[0] = range = 240° → 60°)
                    float resetPitch = indoorMode ? RobrowserIndoorPitchMax : ClassicGravityPitch;
                    pitch = Mathf.Clamp(resetPitch, minPitch, maxPitch);
                }

                if (ctrl) {
                    // roBrowser: +CTRL resets zoom; indoor maps use Camera.js indoorZoom baseline.
                    float resetDistance = indoorMode ? RobrowserIndoorDistance : minDistance;
                    targetDistance = Mathf.Clamp(resetDistance, minDistance, maxDistance);
                    distance = targetDistance;
                }
            }

            fieldOfView = ClassicGravityFieldOfView;
            ApplyTransform();
        }

        private static float NormalizeYaw(float value) {
            value %= 360f;
            return value < 0f ? value + 360f : value;
        }

        private static bool IsCtrlPressed(Keyboard keyboard) {
            if (keyboard == null) {
                return false;
            }

            return keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        }

        private static bool IsShiftPressed(Keyboard keyboard) {
            if (keyboard == null) {
                return false;
            }

            return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }

        private static bool IsTiltModifierPressed(Keyboard keyboard) {
            return IsCtrlPressed(keyboard) && IsShiftPressed(keyboard);
        }

        private bool IsDoubleRightClick(Vector2 mousePosition) {
            return Time.unscaledTime - lastRightClickTime <= ClassicGravityDoubleRightClickSeconds
                && Vector2.Distance(mousePosition, lastRightClickPosition) <= ClassicGravityDoubleRightClickMaxPixels;
        }

        private void ApplyClassicClipPlanes() {
            ApplyClipPlanes(ClassicGravityFarPlane);
        }

        private void ApplyViewpointClipPlanes() {
            ApplyClipPlanes(Mathf.Max(ClassicGravityFarPlane, maxDistance * 1.5f));
        }

        private void ApplyClipPlanes(float farPlane) {
            if (controlledCamera == null) {
                controlledCamera = GetComponent<UnityEngine.Camera>();
            }

            if (controlledCamera == null) {
                return;
            }

            controlledCamera.nearClipPlane = ClassicGravityNearPlane;
            controlledCamera.farClipPlane = farPlane;
        }

        private void ApplyTransform() {
            if (controlledCamera == null) {
                controlledCamera = GetComponent<UnityEngine.Camera>();
            }

            controlledCamera.orthographic = orthographic;
            controlledCamera.fieldOfView = fieldOfView;
            if (orthographic) {
                controlledCamera.orthographicSize = orthographicSize;
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target - rotation * Vector3.forward * distance;
            transform.LookAt(target + Vector3.up, Vector3.up);
            if (RoCameraQuake.Active) {
                transform.position += RoCameraQuake.Sample(transform);   // the classic engine's screen quake (msg 0x7a)
            }
            controlledCamera.transparencySortMode = TransparencySortMode.CustomAxis;
            controlledCamera.transparencySortAxis = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        }
    }
}
