using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellSocialEconomyWorkflow {
        private readonly RoShellGameplayPacketWorkflow gameplayPacketWorkflow;
        private readonly Func<int> legacyTextCodePageLocked;
        private readonly Func<RoServerProfile> profileLocked;

        public RoShellSocialEconomyWorkflow(
            RoShellGameplayPacketWorkflow gameplayPacketWorkflow,
            Func<int> legacyTextCodePageLocked,
            Func<RoServerProfile> profileLocked) {
            this.gameplayPacketWorkflow = gameplayPacketWorkflow ?? throw new ArgumentNullException(nameof(gameplayPacketWorkflow));
            this.legacyTextCodePageLocked = legacyTextCodePageLocked ?? throw new ArgumentNullException(nameof(legacyTextCodePageLocked));
            this.profileLocked = profileLocked ?? throw new ArgumentNullException(nameof(profileLocked));
        }

        public Task BeginPartyInvite(string characterName) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildPartyInvite(characterName, RoShellTextEncoding.Resolve(legacyTextCodePageLocked()), profileLocked()),
                "sending party invite",
                "sent party invite");
        }

        public Task BeginPartyLeave() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildPartyLeave(profileLocked()),
                "leaving party",
                "sent party leave request");
        }

        public Task BeginPartyInviteResponse(uint invitationId, bool accept) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildPartyInviteResponse(invitationId, accept, profileLocked()),
                accept ? "accepting party invite" : "declining party invite",
                accept ? "accepted party invite" : "declined party invite");
        }

        public Task BeginCreateParty(string partyName, byte itemPickupRule, byte itemDivisionRule) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildCreateParty(
                    partyName,
                    itemPickupRule,
                    itemDivisionRule,
                    RoShellTextEncoding.Resolve(legacyTextCodePageLocked()),
                    profileLocked()),
                "creating party",
                "sent party create request");
        }

        public Task BeginExpelPartyMember(uint accountId, string characterName) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildExpelPartyMember(
                    accountId,
                    characterName,
                    RoShellTextEncoding.Resolve(legacyTextCodePageLocked()),
                    profileLocked()),
                "expelling party member",
                "sent party expel request");
        }

        public Task BeginChangePartyLeader(uint accountId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildChangePartyLeader(accountId, profileLocked()),
                "changing party leader",
                "sent party leader change request");
        }

        public Task BeginPartyOptions(uint expOption, byte itemPickupRule, byte itemDivisionRule) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildPartyOptions(expOption, itemPickupRule, itemDivisionRule, profileLocked()),
                "updating party setup",
                "sent party setup request");
        }

        public Task BeginAddFriend(string characterName) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildAddFriend(characterName, RoShellTextEncoding.Resolve(legacyTextCodePageLocked()), profileLocked()),
                "sending friend request",
                "sent friend request");
        }

        public Task BeginRemoveFriend(uint accountId, uint characterId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRemoveFriend(accountId, characterId, profileLocked()),
                "removing friend",
                "sent friend remove request");
        }

        public Task BeginChatRoomForm(bool change, int limit, bool isPublic, string password, string title) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildChatRoomForm(change, limit, isPublic, password, title, RoShellTextEncoding.Resolve(legacyTextCodePageLocked()), profileLocked()),
                change ? "changing the chat room" : "creating a chat room",
                change ? "sent chat room change" : "sent chat room create");
        }

        public Task BeginEnterChatRoom(uint chatId, string password) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildEnterChatRoom(chatId, password, RoShellTextEncoding.Resolve(legacyTextCodePageLocked()), profileLocked()),
                "joining a chat room",
                "sent chat room join");
        }

        public Task BeginChatRoomOwner(string name) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildChatRoomOwner(name, RoShellTextEncoding.Resolve(legacyTextCodePageLocked()), profileLocked()),
                "giving the chat room",
                "sent chat room owner change");
        }

        public Task BeginChatRoomExpel(string name) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildChatRoomExpel(name, RoShellTextEncoding.Resolve(legacyTextCodePageLocked()), profileLocked()),
                "kicking from the chat room",
                "sent chat room kick");
        }

        public Task BeginOpenBuyingStore(uint zenyLimit, bool open, string title, RoBuyingStoreItem[] items) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildOpenBuyingStore(zenyLimit, open, title, items, RoShellTextEncoding.Resolve(legacyTextCodePageLocked()), profileLocked()),
                open ? "opening a buying store" : "cancelling the buying store setup",
                open ? "sent buying store open" : "sent buying store setup cancel");
        }

        public Task BeginCloseBuyingStore() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildCloseBuyingStore(profileLocked()),
                "closing the buying store",
                "sent buying store close");
        }

        public Task BeginClickBuyingStore(uint buyerAid) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildClickBuyingStore(buyerAid, profileLocked()),
                "asking a buying store's list",
                "sent buying store list request");
        }

        public Task BeginTradeBuyingStore(uint buyerAid, uint storeId, (ushort BagIndex, uint ItemId, int Amount)[] items) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildTradeBuyingStore(buyerAid, storeId, items, profileLocked()),
                "selling to a buying store",
                "sent buying store sale");
        }

        public Task BeginPetPacket(Func<RoServerProfile, byte[]> build, string action, string sent) {
            return gameplayPacketWorkflow.Begin(_ => build(profileLocked()), action, sent);
        }

        public Task BeginMakingItem(uint itemId, uint[] materials) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildMakingItem(itemId, materials, profileLocked()),
                itemId == 0 ? "closing the manufacturing list" : "making an item",
                itemId == 0 ? "sent manufacturing cancel" : "sent manufacturing request");
        }

        public Task BeginMergeItem(short[] bagIndexes) {
            return gameplayPacketWorkflow.Begin(
                _ => bagIndexes == null
                    ? RoShellGameplayCommands.BuildCancelMergeItem(profileLocked())
                    : RoShellGameplayCommands.BuildMergeItem(bagIndexes, profileLocked()),
                bagIndexes == null ? "closing item merge" : "merging items",
                bagIndexes == null ? "sent item merge cancel" : "sent item merge request");
        }

        public Task BeginWeaponRefine(int index) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildWeaponRefine(index, profileLocked()),
                index < 0 ? "closing the weapon refine list" : "refining a weapon",
                index < 0 ? "sent weapon refine cancel" : "sent weapon refine request");
        }

        public Task BeginSelectAutoSpell(int skillId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildSelectAutoSpell(skillId, profileLocked()),
                skillId == 0 ? "closing the spell list" : "choosing an auto spell",
                skillId == 0 ? "sent spell list cancel" : "sent auto spell choice");
        }

        public Task BeginItemRepair(short index, uint itemId, uint[] cards, byte refine) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildItemRepair(index, itemId, cards, refine, profileLocked()),
                index < 0 ? "closing the repair list" : "repairing an item",
                index < 0 ? "sent repair cancel" : "sent repair request");
        }

        public Task BeginEmotion(byte emotion) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildEmotion(emotion, profileLocked()),
                "sending an emotion",
                "sent emotion");
        }

        /// <summary>An emotion of a pack (0 = the basic one) as the client sends it (0x0be9, or 0x00bf on a 20200401 server).</summary>
        public Task BeginEmotion(ushort pack, ushort emotion) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildEmotion(pack, emotion, profileLocked()),
                "sending an emotion",
                "sent emotion");
        }

        public Task BeginExitChatRoom() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildExitChatRoom(profileLocked()),
                "leaving the chat room",
                "sent chat room leave");
        }

        public Task BeginRequestNameByGid(uint characterId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRequestNameByGid(characterId, profileLocked()),
                "requesting a character name",
                "sent character name request");
        }

        public Task BeginFriendRequestResponse(uint accountId, uint characterId, bool accept) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRespondFriendRequest(accountId, characterId, accept, profileLocked()),
                accept ? "accepting friend request" : "declining friend request",
                accept ? "accepted friend request" : "declined friend request");
        }

        public Task BeginGuildChat(string message) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildGuildChat(message, RoShellTextEncoding.Resolve(legacyTextCodePageLocked()), profileLocked()),
                "sending guild chat",
                "sent guild chat");
        }

        public Task BeginRequestTrade(uint targetActorId, string targetName) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildTradeRequest(targetActorId, profileLocked()),
                string.IsNullOrWhiteSpace(targetName) ? "requesting trade" : $"requesting trade with {targetName.Trim()}",
                "sent trade request");
        }

        public Task BeginCheckPlayerEquipment(uint targetActorId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildCheckPlayerEquipment(targetActorId, profileLocked()),
                "requesting player equipment",
                "sent player equipment request");
        }

        public Task BeginAnswerTradeRequest(bool accept) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildTradeAnswer(accept, profileLocked()),
                accept ? "accepting trade request" : "declining trade request",
                accept ? "accepted trade request" : "declined trade request");
        }

        public Task BeginAddTradeItem(int bagIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildTradeAddItem(bagIndex, amount, profileLocked()),
                "adding item to trade",
                "sent trade item offer");
        }

        public Task BeginAddTradeZeny(int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildTradeAddItem(0, amount, profileLocked()),
                "adding zeny to trade",
                "sent trade zeny offer");
        }

        public Task BeginLockTrade() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildTradeLock(profileLocked()),
                "locking trade",
                "sent trade lock");
        }

        public Task BeginCancelTrade() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildTradeCancel(profileLocked()),
                "canceling trade",
                "sent trade cancel");
        }

        public Task BeginConfirmTrade() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildTradeConfirm(profileLocked()),
                "confirming trade",
                "sent trade confirm");
        }

        public Task BeginMoveInventoryItemToCart(int bagIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildMoveInventoryItemToCart(bagIndex, amount, profileLocked()),
                $"moving bag {bagIndex} to cart",
                $"sent cart deposit for bag {bagIndex}");
        }

        public Task BeginMoveCartItemToInventory(int cartIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildMoveCartItemToInventory(cartIndex, amount, profileLocked()),
                $"moving cart {cartIndex} to inventory",
                $"sent cart withdraw for {cartIndex}");
        }

        public Task BeginMoveCartItemToStorage(int cartIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildMoveCartItemToStorage(cartIndex, amount, profileLocked()),
                $"moving cart {cartIndex} to storage",
                $"sent cart to storage transfer for {cartIndex}");
        }

        public Task BeginCartOff() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildCartOff(profileLocked()),
                "removing cart",
                "sent cart remove request");
        }

        public Task BeginFeedPet() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildPetFeed(profileLocked()),
                "feeding pet",
                "sent pet feed request");
        }
    }
}
