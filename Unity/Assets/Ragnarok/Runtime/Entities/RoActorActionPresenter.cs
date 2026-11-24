using Ragnarok.Sprites;
using Ragnarok.Network;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.Entities {
    /// <summary>
    /// Single source of truth for the actor action TIMING POLICY (attack / hit / cast / pickup / ...).
    ///
    /// Historically this logic was duplicated across the two parallel actor systems: the local player
    /// (<c>RoClickToMoveController</c>) and remote actors (<c>RoOnlineActorPresenter</c>). The two
    /// copies drifted (one was missing loop handling, they scaled differently, used different constants).
    /// That drift is what produced the high-ASPD "swing cut / Standby flash" bugs. Both systems now route
    /// their action-timing decisions through here so they cannot drift again.
    ///
    /// What lives here (shared, pure policy):
    ///   - how long a finite action should run (<see cref="ResolveDurationSeconds"/>),
    ///   - whether a motion loops (<see cref="ShouldLoop"/>),
    ///   - whether Attack2/Attack3 must fall back to Attack1 (<see cref="ShouldFallbackBasicAttackMotion"/>),
    ///   - fitting finite actions to a duration (<see cref="ApplyActionScaling"/>),
    ///   - player basic-attack frame pacing (<see cref="ApplyPlayerAttackFrameTiming"/>).
    ///
    /// What deliberately stays with each caller: actually driving the sprite (restart/bookkeeping) and the
    /// resting transition (Standby/Idle). The local player predicts on its own clock and the remote actor
    /// is driven by the server clock, so those mechanics are genuinely different; only the policy above
    /// is shared.
    /// </summary>
    public static class RoActorActionPresenter {
        public const float DefaultActionSeconds = 0.45f;
        public const float HitActionSeconds = 0.3f;
        public const float AttackFollowThroughHoldSeconds = 0.05f;

        /// <summary>
        /// Resolve how long a finite action should play. <paramref name="amotionSeconds"/> is the packet's
        /// attack-motion time (amotion); &lt;= 0 falls back to a per-motion default.
        /// </summary>
        public static float ResolveDurationSeconds(RoSpriteMotion motion, float amotionSeconds) {
            if (amotionSeconds > 0f) {
                return amotionSeconds;
            }

            return motion == RoSpriteMotion.Hit ? HitActionSeconds : DefaultActionSeconds;
        }

        public static float ResolveDurationSecondsFromMilliseconds(RoSpriteMotion motion, int amotionMilliseconds) {
            return ResolveDurationSeconds(motion, amotionMilliseconds > 0 ? amotionMilliseconds / 1000f : 0f);
        }

        /// <summary>Whether a motion loops (idle/walk/sit/standby) or plays once and holds (attack/hit/...).</summary>
        public static bool ShouldLoop(RoSpriteMotion motion) {
            return RoOnlineActorPresenter.ShouldLoopSpriteMotion(motion);
        }

        /// <summary>
        /// Fit the visual's current action so the whole thing plays in <paramref name="durationSeconds"/>.
        /// This is correct for finite packet-timed actions such as hit reactions; player basic attacks use
        /// <see cref="ApplyPlayerAttackFrameTiming"/> instead because roBrowser/Rebuild pace them by weapon
        /// motion units rather than by the total number of ACT frames.
        /// </summary>
        public static bool ApplyActionScaling(IRoActorActionVisual visual, float durationSeconds, float fallbackScale = 1f) {
            return visual != null &&
                durationSeconds > 0f &&
                visual.TryScaleCurrentActionsToDurationSeconds(durationSeconds, fallbackScale);
        }

        public static bool ApplyPlayerAttackFrameTiming(
            IRoActorActionVisual visual,
            RoOnlineActorSnapshot snapshot,
            float attackMotionSeconds) {
            if (visual == null ||
                !TryResolvePlayerAttackFrameSeconds(
                    snapshot.ObjectType,
                    snapshot.Job,
                    snapshot.Sex,
                    snapshot.Weapon,
                    attackMotionSeconds,
                    out float perFrameSeconds)) {
                return false;
            }

            visual.SetAbsoluteFrameSeconds(perFrameSeconds);
            return true;
        }

        public static bool TryResolvePlayerAttackFrameSeconds(
            RoMapActorObjectType objectType,
            short jobId,
            byte sex,
            uint weapon,
            float attackMotionSeconds,
            out float perFrameSeconds) {
            perFrameSeconds = 0f;
            if (objectType != RoMapActorObjectType.Player || attackMotionSeconds <= 0f) {
                return false;
            }

            double motionUnits = RoMapWeaponActionSemantics.ResolvePlayerAttackMotionUnits(jobId, sex, weapon);
            if (motionUnits <= 0d) {
                return false;
            }

            perFrameSeconds = Mathf.Max(0.0001f, attackMotionSeconds / (float) motionUnits);
            return true;
        }

        public static bool ShouldFallbackBasicAttackMotion(RoSpriteMotion motion, IRoActorActionVisual visual) {
            if (motion != RoSpriteMotion.Attack2 && motion != RoSpriteMotion.Attack3) {
                return false;
            }

            if (visual == null) {
                return false;
            }

            return !visual.TryGetCurrentActionBaseDurationSeconds(out float baseDurationSeconds) ||
                baseDurationSeconds <= 0f;
        }

    }
}
