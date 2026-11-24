using Ragnarok.Sprites;
using UnityEngine;

namespace Ragnarok.World {
    [CreateAssetMenu(fileName = "RoMovementTuning", menuName = "Ragnarok/Movement Tuning")]
    public sealed class RoMovementTuning : ScriptableObject {
        // RO walk packets carry up to 32 path steps. rAthena/Hercules/eAthena and roBrowser use the
        // same MAX_WALKPATH value, so this stays fixed unless the packet decoder proves otherwise.
        public const int DefaultMaxWalkPathSteps = RoLiveMoveRoute.MaxWalkPathSteps;
        // Original-client baseline: 150 ms per straight cell before speed modifiers.
        public const float DefaultStraightStepMsBaseline = RoLiveMoveRoute.DefaultStraightStepMilliseconds;
        // Classic RO cell costs are 10 straight / 14 diagonal, intentionally not sqrt(2).
        public const int DefaultDiagonalCost = RoLiveMoveRoute.DiagonalStepCost;
        // Presentation smoothing windows: tolerant enough for normal RTT jitter, short enough to avoid
        // a visibly trailing visual. These are exercised by RoMovePresentation correction tests.
        public const float DefaultLeadToleranceMs = RoMovePresentation.DefaultLeadToleranceSeconds * 1000f;
        public const float DefaultCatchupMs = RoMovePresentation.DefaultCatchUpSeconds * 1000f;
        public const float DefaultSoftCorrectionMs = RoMovePresentation.DefaultSoftCorrectionSeconds * 1000f;
        // Normal STOPMOVE/action-interrupt uses a 50-100 ms settle; 70 ms is the parity default.
        public const float DefaultStopSettleMs = 70f;
        // Above this distance we treat the event as teleport/map-load/respawn/large desync and hard snap.
        public const float DefaultHardSnapCells = RoLiveMoveRoute.HardSnapDistanceCells;
        // roBrowser/Rebuild distance-driven walk phase:
        // frame = floor(distCells * 5 * (0.37 * 4) * 24 / max(24, actDelayMs)) % motionCount (game.exe 0x00bd4a20).
        public const float DefaultWalkUnitsPerCell = RoActSpriteAnimator.RagnarokWalkUnitsPerCell;
        public const float DefaultWalkPhaseScale = RoActSpriteAnimator.RagnarokWalkDistancePhaseScale;
        // Remote actors may fast-forward at most one already elapsed step on first presentation; further
        // correction happens through the same smoothing path instead of a visible teleport.
        public const int DefaultRemoteInitialFastForwardMaxSteps = 1;
        // Local server-gated visual may chase slightly faster than walk speed so it reaches the server
        // cell without a late snap, while still reading as walking.
        public const float DefaultServerGatedCorrectionMaxWalkSpeedFactor = 1.15f;
        // Server clock slew is intentionally slow: normal RTT jitter must not move presentation time back
        // and forth enough to show in dist(t).
        public const float DefaultClockSlewMaxMsPerMs = 0.04f;
        // Walk animation is distance-driven, so in-view throttling must not skip phase updates.
        public const bool DefaultDisableWalkAnimatorThrottleInView = true;
        // Extra margin (degrees) the walk heading must leave the current 8-way sector before the rendered
        // facing flips. Stops a slightly off-axis walk (heading hovering on e.g. the South/SouthWest
        // boundary) from flip-flopping the sprite facing every frame/re-route; original RO reads steady.
        // 0 disables hysteresis.
        public const float DefaultWalkFacingHysteresisDegrees = 12f;

        private static RoMovementTuning runtimeDefault;

        [SerializeField, Min(1)] private int maxWalkPathSteps = DefaultMaxWalkPathSteps;
        [SerializeField, Min(1f)] private float straightStepMsBaseline = DefaultStraightStepMsBaseline;
        [SerializeField, Min(1)] private int diagonalCost = DefaultDiagonalCost;
        [SerializeField, Min(0f)] private float leadToleranceMs = DefaultLeadToleranceMs;
        [SerializeField, Min(0.001f)] private float catchupMs = DefaultCatchupMs;
        [SerializeField, Min(0.001f)] private float softCorrectionMs = DefaultSoftCorrectionMs;
        [SerializeField, Min(0f)] private float stopSettleMs = DefaultStopSettleMs;
        [SerializeField, Min(0f)] private float hardSnapCells = DefaultHardSnapCells;
        [SerializeField, Min(0.001f)] private float walkUnitsPerCell = DefaultWalkUnitsPerCell;
        [SerializeField, Min(0.001f)] private float walkPhaseScale = DefaultWalkPhaseScale;
        [SerializeField, Min(0)] private int remoteInitialFastForwardMaxSteps = DefaultRemoteInitialFastForwardMaxSteps;
        [SerializeField, Min(0.001f)] private float serverGatedCorrectionMaxWalkSpeedFactor = DefaultServerGatedCorrectionMaxWalkSpeedFactor;
        [SerializeField, Min(0f)] private float clockSlewMaxMsPerMs = DefaultClockSlewMaxMsPerMs;
        [SerializeField] private bool disableWalkAnimatorThrottleInView = DefaultDisableWalkAnimatorThrottleInView;
        [SerializeField, Min(0f)] private float walkFacingHysteresisDegrees = DefaultWalkFacingHysteresisDegrees;

        public static RoMovementTuning Default {
            get {
                if (runtimeDefault == null) {
                    runtimeDefault = CreateInstance<RoMovementTuning>();
                    runtimeDefault.name = "RO Movement Tuning Runtime Default";
                    runtimeDefault.hideFlags = HideFlags.HideAndDontSave;
                }

                return runtimeDefault;
            }
        }

        public int MaxWalkPathSteps {
            get => Mathf.Max(1, maxWalkPathSteps);
            set => maxWalkPathSteps = Mathf.Max(1, value);
        }

        public float StraightStepMsBaseline {
            get => Mathf.Max(1f, straightStepMsBaseline);
            set => straightStepMsBaseline = Mathf.Max(1f, value);
        }

        public int DiagonalCost {
            get => Mathf.Max(1, diagonalCost);
            set => diagonalCost = Mathf.Max(1, value);
        }

        public float LeadToleranceMs {
            get => Mathf.Max(0f, leadToleranceMs);
            set => leadToleranceMs = Mathf.Max(0f, value);
        }

        public float CatchupMs {
            get => Mathf.Max(0.001f, catchupMs);
            set => catchupMs = Mathf.Max(0.001f, value);
        }

        public float SoftCorrectionMs {
            get => Mathf.Max(0.001f, softCorrectionMs);
            set => softCorrectionMs = Mathf.Max(0.001f, value);
        }

        public float StopSettleMs {
            get => Mathf.Max(0f, stopSettleMs);
            set => stopSettleMs = Mathf.Max(0f, value);
        }

        public float HardSnapCells {
            get => Mathf.Max(0f, hardSnapCells);
            set => hardSnapCells = Mathf.Max(0f, value);
        }

        public float WalkUnitsPerCell {
            get => Mathf.Max(0.001f, walkUnitsPerCell);
            set => walkUnitsPerCell = Mathf.Max(0.001f, value);
        }

        public float WalkPhaseScale {
            get => Mathf.Max(0.001f, walkPhaseScale);
            set => walkPhaseScale = Mathf.Max(0.001f, value);
        }

        public int RemoteInitialFastForwardMaxSteps {
            get => Mathf.Max(0, remoteInitialFastForwardMaxSteps);
            set => remoteInitialFastForwardMaxSteps = Mathf.Max(0, value);
        }

        public float ServerGatedCorrectionMaxWalkSpeedFactor {
            get => Mathf.Max(0.001f, serverGatedCorrectionMaxWalkSpeedFactor);
            set => serverGatedCorrectionMaxWalkSpeedFactor = Mathf.Max(0.001f, value);
        }

        public float ClockSlewMaxMsPerMs {
            get => Mathf.Max(0f, clockSlewMaxMsPerMs);
            set => clockSlewMaxMsPerMs = Mathf.Max(0f, value);
        }

        public bool DisableWalkAnimatorThrottleInView {
            get => disableWalkAnimatorThrottleInView;
            set => disableWalkAnimatorThrottleInView = value;
        }

        public float WalkFacingHysteresisDegrees {
            get => Mathf.Max(0f, walkFacingHysteresisDegrees);
            set => walkFacingHysteresisDegrees = Mathf.Max(0f, value);
        }

        public float LeadToleranceSeconds => LeadToleranceMs / 1000f;
        public float CatchupSeconds => CatchupMs / 1000f;
        public float SoftCorrectionSeconds => SoftCorrectionMs / 1000f;
        public float StopSettleSeconds => StopSettleMs / 1000f;
        public float DiagonalStepMultiplier => DiagonalCost / (float) RoLiveMoveRoute.StraightStepCost;

        public static RoMovementTuning Resolve(RoMovementTuning tuning) {
            return tuning != null ? tuning : Default;
        }

        public string ToTraceSummary() {
            return
                $"tuning=maxPath:{MaxWalkPathSteps} straightMs:{StraightStepMsBaseline:0.###} diagCost:{DiagonalCost} " +
                $"leadMs:{LeadToleranceMs:0.###} catchupMs:{CatchupMs:0.###} softMs:{SoftCorrectionMs:0.###} " +
                $"settleMs:{StopSettleMs:0.###} hardSnap:{HardSnapCells:0.###} walkUnits:{WalkUnitsPerCell:0.###} " +
                $"phaseScale:{WalkPhaseScale:0.###} fastForwardSteps:{RemoteInitialFastForwardMaxSteps} " +
                $"serverGatedCap:{ServerGatedCorrectionMaxWalkSpeedFactor:0.###} " +
                $"clockSlewMsPerMs:{ClockSlewMaxMsPerMs:0.###} disableWalkThrottle:{DisableWalkAnimatorThrottleInView} " +
                $"facingHysteresisDeg:{WalkFacingHysteresisDegrees:0.###}";
        }

        private void OnValidate() {
            MaxWalkPathSteps = maxWalkPathSteps;
            StraightStepMsBaseline = straightStepMsBaseline;
            DiagonalCost = diagonalCost;
            LeadToleranceMs = leadToleranceMs;
            CatchupMs = catchupMs;
            SoftCorrectionMs = softCorrectionMs;
            StopSettleMs = stopSettleMs;
            HardSnapCells = hardSnapCells;
            WalkUnitsPerCell = walkUnitsPerCell;
            WalkPhaseScale = walkPhaseScale;
            RemoteInitialFastForwardMaxSteps = remoteInitialFastForwardMaxSteps;
            ServerGatedCorrectionMaxWalkSpeedFactor = serverGatedCorrectionMaxWalkSpeedFactor;
            ClockSlewMaxMsPerMs = clockSlewMaxMsPerMs;
            WalkFacingHysteresisDegrees = walkFacingHysteresisDegrees;
        }
    }
}
