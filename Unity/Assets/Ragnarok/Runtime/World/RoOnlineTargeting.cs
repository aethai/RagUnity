using Ragnarok.Input;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.World {
    public enum RoOnlineTargetInteraction {
        None = 0,
        Select = 1,
        Talk = 2,
        Attack = 3,
        Pickup = 4,
        EnterPortal = 5
    }

    public readonly struct RoOnlineTargetSelection {
        public RoOnlineTargetSelection(
            uint serverId,
            RoMapActorObjectType objectType,
            RoPointerTargetKind pointerKind,
            RoOnlineTargetInteraction interaction,
            Vector2Int cell,
            Vector3 worldPosition,
            string label,
            RoPointerModifierFlags modifiers,
            uint itemId,
            short itemCount,
            short job
        ) {
            ServerId = serverId;
            ObjectType = objectType;
            PointerKind = pointerKind;
            Interaction = interaction;
            Cell = cell;
            WorldPosition = worldPosition;
            Label = label ?? string.Empty;
            Modifiers = modifiers;
            ItemId = itemId;
            ItemCount = itemCount;
            Job = job;
        }

        public uint ServerId { get; }
        public RoMapActorObjectType ObjectType { get; }
        public RoPointerTargetKind PointerKind { get; }
        public RoOnlineTargetInteraction Interaction { get; }
        public Vector2Int Cell { get; }
        public Vector3 WorldPosition { get; }
        public string Label { get; }
        public RoPointerModifierFlags Modifiers { get; }
        public uint ItemId { get; }
        public short ItemCount { get; }
        public short Job { get; }
        public bool HasTarget => ServerId != 0;

        public static RoOnlineTargetSelection FromSnapshot(
            RoOnlineActorSnapshot actor,
            RoPointerTarget pointerTarget,
            RoPointerModifierFlags modifiers
        ) {
            RoPointerTargetKind pointerKind = pointerTarget.Kind == RoPointerTargetKind.None
                ? RoOnlinePointerTargetMarker.GetPointerTargetKind(actor.ObjectType)
                : pointerTarget.Kind;
            Vector2Int cell = RoOnlineActorPresenter.GetDisplayCell(actor);
            return new RoOnlineTargetSelection(
                actor.ServerId,
                actor.ObjectType,
                pointerKind,
                RoOnlinePointerTargetMarker.GetInteraction(actor, pointerKind, modifiers),
                cell,
                pointerTarget.WorldPosition,
                string.IsNullOrWhiteSpace(pointerTarget.Label) ? actor.Name : pointerTarget.Label,
                modifiers,
                actor.ItemId,
                actor.ItemCount,
                actor.Job);
        }

        public override string ToString() {
            return HasTarget
                ? $"target={ServerId} kind={PointerKind} object={ObjectType} action={Interaction} cell={Cell} label='{Label}'"
                : "target=none";
        }
    }

    /// <summary>What the pick rule asks the session (set by the map session, see <see cref="RoPickHostility.Relations"/>).</summary>
    public interface IRoPickRelations {
        /// <summary>FUN_00d1e3b0(aid, 1): an online member of the own party.</summary>
        bool IsOnlinePartyMember(uint accountId);

        /// <summary>The own guild id (FUN_00d1e2a0 compares an actor's guild with the own player actor's).</summary>
        uint PickOwnGuildId { get; }

        /// <summary>FUN_00a120b0: the guild is an ally (relation 0) of the own guild.</summary>
        bool IsAlliedGuild(uint guildId);

        /// <summary>The own homunculus or mercenary (the owner check of pick type 4, actor +0x2ec).</summary>
        bool IsOwnCompanion(uint actorId);
    }

    /// <summary>The map flags the pick rule reads (<see cref="RoPvpState"/>: CWorld +0x48 / +0x4c / +0x50 / +0x54, 0x99b properties).</summary>
    public readonly struct RoPickMapFlags {
        public RoPickMapFlags(bool isPk, bool isEventPvp, bool isSiege, bool isBattlefield, bool partyProperty, bool guildProperty, bool noLockOn) {
            IsPk = isPk;
            IsEventPvp = isEventPvp;
            IsSiege = isSiege;
            IsBattlefield = isBattlefield;
            PartyProperty = partyProperty;
            GuildProperty = guildProperty;
            NoLockOn = noLockOn;
        }

        public bool IsPk { get; }
        public bool IsEventPvp { get; }
        public bool IsSiege { get; }
        public bool IsBattlefield { get; }
        public bool PartyProperty { get; }
        public bool GuildProperty { get; }
        public bool NoLockOn { get; }

        public static RoPickMapFlags Current => new RoPickMapFlags(RoPvpState.IsPk, RoPvpState.IsEventPvp, RoPvpState.IsSiege,
            RoPvpState.IsBattlefield, RoPvpState.PartyProperty, RoPvpState.GuildProperty, RoPvpState.NoLockOn);
    }

    /// <summary>
    /// Who a plain click attacks — the original's cursor and target choice for the actor under the mouse (FUN_00c05710,
    /// called from FUN_00c02c50). Players: Shift attacks anywhere; the PK / event PvP / no-lock-on maps and the 0x99b
    /// "party" property make everyone outside the own party a target, the "guild" property everyone outside the own guild;
    /// in a siege only the guild decides (anyone outside the own guild and its allies, never the own guild, Shift or not);
    /// on a battlefield only the team does. Monsters: a plain click attacks, Shift does not (unless /noshift), and never a
    /// monster of the own guild (castle guardians, the own Emperium) on a guild-property or siege map. Others' homunculi,
    /// mercenaries, elementals and pets (pick type 4): only on PK / no-lock-on / siege maps, in a siege only when their
    /// owner is outside the own guild and its allies; the own ones never.
    /// </summary>
    public static class RoPickHostility {
        /// <summary>The live session's relations; null outside a map session (then nobody is a party / guild mate).</summary>
        public static IRoPickRelations Relations { get; set; }

        /// <summary>DAT_01552d40, the /noshift option (Game Settings "No Shift"; on by default like RoClientSettings).</summary>
        public static bool NoShift { get; set; } = true;

        /// <summary>DAT_0154ebc4: the own battleground team, the low byte of ZC_BATTLEFIELD_NOTIFY_CAMPINFO 0x2dd for the own AID.</summary>
        public static int OwnBattlefieldTeam { get; set; }

        private static bool SameGuild(IRoPickRelations rel, uint guildId) =>
            guildId != 0 && rel != null && rel.PickOwnGuildId == guildId;

        private static bool InParty(IRoPickRelations rel, uint accountId) => rel != null && rel.IsOnlinePartyMember(accountId);

        private static bool Allied(IRoPickRelations rel, uint guildId) => guildId != 0 && rel != null && rel.IsAlliedGuild(guildId);

        /// <summary>Pick type 0, a player job (FUN_00d02e40): is a plain or Shift click an attack?</summary>
        public static bool AttacksPlayer(RoPickMapFlags map, IRoPickRelations rel, bool shift, uint accountId, uint guildId,
            bool actorPkMode, int actorTeam = 0) {
            bool attack = shift;
            if (map.IsBattlefield) {
                return attack || actorTeam != OwnBattlefieldTeam;
            }

            if (!map.PartyProperty) {
                if (map.GuildProperty && !SameGuild(rel, guildId)) attack = true;
            } else if (!InParty(rel, accountId)) {
                attack = true;
            }

            if (!map.IsSiege) {
                if ((actorPkMode || map.IsPk || map.IsEventPvp || map.NoLockOn) && !InParty(rel, accountId)) attack = true;
                return attack;
            }

            return !SameGuild(rel, guildId) && !Allied(rel, guildId);
        }

        /// <summary>Pick type 0, a monster job (FUN_00bd1eb0).</summary>
        public static bool AttacksMonster(RoPickMapFlags map, IRoPickRelations rel, bool shift, uint guildId) {
            bool attack = !shift || NoShift;
            if ((map.GuildProperty || map.IsSiege) && SameGuild(rel, guildId)) attack = false;
            return attack;
        }

        /// <summary>Pick type 4: someone's homunculus / mercenary / elemental / pet. The owner is not known to this client
        /// (no owner field reaches it), which the original treats like an owner out of sight: no attack in a siege.</summary>
        public static bool AttacksCompanion(RoPickMapFlags map, IRoPickRelations rel, uint actorId) {
            if (rel != null && rel.IsOwnCompanion(actorId)) return false;
            return !map.IsSiege && (map.IsPk || map.NoLockOn);
        }

        /// <summary>The rule for one actor snapshot (dead actors are never attacked: actor +0x70 == 3).</summary>
        public static bool Attacks(RoOnlineActorSnapshot actor, bool shift) {
            if (RoOnlineActorInteractionRules.IsDead(actor)) return false;
            RoPickMapFlags map = RoPickMapFlags.Current;
            IRoPickRelations rel = Relations;
            switch (actor.ObjectType) {
                case RoMapActorObjectType.Player:
                    return AttacksPlayer(map, rel, shift, actor.ServerId, actor.GuildId, actor.IsPkModeOn, actor.BattlefieldTeam);
                case RoMapActorObjectType.Monster:
                    return AttacksMonster(map, rel, shift, actor.GuildId);
                case RoMapActorObjectType.Pet:
                case RoMapActorObjectType.Homunculus:
                case RoMapActorObjectType.Mercenary:
                case RoMapActorObjectType.Elemental:
                    return AttacksCompanion(map, rel, actor.ServerId);
                default:
                    return false;
            }
        }
    }

    public static class RoOnlineActorInteractionRules {
        public static bool IsDead(RoOnlineActorSnapshot actor) {
            return actor.SpawnState == RoMapActorSpawnState.Dead ||
                actor.Motion == RoOnlineActorMotion.Dead && !actor.IsPlayingDead;
        }

        public static bool IsSitting(RoOnlineActorSnapshot actor) {
            return actor.SpawnState == RoMapActorSpawnState.Sitting ||
                actor.Motion == RoOnlineActorMotion.Sitting;
        }

        public static bool BlocksMovementOrAction(RoOnlineActorSnapshot actor) {
            return actor.IsPlayingDead || IsDead(actor) || IsSitting(actor);
        }

        public static bool CanMoveOrInteract(RoOnlineActorSnapshot actor) {
            return !BlocksMovementOrAction(actor);
        }
    }

    public sealed class RoOnlinePointerTargetMarker : MonoBehaviour {
        [SerializeField] private RoOnlineWorldController worldController;
        [SerializeField] private uint serverId;
        [SerializeField] private RoMapActorObjectType objectType = RoMapActorObjectType.Unknown;

        public uint ServerId => serverId;
        public RoMapActorObjectType ObjectType => objectType;

        public void Configure(
            RoOnlineWorldController controller,
            uint actorServerId,
            RoMapActorObjectType actorObjectType
        ) {
            worldController = controller;
            serverId = actorServerId;
            objectType = actorObjectType;
        }

        /// <summary>The live snapshot of this marker's actor (for rules that need more than the object type).</summary>
        public bool TryGetActor(out RoOnlineActorSnapshot actor) {
            RoOnlineWorldController controller = worldController != null ? worldController : GetComponentInParent<RoOnlineWorldController>();
            actor = default;
            return controller != null && controller.TryGetActorSnapshot(serverId, out actor);
        }

        public bool SubmitPointerClick(RoPointerTarget pointerTarget, RoPointerModifierFlags modifiers) {
            RoOnlineWorldController controller = worldController;
            if (controller == null) {
                controller = GetComponentInParent<RoOnlineWorldController>();
            }

            return controller != null && controller.SelectOnlineTarget(serverId, pointerTarget, modifiers);
        }

        public static RoPointerTargetKind GetPointerTargetKind(RoMapActorObjectType objectType) {
            return objectType switch {
                RoMapActorObjectType.Warp => RoPointerTargetKind.Portal,
                RoMapActorObjectType.Item => RoPointerTargetKind.Item,
                RoMapActorObjectType.Monster => RoPointerTargetKind.Monster,
                RoMapActorObjectType.Npc or RoMapActorObjectType.Npc2 => RoPointerTargetKind.Npc,
                RoMapActorObjectType.Player => RoPointerTargetKind.Player,
                RoMapActorObjectType.Pet or
                    RoMapActorObjectType.Homunculus or
                    RoMapActorObjectType.Mercenary or
                    RoMapActorObjectType.Elemental => RoPointerTargetKind.Monster,
                _ => RoPointerTargetKind.Player
            };
        }

        /// <summary>The interaction for a real actor: players, monsters and companions go through
        /// <see cref="RoPickHostility.Attacks"/> (map PK / siege / battlefield flags, party, guild and alliances).</summary>
        public static RoOnlineTargetInteraction GetInteraction(
            RoOnlineActorSnapshot actor,
            RoPointerTargetKind pointerKind,
            RoPointerModifierFlags modifiers
        ) {
            if (pointerKind == RoPointerTargetKind.Player || pointerKind == RoPointerTargetKind.Monster) {
                return RoPickHostility.Attacks(actor, modifiers.HasFlag(RoPointerModifierFlags.Shift))
                    ? RoOnlineTargetInteraction.Attack
                    : RoOnlineTargetInteraction.Select;
            }

            return GetInteraction(pointerKind, modifiers);
        }

        public static RoOnlineTargetInteraction GetInteraction(
            RoPointerTargetKind pointerKind,
            RoPointerModifierFlags modifiers
        ) {
            return pointerKind switch {
                RoPointerTargetKind.Npc => RoOnlineTargetInteraction.Talk,
                RoPointerTargetKind.Monster => RoOnlineTargetInteraction.Attack,
                RoPointerTargetKind.Item => RoOnlineTargetInteraction.Pickup,
                RoPointerTargetKind.Portal => RoOnlineTargetInteraction.EnterPortal,
                RoPointerTargetKind.Player => modifiers.HasFlag(RoPointerModifierFlags.Shift)
                    ? RoOnlineTargetInteraction.Attack
                    : RoOnlineTargetInteraction.Select,
                _ => RoOnlineTargetInteraction.Select
            };
        }
    }
}
