using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.UI.Classic;
using Ragnarok.World;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Ragnarok.UI {
    /// <summary>
    /// The classic trade (codex-ui-research RUNTIME-RULES-TRADE-CART-VENDING-CHATROOM.md "Trade and cart"):
    /// UIExchangeAcceptWnd 0x20, the request (oracle layout: win_msgbox, the name + MsgStr 0x5d and the C-Code line, OK
    /// = accept, Cancel = decline, Enter = OK, Esc ignored), and CUIExchangeUI 0x271b, the trade (560 × 377 at
    /// (scaledX(100), scaledY(100)): exchange_bg2 under the title bar, own items left and the partner's right in ten
    /// 30 px rows, the zeny field and the zeny shown, OK = offer the zeny then lock, Trade once both sides locked,
    /// Cancel; an inventory item dropped while unlocked is offered, a stack through the amount window; Esc ignored).
    /// The trade state follows the trade packets the session keeps in its live drains (ZC_REQ_EXCHANGE_ITEM,
    /// ZC_ACK_EXCHANGE_ITEM, ZC_ADD_EXCHANGE_ITEM, ZC_ACK_ADD_EXCHANGE_ITEM, ZC_CONCLUDE_EXCHANGE_ITEM,
    /// ZC_CANCEL_EXCHANGE_ITEM, ZC_EXEC_EXCHANGE_ITEM), applied by <see cref="Tick"/> when the trade's functional change
    /// sequence moves; the windows show while a request waits or a trade is open. Parts of 0x271b are not in the
    /// research yet (the title, the row drawing, the lock indicator glyph): they are drawn plainly here and marked.
    /// </summary>
    public sealed class RoClassicTrade {
        private const string TrUi = "유저인터페이스\\";
        private const string TrBasic = "유저인터페이스\\basic_interface\\";
        private const int TrOkId = 0xb4;
        private const int TrCancelId = 0xb5;
        private const int TrTradeId = 0x1f7;     // own id for the exchange button (the client's is unknown)
        private const int TrRowH = 30;
        private const int TrRows = 10;
        private const int MaxTradeItems = 10;
        private static readonly Color32 TrBlack = new Color32(0, 0, 0, 255);
        private static readonly Color32 TrWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 TrLockedFill = new Color32(0xc8, 0xc8, 0xc8, 255);

        private static RoClassicTrade instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private RoClassicUiSystem system;
        private RoLoginShellSession session;
        private IRoSocialEconomyCommandExecutor commandExecutor;
        private RoClassicChat chat;
        private RoItemMetadataTable itemMetadata = RoItemMetadataTable.Empty;
        private RoOnlineWorldController worldController;
        private int seenSequence = int.MinValue;

        // ---- the trade state (from the packets) ----
        private readonly List<TradeItemModel> selfItems = new List<TradeItemModel>(MaxTradeItems);
        private readonly List<TradeItemModel> partnerItems = new List<TradeItemModel>(MaxTradeItems);
        private readonly List<RoOnlineActorSnapshot> actorSnapshotScratch = new List<RoOnlineActorSnapshot>(64);
        private readonly Dictionary<int, PendingTradeItem> pendingItems = new Dictionary<int, PendingTradeItem>();
        private int lastAppliedLiveDrainSequence = int.MinValue;
        private long lastAppliedFunctionalSequence = long.MinValue;
        private bool isOpen;
        private bool hasPendingIncomingRequest;
        private string pendingIncomingName = string.Empty;
        private uint pendingIncomingTargetId;
        private short pendingIncomingLevel;
        private string partnerName = string.Empty;
        private uint partnerId;
        // the actor the last own request went to: ZC_ACK_EXCHANGE_ITEM2 names the partner by character id, not AID
        private uint requestedTargetId;
        private string requestedTargetName = string.Empty;
        private short partnerLevel;
        private bool selfLocked;
        private bool partnerLocked;
        private bool tradeSubmitted;
        private int selfZeny;
        private int partnerZeny;
        private int pendingZeny;

        // ---- the windows ----
        private RoClassicWindow classicTrade;
        private RoClassicWindow classicTradeRequest;
        private RoClassicEditField tradeZenyEdit;
        private uint classicRequestShownFor;
        private string classicRequestShownName;
        private Func<bool> tradeRequestEnterClaim;
        private int tradeRequestOpenedFrame;
        private bool classicTradeWasOpen;
        private bool classicLockSent;
        private bool classicRequestAnswering;

        /// <summary>Whether a trade is open (CUIExchangeUI up).</summary>
        public static bool IsOpen => instance != null && instance.isOpen;

        private static RoClassicTrade Ensure() {
            RoClassicUiSystem system = RoClassicUiSystem.Instance;
            if (system == null) return null;
            if (instance == null || instance.system != system) instance = new RoClassicTrade { system = system };
            return instance;
        }

        /// <summary>
        /// Per frame (RoClassicGameUi): when the trade's change sequence moves, the new trade packets are applied and the
        /// request / trade windows follow; Enter answers the request, the hovered row shows its item.
        /// </summary>
        public static void Tick(RoLoginShellSession loginSession, RoClassicChat chatWindow, RoItemMetadataTable metadata) {
            if (loginSession == null) return;
            RoClassicTrade t = Ensure();
            if (t == null) return;
            if (!ReferenceEquals(t.session, loginSession)) t.Bind(loginSession);
            t.chat = chatWindow;
            metadata ??= RoItemMetadataTable.Empty;
            if (!ReferenceEquals(t.itemMetadata, metadata)) {
                t.itemMetadata = metadata;
                t.classicTrade?.Invalidate();
            }

            int sequence = loginSession.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Trade);
            if (sequence != t.seenSequence) {
                t.seenSequence = sequence;
                t.RefreshNow();
            }

            t.TickClassicTrade();
        }

        /// <summary>Leaving the map: the request and the trade windows go and the trade state is dropped (the packets already applied stay applied).</summary>
        public static void Close() {
            RoClassicTrade t = instance;
            if (t == null) return;
            t.ResetTradeState();
            t.CloseClassicTradeRequest();
            t.FollowClassicTrade();
        }

        /// <summary>A new session: the state from the start, its trade packets applied from the first one.</summary>
        private void Bind(RoLoginShellSession loginSession) {
            session = loginSession;
            commandExecutor = new RoLegacySocialEconomyCommandExecutor(loginSession);
            worldController = null;
            ResetTradeState();
            lastAppliedLiveDrainSequence = int.MinValue;
            lastAppliedFunctionalSequence = long.MinValue;
            seenSequence = int.MinValue;
            CloseClassicTradeRequest();
            FollowClassicTrade();
        }

        /// <summary>The trade packets not applied yet, then the request and the trade windows.</summary>
        private void RefreshNow() {
            ApplyTradeEvents();
            FollowClassicRequest();
            FollowClassicTrade();
        }

        // ---- the trade packets ----

        private void ApplyTradeEvents() {
            if (session == null) return;
            RoShellFunctionalWindowSnapshot snapshot = session.FunctionalWindowEventSnapshot();
            bool appliedSequenced = false;
            RoLiveDrainSnapshot[] recent = snapshot.RecentLiveDrains ?? Array.Empty<RoLiveDrainSnapshot>();
            for (int i = 0; i < recent.Length; i++) {
                RoLiveDrainSnapshot live = recent[i];
                if (live.Sequence <= lastAppliedLiveDrainSequence) continue;
                ApplyTradeDrain(live.Drain);
                lastAppliedLiveDrainSequence = live.Sequence;
                appliedSequenced = true;
            }

            long functionalSequence = session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Trade);
            if (!appliedSequenced && functionalSequence != lastAppliedFunctionalSequence) ApplyTradeDrain(snapshot.LatestLiveDrain);
            lastAppliedFunctionalSequence = functionalSequence;
        }

        private void ApplyTradeDrain(RoMapEntryDrainResult drain) {
            if (drain?.OrderedEvents == null) return;
            RoMapEntryDrainEvent[] events = drain.OrderedEvents;
            for (int i = 0; i < events.Length; i++) ApplyTradeEvent(events[i]);
        }

        private void ApplyTradeEvent(RoMapEntryDrainEvent drainEvent) {
            switch (drainEvent.Kind) {
                case RoMapEntryDrainEventKind.TradeRequest:
                    if (RoChatSlashCommands.GetOption("/notrade", null)) {
                        // /notrade (0x01552d70, 0xc2e7b5): no request window, mode Msg(0x32, 4) declines at once
                        if (commandExecutor != null) _ = commandExecutor.AnswerTradeRequestAsync(false);
                        break;
                    }

                    hasPendingIncomingRequest = true;
                    pendingIncomingName = drainEvent.TradeRequest.PartnerName ?? string.Empty;
                    pendingIncomingTargetId = drainEvent.TradeRequest.PartnerId;
                    pendingIncomingLevel = drainEvent.TradeRequest.PartnerLevel;
                    break;
                case RoMapEntryDrainEventKind.TradeAck:
                    ApplyTradeAck(drainEvent.TradeAck);
                    break;
                case RoMapEntryDrainEventKind.TradeAddItemAck:
                    ApplyTradeAddItemAck(drainEvent.TradeAddItemAck);
                    break;
                case RoMapEntryDrainEventKind.TradeItemAdd:
                    ApplyPartnerTradeItem(drainEvent.TradeItem);
                    break;
                case RoMapEntryDrainEventKind.TradeConclude:
                    if (drainEvent.TradeConclude.RemoteSide) {
                        partnerLocked = true;
                    } else {
                        selfLocked = true;
                    }

                    break;
                case RoMapEntryDrainEventKind.TradeCancel:
                    ResetTradeState();
                    ClassicTradeMessage(0x4a, "The deal has been canceled.", "#FF0000");
                    break;
                case RoMapEntryDrainEventKind.TradeSubmit:
                    ResetTradeState();
                    if (drainEvent.TradeSubmit.Result == 0) ClassicTradeMessage(0x4b, "The deal has successfully completed.", "#00FFFF");
                    else ClassicTradeMessage(0x4c, "The deal has failed.", "#FF0000");
                    break;
            }
        }

        /// <summary>ZC_ACK_EXCHANGE_ITEM(2): 3 opens the trade with the partner, the other results end the request.</summary>
        private void ApplyTradeAck(RoZcTradeAck ack) {
            if (ack.Result == 3) {
                string resolvedName = ResolvePartnerName(ack.PartnerId);
                uint resolvedId = ack.PartnerId != 0 ? ack.PartnerId : pendingIncomingTargetId != 0 ? pendingIncomingTargetId : partnerId;
                short resolvedLevel = ack.PartnerLevel != 0 ? ack.PartnerLevel : pendingIncomingLevel;
                ResetTradeState();
                isOpen = true;
                partnerName = string.IsNullOrWhiteSpace(resolvedName) ? "Unknown" : resolvedName;
                partnerId = resolvedId;
                partnerLevel = resolvedLevel;
                return;
            }

            hasPendingIncomingRequest = false;
            ClassicTradeRequestResult(ack.Result);
        }

        /// <summary>ZC_ACK_ADD_EXCHANGE_ITEM: index 0 is the zeny offer; an accepted item joins the own list, 2 ends the trade.</summary>
        private void ApplyTradeAddItemAck(RoZcTradeAddItemAck ack) {
            if (ack.InventoryIndex == 0) {
                if (ack.Result == 0) selfZeny = pendingZeny;
                pendingZeny = 0;
                ClassicTradeAddResult(ack.Result);
                return;
            }

            int key = ack.InventoryIndex;
            if (!pendingItems.TryGetValue(key, out PendingTradeItem pending) && key >= 2) pendingItems.TryGetValue(key - 2, out pending);
            if (pending.Item != null) pendingItems.Remove(pending.Item.BagIndex);
            pendingItems.Remove(key);
            if (key >= 2) pendingItems.Remove(key - 2);
            if (ack.Result == 0 && pending.Item != null) {
                selfItems.Add(new TradeItemModel(pending.Item, pending.Amount));
            } else if (ack.Result == 2) {
                ResetTradeState();
            }

            ClassicTradeAddResult(ack.Result);
        }

        /// <summary>ZC_ADD_EXCHANGE_ITEM: the partner's item (item id 0: the partner's zeny).</summary>
        private void ApplyPartnerTradeItem(RoZcTradeItem item) {
            isOpen = true;
            if (item.ItemId == 0) {
                partnerZeny = item.Count;
                return;
            }

            partnerItems.Add(new TradeItemModel(ToItemInstance(new RoEconomyItemSnapshot(
                0,
                item.ItemId,
                item.ItemTypeRaw,
                item.Count,
                item.IsIdentified,
                item.IsDamaged,
                item.RefineLevel,
                item.CardIds,
                RoEconomyItemSnapshot.IsKnownEquipmentItemType(item.ItemTypeRaw))), item.Count));
        }

        private void ResetTradeState() {
            isOpen = false;
            hasPendingIncomingRequest = false;
            pendingIncomingName = string.Empty;
            pendingIncomingTargetId = 0;
            pendingIncomingLevel = 0;
            partnerName = string.Empty;
            partnerId = 0;
            partnerLevel = 0;
            selfLocked = false;
            partnerLocked = false;
            tradeSubmitted = false;
            selfZeny = 0;
            partnerZeny = 0;
            pendingZeny = 0;
            selfItems.Clear();
            partnerItems.Clear();
            pendingItems.Clear();
        }

        // ---- commands ----

        private async Task AnswerTradeAsync(bool accept) {
            RefreshNow();
            if (!hasPendingIncomingRequest || commandExecutor == null) return;
            RoEconomyCommandResult result = await commandExecutor.AnswerTradeRequestAsync(accept);
            if (!result.Accepted) Debug.LogWarning("Trade answer was not sent: " + result.Message);
        }

        private async Task ConfirmTradeAsync() {
            RefreshNow();
            if (!isOpen || !selfLocked || !partnerLocked || commandExecutor == null) return;
            RoEconomyCommandResult result = await commandExecutor.ConfirmTradeAsync();
            tradeSubmitted = result.Accepted;
            if (classicTrade != null) FollowClassicTrade();
        }

        private async Task CancelTradeAsync() {
            RefreshNow();
            if ((!isOpen && !hasPendingIncomingRequest) || commandExecutor == null) return;
            RoEconomyCommandResult result = await commandExecutor.CancelTradeAsync();
            if (result.Accepted) {
                ResetTradeState();
                FollowClassicRequest();
                FollowClassicTrade();
            }
        }

        /// <summary>
        /// Msg(0x33, index, amount): CZ_ADD_EXCHANGE_ITEM while unlocked, at most ten kinds, each inventory slot once; the
        /// item is kept pending until ZC_ACK_ADD_EXCHANGE_ITEM.
        /// </summary>
        private async Task AddTradeItemAsync(int bagIndex, int amount) {
            RefreshNow();
            if (!isOpen || selfLocked || amount <= 0) return;
            if (selfItems.Count + pendingItems.Count >= MaxTradeItems || pendingItems.ContainsKey(bagIndex) || HasOfferedItem(bagIndex)) return;
            if (commandExecutor == null) return;
            RoItemInstance item = TryFindInventoryItem(bagIndex, out RoItemInstance found)
                ? found
                : RoItemInstance.CreateRegular(bagIndex, 0, amount);
            int max = item.ItemId != 0 ? Math.Max(1, item.Count) : amount;
            int clampedAmount = Mathf.Clamp(amount, 1, max);
            pendingItems[bagIndex] = new PendingTradeItem(item, clampedAmount);
            RoEconomyCommandResult result = await commandExecutor.AddTradeItemAsync(bagIndex, clampedAmount);
            if (!result.Accepted) pendingItems.Remove(bagIndex);
            if (classicTrade != null) classicTrade.Invalidate();
        }

        private bool TryFindInventoryItem(int bagIndex, out RoItemInstance item) {
            item = null;
            RoGameplayInventorySnapshot inventory = session != null ? session.InventorySnapshot() : RoGameplayInventorySnapshot.Empty;
            RoItemInstance[] items = inventory.InventoryItems ?? Array.Empty<RoItemInstance>();
            for (int i = 0; i < items.Length; i++) {
                if (items[i] != null && items[i].BagIndex == bagIndex) {
                    item = items[i];
                    return true;
                }
            }

            return false;
        }

        private bool HasOfferedItem(int bagIndex) {
            for (int i = 0; i < selfItems.Count; i++) {
                RoItemInstance item = selfItems[i].Item;
                if (item == null) continue;
                if (item.BagIndex == bagIndex ||
                    (bagIndex >= 2 && item.BagIndex == bagIndex - 2) ||
                    (item.BagIndex >= 2 && item.BagIndex - 2 == bagIndex)) {
                    return true;
                }
            }

            return false;
        }

        private static RoItemInstance ToItemInstance(RoEconomyItemSnapshot item) {
            int itemId = unchecked((int) item.ItemId);
            if (item.IsEquipment || RoEconomyItemSnapshot.IsKnownEquipmentItemType(item.ItemTypeRaw)) {
                return RoItemInstance.CreateEquipment(
                    item.Index,
                    itemId,
                    Math.Max(1, item.Count),
                    item.IsIdentified,
                    refineLevel: item.RefineLevel,
                    cardIds: item.CardIds,
                    itemTypeRaw: item.ItemTypeRaw,
                    isDamaged: item.IsDamaged);
            }

            return RoItemInstance.CreateRegular(
                item.Index,
                itemId,
                item.Count,
                item.IsIdentified,
                itemTypeRaw: item.ItemTypeRaw,
                isDamaged: item.IsDamaged);
        }

        /// <summary>
        /// An own CZ_REQ_EXCHANGE_ITEM went to this player (the player menu): ZC_ACK_EXCHANGE_ITEM2 names the partner only by
        /// character id, so the trade window takes the name from the request, as the client keeps it (it showed "Unknown").
        /// </summary>
        public static void NoteOutgoingRequest(uint actorId, string name) {
            RoClassicTrade t = Ensure();
            if (t == null) return;
            t.requestedTargetId = actorId;
            t.requestedTargetName = name ?? string.Empty;
        }

        private string ResolvePartnerName(uint id) {
            if (!string.IsNullOrWhiteSpace(pendingIncomingName)) return pendingIncomingName;
            if (!string.IsNullOrWhiteSpace(partnerName)) return partnerName;
            if (!string.IsNullOrWhiteSpace(requestedTargetName)) return requestedTargetName;
            string name = FindActorName(id);
            return name.Length > 0 ? name : FindActorName(requestedTargetId);
        }

        private string FindActorName(uint id) {
            if (id == 0) return string.Empty;
            if (worldController == null) worldController = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            if (worldController == null) return string.Empty;
            worldController.SnapshotActors(actorSnapshotScratch);
            for (int i = 0; i < actorSnapshotScratch.Count; i++) {
                RoOnlineActorSnapshot actor = actorSnapshotScratch[i];
                if ((actor.ServerId == id || actor.AccountId == id) && !string.IsNullOrWhiteSpace(actor.Name)) return actor.Name;
            }

            return string.Empty;
        }

        // ---- 0x20 the request ----

        private void FollowClassicRequest() {
            if (!hasPendingIncomingRequest || isOpen || classicRequestAnswering) {
                CloseClassicTradeRequest();
                return;
            }

            string name = pendingIncomingName ?? string.Empty;
            if (classicTradeRequest != null && classicRequestShownFor == pendingIncomingTargetId && classicRequestShownName == name) return;
            if (classicTradeRequest == null) {
                var asset = Resources.Load<TextAsset>("Ragnarok/Generated/OriginalClient/ui/UIExchangeAcceptWnd");
                if (asset == null) return;
                RoClassicWindowLayout layout = RoClassicWindowLayout.Parse(asset.text);
                foreach (RoClassicChildLayout child in layout.Children) {
                    if (child.Class != "UITextViewer") continue;
                    child.Fields["text"] = "Message";
                    child.Ops.RemoveAll(op => op.Kind == RoClassicOpKind.Text); // the captured "deal" placeholder
                }

                classicTradeRequest = new RoClassicWindow(system, layout);
                classicTradeRequest.Clicked += child => {
                    if (child.Id == TrOkId) AnswerClassicTradeRequest(true);
                    else if (child.Id == TrCancelId) AnswerClassicTradeRequest(false);
                };
                Vector2Int client = system.ClientSize;
                classicTradeRequest.MoveTo((client.x - 640) / 2 + 200, 200 * client.y / 480);
                tradeRequestEnterClaim ??= () => classicTradeRequest != null && classicTradeRequest.Visible;
                RoClassicTextInput.AddKeyboardClaim(tradeRequestEnterClaim);
                tradeRequestOpenedFrame = Time.frameCount;
            }

            // msg 0x32: name + MsgStr 0x5d, then "C-Code : <encoded id>     <level>" (the exact composition is unconfirmed)
            classicRequestShownFor = pendingIncomingTargetId;
            classicRequestShownName = name;
            string text = name + RoClassicUiHost.MsgStr(0x5d, " requests a deal.");
            if (pendingIncomingTargetId != 0 || pendingIncomingLevel != 0) {
                text += "\nC-Code : " + RoClassicEscMenu.EncodeCharacterId((int) pendingIncomingTargetId) + "     Lv." +
                    pendingIncomingLevel.ToString(CultureInfo.InvariantCulture);
            }

            classicTradeRequest.Set("Message", text);
            classicTradeRequest.Visible = true;
            system.Windows.BringToFront(classicTradeRequest);
            classicTradeRequest.Invalidate();
        }

        /// <summary>OK → CZ_ACK_EXCHANGE_ITEM 3 (accept), Cancel → 4 (decline).</summary>
        private void AnswerClassicTradeRequest(bool accept) {
            CloseClassicTradeRequest();
            _ = AnswerClassicTradeRequestAsync(accept);
        }

        private async Task AnswerClassicTradeRequestAsync(bool accept) {
            classicRequestAnswering = true;
            try {
                await AnswerTradeAsync(accept);
            } finally {
                hasPendingIncomingRequest = false; // a new ZC_REQ_EXCHANGE_ITEM sets it again
                classicRequestAnswering = false;
            }
        }

        private void CloseClassicTradeRequest() {
            if (classicTradeRequest == null) return;
            RoClassicTextInput.RemoveKeyboardClaim(tradeRequestEnterClaim);
            classicTradeRequest.Destroy();
            classicTradeRequest = null;
            classicRequestShownFor = 0;
            classicRequestShownName = null;
        }

        // ---- 0x271b the trade ----

        private void FollowClassicTrade() {
            if (!isOpen) {
                if (classicTradeWasOpen) {
                    classicTradeWasOpen = false;
                    classicLockSent = false;
                    if (tradeZenyEdit != null && tradeZenyEdit.HasFocus) RoClassicTextInput.Focus(null);
                    if (classicTrade != null) classicTrade.Visible = false;
                }

                return;
            }

            EnsureClassicTradeWindow();
            if (!classicTradeWasOpen) {
                // msg 0x32 after the window is (re)made: lists and zeny reset, Trade disabled, the field takes the keyboard
                classicTradeWasOpen = true;
                classicLockSent = false;
                Vector2Int client = system.ClientSize;
                classicTrade.MoveTo(Mathf.Clamp((client.x - 640) / 2 + 100, 0, Math.Max(0, client.x - classicTrade.Width)),
                    Mathf.Clamp(100 * client.y / 480, 0, Math.Max(0, client.y - classicTrade.Height)));
                tradeZenyEdit.SetText("0");
                tradeZenyEdit.Focus();
                classicTrade.Visible = true;
                system.Windows.BringToFront(classicTrade);
            }

            SkinTradeButtons();
            classicTrade.Invalidate();
        }

        private void EnsureClassicTradeWindow() {
            if (classicTrade != null) return;
            var layout = new RoClassicWindowLayout { Class = "CUIExchangeUI", WindowId = 0x271b, Width = 560, Height = 377 };
            RoClassicFrames.TitleBar(layout.Ops, 560, false);
            RoClassicFrames.Bitmap(layout.Ops, TrBasic + "exchange_bg2.bmp", 0, 17);
            layout.Children.Add(TrButton(TrOkId, "ok", 3, 354, "btn_ok"));
            layout.Children.Add(TrButton(TrTradeId, "trade", 260, 354, "btn_exchange"));
            layout.Children.Add(TrButton(TrCancelId, "cancel", 510, 354, "btn_cancel"));
            classicTrade = new RoClassicWindow(system, layout) { Visible = false };
            classicTrade.DrawDynamic += DrawClassicTrade;
            classicTrade.Clicked += OnClassicTradeClicked;
            classicTrade.Dropped += OnClassicTradeDropped;
            classicTrade.RightClicked += (w, local) => {
                RoItemInstance item = ClassicTradeItemAt(local);
                if (item != null) RoClassicItemDescription.Toggle(item, itemMetadata);
            };
            // +0xd4: the zeny field (140, 330, 100, 16), digits only, "0" at the start
            tradeZenyEdit = new RoClassicEditField { Rect = new RectInt(140, 330, 100, 16), MaxBytes = 10, Background = new Color32(255, 255, 255, 255) };
            tradeZenyEdit.Changed += field => {
                string digits = FilterDigits(field.Text);
                if (digits != field.Text) field.SetText(digits);
            };
            classicTrade.AddEditField(tradeZenyEdit);
        }

        private static RoClassicChildLayout TrButton(int id, string path, int x, int y, string bitmap) {
            var button = new RoClassicChildLayout { Class = "UIBitmapButton", Id = id, Path = path, X = x, Y = y, W = 42, H = 20 };
            button.States["normal"] = TrUi + bitmap + ".bmp";
            button.States["hover"] = TrUi + bitmap + "_a.bmp";
            button.States["pressed"] = TrUi + bitmap + "_b.bmp";
            return button;
        }

        /// <summary>OK disabled once locked (lambda 7), Trade enabled only with both sides locked (msg 0x33 / 0x34), Cancel after it was sent.</summary>
        private void SkinTradeButtons() {
            foreach (RoClassicChildLayout child in classicTrade.Layout.Children) {
                bool enabled = child.Id == TrOkId ? !selfLocked && !classicLockSent
                    : child.Id == TrTradeId ? selfLocked && partnerLocked && !tradeSubmitted
                    : true;
                string bitmap = child.Id == TrOkId ? "btn_ok" : child.Id == TrTradeId ? "btn_exchange" : "btn_cancel";
                string normal = TrUi + bitmap + (enabled ? ".bmp" : "_dis.bmp");
                if (child.States["normal"] == normal) continue;
                child.States["normal"] = normal;
                child.States["hover"] = enabled ? TrUi + bitmap + "_a.bmp" : normal;
                child.States["pressed"] = enabled ? TrUi + bitmap + "_b.bmp" : normal;
                classicTrade.Invalidate();
            }

            tradeZenyEdit.Visible = !selfLocked && !classicLockSent;
        }

        private static string FilterDigits(string text) {
            var s = new StringBuilder(text.Length);
            foreach (char ch in text) {
                if (ch >= '0' && ch <= '9') s.Append(ch);
            }

            return s.ToString();
        }

        private void DrawClassicTrade(RoClassicWindow w) {
            string title = RoClassicUiHost.MsgStr(0xce, "Trade");
            w.TextOut(19, 4, title, 0, 12, TrWhite);
            w.TextOut(18, 3, title, 0, 12, TrBlack);
            if (selfLocked) w.FillRect(4, 21, 272, TrRows * TrRowH, TrLockedFill);
            if (partnerLocked) w.FillRect(283, 21, 272, TrRows * TrRowH, TrLockedFill);
            DrawTradeRows(w, 4, selfItems);
            DrawTradeRows(w, 283, partnerItems);

            // the zeny: the own offer once locked (the field before), the partner's offer (+0xdc), right-aligned in 150 px
            RoClassicGdiFont font = w.FontFor(0, 12);
            if (selfLocked || classicLockSent) {
                string own = RoClassicItemText.Commas(selfZeny);
                w.TextOut(69 + 150 - font.Measure(own), 331, own, 0, 12, TrBlack);
            }

            string other = RoClassicItemText.Commas(partnerZeny);
            w.TextOut(349 + 150 - font.Measure(other), 331, other, 0, 12, TrBlack);

            // +0xec: the partner (the label's exact text and the +0xe8 lock indicator glyph are unconfirmed)
            string partner = (partnerName ?? string.Empty) + (partnerLevel > 0 ? " Lv." + partnerLevel.ToString(CultureInfo.InvariantCulture) : string.Empty);
            w.ClipTo(123, 356, 135, 14);
            w.TextOut(123, 356, partner, 0, 12, TrBlack);
            w.ResetClip();
            w.FillRect(105, 359, 10, 10, partnerLocked ? new Color32(0x10, 0xb0, 0x10, 255) : new Color32(0xc8, 0xc8, 0xc8, 255));
        }

        private void DrawTradeRows(RoClassicWindow w, int x, List<TradeItemModel> items) {
            for (int r = 0; r < TrRows && r < items.Count; r++) {
                RoItemInstance item = items[r].Item;
                if (item == null) continue;
                int y = 21 + TrRowH * r;
                RoClassicItemText.DrawIcon(w, x + 3, y + 3, item, itemMetadata, items[r].Amount);
                RoClassicItemText.DrawName(w, x + 34, y + 8, 230, item, itemMetadata);
            }
        }

        private RoItemInstance ClassicTradeItemAt(Vector2Int local) {
            List<TradeItemModel> items = local.x >= 4 && local.x < 276 ? selfItems : local.x >= 283 && local.x < 555 ? partnerItems : null;
            if (items == null || local.y < 21 || local.y >= 21 + TrRows * TrRowH) return null;
            int r = (local.y - 21) / TrRowH;
            return r < items.Count ? items[r].Item : null;
        }

        private void OnClassicTradeClicked(RoClassicChildLayout child) {
            switch (child.Id) {
                case TrOkId:
                    if (!selfLocked && !classicLockSent) _ = ClassicLockTradeAsync();
                    break;
                case TrTradeId:
                    if (selfLocked && partnerLocked && !tradeSubmitted) _ = ConfirmTradeAsync();
                    break;
                case TrCancelId:
                    _ = CancelTradeAsync();
                    break;
            }
        }

        /// <summary>
        /// Lambda 7: the zeny field clamped to the zeny held (Msg(0x33, 0, zeny)), then the lock (Msg(0x34)); OK goes
        /// disabled.
        /// </summary>
        private async Task ClassicLockTradeAsync() {
            if (commandExecutor == null || !isOpen) return;
            classicLockSent = true;
            SkinTradeButtons();
            long.TryParse(FilterDigits(tradeZenyEdit.Text), NumberStyles.Integer, CultureInfo.InvariantCulture, out long zeny);
            long held = session != null ? session.BasicInfoSnapshot().Status.Zeny : 0;
            zeny = Math.Max(0, Math.Min(Math.Min(zeny, held), 1000000000L));
            if (zeny > 0) {
                pendingZeny = (int) zeny;
                RoEconomyCommandResult zenyResult = await commandExecutor.AddTradeZenyAsync((int) zeny);
                if (!zenyResult.Accepted) pendingZeny = 0;
            }

            await commandExecutor.LockTradeAsync();
        }

        /// <summary>vf10 0x9653a0: an inventory item while unlocked is offered (Msg(0x33, index, amount)); a stack asks the amount.</summary>
        private void OnClassicTradeDropped(RoClassicWindow window, Vector2Int local, IRoClassicDragPayload payload) {
            if (!(payload is RoClassicItemPayload carried) || carried.Item == null || carried.Item.IsEquipped || selfLocked || classicLockSent || !isOpen) return;
            RoItemInstance item = carried.Item;
            if (item.Count <= 1) {
                _ = AddTradeItemAsync(item.BagIndex, 1);
                return;
            }

            RoClassicCountWindow.Show(item.GetFullDisplayName(itemMetadata), item.Count, n => {
                n = Math.Min(n, item.Count);
                if (n >= 1) _ = AddTradeItemAsync(item.BagIndex, (int) n);
            });
        }

        /// <summary>Per frame: Enter answers the request with OK (its default button, through the Enter chain); the hovered row's item.</summary>
        private void TickClassicTrade() {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (classicTradeRequest != null && classicTradeRequest.Visible && keyboard != null && tradeRequestOpenedFrame != Time.frameCount &&
                !RoClassicMessageBox.ModalOpen && RoClassicTextInput.Focused == null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) {
                AnswerClassicTradeRequest(true);
            }
#endif
            if (classicTrade == null || !classicTrade.Visible) return;
            RoClassicWindowManager windows = system.Windows;
            if (windows == null || windows.HoverWindow != classicTrade || windows.Dragging != null) return;
            Vector2Int local = windows.Mouse - classicTrade.Position;
            RoItemInstance item = ClassicTradeItemAt(local);
            if (item == null) return;
            int count = 0;
            foreach (TradeItemModel model in local.x < 280 ? selfItems : partnerItems) {
                if (model.Item == item) count = model.Amount;
            }

            RoClassicTooltip.Shared.Show(string.Format(CultureInfo.InvariantCulture, "{0}: {1} ea.", item.GetFullDisplayName(itemMetadata), count),
                RoClassicItemText.TooltipColor(item, itemMetadata), classicTrade.Position.x + local.x + 8, classicTrade.Position.y + local.y - 20);
        }

        // ---- the client's trade lines in the chat ----

        private void ClassicTradeMessage(int id, string fallback, string color, string name = null) {
            string text = RoClassicUiHost.MsgStr(id, fallback);
            if (name != null) {
                int at = text.IndexOf("%s", StringComparison.Ordinal);
                if (at >= 0) text = text.Substring(0, at) + name + text.Substring(at + 2);
            }

            chat?.AddLine(text, color);
        }

        /// <summary>ZC_ACK_EXCHANGE_ITEM2 (0xc3e800): 0 too far 0x46, 1 not online 0x47, 2 in a deal 0x48, 4 rejected 0x45, 5 busy 0x6ac.</summary>
        private void ClassicTradeRequestResult(int result) {
            switch (result) {
                case 0: ClassicTradeMessage(0x46, "You are too far away from the person to trade.", "#FF0000"); break;
                case 1: ClassicTradeMessage(0x47, "The Character is not currently online or does not exist.", "#FF0000"); break;
                case 2: ClassicTradeMessage(0x48, "The person is in another deal.", "#FF0000"); break;
                case 4: ClassicTradeMessage(0x45, "The deal has been rejected.", "#FF0000"); break;
                case 5: ClassicTradeMessage(0x6ac, "[%s] is currently on trade and cannot accept the request.", "#FF0000", partnerName); break;
            }
        }

        /// <summary>ZC_ACK_ADD_EXCHANGE_ITEM (0xc3e380): 1 over the weight limit 0x49, 3 / 4 the partner's item counts 0x792 / 0x793.</summary>
        private void ClassicTradeAddResult(int result) {
            switch (result) {
                case 1: ClassicTradeMessage(0x49, "You cannot trade because this character will exceed his weight limit.", "#FF0000"); break;
                case 3: ClassicTradeMessage(0x792, "Relative character that has possession of the items cannot trade because amount is exceeded..", "#FF0000"); break;
                case 4: ClassicTradeMessage(0x793, "Relative character that has possession of the item amount which exceeds makes it impossible to trade.", "#FF0000"); break;
            }
        }

        // ---- diagnostics console (RoSkillSweepRunner "!trade ...") ----

        /// <summary>request aid | yes | no | zeny n | add bagIndex n | ok | trade | cancel | state.</summary>
        internal static string Sweep(string command) {
            RoClassicTrade t = instance;
            return t == null ? "no classic trade (not in game)" : t.SweepClassicTrade(command);
        }

        private string SweepClassicTrade(string command) {
            string[] a = (command ?? string.Empty).Trim().Split(' ');
            int Arg(int i) => i < a.Length && int.TryParse(a[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
            switch (a[0]) {
                case "yes":
                case "no":
                    if (classicTradeRequest != null) AnswerClassicTradeRequest(a[0] == "yes");
                    break;
                case "zeny":
                    tradeZenyEdit?.SetText(Arg(1).ToString(CultureInfo.InvariantCulture));
                    break;
                case "ok":
                    if (classicTrade != null) OnClassicTradeClicked(new RoClassicChildLayout { Id = TrOkId });
                    break;
                case "trade":
                    if (classicTrade != null) OnClassicTradeClicked(new RoClassicChildLayout { Id = TrTradeId });
                    break;
                case "cancel":
                    if (classicTrade != null) OnClassicTradeClicked(new RoClassicChildLayout { Id = TrCancelId });
                    break;
                case "add":
                    // add <inventory bag index> <amount>: as if dropped
                    if (isOpen && !selfLocked) _ = AddTradeItemAsync(Arg(1), Math.Max(1, Arg(2)));
                    break;
                case "request":
                    // request <aid>: CZ_REQ_EXCHANGE_ITEM
                    if (commandExecutor != null) {
                        requestedTargetId = (uint) Arg(1);
                        requestedTargetName = FindActorName(requestedTargetId);
                        _ = commandExecutor.RequestTradeAsync(requestedTargetId, requestedTargetName);
                    }
                    break;
            }

            RefreshNow();
            var s = new StringBuilder();
            s.Append("classic=").Append(true).Append(" request=").Append(classicTradeRequest != null ? classicRequestShownName : "-")
                .Append(" open=").Append(isOpen).Append(" partner=").Append(partnerName).Append(" lv=").Append(partnerLevel)
                .Append(" locks=").Append(selfLocked ? 1 : 0).Append('/').Append(partnerLocked ? 1 : 0).Append(" sent=").Append(classicLockSent)
                .Append(" zeny=").Append(selfZeny).Append('/').Append(partnerZeny).Append(" mine=");
            foreach (TradeItemModel m in selfItems) s.Append(m.Item?.ItemId).Append('x').Append(m.Amount).Append(' ');
            s.Append("theirs=");
            foreach (TradeItemModel m in partnerItems) s.Append(m.Item?.ItemId).Append('x').Append(m.Amount).Append('+').Append(m.Item?.RefineLevel).Append(' ');
            if (classicTrade != null) s.Append("pos=").Append(classicTrade.Position);
            return s.ToString().TrimEnd();
        }

        private readonly struct PendingTradeItem {
            public PendingTradeItem(RoItemInstance item, int amount) {
                Item = item;
                Amount = Math.Max(1, amount);
            }

            public RoItemInstance Item { get; }
            public int Amount { get; }
        }

        private readonly struct TradeItemModel {
            public TradeItemModel(RoItemInstance item, int amount) {
                Item = item;
                Amount = Math.Max(1, amount);
            }

            public RoItemInstance Item { get; }
            public int Amount { get; }
        }
    }
}
