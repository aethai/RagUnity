using Ragnarok.Network;
using Ragnarok.Sprites;
using Ragnarok.World;

namespace Ragnarok.Combat {
    /// <summary>How a motion plays (CGameActor::vf07 SetMotion 0xbe3ab0: play mode 0 loops, 1 plays once and holds).</summary>
    public enum RoMotionMode : byte {
        /// <summary>Not animated: the idle / sit head-direction frame, or the cast pose's frame 0.</summary>
        Frozen,
        Loop,
        /// <summary>Plays once, then holds its last frame until the step's length.</summary>
        Once
    }

    /// <summary>What an actor shows at a moment of its current world action.</summary>
    public readonly struct RoMotionStep {
        public RoMotionStep(RoSpriteMotion motion, RoMotionMode mode, float frameDelayScale = 1f, float lengthMs = 0f) {
            Motion = motion;
            Mode = mode;
            FrameDelayScale = frameDelayScale;
            LengthMs = lengthMs;
        }

        public RoSpriteMotion Motion { get; }
        public RoMotionMode Mode { get; }

        /// <summary>Multiplies the ACT delay of every frame (the one-shot speed factor, +0x64).</summary>
        public float FrameDelayScale { get; }

        /// <summary>For <see cref="RoMotionMode.Once"/>: how long the step lasts, held last frame included.</summary>
        public float LengthMs { get; }

        public bool Loops => Mode == RoMotionMode.Loop;
    }

    /// <summary>The inputs of <see cref="RoOriginalCombatRules.ActorMotion"/>.</summary>
    public struct RoMotionInput {
        public RoOnlineActorMotion WorldMotion;
        public bool IsPlayer;
        /// <summary>attackMT of the action (basic attack, ZC_NOTIFY_SKILL); 0 = none (plain ACT speed).</summary>
        public int AttackMotionMs;
        /// <summary>Hit (flinch): its length from <see cref="RoOriginalCombatRules.MotionLengthMs"/>.</summary>
        public int DurationMs;
        /// <summary>The player's weapon uses arrows: the attack speed factor has no cap (CPc 0xccca30).</summary>
        public bool PlayerWithBow;
        public float ElapsedMs;
        /// <summary>
        /// frames × ACT delay × 24 of the motion's action at speed 1, from the actor's own ACT; ≤ 0 when it is not
        /// known yet (the step then lasts until it is).
        /// </summary>
        public float AuthoredMs;
    }

    public static partial class RoOriginalCombatRules {
        /// <summary>
        /// The original client's actor state machine (reference packages/actormotion §2, §4, §7; combat-timing §1.3,
        /// §2.3): the motion, how it plays and its speed at <see cref="RoMotionInput.ElapsedMs"/> into the actor's world
        /// action. The local player animates exactly like every other player (CPlayer's state tick is CPc's).
        /// <list type="bullet">
        /// <item>Idle and sit are frozen on the head-direction frame for players; monsters loop their idle.</item>
        /// <item>Attacks play once at ACT delay × min(attackMT, 432) / 432 (bows uncapped), then players go to the
        /// looping ready pose after motion 2 (Attack2, ACT action 10) and to idle after motion 9; monsters to idle.</item>
        /// <item>The flinch lasts its length: compressed when shorter than the action, else played at speed 1 and held
        /// on its last frame; then the ready pose (players) or idle.</item>
        /// <item>The cast pose holds frame 0 until another action replaces it.</item>
        /// <item>Pick up and skill actions play once (skill actions at the attackMT speed when the packet has one), then
        /// idle.</item>
        /// </list>
        /// </summary>
        public static RoMotionStep ActorMotion(in RoMotionInput input) {
            switch (input.WorldMotion) {
                case RoOnlineActorMotion.Dead:
                    return new RoMotionStep(RoSpriteMotion.Dead, RoMotionMode.Once);
                case RoOnlineActorMotion.Moving:
                    return new RoMotionStep(RoSpriteMotion.Walk, RoMotionMode.Loop);
                case RoOnlineActorMotion.Sitting:
                    return input.IsPlayer ? new RoMotionStep(RoSpriteMotion.Sit, RoMotionMode.Frozen) : Rest(input.IsPlayer);
                case RoOnlineActorMotion.Standby:
                    return input.IsPlayer ? Ready : Rest(false);
                case RoOnlineActorMotion.Casting:
                    return input.IsPlayer ? new RoMotionStep(RoSpriteMotion.Casting, RoMotionMode.Frozen) : Rest(false);
                case RoOnlineActorMotion.Attack:
                case RoOnlineActorMotion.Attack2:
                case RoOnlineActorMotion.Attack3:
                case RoOnlineActorMotion.Throwing: {
                    float factor = MotionSpeedFactor(EffectiveAttackMotion(input.AttackMotionMs), input.PlayerWithBow);
                    // players: motion 2 (Attack2) leads to the ready pose, motion 9 to idle (CPc::SetAction 0xcd0abc)
                    RoMotionStep after = input.IsPlayer && input.WorldMotion == RoOnlineActorMotion.Attack2 ? Ready : Rest(input.IsPlayer);
                    return Once(input, AttackSpriteMotion(input.WorldMotion), factor, input.AuthoredMs * factor, after);
                }
                case RoOnlineActorMotion.Skill: {
                    float factor = input.AttackMotionMs > 0 ? MotionSpeedFactor(input.AttackMotionMs, input.PlayerWithBow) : 1f;
                    RoSpriteMotion motion = input.IsPlayer ? RoSpriteMotion.Casting : RoSpriteMotion.Special;
                    return Once(input, motion, factor, input.AuthoredMs * factor, Rest(input.IsPlayer));
                }
                case RoOnlineActorMotion.Pickup:
                    return input.IsPlayer
                        ? Once(input, RoSpriteMotion.PickUp, 1f, input.AuthoredMs, Rest(true))
                        : Rest(false);
                case RoOnlineActorMotion.Hit: {
                    // the length is the original's (frames × fd × 24 × hold): squeeze the action into it when it is
                    // shorter, else play it at speed 1 and hold the last frame
                    float length = input.DurationMs > 0 ? input.DurationMs : input.AuthoredMs;
                    float scale = input.AuthoredMs > 0f && length > 0f && length < input.AuthoredMs ? length / input.AuthoredMs : 1f;
                    return Once(input, RoSpriteMotion.Hit, scale, length, input.IsPlayer ? Ready : Rest(false));
                }
                default:
                    return Rest(input.IsPlayer);
            }
        }

        static readonly RoMotionStep Ready = new RoMotionStep(RoSpriteMotion.Standby, RoMotionMode.Loop);

        /// <summary>Idle: frozen on the head direction for players, a looping idle for everyone else.</summary>
        static RoMotionStep Rest(bool player) =>
            new RoMotionStep(RoSpriteMotion.Idle, player ? RoMotionMode.Frozen : RoMotionMode.Loop);

        static RoMotionStep Once(in RoMotionInput input, RoSpriteMotion motion, float scale, float lengthMs, RoMotionStep after) {
            // an unknown length (no ACT yet) keeps the action rather than cutting it
            if (lengthMs > 0f && input.ElapsedMs >= lengthMs) {
                return after;
            }

            return new RoMotionStep(motion, RoMotionMode.Once, scale, lengthMs);
        }

        static RoSpriteMotion AttackSpriteMotion(RoOnlineActorMotion motion) => motion switch {
            RoOnlineActorMotion.Attack2 => RoSpriteMotion.Attack2,
            RoOnlineActorMotion.Attack3 => RoSpriteMotion.Attack3,
            RoOnlineActorMotion.Throwing => RoSpriteMotion.Throwing,
            _ => RoSpriteMotion.Attack1
        };
    }
}
