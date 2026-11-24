using System;
using System.Collections.Generic;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>
    /// The chat lines the original's packet handlers add with their own chat type (docs/audits/chat-types-2026-10-04.md):
    /// equipment on / off (7), the weight limits (8), party configuration (12), damaged equipment (13), experience (18),
    /// quests (19) and the server's typed line ZC_BROADCAST4 (0x081f: any type, also 20 battlefield / 22 call). The shell's
    /// drain hands its events here (<see cref="OnDrainEvent"/>), the entry-queue carrier its raw frames
    /// (<see cref="HandleRaw"/>). The texts are <see cref="RoChatLineTypes"/>'.
    /// </summary>
    public static class RoClassicSystemLines {
        public const ushort ZcNotifyExp = 0x07f6;
        public const ushort ZcNotifyExp2 = 0x0acc;
        public const ushort ZcEquipItemDamaged = 0x02bb;
        public const ushort ZcBroadcast4 = 0x081f;
        public const ushort ZcGospelInfo = 0x0215;

        /// <summary>RG_STEALCOIN: ZC_USE_SKILL's case 0xd3 (0xc9aa00) writes the battle line of the coins.</summary>
        public const int SkillStealCoin = 211;

        private static readonly RoChatLineTypes.MsgSource Msg = (id, fallback) => RoClassicUiHost.MsgStr(id, fallback);
        private static readonly List<string> lines = new List<string>(3);
        private static readonly List<(string Text, string Color)> weightLines = new List<(string, string)>(2);

        // ZC_PAR_CHANGE's weight state: the client's globals 0x0154f3b0 (weight / 10), 0x0154f3ac (max weight / 10) and
        // 0x015556ac (the recovery limit, 0 until ZC_OVERWEIGHT_PERCENT), never reset (a relog starts from the last values)
        private static int weight;
        private static int maxWeight;
        private static int overweightLimit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            weight = 0;
            maxWeight = 0;
            overweightLimit = 0;
        }

        /// <summary>The drain events this class writes lines for (the shell's chat gate).</summary>
        public static bool IsChatEvent(in RoMapEntryDrainEvent e) {
            switch (e.Kind) {
                case RoMapEntryDrainEventKind.ActionFailure:
                case RoMapEntryDrainEventKind.PartyOption:
                case RoMapEntryDrainEventKind.PartyLeaderChanged:
                case RoMapEntryDrainEventKind.QuestDelete:
                case RoMapEntryDrainEventKind.QuestObjectiveUpdate:
                case RoMapEntryDrainEventKind.WearEquipmentAck:
                case RoMapEntryDrainEventKind.TakeoffEquipmentAck:
                case RoMapEntryDrainEventKind.WeightPercentage:
                    return true;
                case RoMapEntryDrainEventKind.ParameterChange:
                    return e.ParameterChange.Property == RoPlayerStatusProperty.Weight || e.ParameterChange.Property == RoPlayerStatusProperty.MaxWeight;
                case RoMapEntryDrainEventKind.StatusChange:
                    return RoStatusChatLines.HasLine(e.StatusChange.PacketId, e.StatusChange.StatusId,
                        RoStatusChatLines.IsOn(e.StatusChange.PacketId, e.StatusChange.Active), e.StatusChange.Value1);
                case RoMapEntryDrainEventKind.SkillResult:
                    return e.SkillResult.SkillId == SkillStealCoin;
                case RoMapEntryDrainEventKind.ItemThrowAck:
                    return e.ItemThrowAck.Count > 0;
                case RoMapEntryDrainEventKind.UseItemAck:
                    return e.UseItemAck.Result != 0 && e.UseItemAck.ItemId == RoChatLineTypes.ItemBattleManual75;
                case RoMapEntryDrainEventKind.CharacterNameByGid:
                    return RoClassicRanking.Pending;
                default:
                    return false;
            }
        }

        /// <summary>A drain event of the map session, in packet order (the shell's chat pass).</summary>
        public static void OnDrainEvent(RoClassicChat chat, RoLoginShellSession session, in RoMapEntryDrainEvent e) {
            if (chat == null) return;
            switch (e.Kind) {
                case RoMapEntryDrainEventKind.ActionFailure: {
                    int job = session?.InventorySnapshot()?.Status?.JobId ?? 0;
                    string text = RoChatLineTypes.ActionFailureLine(e.ActionFailure.TypeRaw, job, Msg, out string color, out bool popOpen);
                    if (text != null) chat.AddTypedLine(text, color, RoChatLineTypes.Public, popOpen);
                    break;
                }
                case RoMapEntryDrainEventKind.PartyOption:
                    PartyOption(chat, e.PartyOption);
                    break;
                case RoMapEntryDrainEventKind.PartyLeaderChanged:
                    // 0xc47b30: only for a new leader in the party list; the line with that member's name
                    if (session != null && session.TryGetPartyMember(e.PartyLeaderChanged.NewMasterAccountId, out RoPartyMemberRuntimeSnapshot leader)) {
                        chat.AddTypedLine(RoChatLineTypes.PartyLeaderLine(leader.Name, Msg), RoChatLineTypes.Yellow, RoChatLineTypes.PartyConfig);
                    }

                    break;
                case RoMapEntryDrainEventKind.QuestDelete:
                    chat.AddTypedLine(RoChatLineTypes.QuestDeletedLine(QuestTitle(e.QuestDelete.QuestId), Msg), RoChatLineTypes.Yellow, RoChatLineTypes.Quest);
                    break;
                case RoMapEntryDrainEventKind.QuestObjectiveUpdate:
                    QuestHunts(chat, session, e.QuestObjectiveUpdate);
                    break;
                case RoMapEntryDrainEventKind.WearEquipmentAck: {
                    RoZcWearEquipmentAck ack = e.WearEquipmentAck;
                    if (ack.Result == 0) {
                        // FUN_00d03af0 for the item at the index (none: no line)
                        string name = InventoryItemName(session, ack.BagIndex);
                        if (name != null) chat.AddTypedLine(RoChatLineTypes.EquipOnLine(name, Msg), RoChatLineTypes.EquipOn, RoChatLineTypes.EquipOnOff);
                    } else {
                        string text = RoChatLineTypes.EquipFailLine(true, ack.Result, Msg);
                        if (text != null) chat.AddTypedLine(text, RoChatLineTypes.Red, RoChatLineTypes.EquipOnOff);
                    }

                    break;
                }
                case RoMapEntryDrainEventKind.TakeoffEquipmentAck: {
                    RoZcTakeoffEquipmentAck ack = e.TakeoffEquipmentAck;
                    if (ack.Result == 0) {
                        string name = ack.BagIndex != 0 ? InventoryItemName(session, ack.BagIndex) : null;
                        if (name != null) chat.AddTypedLine(RoChatLineTypes.EquipOffLine(name, Msg), RoChatLineTypes.Red, RoChatLineTypes.EquipOnOff);
                    } else {
                        string text = RoChatLineTypes.EquipFailLine(false, ack.Result, Msg);
                        if (text != null) chat.AddTypedLine(text, RoChatLineTypes.Red, RoChatLineTypes.EquipOnOff);
                    }

                    break;
                }
                case RoMapEntryDrainEventKind.WeightPercentage:
                    // 0xc989a0 (ZC_OVERWEIGHT_PERCENT 0x0ade): a positive limit replaces the old one
                    if (e.WeightPercentage.WeightPercentage > 0) overweightLimit = e.WeightPercentage.WeightPercentage;
                    break;
                case RoMapEntryDrainEventKind.ParameterChange:
                    Weight(chat, e.ParameterChange);
                    break;
                case RoMapEntryDrainEventKind.StatusChange:
                    StatusLine(chat, session, e.StatusChange);
                    break;
                case RoMapEntryDrainEventKind.SkillResult:
                    StealCoin(chat, session, e.SkillResult);
                    break;
                case RoMapEntryDrainEventKind.UseItemAck:
                    // 0xc33b4d → FUN_00bfef40 (a result, the local player): BM75's line, by the job
                    if (session != null && e.UseItemAck.Result != 0 && e.UseItemAck.ActorId == session.LocalActorId &&
                        e.UseItemAck.ItemId == RoChatLineTypes.ItemBattleManual75) {
                        chat.AddTypedLine(RoChatLineTypes.BattleManualLine(session.InventorySnapshot()?.Status?.JobId ?? 0, Msg), RoChatLineTypes.Cyan,
                            RoChatLineTypes.AbnormalStatus);
                    }

                    break;
                case RoMapEntryDrainEventKind.ItemThrowAck:
                    // 0xc2d17f (ZC_ITEM_THROW_ACK): the item at the index and a count above 0 → "You dropped %s (%d)."
                    // (0x649), COLORREF 0x00FFFF00, type 6 "Item get/drop"
                    if (session != null && e.ItemThrowAck.Count > 0 && session.TryGetThrownItem(e.ItemThrowAck.BagIndex, out RoItemInstance thrown)) {
                        chat.AddTypedLine(RoChatLineTypes.Printf(Msg(0x649, "You dropped %s (%d)."), thrown.GetFullDisplayName(Items()), e.ItemThrowAck.Count),
                            RoChatLineTypes.Cyan, RoChatLineTypes.ItemGetDrop);
                    }

                    break;
                case RoMapEntryDrainEventKind.CharacterNameByGid:
                    RoClassicRanking.OnCharacterName(e.CharacterNameByGid.CharacterId, e.CharacterNameByGid.Name);
                    break;
            }
        }

        /// <summary>The raw frames of the entry-queue carrier this class answers.</summary>
        public static void HandleRaw(byte[] p) {
            if (p == null || p.Length < 2) return;
            RoClassicChat chat = RoClassicChat.Instance;
            switch ((ushort) (p[0] | p[1] << 8)) {
                case ZcNotifyExp2:
                    // 0xc93400: {AID, s64 exp, u16 var, u16 expType}
                    if (chat != null && p.Length >= 18 && IsOwnAccount(BitConverter.ToUInt32(p, 2)) &&
                        RoChatLineTypes.TryExpLine(BitConverter.ToInt64(p, 6), BitConverter.ToUInt16(p, 14), BitConverter.ToUInt16(p, 16), true, Msg,
                            out string text, out string color)) {
                        chat.AddTypedLine(text, color, RoChatLineTypes.Exp);
                    }

                    break;
                case ZcNotifyExp:
                    // 0xc55980: {AID, s32 exp, u16 var, u16 expType}
                    if (chat != null && p.Length >= 14 && IsOwnAccount(BitConverter.ToUInt32(p, 2)) &&
                        RoChatLineTypes.TryExpLine(BitConverter.ToInt32(p, 6), BitConverter.ToUInt16(p, 10), BitConverter.ToUInt16(p, 12), false, Msg,
                            out string oldText, out string oldColor)) {
                        chat.AddTypedLine(oldText, oldColor, RoChatLineTypes.Exp);
                    }

                    break;
                case ZcEquipItemDamaged:
                    if (chat != null && p.Length >= 8) EquipDamaged(chat, BitConverter.ToUInt16(p, 2), BitConverter.ToUInt32(p, 4));
                    break;
                case ZcBroadcast4:
                    Broadcast4(chat, p);
                    break;
                case ZcGospelInfo:
                    // 0xc60d50: {u32 type} → its MsgStr in COLORREF 0x009B9BFF, type 8
                    if (chat != null && p.Length >= 6) {
                        string gospel = RoChatLineTypes.GospelLine(BitConverter.ToInt32(p, 2), Msg);
                        if (!string.IsNullOrEmpty(gospel)) chat.AddTypedLine(gospel, RoChatLineTypes.Gospel, RoChatLineTypes.AbnormalStatus);
                    }

                    break;
                default:
                    RoClassicRanking.HandleRaw(p);
                    break;
            }
        }

        // ---- the producers ----

        /// <summary>0xc5fec0 / 0xc315af: mgr msg 3, then the setup lines (0x0101 carries the exp rule only).</summary>
        private static void PartyOption(RoClassicChat chat, in RoZcPartyOption option) {
            if (option.HasItemRules) {
                RoChatLineTypes.PartyOptionLines(option.ExpOption, option.ItemPickupRule, option.ItemDivisionRule, Msg, lines);
                for (int i = 0; i < lines.Count; i++) chat.AddTypedLine(lines[i], RoChatLineTypes.Yellow, RoChatLineTypes.PartyConfig, popOpen: i == 0);
                if (lines.Count == 0) chat.AddTypedLine(string.Empty, RoChatLineTypes.Yellow, RoChatLineTypes.PartyConfig, popOpen: true);
                return;
            }

            string line = RoChatLineTypes.LegacyPartyOptionLine(option.ExpOption, Msg);
            chat.AddTypedLine(line ?? string.Empty, RoChatLineTypes.Yellow, RoChatLineTypes.PartyConfig, popOpen: true);
        }

        /// <summary>0xc68c40 (0x0afe) / 0xc69100 (0x09fa): a line per updated objective of a held quest.</summary>
        private static void QuestHunts(RoClassicChat chat, RoLoginShellSession session, in RoZcQuestObjectiveUpdate update) {
            RoZcQuestObjectiveCounter[] counters = update.Updates;
            if (counters == null || counters.Length == 0 || session == null) return;
            RoQuestRuntimeEntrySnapshot[] quests = session.QuestSnapshot()?.Quests;
            if (quests == null) return;
            foreach (RoZcQuestObjectiveCounter counter in counters) {
                if (!TryFindObjective(quests, counter, out RoQuestObjectiveRuntimeSnapshot objective)) continue;
                chat.AddTypedLine(RoChatLineTypes.QuestHuntLine(QuestTitle(counter.QuestId), HuntName(objective), counter.Current, counter.Total, Msg),
                    RoChatLineTypes.Yellow, RoChatLineTypes.Quest);
            }
        }

        private static bool TryFindObjective(RoQuestRuntimeEntrySnapshot[] quests, in RoZcQuestObjectiveCounter counter, out RoQuestObjectiveRuntimeSnapshot objective) {
            objective = default;
            foreach (RoQuestRuntimeEntrySnapshot quest in quests) {
                if (quest.QuestId != counter.QuestId || quest.Objectives == null) continue;
                foreach (RoQuestObjectiveRuntimeSnapshot o in quest.Objectives) {
                    if (o.HuntIdent == counter.HuntIdent && o.HuntIdent2 == counter.HuntIdent2) {
                        objective = o;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The hunt's stored name (0xc43080): a monster keeps the packet's name, 0x1099 / 0x109a are MsgStr 0xde6 / 0xde7, 0x287b names the map.</summary>
        private static string HuntName(in RoQuestObjectiveRuntimeSnapshot objective) {
            int mob = unchecked((int) objective.MobId);
            string name = objective.MobName ?? string.Empty;
            bool monster = !((mob < 0x3e9 || mob > 0xf9e) && unchecked((uint) (mob - 0x4e35)) > 0x2eca);
            if (monster) return name;
            if (mob == 0x1099) return RoClassicUiHost.MsgStr(0xde6, name);
            if (mob == 0x109a) return RoClassicUiHost.MsgStr(0xde7, name);
            if (mob == 0x287b) return RoClassicUiHost.MapDisplayName(name);
            return name;
        }

        private static string QuestTitle(uint questId) =>
            RoClassicQuestData.Metadata().TryGetQuest(questId, out RoQuestMetadata info) && info != null ? info.Title ?? string.Empty : string.Empty;

        /// <summary>
        /// FUN_00c767a0 / FUN_00c90ca0 (0x0196): the local player's status line (type 8) or an online party member's (type 10, after " %s's "), after
        /// mgr msg 3; anyone else's status writes nothing.
        /// </summary>
        private static void StatusLine(RoClassicChat chat, RoLoginShellSession session, in RoZcMsgStateChange status) {
            if (session == null) return;
            string prefix = string.Empty;
            if (status.Aid != session.LocalActorId) {
                if (!session.TryGetPartyMember(status.Aid, out RoPartyMemberRuntimeSnapshot member) || member.State != 0) return;
                prefix = RoStatusChatLines.MemberPrefix(member.Name, Msg);
            }

            string text = RoStatusChatLines.Text(status.PacketId, status.StatusId, RoStatusChatLines.IsOn(status.PacketId, status.Active),
                status.RemainingMilliseconds, status.Value1, status.Value2, Msg, out string color);
            if (string.IsNullOrEmpty(text) || text.Length < 2) return;
            chat.AddTypedLine(prefix + text, color, prefix.Length > 1 ? RoChatLineTypes.PartyStatus : RoChatLineTypes.AbnormalStatus, popOpen: true);
        }

        /// <summary>
        /// ZC_USE_SKILL (0xc9aa00) case 0xd3, RG_STEALCOIN: the local player's Mug on an actor in view writes "From [%s], '%d'
        /// coins were stolen." (0x651) with the target's name and the packet's amount, COLORREF 0x00C8C8FF, type 16.
        /// </summary>
        private static void StealCoin(RoClassicChat chat, RoLoginShellSession session, in RoZcUseSkillResult result) {
            if (session == null || result.SourceId == 0 || result.SourceId != session.LocalActorId) return;
            Ragnarok.World.RoOnlineWorldController world = UnityEngine.Object.FindFirstObjectByType<Ragnarok.World.RoOnlineWorldController>();
            if (world == null || !(world.State.TryGetActor(result.TargetId, out Ragnarok.World.RoOnlineActorSnapshot target) ||
                    world.State.TryGetActorByAccountId(result.TargetId, out target))) {
                return;
            }

            string text = RoChatLineTypes.Printf(Msg(0x651, "From [%s], '%d' coins were stolen."), target.Name ?? string.Empty, result.Value);
            chat.AddTypedLine(text, RoChatLineTypes.Pink, RoChatLineTypes.Battle);
        }

        /// <summary>0xc5c6ca / 0xc5c8a7: the weight lines of ZC_PAR_CHANGE 0x18 / 0x19 (the values in tenths).</summary>
        private static void Weight(RoClassicChat chat, in RoZcParameterChange change) {
            int value = (int) (change.Value / 10);
            if (change.Property == RoPlayerStatusProperty.Weight) {
                int before = weight * 100 / Math.Max(maxWeight, 1);
                weight = value;
                if (maxWeight == 0) return;
                RoChatLineTypes.WeightLines(before, weight * 100 / Math.Max(maxWeight, 1), overweightLimit, false, Msg, weightLines);
            } else {
                int oldMax = maxWeight;
                int before = weight * 100 / Math.Max(oldMax, 1);
                maxWeight = value;
                if (oldMax == 0 || oldMax >= maxWeight) return;
                RoChatLineTypes.WeightLines(before, weight * 100 / Math.Max(maxWeight, 1), overweightLimit, true, Msg, weightLines);
            }

            foreach ((string text, string color) in weightLines) chat.AddTypedLine(text, color, RoChatLineTypes.AbnormalStatus, popOpen: true);
        }

        /// <summary>0xc49710: the own item at the location, or a party member's weapon / armour; mgr msg 3, red, type 13.</summary>
        private static void EquipDamaged(RoClassicChat chat, int location, uint accountId) {
            if (location == 0) return;
            RoLoginShellSession session = chat.Model.Session;
            string text;
            if (IsOwnAccount(accountId)) {
                text = RoChatLineTypes.EquipDamagedLine(true, location, EquippedItemName(session, location), null, Msg);
            } else {
                string member = session != null && session.TryGetPartyMember(accountId, out RoPartyMemberRuntimeSnapshot m) ? m.Name : null;
                text = RoChatLineTypes.EquipDamagedLine(false, location, null, member, Msg);
            }

            if (!string.IsNullOrEmpty(text)) chat.AddTypedLine(text, RoChatLineTypes.Red, RoChatLineTypes.EquipDamaged, popOpen: true);
        }

        /// <summary>
        /// 0xc45a10 (ZC_BROADCAST4 0x081f): {u16 len, u8 type, u32 COLORREF, text[len − 9]} straight into the chat with the
        /// packet's type and colour (the only way to a battlefield (20) or call (22) line). A type past 24 reads beyond the
        /// tab's 24 flags in the client (undefined): dropped here.
        /// </summary>
        private static void Broadcast4(RoClassicChat chat, byte[] p) {
            if (chat == null || p.Length < 10) return;
            int len = Math.Min(BitConverter.ToUInt16(p, 2), p.Length);
            int type = p[4];
            if (type > RoChatLineTypes.EveryTab || len <= 9) return;
            int n = 0;
            while (9 + n < len && p[9 + n] != 0) n++;
            if (n == 0) return;
            var bytes = new byte[n];
            Array.Copy(p, 9, bytes, 0, n);
            string text = Ragnarok.Assets.RoTextEncoding.Decode(bytes) ?? string.Empty;
            // COLORREF 0x00BBGGRR
            string color = "#" + p[5].ToString("X2") + p[6].ToString("X2") + p[7].ToString("X2");
            chat.AddTypedLine(text, color == "#000000" ? "#FFFFFF" : color, type);
        }

        // ---- helpers ----

        private static bool IsOwnAccount(uint accountId) {
            RoLoginShellSession session = RoClassicChat.Instance?.Model.Session;
            return accountId != 0 && session != null && session.LocalActorId == accountId;
        }

        private static RoItemMetadataTable Items() => RoClassicUiHost.GameAssets()?.GameData?.Items ?? RoItemMetadataTable.Empty;

        /// <summary>FUN_00ce9c60: the inventory item at the bag index, its display name (FUN_0065d630).</summary>
        private static string InventoryItemName(RoLoginShellSession session, int bagIndex) {
            RoItemInstance[] items = session?.InventorySnapshot()?.InventoryItems;
            if (items == null) return null;
            foreach (RoItemInstance item in items) {
                if (item != null && item.BagIndex == bagIndex) return item.GetFullDisplayName(Items());
            }

            return null;
        }

        /// <summary>FUN_00ce4af0: the equipped item worn at the location.</summary>
        private static string EquippedItemName(RoLoginShellSession session, int location) {
            RoItemInstance[] items = session?.InventorySnapshot()?.InventoryItems;
            if (items == null) return null;
            foreach (RoItemInstance item in items) {
                if (item != null && ((int) item.WearState & location) != 0) return item.GetFullDisplayName(Items());
            }

            return null;
        }
    }
}
