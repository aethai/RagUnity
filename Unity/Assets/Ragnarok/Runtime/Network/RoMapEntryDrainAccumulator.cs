using System;
using System.Collections.Generic;
using System.Text;
using Ragnarok.Diagnostics;
using Unity.Profiling;

namespace Ragnarok.Network {
    internal sealed class RoMapEntryDrainAccumulator {
        private static readonly ProfilerMarker ToResultMarker = new ProfilerMarker("RO.Net.Drain.ToResult");

        private readonly Encoding textEncoding;
        private readonly List<RoMapEntryDrainedPacket> packets = new List<RoMapEntryDrainedPacket>();
        private readonly List<RoZcNotifyPlayerChat> playerChats = new List<RoZcNotifyPlayerChat>();
        private readonly List<RoZcNotifyTime> timeNotifications = new List<RoZcNotifyTime>();
        private readonly List<RoZcNotifyActorEntry> actorEntries = new List<RoZcNotifyActorEntry>();
        private readonly List<RoZcNotifyMove> actorMoves = new List<RoZcNotifyMove>();
        private readonly List<RoZcNotifyPlayerMove> playerMoves = new List<RoZcNotifyPlayerMove>();
        private readonly List<RoZcStopMove> stopMoves = new List<RoZcStopMove>();
        private readonly List<RoZcNotifyVanish> actorVanishes = new List<RoZcNotifyVanish>();
        private readonly List<RoMapEntryDrainEvent> orderedEvents = new List<RoMapEntryDrainEvent>();
        private readonly List<string> parseFailureMessages = new List<string>();
        private int parseFailureCount;

        public RoMapEntryDrainAccumulator(Encoding textEncoding) {
            this.textEncoding = textEncoding ?? Encoding.ASCII;
        }

        public int PacketCount => packets.Count;
        public bool HasMovementResponse => playerMoves.Count > 0 || actorMoves.Count > 0 || stopMoves.Count > 0;

        public void AddFrame(RoPacketFrame frame) {
            packets.Add(ToDrainedPacket(frame));
            RoNetAudit.Received(frame);
            try {
                ApplyFrameToOrderedEvents(frame);
            } catch (Exception exception) when (
                exception is ArgumentException ||
                exception is System.IO.EndOfStreamException ||
                exception is FormatException) {
                parseFailureCount++;
                RoNetAudit.Failed($"0x{frame.PacketId:x4} ({frame.Definition.Name}, {frame.Length}B): {exception.Message}");
                if (parseFailureMessages.Count < 16) {
                    parseFailureMessages.Add(
                        $"0x{frame.PacketId:x4} ({frame.Definition.Name}, {frame.Length}B): {exception.Message}");
                }
            }
        }

        private void ApplyFrameToOrderedEvents(RoPacketFrame frame) {
            switch (frame.PacketId) {
                case RoPacketIds.ZcNotifyChat:
                    RoZcNotifyPlayerChat notifyChat = RoMapPacketParser.ParseZcNotifyChat(frame.Bytes, textEncoding);
                    playerChats.Add(notifyChat);
                    orderedEvents.Add(new RoMapEntryDrainEvent(notifyChat));
                    break;
                case RoPacketIds.ZcNotifyPlayerChat:
                    RoZcNotifyPlayerChat playerChat = RoMapPacketParser.ParseZcNotifyPlayerChat(frame.Bytes, textEncoding);
                    playerChats.Add(playerChat);
                    orderedEvents.Add(new RoMapEntryDrainEvent(playerChat));
                    break;
                case RoPacketIds.ZcBroadcast:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBroadcast(frame.Bytes, textEncoding)));
                    // the raw frame too: the classic broadcast boxes read its prefix forms as the client does
                    orderedEvents.Add(new RoMapEntryDrainEvent(new RoZcEntryQueue(frame.PacketId, (byte[]) frame.Bytes.Clone())));
                    break;
                case RoPacketIds.ZcBroadcast2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBroadcast2(frame.Bytes, textEncoding)));
                    orderedEvents.Add(new RoMapEntryDrainEvent(new RoZcEntryQueue(frame.PacketId, (byte[]) frame.Bytes.Clone())));
                    break;
                case RoPacketIds.ZcDebugMsg:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDebugMsg(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcNpcChat:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcChat(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyChat:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyChat(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcClanChat:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcClanChat(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcWhisper:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcWhisper(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcWhisper2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcWhisper2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckWhisper:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckWhisper(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckWhisper2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckWhisper2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMsg:
                    RoZcMessage message = RoMapPacketParser.ParseZcMsg(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(message));
                    break;
                case RoPacketIds.ZcMsgValue:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMsgValue(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMsgSkill:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMsgSkill(frame.Bytes)));
                    break;
                case RoPacketIds.ZcFormatStringMsg:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFormatStringMsg(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcMsgColor:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMsgColor(frame.Bytes)));
                    break;
                case RoPacketIds.ZcFormatStringMsgColor:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFormatStringMsgColor(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcNotifyTime:
                    RoZcNotifyTime timeNotification = RoMapPacketParser.ParseZcNotifyTime(frame.Bytes);
                    timeNotifications.Add(timeNotification);
                    orderedEvents.Add(new RoMapEntryDrainEvent(timeNotification));
                    break;
                case RoPacketIds.ZcNpcAckMapMove:
                    RoZcNpcMapMove npcMapMove = RoMapPacketParser.ParseZcNpcAckMapMove(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(npcMapMove));
                    break;
                case RoPacketIds.ZcNpcAckServerMove:
                    RoZcNpcServerMove npcServerMove = RoMapPacketParser.ParseZcNpcAckServerMove(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(npcServerMove));
                    break;
                case RoPacketIds.ZcNpcAckServerMoveDomain:
                    RoZcNpcServerMove npcServerMoveDomain = RoMapPacketParser.ParseZcNpcAckServerMoveDomain(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(npcServerMoveDomain));
                    break;
                case RoPacketIds.ZcSayDialog:
                    RoZcNpcDialogText npcDialogText = RoMapPacketParser.ParseZcSayDialog(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogText, npcDialogText));
                    break;
                case RoPacketIds.ZcWaitDialog:
                    RoZcNpcDialogSignal npcDialogNext = RoMapPacketParser.ParseZcWaitDialog(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogNext, npcDialogNext));
                    break;
                case RoPacketIds.ZcCloseDialog:
                    RoZcNpcDialogSignal npcDialogClose = RoMapPacketParser.ParseZcCloseDialog(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogClose, npcDialogClose));
                    break;
                case RoPacketIds.ZcMenuList:
                    RoZcNpcDialogText npcDialogMenu = RoMapPacketParser.ParseZcMenuList(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogMenu, npcDialogMenu));
                    break;
                case RoPacketIds.ZcOpenEditDlg:
                    RoZcNpcDialogSignal npcDialogNumberInput = RoMapPacketParser.ParseZcOpenEditDlg(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogNumberInput, npcDialogNumberInput));
                    break;
                case RoPacketIds.ZcShowImage:
                    RoZcNpcCutin npcCutin = RoMapPacketParser.ParseZcShowImage(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(npcCutin));
                    break;
                case RoPacketIds.ZcShowScript:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcShowScript(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcCompass:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCompass(frame.Bytes)));
                    break;
                case RoPacketIds.ZcUiOpen:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcUiOpen(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAttendanceResponse:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAttendanceResponse(frame.Bytes)));
                    break;
                case RoPacketIds.ZcOpenEditDlgStr:
                    RoZcNpcDialogSignal npcDialogTextInput = RoMapPacketParser.ParseZcOpenEditDlgStr(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogTextInput, npcDialogTextInput));
                    break;
                case RoPacketIds.ZcShowImage2:
                    RoZcNpcCutin npcCutin2 = RoMapPacketParser.ParseZcShowImage2(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(npcCutin2));
                    break;
                case RoPacketIds.ZcClearDialog:
                    RoZcNpcDialogSignal npcDialogClear = RoMapPacketParser.ParseZcClearDialog(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogClear, npcDialogClear));
                    break;
                case RoPacketIds.ZcSelectDealType:
                    RoZcSelectDealType shopDealType = RoMapPacketParser.ParseZcSelectDealType(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(shopDealType));
                    break;
                case RoPacketIds.ZcPcPurchaseItemList:
                    RoZcNpcShopBuyList shopBuyList = RoMapPacketParser.ParseZcPcPurchaseItemList(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(shopBuyList));
                    break;
                case RoPacketIds.ZcPcPurchaseItemList2:
                    RoZcNpcShopBuyList shopBuyList2 = RoMapPacketParser.ParseZcPcPurchaseItemList2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(shopBuyList2));
                    break;
                case RoPacketIds.ZcPcSellItemList:
                    RoZcNpcShopSellList shopSellList = RoMapPacketParser.ParseZcPcSellItemList(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(shopSellList));
                    break;
                case RoPacketIds.ZcPcPurchaseResult:
                    RoZcNpcShopResult shopBuyResult = RoMapPacketParser.ParseZcPcPurchaseResult(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcShopBuyResult, shopBuyResult));
                    break;
                case RoPacketIds.ZcPcSellResult:
                    RoZcNpcShopResult shopSellResult = RoMapPacketParser.ParseZcPcSellResult(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcShopSellResult, shopSellResult));
                    break;
                case RoPacketIds.ZcOpenStore:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcOpenStore(frame.Bytes)));
                    break;
                case RoPacketIds.ZcStoreEntry:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcStoreEntry(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcDisappearEntry:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDisappearEntry(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPcPurchaseItemListFromMc:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.PlayerShopListReceived,
                        RoMapPacketParser.ParseZcPcPurchaseItemListFromMc(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPcPurchaseItemListFromMc2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.PlayerShopListReceived,
                        RoMapPacketParser.ParseZcPcPurchaseItemListFromMc2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPcPurchaseItemListFromMc3:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.PlayerShopListReceived,
                        RoMapPacketParser.ParseZcPcPurchaseItemListFromMc3(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPcPurchaseMyItemList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.VendingOpenAccepted,
                        RoMapPacketParser.ParseZcPcPurchaseMyItemList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPcPurchaseMyItemList2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.VendingOpenAccepted,
                        RoMapPacketParser.ParseZcPcPurchaseMyItemList2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPcPurchaseResultFromMc:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPcPurchaseResultFromMc(frame.Bytes)));
                    break;
                case RoPacketIds.ZcDeleteItemFromMcStore:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDeleteItemFromMcStore(frame.Bytes)));
                    break;
                case RoPacketIds.ZcDeleteItemFromMcStore2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDeleteItemFromMcStore2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckOpenStore2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckOpenStore2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNpcMarketOpen:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcMarketOpen(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNpcMarketOpen2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcMarketOpen2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNpcMarketPurchaseResult:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcMarketPurchaseResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNpcMarketPurchaseResult2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcMarketPurchaseResult2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNpcBarterMarketItemInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcBarterList, RoMapPacketParser.ParseZcNpcBarterMarketItemInfo(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNpcExpandedBarterMarketItemInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcExpandedBarterList, RoMapPacketParser.ParseZcNpcExpandedBarterMarketItemInfo(frame.Bytes)));
                    break;
                case RoPacketIds.ZcStorageItemListNormal:
                case RoPacketIds.ZcStorageItemListEquip:
                case RoPacketIds.ZcStorageItemListNormal2:
                case RoPacketIds.ZcStorageItemListEquip2:
                case RoPacketIds.ZcStorageItemListEquip3:
                case RoPacketIds.ZcStorageItemListNormal3:
                case RoPacketIds.ZcStorageItemListNormal4:
                case RoPacketIds.ZcStorageItemListEquip4:
                case RoPacketIds.ZcStorageItemListEquip5:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcStorageItemList(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcNotifyStoreItemCountInfo:
                    RoZcStorageAmount storageAmount = RoMapPacketParser.ParseZcNotifyStoreItemCountInfo(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(storageAmount));
                    break;
                case RoPacketIds.ZcAddItemToStore:
                case RoPacketIds.ZcAddItemToStore2:
                case RoPacketIds.ZcAddItemToStore3:
                case RoPacketIds.ZcAddItemToStore4:
                    RoZcStorageItem storageItem = RoMapPacketParser.ParseZcAddItemToStore(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(storageItem));
                    break;
                case RoPacketIds.ZcDeleteItemFromStore:
                    RoZcStorageItemDelete storageItemDelete = RoMapPacketParser.ParseZcDeleteItemFromStore(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(storageItemDelete));
                    break;
                case RoPacketIds.ZcCloseStore:
                    RoMapPacketParser.ParseZcCloseStore(frame.Bytes);
                    orderedEvents.Add(RoMapEntryDrainEvent.StorageClosed());
                    break;
                case RoPacketIds.ZcNotifyCartItemCountInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyCartItemCountInfo(frame.Bytes)));
                    break;
                case RoPacketIds.ZcEquipItemListCart:
                case RoPacketIds.ZcNonstackableItemListCart:
                case RoPacketIds.ZcEquipItemListCart2:
                case RoPacketIds.ZcNonstackableItemListCart2:
                case RoPacketIds.ZcCartNormalItemList4:
                case RoPacketIds.ZcCartEquipItemList4:
                case RoPacketIds.ZcCartEquipItemList5:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCartItemList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAddItemToCart:
                case RoPacketIds.ZcAddItemToCart2:
                case RoPacketIds.ZcAddItemToCart3:
                case RoPacketIds.ZcAddItemToCart4:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.CartItemAdd,
                        RoMapPacketParser.ParseZcAddItemToCart(frame.Bytes)));
                    break;
                case RoPacketIds.ZcDeleteItemFromCart:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.CartItemDelete,
                        RoMapPacketParser.ParseZcDeleteItemFromCart(frame.Bytes)));
                    break;
                case RoPacketIds.ZcCartOff:
                    RoMapPacketParser.ParseZcCartOff(frame.Bytes);
                    orderedEvents.Add(RoMapEntryDrainEvent.CartOff());
                    break;
                case RoPacketIds.ZcBankingCheck:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBankingCheck(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckBankingDeposit:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.BankDepositAck,
                        RoMapPacketParser.ParseZcAckBankingDeposit(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckBankingWithdraw:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.BankWithdrawAck,
                        RoMapPacketParser.ParseZcAckBankingWithdraw(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckBankOpen:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.BankOpenAck,
                        RoMapPacketParser.ParseZcAckBankOpen(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckBankClose:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.BankCloseAck,
                        RoMapPacketParser.ParseZcAckBankClose(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNotifyUnreadMail:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyUnreadMail(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckRodexList:
                    AddRodexListEvent(frame, () => RoMapPacketParser.ParseZcAckRodexList(frame.Bytes, textEncoding));
                    break;
                case RoPacketIds.ZcAckRodexList2:
                    AddRodexListEvent(frame, () => RoMapPacketParser.ParseZcAckRodexList2(frame.Bytes, textEncoding));
                    break;
                case RoPacketIds.ZcAckRodexList3:
                    AddRodexListEvent(frame, () => RoMapPacketParser.ParseZcAckRodexList3(frame.Bytes, textEncoding));
                    break;
                case RoPacketIds.ZcAckRodexList4:
                    AddRodexListEvent(frame, () => RoMapPacketParser.ParseZcAckRodexList4(frame.Bytes, textEncoding));
                    break;
                case RoPacketIds.ZcAckFailedAllRodexList:
                    RoMapPacketParser.ParseZcAckFailedAllRodexList(frame.Bytes);
                    orderedEvents.Add(RoMapEntryDrainEvent.RodexFailedList());
                    break;
                case RoPacketIds.ZcAckReadRodex:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReadRodex(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckReadRodex2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReadRodex2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckSendRodex:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckSendRodex(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckZenyFromRodex:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.RodexZenyAck,
                        RoMapPacketParser.ParseZcAckZenyFromRodex(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckItemFromRodex:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.RodexItemsAck,
                        RoMapPacketParser.ParseZcAckItemFromRodex(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckDeleteRodex:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckDeleteRodex(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckAddItemRodex:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckAddItemRodex(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckAddItemRodex2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckAddItemRodex2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckRemoveRodexItem:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckRemoveRodexItem(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckOpenWriteRodex:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckOpenWriteRodex(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcCheckReceiveCharacterName:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCheckReceiveCharacterName(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcCheckReceiveCharacterName2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCheckReceiveCharacterName2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcReqExchangeItem:
                case RoPacketIds.ZcReqExchangeItem2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcReqExchangeItem(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckExchangeItem:
                case RoPacketIds.ZcAckExchangeItem2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckExchangeItem(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAddExchangeItem:
                case RoPacketIds.ZcAddExchangeItem2:
                case RoPacketIds.ZcAddExchangeItem3:
                case RoPacketIds.ZcAddExchangeItem4:
                case RoPacketIds.ZcAddExchangeItem5:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAddExchangeItem(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckAddExchangeItem:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckAddExchangeItem(frame.Bytes)));
                    break;
                case RoPacketIds.ZcConcludeExchangeItem:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcConcludeExchangeItem(frame.Bytes)));
                    break;
                case RoPacketIds.ZcCancelExchangeItem:
                    RoMapPacketParser.ParseZcCancelExchangeItem(frame.Bytes);
                    orderedEvents.Add(RoMapEntryDrainEvent.TradeCancel());
                    break;
                case RoPacketIds.ZcExecExchangeItem:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcExecExchangeItem(frame.Bytes)));
                    break;
                case RoPacketIds.ZcGuildBelongInfo:
                case RoPacketIds.ZcGuildBelongInfo2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildBelongInfo(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcGuildNotice:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildNotice(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcGuildMemberInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildMemberInfo(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcGuildInvite:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildInvite(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcGuildInviteResult:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildInviteResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcGuildLeaveAck:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildLeaveAck(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcGuildChat:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildChat(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcGuildMenuInterface:
                case RoPacketIds.ZcGuildInfo3:
                case RoPacketIds.ZcGuildInfo4:
                case RoPacketIds.ZcMemberMgrInfo2:
                case RoPacketIds.ZcMemberMgrInfo3:
                case RoPacketIds.ZcPositionIdNameInfo:
                case RoPacketIds.ZcPositionInfo:
                case RoPacketIds.ZcAckChangeGuildPositionInfo:
                case RoPacketIds.ZcAckReqChangeMembers:
                case RoPacketIds.ZcMyGuildBasicInfo:
                case RoPacketIds.ZcGuildAgitInfo:
                case RoPacketIds.ZcUpdateCharStat2:
                case RoPacketIds.ZcAckBanGuild3:
                case RoPacketIds.ZcAckLeaveGuild2:
                case RoPacketIds.ZcGuildSkillInfo:
                case RoPacketIds.ZcBanList2:
                case RoPacketIds.ZcBanList3:
                case RoPacketIds.ZcAckDisorganizeGuildResult:
                case RoPacketIds.ZcResultMakeGuild:
                case RoPacketIds.ZcReqAllyGuild:
                case RoPacketIds.ZcAckReqAllyGuild:
                case RoPacketIds.ZcAckReqHostileGuild:
                case RoPacketIds.ZcAddRelatedGuild:
                case RoPacketIds.ZcDeleteRelatedGuild:
                    orderedEvents.Add(RoMapEntryDrainEvent.GuildManagerEvent(RoGuildPacketParser.Parse(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPetProperty:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPetProperty(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcFeedPet:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFeedPet(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPetChangeState:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPetChangeState(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPetAct:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPetAct(frame.Bytes)));
                    break;
                case RoPacketIds.ZcHomunculusProperty:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcHomunculusProperty(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcHomunculusChangeState:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.HomunculusChangeState, RoMapPacketParser.ParseZcHomunculusChangeState(frame.Bytes)));
                    break;
                case RoPacketIds.ZcHomunculusParChange:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcHomunculusParChange(frame.Bytes)));
                    break;
                case RoPacketIds.ZcHomunculusSkillInfoList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.HomunculusSkillInfoList, RoMapPacketParser.ParseZcHomunculusSkillInfoList(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcHomunculusProperty3:
                case RoPacketIds.ZcHomunculusProperty4:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcHomunculusPropertyV3(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcHomunculusSkillUpdate:
                    orderedEvents.Add(RoMapEntryDrainEvent.HomunculusSkill(RoMapPacketParser.ParseZcHomunculusSkillUpdate(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMercenaryParChange:
                    orderedEvents.Add(RoMapEntryDrainEvent.MercenaryParameterEvent(RoMapPacketParser.ParseZcMercenaryParChange(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMercenaryInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMercenaryInfo(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcMercenarySkillInfoList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.MercenarySkillInfoList, RoMapPacketParser.ParseZcMercenarySkillInfoList(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcNotifyMapPropertyPk:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyMapPropertyPk(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNotifyRanking:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyRanking(frame.Bytes)));
                    break;
                case RoPacketIds.ZcBattlefieldNotifyPoint:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyPoint(frame.Bytes)));
                    break;
                case RoPacketIds.ZcBattlefieldNotifyCampInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyCampInfo(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcBattlefieldNotifyPosition:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyPosition(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcBattlefieldNotifyHpLegacy:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyHpLegacy(frame.Bytes)));
                    break;
                case RoPacketIds.ZcBattlefieldNotifyHp:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyHp(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNotifyMapProperty:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyMapProperty(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNotifyMapProperty2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyMapProperty2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcDeleteItemFromBody:
                    RoZcInventoryItemDelete inventoryItemDelete = RoMapPacketParser.ParseZcDeleteItemFromBody(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryItemDelete));
                    break;
                case RoPacketIds.ZcNotifyNewEntry11:
                    RoZcNotifyActorEntry newEntry = RoMapPacketParser.ParseZcNotifyNewEntry11(frame.Bytes, textEncoding);
                    actorEntries.Add(newEntry);
                    orderedEvents.Add(new RoMapEntryDrainEvent(newEntry));
                    break;
                case RoPacketIds.ZcNotifyStandEntry11:
                    RoZcNotifyActorEntry standEntry = RoMapPacketParser.ParseZcNotifyStandEntry11(frame.Bytes, textEncoding);
                    actorEntries.Add(standEntry);
                    orderedEvents.Add(new RoMapEntryDrainEvent(standEntry));
                    break;
                case RoPacketIds.ZcNotifyMoveEntry11:
                    RoZcNotifyActorEntry moveEntry = RoMapPacketParser.ParseZcNotifyMoveEntry11(frame.Bytes, textEncoding);
                    actorEntries.Add(moveEntry);
                    orderedEvents.Add(new RoMapEntryDrainEvent(moveEntry));
                    break;
                case RoPacketIds.ZcNotifyMove:
                    RoZcNotifyMove actorMove = RoMapPacketParser.ParseZcNotifyMove(frame.Bytes);
                    actorMoves.Add(actorMove);
                    orderedEvents.Add(new RoMapEntryDrainEvent(actorMove));
                    break;
                case RoPacketIds.ZcNotifyPlayerMove:
                    RoZcNotifyPlayerMove playerMove = RoMapPacketParser.ParseZcNotifyPlayerMove(frame.Bytes);
                    playerMoves.Add(playerMove);
                    orderedEvents.Add(new RoMapEntryDrainEvent(playerMove));
                    break;
                case RoPacketIds.ZcStopMove:
                    RoZcStopMove stopMove = RoMapPacketParser.ParseZcStopMove(frame.Bytes);
                    stopMoves.Add(stopMove);
                    orderedEvents.Add(new RoMapEntryDrainEvent(stopMove));
                    break;
                case RoPacketIds.ZcAckReqName:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqName(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckReqNameAll:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameAll(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckReqNameAll2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameAll2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckReqNameAllNpc:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameAllNpc(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcHpInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcHpInfo(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNotifyVanish:
                    RoZcNotifyVanish actorVanish = RoMapPacketParser.ParseZcNotifyVanish(frame.Bytes);
                    actorVanishes.Add(actorVanish);
                    orderedEvents.Add(new RoMapEntryDrainEvent(actorVanish));
                    break;
                case RoPacketIds.ZcChangeDirection:
                    RoZcChangeDirection directionChange = RoMapPacketParser.ParseZcChangeDirection(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(directionChange));
                    break;
                case RoPacketIds.ZcSpriteChange:
                    RoZcSpriteChange spriteChange = RoMapPacketParser.ParseZcSpriteChange(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(spriteChange));
                    break;
                case RoPacketIds.ZcSpriteChange2:
                    RoZcSpriteChange spriteChange2 = RoMapPacketParser.ParseZcSpriteChange2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(spriteChange2));
                    break;
                case RoPacketIds.ZcItemEntry:
                    RoZcItemEntry itemEntry = RoMapPacketParser.ParseZcItemEntry(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(itemEntry));
                    break;
                case RoPacketIds.ZcItemFallEntry:
                    RoZcItemEntry itemFallEntry = RoMapPacketParser.ParseZcItemFallEntry(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(itemFallEntry));
                    break;
                case RoPacketIds.ZcItemFallEntry2:
                    RoZcItemEntry itemFallEntry2 = RoMapPacketParser.ParseZcItemFallEntry2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(itemFallEntry2));
                    break;
                case RoPacketIds.ZcItemFallEntry5:
                    RoZcItemEntry itemFallEntry5 = RoMapPacketParser.ParseZcItemFallEntry5(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(itemFallEntry5));
                    break;
                case RoPacketIds.ZcItemDisappear:
                    RoZcItemDisappear itemDisappear = RoMapPacketParser.ParseZcItemDisappear(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(itemDisappear));
                    break;
                case RoPacketIds.ZcNotifyAct:
                    RoZcNotifyAct actorAction = RoMapPacketParser.ParseZcNotifyAct(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(actorAction));
                    break;
                case RoPacketIds.ZcNotifyAct2:
                    RoZcNotifyAct actorAction2 = RoMapPacketParser.ParseZcNotifyAct2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(actorAction2));
                    break;
                case RoPacketIds.ZcNotifyAct3:
                    RoZcNotifyAct actorAction3 = RoMapPacketParser.ParseZcNotifyAct3(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(actorAction3));
                    break;
                case RoPacketIds.ZcEmotion:
                    RoZcEmotion emotion = RoMapPacketParser.ParseZcEmotion(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(emotion));
                    break;
                case RoPacketIds.ZcStateChange:
                    RoZcStateChange actorStateChange = RoMapPacketParser.ParseZcStateChange(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(actorStateChange));
                    break;
                case RoPacketIds.ZcStateChange3:
                    RoZcStateChange actorStateChange3 = RoMapPacketParser.ParseZcStateChange3(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(actorStateChange3));
                    break;
                case RoPacketIds.ZcMsgStateChange:
                    RoZcMsgStateChange statusChange = RoMapPacketParser.ParseZcMsgStateChange(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusChange));
                    break;
                case RoPacketIds.ZcMsgStateChange2:
                    RoZcMsgStateChange statusChange2 = RoMapPacketParser.ParseZcMsgStateChange2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusChange2));
                    break;
                case RoPacketIds.ZcMsgStateChangeTimed:
                    RoZcMsgStateChange statusChangeTimed = RoMapPacketParser.ParseZcMsgStateChangeTimed(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusChangeTimed));
                    break;
                case RoPacketIds.ZcMsgStateChange3:
                    RoZcMsgStateChange statusChange3 = RoMapPacketParser.ParseZcMsgStateChange3(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusChange3));
                    break;
                case RoPacketIds.ZcMsgStateChangeTimedWithTotal:
                    RoZcMsgStateChange statusChangeTimedWithTotal = RoMapPacketParser.ParseZcMsgStateChangeTimedWithTotal(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusChangeTimedWithTotal));
                    break;
                case RoPacketIds.ZcResurrection:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcResurrection(frame.Bytes)));
                    break;
                case RoPacketIds.ZcHighJump:
                    break;
                case RoPacketIds.ZcInventoryStart:
                    RoZcInventoryStart inventoryStart = RoMapPacketParser.ParseZcInventoryStart(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryStart));
                    break;
                case RoPacketIds.ZcInventoryListNormal:
                    RoZcInventoryItemList inventoryListNormal = RoMapPacketParser.ParseZcInventoryListNormal(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListNormal));
                    break;
                case RoPacketIds.ZcInventoryListNormal4:
                    RoZcInventoryItemList inventoryListNormal4 = RoMapPacketParser.ParseZcInventoryListNormal4(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListNormal4));
                    break;
                case RoPacketIds.ZcInventoryListEquip:
                    RoZcInventoryItemList inventoryListEquip = RoMapPacketParser.ParseZcInventoryListEquip(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListEquip));
                    break;
                case RoPacketIds.ZcInventoryListEquip4:
                    RoZcInventoryItemList inventoryListEquip4 = RoMapPacketParser.ParseZcInventoryListEquip4(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListEquip4));
                    break;
                case RoPacketIds.ZcInventoryListEquip5:
                    RoZcInventoryItemList inventoryListEquip5 = RoMapPacketParser.ParseZcInventoryListEquip5(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListEquip5));
                    break;
                case RoPacketIds.ZcInventoryListEquip2:
                    RoZcInventoryItemList inventoryListEquip2 = RoMapPacketParser.ParseZcInventoryListEquip2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListEquip2));
                    break;
                case RoPacketIds.ZcChangeItemOption:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcChangeItemOption(frame.Bytes)));
                    break;
                case RoPacketIds.ZcInventoryEnd:
                    RoZcInventoryEnd inventoryEnd = RoMapPacketParser.ParseZcInventoryEnd(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(inventoryEnd));
                    break;
                case RoPacketIds.ZcEquipSwitchList:
                    RoZcEquipSwitchList equipSwitchList = RoMapPacketParser.ParseZcEquipSwitchList(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(equipSwitchList));
                    break;
                case RoPacketIds.ZcBattlefieldChat:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldChat(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckEntryQueueApply:
                case RoPacketIds.ZcNotifyEntryQueueApply:
                case RoPacketIds.ZcAckEntryQueueCancel:
                case RoPacketIds.ZcNotifyEntryQueueAdmission:
                case RoPacketIds.ZcReplyAckEntryQueueAdmission:
                case RoPacketIds.ZcNotifyLobbyAdmission:
                case RoPacketIds.ZcReplyAckLobbyAdmission:
                case RoPacketIds.ZcEntryQueueReset:
                case RoPacketIds.ZcPrivateAirshipResponse:
                case RoPacketIds.ZcAllAchievementList:
                case RoPacketIds.ZcAchievementUpdate:
                case 0x0a26: // ZC_REQ_ACH_REWARD_ACK
                case 0x0a2f: // ZC_ACK_CHANGE_TITLE
                case 0x07fd: // ZC_BROADCASTING_SPECIAL_ITEM_OBTAIN
                case 0x0bba: // ZC_BROADCASTING_SPECIAL_ITEM_OBTAIN (20220518+, the refine level appended)
                case 0x0b67: // ZC_ITEM_PICKUP_PARTY (20200916+)
                case 0x0acc: // ZC_NOTIFY_EXP2 (the exp line, chat type 18)
                case 0x07f6: // ZC_NOTIFY_EXP
                case 0x02bb: // ZC_EQUIPITEM_DAMAGED (type 13)
                case 0x081f: // ZC_BROADCAST4 (a chat line with the packet's type)
                case 0x0af6: // ZC_ACK_RANKING (/blacksmith /alchemist /taekwon /pk)
                case 0x097d: // ZC_ACK_RANKING (old, names inline)
                case 0x097e: // ZC_UPDATE_RANKING_POINT
                case 0x0215: // ZC_GOSPEL_INFO (a type-8 chat line)
                case 0x0ada: // ZC_BROADCAST_ITEMREFINING_RESULT
                case 0x00c2: // ZC_USER_COUNT
                case 0x011e: // ZC_ACK_REMEMBER_WARPPOINT
                case 0x00d1: // ZC_SETTING_WHISPER_PC
                case 0x00d2: // ZC_SETTING_WHISPER_STATE
                case 0x00d4: // ZC_WHISPER_LIST
                case 0x0ae1: // ZC_SETTING_WHISPER_PC2
                case 0x01f6: // ZC_REQ_BABY
                case 0x0216: // ZC_BABYMSG
                case 0x0b2d: // ZC_REQ_ACK_AGIT_INVESTMENT
                case 0x0b2e: // ZC_REQ_ACK_MOVE_GUILD_AGIT
                case 0x0b36: // ZC_GUILD_EMBLEM_IMG
                case 0x0ae7: // ZC_ADVENTURER_AGENCY_JOIN_REQ
                case 0x02cb: // ZC_MEMORIALDUNGEON_SUBSCRIPTION_INFO
                case 0x02cc: // ZC_MEMORIALDUNGEON_SUBSCRIPTION_NOTIFY
                case 0x02cd: // ZC_MEMORIALDUNGEON_INFO
                case 0x02ce: // ZC_MEMORIALDUNGEON_NOTIFY
                case 0x098a: // ZC_CLANINFO
                case 0x0988: // ZC_NOTIFY_CLAN_CONNECTINFO
                case 0x0989: // ZC_ACK_CLAN_LEAVE
                case 0x0294: // ZC_READ_BOOK
                case 0x018c: // ZC_MONSTER_INFO
                case 0x0253: // ZC_STARPLACE
                case 0x020e: // ZC_STARSKILL
                case 0x0afa: // ZC_ADVENTURER_AGENCY_JOIN_RESULT
                case 0x0b47: // ZC_CHANGE_GUILD
                case 0x01b4: // ZC_CHANGE_GUILD (old)
                    orderedEvents.Add(new RoMapEntryDrainEvent(new RoZcEntryQueue(frame.PacketId, (byte[]) frame.Bytes.Clone())));
                    break;
                case RoPacketIds.ZcNavigationActive:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNavigationActive(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcEquipWinMicroscope:
                case RoPacketIds.ZcEquipWinMicroscopeV6:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcEquipWinMicroscope(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckWearEquipV5:
                    RoZcWearEquipmentAck wearEquipmentAck = RoMapPacketParser.ParseZcAckWearEquipV5(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(wearEquipmentAck));
                    break;
                case RoPacketIds.ZcAckTakeoffEquipV5:
                    RoZcTakeoffEquipmentAck takeoffEquipmentAck = RoMapPacketParser.ParseZcAckTakeoffEquipV5(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(takeoffEquipmentAck));
                    break;
                case RoPacketIds.ZcReqTakeoffEquipAck:
                    RoZcTakeoffEquipmentAck takeoffEquipmentAckLegacy = RoMapPacketParser.ParseZcReqTakeoffEquipAck(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(takeoffEquipmentAckLegacy));
                    break;
                case RoPacketIds.ZcReqTakeoffEquipAck2:
                    RoZcTakeoffEquipmentAck takeoffEquipmentAck2 = RoMapPacketParser.ParseZcReqTakeoffEquipAck2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(takeoffEquipmentAck2));
                    break;
                case RoPacketIds.ZcEquipArrow:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcEquipArrow(frame.Bytes)));
                    break;
                case RoPacketIds.ZcItemThrowAck:
                    RoZcItemThrowAck itemThrowAck = RoMapPacketParser.ParseZcItemThrowAck(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(itemThrowAck));
                    break;
                case RoPacketIds.ZcUseItemAck:
                    RoZcUseItemAck useItemAck = RoMapPacketParser.ParseZcUseItemAck(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(useItemAck));
                    break;
                case RoPacketIds.ZcUseItemAck2:
                    RoZcUseItemAck useItemAck2 = RoMapPacketParser.ParseZcUseItemAck2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(useItemAck2));
                    break;
                case RoPacketIds.ZcItemIdentifyList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcItemIdentifyList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckItemIdentify:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckItemIdentify(frame.Bytes)));
                    break;
                case RoPacketIds.ZcItemCompositionList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcItemCompositionList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMakingArrowList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMakingArrowList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcSelectCart:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcSelectCart(frame.Bytes)));
                    break;
                case RoPacketIds.ZcWarpList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcWarpList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcWarpList2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcWarpList2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNotifyMapInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyMapInfo(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckItemComposition:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckItemComposition(frame.Bytes)));
                    break;
                case RoPacketIds.ZcItemPickupAck7:
                    RoZcItemPickupAck itemPickupAck = RoMapPacketParser.ParseZcItemPickupAck7(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(itemPickupAck));
                    break;
                case RoPacketIds.ZcItemPickupAck8:
                    RoZcItemPickupAck itemPickupAck8 = RoMapPacketParser.ParseZcItemPickupAck8(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(itemPickupAck8));
                    break;
                case RoPacketIds.ZcParChange:
                    RoZcParameterChange parameterChange = RoMapPacketParser.ParseZcParChange(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(parameterChange));
                    break;
                case RoPacketIds.ZcLongParChange:
                    RoZcParameterChange longParameterChange = RoMapPacketParser.ParseZcLongParChange(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(longParameterChange));
                    break;
                case RoPacketIds.ZcLongLongParChange:
                    RoZcParameterChange longLongParameterChange = RoMapPacketParser.ParseZcLongLongParChange(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(longLongParameterChange));
                    break;
                case RoPacketIds.ZcStatusChangeAck:
                    RoZcStatusChangeAck statusChangeAck = RoMapPacketParser.ParseZcStatusChangeAck(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusChangeAck));
                    break;
                case RoPacketIds.ZcCoupleStatus:
                    RoZcCoupleStatus statusBaseBonus = RoMapPacketParser.ParseZcCoupleStatus(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusBaseBonus));
                    break;
                case RoPacketIds.ZcStatus:
                    RoZcStatusSnapshot statusSnapshot = RoMapPacketParser.ParseZcStatus(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusSnapshot));
                    break;
                case RoPacketIds.ZcStatusChange:
                    RoZcStatusPropertyChange statusPropertyChange = RoMapPacketParser.ParseZcStatusChange(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(statusPropertyChange));
                    break;
                case RoPacketIds.ZcNotifyWeightPercentage:
                    RoZcNotifyWeightPercentage weightPercentage = RoMapPacketParser.ParseZcNotifyWeightPercentage(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(weightPercentage));
                    break;
                case RoPacketIds.ZcAttackRange:
                    RoZcAttackRange attackRange = RoMapPacketParser.ParseZcAttackRange(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(attackRange));
                    break;
                case RoPacketIds.ZcAckToUseSkill:
                    RoZcAckToUseSkill skillUseAck = RoMapPacketParser.ParseZcAckToUseSkill(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillUseAck));
                    break;
                case RoPacketIds.ZcAttackFailureForDistance:
                    RoZcAttackFailureForDistance attackFailureForDistance = RoMapPacketParser.ParseZcAttackFailureForDistance(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(attackFailureForDistance));
                    break;
                case RoPacketIds.ZcActionFailure:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcActionFailure(frame.Bytes)));
                    break;
                case RoPacketIds.ZcUseSkillAckLegacy:
                    RoZcUseSkillAck skillCastLegacy = RoMapPacketParser.ParseZcUseSkillAckLegacy(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillCastLegacy));
                    break;
                case RoPacketIds.ZcUseSkillAck2:
                    RoZcUseSkillAck skillCast2 = RoMapPacketParser.ParseZcUseSkillAck2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillCast2));
                    break;
                case RoPacketIds.ZcDispel:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDispel(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNotifyEffect2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyEffect2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNotifyEffect3:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyEffect3(frame.Bytes)));
                    break;
                case RoPacketIds.ZcNpcShowEfstUpdate:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcShowEfstUpdate(frame.Bytes)));
                    break;
                case RoPacketIds.ZcUseSkillAck3:
                    RoZcUseSkillAck skillCast3 = RoMapPacketParser.ParseZcUseSkillAck3(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillCast3));
                    break;
                case RoPacketIds.ZcNotifySkillLegacy:
                    RoZcNotifySkill skillDamageLegacy = RoMapPacketParser.ParseZcNotifySkillLegacy(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillDamageLegacy));
                    break;
                case RoPacketIds.ZcNotifySkill2:
                    RoZcNotifySkill skillDamage2 = RoMapPacketParser.ParseZcNotifySkill2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillDamage2));
                    break;
                case RoPacketIds.ZcUseSkill:
                    RoZcUseSkillResult skillResult = RoMapPacketParser.ParseZcUseSkill(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillResult));
                    break;
                case RoPacketIds.ZcNotifyEffect:
                    RoZcNotifyEffect notifyEffect = RoMapPacketParser.ParseZcNotifyEffect(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(notifyEffect));
                    break;
                case RoPacketIds.ZcReserved0306:
                    break;
                case RoPacketIds.ZcNotifyGroundSkill:
                    RoZcNotifyGroundSkill groundSkill = RoMapPacketParser.ParseZcNotifyGroundSkill(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(groundSkill));
                    break;
                case RoPacketIds.ZcSkillDisappear:
                    RoZcSkillDisappear skillDisappear = RoMapPacketParser.ParseZcSkillDisappear(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillDisappear));
                    break;
                case RoPacketIds.ZcSkillPostDelay:
                    RoZcSkillPostDelay skillPostDelay = RoMapPacketParser.ParseZcSkillPostDelay(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillPostDelay));
                    break;
                case RoPacketIds.ZcNotifySkillPosition:
                    RoZcNotifySkillPosition skillPosition = RoMapPacketParser.ParseZcNotifySkillPosition(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillPosition));
                    break;
                case RoPacketIds.ZcSkillEntry:
                    RoZcSkillEntry skillEntryLegacy = RoMapPacketParser.ParseZcSkillEntry(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillEntryLegacy));
                    break;
                case RoPacketIds.ZcSkillEntry2:
                    RoZcSkillEntry skillEntry2 = RoMapPacketParser.ParseZcSkillEntry2(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillEntry2));
                    break;
                case RoPacketIds.ZcSkillEntry3:
                    RoZcSkillEntry skillEntry3 = RoMapPacketParser.ParseZcSkillEntry3(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillEntry3));
                    break;
                case RoPacketIds.ZcSkillEntry4:
                    RoZcSkillEntry skillEntry4 = RoMapPacketParser.ParseZcSkillEntry4(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillEntry4));
                    break;
                case RoPacketIds.ZcSkillEntry5:
                    RoZcSkillEntry skillEntry = RoMapPacketParser.ParseZcSkillEntry5(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillEntry));
                    break;
                case RoPacketIds.ZcAutoRunSkill:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.AutoRunSkill,
                        RoMapPacketParser.ParseZcAutoRunSkill(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcSkillInfoUpdate:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.SkillInfoUpdate,
                        RoMapPacketParser.ParseZcSkillInfoUpdate(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAddSkill:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.SkillInfoAdd,
                        RoMapPacketParser.ParseZcAddSkill(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcSkillInfoUpdate2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(
                        RoMapEntryDrainEventKind.SkillInfoUpdate2,
                        RoMapPacketParser.ParseZcSkillInfoUpdate2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcSkillInfoListLegacy:
                    RoZcSkillInfoList legacySkillInfoList = RoMapPacketParser.ParseZcSkillInfoListLegacy(frame.Bytes, textEncoding);
                    orderedEvents.Add(new RoMapEntryDrainEvent(legacySkillInfoList));
                    break;
                case RoPacketIds.ZcSkillInfoList:
                    RoZcSkillInfoList skillInfoList = RoMapPacketParser.ParseZcSkillInfoList(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(skillInfoList));
                    break;
                case RoPacketIds.ZcShortcutKeyList:
                    RoZcShortcutKeyList shortcutKeyList = RoMapPacketParser.ParseZcShortcutKeyList(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(shortcutKeyList));
                    break;
                case RoPacketIds.ZcQuestList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestList(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcQuestAdd:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestAdd(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcQuestDelete:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestDelete(frame.Bytes)));
                    break;
                case RoPacketIds.ZcQuestActive:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestActive(frame.Bytes)));
                    break;
                case RoPacketIds.ZcQuestObjectiveUpdateLegacy:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestObjectiveUpdateLegacy(frame.Bytes)));
                    break;
                case RoPacketIds.ZcQuestHuntInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestHuntInfo(frame.Bytes)));
                    break;
                case RoPacketIds.ZcQuestObjectiveUpdate:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestObjectiveUpdate(frame.Bytes)));
                    break;
                case RoPacketIds.ZcQuestObjectiveUpdateModern:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestObjectiveUpdateModern(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyOptionLegacy:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyOptionLegacy(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyOption:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyOption(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyCreateAck:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyCreateAck(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyConfig:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyConfig(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyLeaderChanged:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyLeaderChanged(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyInfoLegacy:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInfoLegacy(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyInfo2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInfo2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInfo(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyMemberInfoLegacy:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberInfoLegacy(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyMemberInfo2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberInfo2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyMemberInfo3:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberInfo3(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyMemberInfo:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberInfo(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyMemberLeave:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberLeave(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyPosition:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyPosition(frame.Bytes)));
                    break;
                case RoPacketIds.ZcGuildPosition:
                    orderedEvents.Add(RoMapEntryDrainEvent.Marker(RoMapPacketParser.ParseZcGuildPosition(frame.Bytes)));
                    break;
                case RoPacketIds.ZcQuestNotifyEffect:
                    orderedEvents.Add(RoMapEntryDrainEvent.Marker(RoMapPacketParser.ParseZcQuestNotifyEffect(frame.Bytes)));
                    break;
                case RoPacketIds.ZcBossInfo:
                    orderedEvents.Add(RoMapEntryDrainEvent.Marker(RoMapPacketParser.ParseZcBossInfo(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMinimapPing:
                    orderedEvents.Add(RoMapEntryDrainEvent.Marker(RoMapPacketParser.ParseZcMinimapPing(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyHpLegacy:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyHpLegacy(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyHp:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyHp(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyAlive:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyAlive(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyJobAndLevel:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyJobAndLevel(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPartyInvite:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInvite(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyInvite2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInvite2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyInviteAck:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInviteAck(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcPartyInviteAck2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInviteAck2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcFriendsList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendsList(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcFriendState:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendState(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckReqNameByGid:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameByGid(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckReqNameByGid2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameByGid2(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckCreateChatRoom:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ChatRoomCreateAck, RoMapPacketParser.ParseZcChatRoomResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcRefuseEnterRoom:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ChatRoomRefuse, RoMapPacketParser.ParseZcChatRoomResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcRoomNewEntry:
                case RoPacketIds.ZcChangeChatRoom:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcChatRoomInfo(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcDestroyRoom:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDestroyRoom(frame.Bytes)));
                    break;
                case RoPacketIds.ZcEnterRoom:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcEnterRoom(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcMemberNewEntry:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ChatRoomMemberJoin, RoMapPacketParser.ParseZcChatRoomMember(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcMemberExit:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ChatRoomMemberExit, RoMapPacketParser.ParseZcChatRoomMember(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcRoleChange:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcRoleChange(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcAckOpenBuyingStore:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreSetup, RoMapPacketParser.ParseZcBuyingStoreResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcFailedOpenBuyingStore:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreOpenFailed, RoMapPacketParser.ParseZcBuyingStoreResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcFailedTradeBuyingStoreToBuyer:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreBuyerFailed, RoMapPacketParser.ParseZcBuyingStoreResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcFailedTradeBuyingStoreToSeller:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreSellerFailed, RoMapPacketParser.ParseZcBuyingStoreResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMyItemListBuyingStore:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreOwnList, RoMapPacketParser.ParseZcBuyingStoreList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckItemListBuyingStore:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreItemList, RoMapPacketParser.ParseZcBuyingStoreList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcBuyingStoreEntry:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreEntry, RoMapPacketParser.ParseZcBuyingStoreEntry(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcDisappearBuyingStoreEntry:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreDisappear, RoMapPacketParser.ParseZcBuyingStoreEntry(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcDeleteItemFromBuyingStore: {
                    // sent instead of ZC_DELETE_ITEM_FROM_BODY (clif_buyingstore_delete_item): the inventory loses the item here too
                    RoZcBuyingStoreSold buyingStoreSold = RoMapPacketParser.ParseZcDeleteItemFromBuyingStore(frame.Bytes);
                    orderedEvents.Add(new RoMapEntryDrainEvent(buyingStoreSold));
                    orderedEvents.Add(new RoMapEntryDrainEvent(new RoZcInventoryItemDelete(0, buyingStoreSold.BagIndex, buyingStoreSold.Amount)));
                    break;
                }
                case RoPacketIds.ZcNotifyWeaponItemList:
                    orderedEvents.Add(RoMapEntryDrainEvent.WeaponRefineItems(RoMapPacketParser.ParseZcNotifyWeaponItemList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckWeaponRefine:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckWeaponRefine(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPetEggList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPetEggList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcStartCapture:
                    orderedEvents.Add(RoMapEntryDrainEvent.PetCapture(true, 0));
                    break;
                case RoPacketIds.ZcTryCaptureMonster:
                    orderedEvents.Add(RoMapEntryDrainEvent.PetCapture(false, RoMapPacketParser.ParseZcTryCaptureMonster(frame.Bytes)));
                    break;
                case RoPacketIds.ZcPetEvolutionResult:
                    orderedEvents.Add(RoMapEntryDrainEvent.PetEvolution(RoMapPacketParser.ParseZcPetEvolutionResult(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMakableItemList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMakableItemList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckReqMakingItem:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqMakingItem(frame.Bytes)));
                    break;
                case RoPacketIds.ZcMergeItemOpen:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMergeItemOpen(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckMergeItem:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckMergeItem(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAutoSpellList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAutoSpellList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcRepairItemList:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcRepairItemList(frame.Bytes)));
                    break;
                case RoPacketIds.ZcRepairItemList2:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcRepairItemList2(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAckItemRepair:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckItemRepair(frame.Bytes)));
                    break;
                case RoPacketIds.ZcUpdateItemFromBuyingStore:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcUpdateItemFromBuyingStore(frame.Bytes)));
                    break;
                case RoPacketIds.ZcFriendRequest:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendRequest(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcFriendAddResult:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendAddResult(frame.Bytes, textEncoding)));
                    break;
                case RoPacketIds.ZcFriendDelete:
                    orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendDelete(frame.Bytes)));
                    break;
                default:
                    RoNetAudit.Unhandled(frame.PacketId);
                    break;
            }
        }

        private static RoMapEntryDrainedPacket ToDrainedPacket(RoPacketFrame frame) {
            string packetName = string.IsNullOrWhiteSpace(frame.Definition.Name) ? $"0x{frame.PacketId:x4}" : frame.Definition.Name;
            return new RoMapEntryDrainedPacket(frame.PacketId, packetName, frame.Length, RoPacketFrameStatus.Complete);
        }

        private void AddRodexListEvent(RoPacketFrame frame, Func<RoZcRodexMailList> parse) {
            try {
                orderedEvents.Add(new RoMapEntryDrainEvent(parse()));
            } catch (Exception exception) {
                string message = $"RODEX inbound list parse failed packet=0x{frame.PacketId:x4} length={frame.Length}: {exception.Message}";
                RoNetworkRuntimeDiagnostics.RecordDiagnostic(
                    message,
                    frame.PacketId,
                    frame.Definition.Name,
                    RoPacketFrameStatus.Complete,
                    frame.Length,
                    RoNetworkRuntimeDiagnostics.FormatPacketPrefix(frame.Bytes, 24));
                orderedEvents.Add(RoMapEntryDrainEvent.RodexFailedList(openWindow: false));
            }
        }

        public RoMapEntryDrainResult ToResult(bool stoppedOnUnsupportedPacket, string stopReason) {
            if (RoRuntimeDiagnostics.RuntimePerfLogEnabled) {
                RoRuntimePerfCounters.AddDrainToResultSnapshot(
                    packets.Count,
                    playerChats.Count,
                    timeNotifications.Count,
                    actorEntries.Count,
                    actorMoves.Count,
                    playerMoves.Count,
                    stopMoves.Count,
                    actorVanishes.Count,
                    orderedEvents.Count);

                using (ToResultMarker.Auto()) {
                    return new RoMapEntryDrainResult(
                        packets.ToArray(),
                        playerChats.ToArray(),
                        timeNotifications.ToArray(),
                        actorEntries.ToArray(),
                        actorMoves.ToArray(),
                        playerMoves.ToArray(),
                        stopMoves.ToArray(),
                        actorVanishes.ToArray(),
                        orderedEvents.ToArray(),
                        stoppedOnUnsupportedPacket,
                        stopReason ?? string.Empty,
                        parseFailureCount,
                        parseFailureMessages.ToArray());
                }
            }

            return new RoMapEntryDrainResult(
                packets.ToArray(),
                playerChats.ToArray(),
                timeNotifications.ToArray(),
                actorEntries.ToArray(),
                actorMoves.ToArray(),
                playerMoves.ToArray(),
                stopMoves.ToArray(),
                actorVanishes.ToArray(),
                orderedEvents.ToArray(),
                stoppedOnUnsupportedPacket,
                stopReason ?? string.Empty,
                parseFailureCount,
                parseFailureMessages.ToArray());
        }
    }
}
