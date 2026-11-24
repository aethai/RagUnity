using System;
using System.Threading.Tasks;
using Ragnarok.Data;
using Ragnarok.Input;
using Ragnarok.Maps;
using Ragnarok.Network;
using Ragnarok.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ragnarok.UI {
    [DisallowMultipleComponent]
    /// <summary>
    /// The skill / capture targeting state (the client's targeting mode): the pending skill and level, Esc or a right
    /// click cancels, the wheel changes a selectable level, an actor or ground click submits. The classic UI shows only
    /// the level digits by the cursor (RoClassicSkillLevel).
    /// </summary>
    public sealed class RoSkillTargetSelection : MonoBehaviour {
        private const string ObjectName = "RO SkillTargetSelection";
        public const string DefaultDescription = "Select a Target";
        private const float CursorControllerLookupRetrySeconds = 0.5f;



        private RoLoginShellSession session;
        private RoCursorStateController cursorStateController;
        private bool active;
        private int skillId;
        private int skillLevel;
        private bool canSelectLevel;
        private int maxSelectableLevel = 1;
        private string displayName = string.Empty;
        private RoSkillTargetType targetType = RoSkillTargetType.Unknown;
        private RoSkillTargetFlags targetFlags = RoSkillTargetFlags.None;
        private string targetCursorDiagnostic = string.Empty;
        private int targetCursorDiagnosticSkillId;
        private int targetCursorDiagnosticSkillLevel;
        private bool targetCursorDiagnosticValid;
        private float nextCursorControllerLookupTime;
        private Action<uint> captureTarget;

        /// <summary>
        /// ZC_START_CAPTURE (0x00c9a2d0: targeting state 2, token 0x2710): the next actor click goes to <paramref name="onActor"/>
        /// (the taming roulette), without a skill label; a ground click does nothing.
        /// </summary>
        public void BeginCapture(RoLoginShellSession loginSession, Action<uint> onActor) {
            Current = this;
            session = loginSession;
            captureTarget = onActor;
            skillId = 0;
            AttackRange = 0;
            skillLevel = 1;
            canSelectLevel = false;
            displayName = string.Empty;
            targetFlags = RoSkillTargetFlags.Enemy;
            targetType = RoSkillMetadata.ToLegacyTargetType(targetFlags);
            active = onActor != null;
            gameObject.SetActive(active);
            if (!active) {
                return;
            }

            ForceTargetCursor();
        }

        public bool CapturesTarget => active && captureTarget != null;

        public static RoSkillTargetSelection Current { get; private set; }
        public bool HasActiveSelection => active;
        public int SkillId => skillId;
        public int SkillLevel => skillLevel;
        public string DisplayName => displayName;
        public RoSkillTargetType TargetType => targetType;
        public RoSkillTargetFlags TargetFlags => targetFlags;
        public bool TargetsGround => IsGroundTarget(targetFlags);
        public bool TargetsEntity => !TargetsGround && (targetFlags & (RoSkillTargetFlags.Enemy | RoSkillTargetFlags.Friend | RoSkillTargetFlags.Pet | RoSkillTargetFlags.Homun | RoSkillTargetFlags.Trap)) != 0;

        /// <summary>
        /// The skill's range in cells (mode msg 0x48, 0xc1aff8: CGameMode+0x418 = the skill info's range, ZC_SKILLINFO_LIST);
        /// 0 when the skill list does not have it. Custom's /range draws it.
        /// </summary>
        public int AttackRange { get; private set; }

        private static int ResolveAttackRange(RoLoginShellSession loginSession, int id) {
            if (loginSession == null || id <= 0) return 0;
            foreach (Ragnarok.Gameplay.RoGameplaySkillInfo skill in loginSession.SkillSnapshot().Skills) {
                if (skill.SkillId == id) return skill.AttackRange;
            }

            return 0;
        }

        public static RoSkillTargetSelection EnsureInScene() {
            if (Current != null) {
                return Current;
            }

            RoSkillTargetSelection existing =
                FindFirstObjectByType<RoSkillTargetSelection>(FindObjectsInactive.Include);
            if (existing != null) {
                Current = existing;
                return existing;
            }

            var overlayObject = new GameObject(ObjectName);
            var overlay = overlayObject.AddComponent<RoSkillTargetSelection>();
            Current = overlay;
            return overlay;
        }

        public void Begin(
            RoLoginShellSession loginSession,
            int selectedSkillId,
            int selectedSkillLevel,
            string selectedDisplayName,
            RoSkillTargetType selectedTargetType
        ) {
            Begin(
                loginSession,
                selectedSkillId,
                selectedSkillLevel,
                selectedDisplayName,
                RoSkillMetadata.ToTargetFlags(selectedTargetType),
                false,
                selectedSkillLevel);
        }

        public void Begin(
            RoLoginShellSession loginSession,
            int selectedSkillId,
            int selectedSkillLevel,
            string selectedDisplayName,
            RoSkillTargetFlags selectedTargetFlags,
            bool selectableLevel,
            int selectableMaxLevel
        ) {
            Current = this;
            session = loginSession;
            skillId = Math.Max(0, selectedSkillId);
            skillLevel = Math.Max(1, selectedSkillLevel);
            canSelectLevel = selectableLevel;
            maxSelectableLevel = Math.Max(1, selectableMaxLevel);
            displayName = string.IsNullOrWhiteSpace(selectedDisplayName)
                ? "Skill " + skillId.ToString()
                : selectedDisplayName.Trim();
            targetFlags = selectedTargetFlags;
            targetType = RoSkillMetadata.ToLegacyTargetType(selectedTargetFlags);
            AttackRange = ResolveAttackRange(loginSession, skillId);
            captureTarget = null;
            active = skillId > 0;
            gameObject.SetActive(active);
            if (!active) {
                return;
            }

            ForceTargetCursor();
        }

        public void Cancel() {
            if (!active && gameObject != null && !gameObject.activeSelf) {
                return;
            }

            active = false;
            skillId = 0;
            skillLevel = 0;
            captureTarget = null;
            canSelectLevel = false;
            maxSelectableLevel = 1;
            displayName = string.Empty;
            targetType = RoSkillTargetType.Unknown;
            targetFlags = RoSkillTargetFlags.None;
            if (gameObject != null) {
                gameObject.SetActive(false);
            }

            ResetTargetCursor();
            InvalidateTargetCursorDiagnostic();
        }

        public bool TrySubmitEntityTarget(RoPointerTarget pointerTarget, RoPointerModifierFlags modifiers) {
            if (CapturesTarget && pointerTarget.GameObject != null) {
                // mode 0x71 / 0x45 with 0x2710: any actor but oneself opens the roulette (the server judges the target)
                RoOnlinePointerTargetMarker picked = pointerTarget.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>();
                if (picked == null || picked.ServerId == 0) {
                    return false;
                }

                Action<uint> onActor = captureTarget;
                Cancel();
                onActor(picked.ServerId);
                return true;
            }

            if (!active || !TargetsEntity || pointerTarget.GameObject == null) {
                return false;
            }

            RoOnlinePointerTargetMarker marker =
                pointerTarget.GameObject.GetComponentInParent<RoOnlinePointerTargetMarker>();
            if (marker == null || marker.ServerId == 0 || marker.ServerId > int.MaxValue) {
                return false;
            }

            bool hostilePlayer = marker.ObjectType == RoMapActorObjectType.Player &&
                marker.TryGetActor(out RoOnlineActorSnapshot pickedActor) &&
                RoPickHostility.Attacks(pickedActor, modifiers.HasFlag(RoPointerModifierFlags.Shift));
            if (!CanTargetEntity(targetFlags, pointerTarget.Kind, marker.ObjectType, modifiers, hostilePlayer)) {
                return false;
            }

            int targetAid = unchecked((int) marker.ServerId);
            int selectedSkillId = skillId;
            int selectedLevel = Math.Max(1, skillLevel);
            Cancel();
            if (TrySubmitTargetSkillThroughMap(selectedSkillId, selectedLevel, targetAid)) {
                return true;
            }

            _ = SendSkillToTargetAsync(selectedSkillId, selectedLevel, targetAid);
            return true;
        }

        public bool TrySubmitGroundTarget(Vector2Int cell) {
            if (CapturesTarget) {
                return true;
            }

            if (!active || !TargetsGround) {
                return false;
            }

            int selectedSkillId = skillId;
            int selectedLevel = Math.Max(1, skillLevel);
            Cancel();
            _ = SendSkillToGroundAsync(selectedSkillId, selectedLevel, cell.x, cell.y);
            return true;
        }

        public static bool IsGroundTarget(RoSkillTargetType value) {
            return value == RoSkillTargetType.Ground || value == RoSkillTargetType.Trap;
        }

        public static bool IsGroundTarget(RoSkillTargetFlags value) {
            return (value & (RoSkillTargetFlags.Place | RoSkillTargetFlags.Trap)) != 0;
        }

        public static bool CanTargetEntity(
            RoSkillTargetType skillTargetType,
            RoPointerTargetKind pointerKind,
            RoMapActorObjectType objectType,
            RoPointerModifierFlags modifiers
        ) {
            return CanTargetEntity(RoSkillMetadata.ToTargetFlags(skillTargetType), pointerKind, objectType, modifiers);
        }

        /// <param name="hostilePlayer">The player is an enemy without Shift on this map (<see cref="RoPickHostility.Attacks"/>:
        /// PK / siege / battlefield / party and guild properties, FUN_00c05710).</param>
        public static bool CanTargetEntity(
            RoSkillTargetFlags skillTargetFlags,
            RoPointerTargetKind pointerKind,
            RoMapActorObjectType objectType,
            RoPointerModifierFlags modifiers,
            bool hostilePlayer = false
        ) {
            if (pointerKind == RoPointerTargetKind.Item ||
                pointerKind == RoPointerTargetKind.Npc ||
                pointerKind == RoPointerTargetKind.Portal ||
                pointerKind == RoPointerTargetKind.GroundCell ||
                pointerKind == RoPointerTargetKind.None) {
                return false;
            }

            if ((skillTargetFlags & RoSkillTargetFlags.Enemy) != 0 &&
                (pointerKind == RoPointerTargetKind.Monster ||
                 (pointerKind == RoPointerTargetKind.Player && (modifiers.HasFlag(RoPointerModifierFlags.Shift) || hostilePlayer)))) {
                return true;
            }

            if ((skillTargetFlags & RoSkillTargetFlags.Friend) != 0 &&
                (pointerKind == RoPointerTargetKind.Player ||
                 objectType == RoMapActorObjectType.Homunculus ||
                 objectType == RoMapActorObjectType.Mercenary ||
                 objectType == RoMapActorObjectType.Elemental)) {
                return true;
            }

            if ((skillTargetFlags & RoSkillTargetFlags.Pet) != 0 &&
                (objectType == RoMapActorObjectType.Pet || pointerKind == RoPointerTargetKind.Monster)) {
                return true;
            }

            if ((skillTargetFlags & RoSkillTargetFlags.Homun) != 0 &&
                objectType == RoMapActorObjectType.Homunculus) {
                return true;
            }

            return false;
        }

        private void Awake() {
            Current = this;
            Cancel();
        }

        private void OnDestroy() {
            if (ReferenceEquals(Current, this)) {
                Current = null;
            }
        }

        private void Update() {
            if (!active) {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                Cancel();
                return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) {
                Cancel();
                return;
            }

            if (canSelectLevel && Mouse.current != null) {
                float wheel = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) {
                    int delta = wheel > 0f ? 1 : -1;
                    skillLevel = Mathf.Clamp(skillLevel + delta, 1, Math.Max(1, maxSelectableLevel));
                    InvalidateTargetCursorDiagnostic();
                }
            }

            ForceTargetCursor();
        }

        private async Task SendSkillToTargetAsync(int selectedSkillId, int selectedLevel, int targetAid) {
            if (session == null) {
                return;
            }

            try {
                await session.BeginLegacyUseSkillOnTarget(selectedSkillId, selectedLevel, targetAid);
            } catch (Exception ex) {
                Debug.LogWarning($"Skill target request for {selectedSkillId} on {targetAid} was not sent: {ex.Message}");
            }
        }

        private static bool TrySubmitTargetSkillThroughMap(int selectedSkillId, int selectedLevel, int targetAid) {
            RoRuntimeMapController mapController = FindFirstObjectByType<RoRuntimeMapController>();
            return mapController != null &&
                mapController.TrySubmitLocalTargetSkillUse(selectedSkillId, selectedLevel, targetAid);
        }

        private async Task SendSkillToGroundAsync(int selectedSkillId, int selectedLevel, int x, int y) {
            if (session == null) {
                return;
            }

            // the Talkie Box / Graffiti ground click opens the text box instead (0xcd662b)
            if (RoClassicPrompts.TryTalkBox(selectedSkillId, selectedLevel, x, y)) return;
            try {
                await session.BeginLegacyUseSkillOnGround(selectedSkillId, selectedLevel, x, y);
            } catch (Exception ex) {
                Debug.LogWarning($"Ground skill request for {selectedSkillId} at {x},{y} was not sent: {ex.Message}");
            }
        }

        private void ForceTargetCursor() {
            if (!TryResolveCursorStateController(out RoCursorStateController controller)) {
                return;
            }

            if (targetCursorDiagnosticValid &&
                controller.CurrentState == RoCursorState.Target &&
                string.Equals(controller.LastDiagnostic, targetCursorDiagnostic, StringComparison.Ordinal)) {
                return;
            }

            controller.SetState(RoCursorState.Target, ResolveTargetCursorDiagnostic());
        }

        private void ResetTargetCursor() {
            if (!TryResolveCursorStateController(out RoCursorStateController controller, true)) {
                return;
            }

            controller.SetState(RoCursorState.Default, "skill-target-selection: canceled");
        }

        private bool TryResolveCursorStateController(out RoCursorStateController controller, bool force = false) {
            if (cursorStateController != null) {
                controller = cursorStateController;
                return true;
            }

            if (!force && Application.isPlaying && Time.unscaledTime < nextCursorControllerLookupTime) {
                controller = null;
                return false;
            }

            cursorStateController = FindFirstObjectByType<RoCursorStateController>();
            if (cursorStateController == null && Application.isPlaying) {
                nextCursorControllerLookupTime = Time.unscaledTime + CursorControllerLookupRetrySeconds;
            } else {
                nextCursorControllerLookupTime = 0f;
            }

            controller = cursorStateController;
            return controller != null;
        }

        private string ResolveTargetCursorDiagnostic() {
            if (targetCursorDiagnosticValid &&
                targetCursorDiagnosticSkillId == skillId &&
                targetCursorDiagnosticSkillLevel == skillLevel) {
                return targetCursorDiagnostic;
            }

            targetCursorDiagnostic = "skill-target-selection: skill=" + skillId.ToString() + " level=" + skillLevel.ToString();
            targetCursorDiagnosticSkillId = skillId;
            targetCursorDiagnosticSkillLevel = skillLevel;
            targetCursorDiagnosticValid = true;
            return targetCursorDiagnostic;
        }

        private void InvalidateTargetCursorDiagnostic() {
            targetCursorDiagnosticValid = false;
        }
    }
}
