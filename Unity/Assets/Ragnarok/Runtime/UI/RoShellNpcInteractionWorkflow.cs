using System;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Gameplay;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellNpcInteractionWorkflow {
        private readonly RoShellGameplayPacketWorkflow gameplayPacketWorkflow;
        private readonly Func<int> legacyTextCodePageLocked;
        private readonly Func<RoServerProfile> profileLocked;
        private readonly Action<uint> advanceNpcDialogPageLocked;
        private readonly Action clearNpcDialogLocked;

        public RoShellNpcInteractionWorkflow(
            RoShellGameplayPacketWorkflow gameplayPacketWorkflow,
            Func<int> legacyTextCodePageLocked,
            Func<RoServerProfile> profileLocked,
            Action<uint> advanceNpcDialogPageLocked,
            Action clearNpcDialogLocked) {
            this.gameplayPacketWorkflow = gameplayPacketWorkflow ?? throw new ArgumentNullException(nameof(gameplayPacketWorkflow));
            this.legacyTextCodePageLocked = legacyTextCodePageLocked ?? throw new ArgumentNullException(nameof(legacyTextCodePageLocked));
            this.profileLocked = profileLocked ?? throw new ArgumentNullException(nameof(profileLocked));
            this.advanceNpcDialogPageLocked = advanceNpcDialogPageLocked ?? throw new ArgumentNullException(nameof(advanceNpcDialogPageLocked));
            this.clearNpcDialogLocked = clearNpcDialogLocked ?? throw new ArgumentNullException(nameof(clearNpcDialogLocked));
        }

        public Task BeginContactNpc(int npcId, byte type) {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildContactNpc(npcId, type),
                $"contacting NPC {npcId}",
                $"contacted NPC {npcId}"));
        }

        public Task BeginNpcDialogNext() {
            uint preparedNpcId = 0;
            return BeginPrepared(snapshot => {
                byte[] packet = RoShellGameplayCommands.BuildNpcDialogNext(snapshot, out uint npcId);
                preparedNpcId = npcId;
                return new RoShellPreparedGameplayPacket(packet, $"continuing NPC {npcId}", $"continued NPC {npcId}");
            }, () => advanceNpcDialogPageLocked(preparedNpcId));
        }

        public Task BeginNpcDialogClose() {
            return BeginPrepared(snapshot => {
                byte[] packet = RoShellGameplayCommands.BuildNpcDialogClose(snapshot, out uint npcId);
                return new RoShellPreparedGameplayPacket(packet, $"closing NPC {npcId}", $"closed NPC {npcId}");
            }, clearNpcDialogLocked);
        }

        public Task BeginNpcDialogMenuChoice(int choice) {
            return BeginPrepared(
                snapshot => new RoShellPreparedGameplayPacket(
                    RoShellGameplayCommands.BuildNpcDialogMenuChoice(snapshot, choice),
                    choice == byte.MaxValue ? "canceling NPC menu" : $"selecting NPC menu {choice}",
                    choice == byte.MaxValue ? "canceled NPC menu" : $"selected NPC menu {choice}"),
                choice == byte.MaxValue ? clearNpcDialogLocked : null);
        }

        public Task BeginNpcDialogNumberInput(int value) {
            return BeginPrepared(snapshot => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcDialogNumberInput(snapshot, value),
                $"sending NPC number {value}",
                $"sent NPC number {value}"));
        }

        public Task BeginNpcDialogTextInput(string value) {
            return BeginPrepared(snapshot => {
                Encoding encoding = RoShellTextEncoding.Resolve(legacyTextCodePageLocked());
                return new RoShellPreparedGameplayPacket(
                    RoShellGameplayCommands.BuildNpcDialogTextInput(snapshot, value, encoding),
                    "sending NPC text",
                    "sent NPC text");
            });
        }

        public Task BeginNpcShopOpenBuy() {
            return BeginNpcShopSelectDealType(0);
        }

        public Task BeginNpcShopOpenSell() {
            return BeginNpcShopSelectDealType(1);
        }

        public Task BeginNpcShopSelectDealType(byte type) {
            return BeginPrepared(snapshot => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcShopSelectDealType(snapshot, type),
                type == 0 ? "opening NPC buy list" : "opening NPC sell list",
                type == 0 ? "opened NPC buy list" : "opened NPC sell list"));
        }

        public Task BeginNpcShopBuyFirst(int amount) {
            return BeginPrepared(snapshot => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcShopBuyFirst(snapshot, amount, profileLocked()),
                "buying first NPC shop item",
                "sent NPC shop buy request"));
        }

        public Task BeginNpcShopBuy(uint itemId, int amount) {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcShopBuy(itemId, amount, profileLocked()),
                $"buying NPC shop item {itemId}",
                $"sent NPC shop buy request for {itemId}"));
        }

        public Task BeginNpcShopBuy(params RoNpcShopPurchaseRequest[] items) {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcShopBuy(profileLocked(), items),
                "buying NPC shop items",
                "sent NPC shop buy request"));
        }

        public Task BeginNpcShopSellFirst(int amount) {
            return BeginPrepared(snapshot => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcShopSellFirst(snapshot, amount, profileLocked()),
                "selling first NPC shop item",
                "sent NPC shop sell request"));
        }

        public Task BeginNpcShopSell(int bagIndex, int amount) {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcShopSell(bagIndex, amount, profileLocked()),
                $"selling bag item {bagIndex}",
                $"sent NPC shop sell request for bag {bagIndex}"));
        }

        public Task BeginNpcShopSell(params RoNpcShopSellRequest[] items) {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcShopSell(profileLocked(), items),
                "selling NPC shop items",
                "sent NPC shop sell request"));
        }

        public Task BeginNpcShopClose() {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcShopClose(profileLocked()),
                "closing NPC shop",
                "closed NPC shop"));
        }

        public Task BeginNpcMarketPurchase(params RoNpcMarketPurchaseRequest[] items) {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcMarketPurchase(profileLocked(), items),
                "buying NPC market items",
                "sent NPC market purchase request"));
        }

        public Task BeginNpcMarketClose() {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcMarketClose(profileLocked()),
                "closing NPC market",
                "closed NPC market"));
        }

        public Task BeginNpcBarterPurchase(params RoNpcBarterPurchaseRequest[] items) {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcBarterPurchase(profileLocked(), items),
                "buying NPC barter items",
                "sent NPC barter purchase request"));
        }

        public Task BeginNpcBarterClose() {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcBarterClose(profileLocked()),
                "closing NPC barter",
                "closed NPC barter"));
        }

        public Task BeginNpcExpandedBarterPurchase(params RoNpcExpandedBarterPurchaseRequest[] items) {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcExpandedBarterPurchase(profileLocked(), items),
                "buying NPC expanded barter items",
                "sent NPC expanded barter purchase request"));
        }

        public Task BeginNpcExpandedBarterClose() {
            return BeginPrepared(_ => new RoShellPreparedGameplayPacket(
                RoShellGameplayCommands.BuildNpcExpandedBarterClose(profileLocked()),
                "closing NPC expanded barter",
                "closed NPC expanded barter"));
        }

        private Task BeginPrepared(Func<RoGameplaySnapshot, RoShellPreparedGameplayPacket> prepare, Action afterPrepareLocked = null) {
            Action<RoGameplaySnapshot, RoShellPreparedGameplayPacket> afterPrepare = null;
            if (afterPrepareLocked != null) {
                afterPrepare = (_, _) => afterPrepareLocked();
            }

            return gameplayPacketWorkflow.Begin(prepare, afterPrepare);
        }
    }
}
