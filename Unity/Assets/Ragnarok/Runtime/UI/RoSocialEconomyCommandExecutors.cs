using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    public interface IRoSocialEconomyCommandExecutor {
        Task<RoSocialCommandResult> InvitePartyAsync(string characterName);
        Task<RoSocialCommandResult> LeavePartyAsync();
        Task<RoSocialCommandResult> RespondPartyInviteAsync(uint invitationId, bool accept);
        Task<RoSocialCommandResult> CreatePartyAsync(string partyName, byte itemPickupRule, byte itemDivisionRule);
        Task<RoSocialCommandResult> ExpelPartyMemberAsync(uint accountId, string characterName);
        Task<RoSocialCommandResult> ChangePartyLeaderAsync(uint accountId);
        Task<RoSocialCommandResult> UpdatePartyOptionsAsync(uint expOption, byte itemPickupRule, byte itemDivisionRule);
        Task<RoSocialCommandResult> AddFriendAsync(string characterName);
        Task<RoSocialCommandResult> RemoveFriendAsync(uint accountId, uint characterId);
        Task<RoSocialCommandResult> RespondFriendRequestAsync(uint accountId, uint characterId, bool accept);
        Task<RoAdvancedCommandResult> SendGuildChatAsync(string message);
        Task<RoEconomyCommandResult> CheckPlayerEquipmentAsync(uint targetActorId);
        Task<RoEconomyCommandResult> RequestTradeAsync(uint targetActorId, string targetName);
        Task<RoEconomyCommandResult> AnswerTradeRequestAsync(bool accept);
        Task<RoEconomyCommandResult> AddTradeItemAsync(int bagIndex, int amount);
        Task<RoEconomyCommandResult> AddTradeZenyAsync(int amount);
        Task<RoEconomyCommandResult> LockTradeAsync();
        Task<RoEconomyCommandResult> CancelTradeAsync();
        Task<RoEconomyCommandResult> ConfirmTradeAsync();
        Task<RoEconomyCommandResult> MoveInventoryItemToStorageAsync(int bagIndex, int amount);
        Task<RoEconomyCommandResult> MoveStorageItemToInventoryAsync(int storageIndex, int amount);
        Task<RoEconomyCommandResult> MoveStorageItemToCartAsync(int storageIndex, int amount);
        Task<RoEconomyCommandResult> CloseStorageAsync();
        Task<RoEconomyCommandResult> MoveInventoryItemToCartAsync(int bagIndex, int amount);
        Task<RoEconomyCommandResult> MoveCartItemToInventoryAsync(int cartIndex, int amount);
        Task<RoEconomyCommandResult> MoveCartItemToStorageAsync(int cartIndex, int amount);
        Task<RoItemCommandResult> UseCartItemAsync(int cartIndex);
        Task<RoEconomyCommandResult> CartOffAsync();
        Task<RoEconomyCommandResult> RemoveRentalOptionAsync();
        Task<RoAdvancedCommandResult> StartVendingAsync(string title);
        Task<RoAdvancedCommandResult> OpenVendingStoreAsync(string title, RoVendingOpenStoreItem[] items);
        Task<RoAdvancedCommandResult> CancelVendingSetupAsync(string title);
        Task<RoAdvancedCommandResult> CloseVendingStoreAsync();
        Task<RoAdvancedCommandResult> RequestPlayerShopAsync(uint ownerAid);
        Task<RoAdvancedCommandResult> PurchaseFromPlayerShopAsync(uint ownerAid, uint uniqueId, RoPlayerShopPurchaseRequest[] items);
        Task<RoAdvancedCommandResult> FeedPetAsync();
    }
    public sealed class RoLegacySocialEconomyCommandExecutor : IRoSocialEconomyCommandExecutor {
        private readonly RoLoginShellSession session;

        public RoLegacySocialEconomyCommandExecutor(RoLoginShellSession session) {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public Task<RoSocialCommandResult> InvitePartyAsync(string characterName) {
            return RunSocialAsync(() => session.BeginLegacyPartyInvite(characterName));
        }

        public Task<RoSocialCommandResult> LeavePartyAsync() {
            return RunSocialAsync(() => session.BeginLegacyPartyLeave());
        }

        public Task<RoSocialCommandResult> RespondPartyInviteAsync(uint invitationId, bool accept) {
            return RunSocialAsync(() => session.BeginLegacyPartyInviteResponse(invitationId, accept));
        }

        public Task<RoSocialCommandResult> CreatePartyAsync(string partyName, byte itemPickupRule, byte itemDivisionRule) {
            return RunSocialAsync(() => session.BeginLegacyCreateParty(partyName, itemPickupRule, itemDivisionRule));
        }

        public Task<RoSocialCommandResult> ExpelPartyMemberAsync(uint accountId, string characterName) {
            return RunSocialAsync(() => session.BeginLegacyExpelPartyMember(accountId, characterName));
        }

        public Task<RoSocialCommandResult> ChangePartyLeaderAsync(uint accountId) {
            return RunSocialAsync(() => session.BeginLegacyChangePartyLeader(accountId));
        }

        public Task<RoSocialCommandResult> UpdatePartyOptionsAsync(uint expOption, byte itemPickupRule, byte itemDivisionRule) {
            return RunSocialAsync(() => session.BeginLegacyPartyOptions(expOption, itemPickupRule, itemDivisionRule));
        }

        public Task<RoSocialCommandResult> AddFriendAsync(string characterName) {
            return RunSocialAsync(() => session.BeginLegacyAddFriend(characterName));
        }

        public Task<RoSocialCommandResult> RemoveFriendAsync(uint accountId, uint characterId) {
            return RunSocialAsync(() => session.BeginLegacyRemoveFriend(accountId, characterId));
        }

        public Task<RoSocialCommandResult> RespondFriendRequestAsync(uint accountId, uint characterId, bool accept) {
            return RunSocialAsync(() => session.BeginLegacyFriendRequestResponse(accountId, characterId, accept));
        }

        public Task<RoAdvancedCommandResult> SendGuildChatAsync(string message) {
            return RunAdvancedAsync(() => session.BeginLegacyGuildChat(message));
        }

        public Task<RoEconomyCommandResult> CheckPlayerEquipmentAsync(uint targetActorId) {
            return RunEconomyAsync(() => session.BeginLegacyCheckPlayerEquipment(targetActorId));
        }

        public Task<RoEconomyCommandResult> RequestTradeAsync(uint targetActorId, string targetName) {
            return RunEconomyAsync(() => session.BeginLegacyRequestTrade(targetActorId, targetName));
        }

        public Task<RoEconomyCommandResult> AnswerTradeRequestAsync(bool accept) {
            return RunEconomyAsync(() => session.BeginLegacyAnswerTradeRequest(accept));
        }

        public Task<RoEconomyCommandResult> AddTradeItemAsync(int bagIndex, int amount) {
            return RunEconomyAsync(() => session.BeginLegacyAddTradeItem(bagIndex, amount));
        }

        public Task<RoEconomyCommandResult> AddTradeZenyAsync(int amount) {
            return RunEconomyAsync(() => session.BeginLegacyAddTradeZeny(amount));
        }

        public Task<RoEconomyCommandResult> LockTradeAsync() {
            return RunEconomyAsync(() => session.BeginLegacyLockTrade());
        }

        public Task<RoEconomyCommandResult> CancelTradeAsync() {
            return RunEconomyAsync(() => session.BeginLegacyCancelTrade());
        }

        public Task<RoEconomyCommandResult> ConfirmTradeAsync() {
            return RunEconomyAsync(() => session.BeginLegacyConfirmTrade());
        }

        public Task<RoEconomyCommandResult> MoveInventoryItemToStorageAsync(int bagIndex, int amount) {
            RoItemTransferDebugLog.ExecutorTransfer(
                nameof(RoLegacySocialEconomyCommandExecutor),
                "CZ_MOVE_ITEM_FROM_BODY_TO_STORE2",
                bagIndex,
                amount);
            return RunEconomyAsync(() => session.BeginLegacyMoveInventoryItemToStorage(bagIndex, amount));
        }

        public Task<RoEconomyCommandResult> MoveStorageItemToInventoryAsync(int storageIndex, int amount) {
            RoItemTransferDebugLog.ExecutorTransfer(
                nameof(RoLegacySocialEconomyCommandExecutor),
                "CZ_MOVE_ITEM_FROM_STORE_TO_BODY2",
                storageIndex,
                amount);
            return RunEconomyAsync(() => session.BeginLegacyMoveStorageItemToInventory(storageIndex, amount));
        }

        public Task<RoEconomyCommandResult> MoveStorageItemToCartAsync(int storageIndex, int amount) {
            RoItemTransferDebugLog.ExecutorTransfer(
                nameof(RoLegacySocialEconomyCommandExecutor),
                "CZ_MOVE_ITEM_FROM_STORE_TO_CART",
                storageIndex,
                amount);
            return RunEconomyAsync(() => session.BeginLegacyMoveStorageItemToCart(storageIndex, amount));
        }

        public Task<RoEconomyCommandResult> CloseStorageAsync() {
            return RunEconomyAsync(() => session.BeginLegacyCloseStorage());
        }

        public Task<RoEconomyCommandResult> MoveInventoryItemToCartAsync(int bagIndex, int amount) {
            RoItemTransferDebugLog.ExecutorTransfer(
                nameof(RoLegacySocialEconomyCommandExecutor),
                "CZ_MOVE_ITEM_FROM_BODY_TO_CART",
                bagIndex,
                amount);
            return RunEconomyAsync(() => session.BeginLegacyMoveInventoryItemToCart(bagIndex, amount));
        }

        public Task<RoEconomyCommandResult> MoveCartItemToInventoryAsync(int cartIndex, int amount) {
            RoItemTransferDebugLog.ExecutorTransfer(
                nameof(RoLegacySocialEconomyCommandExecutor),
                "CZ_MOVE_ITEM_FROM_CART_TO_BODY",
                cartIndex,
                amount);
            return RunEconomyAsync(() => session.BeginLegacyMoveCartItemToInventory(cartIndex, amount));
        }

        public Task<RoEconomyCommandResult> MoveCartItemToStorageAsync(int cartIndex, int amount) {
            RoItemTransferDebugLog.ExecutorTransfer(
                nameof(RoLegacySocialEconomyCommandExecutor),
                "CZ_MOVE_ITEM_FROM_CART_TO_STORE",
                cartIndex,
                amount);
            return RunEconomyAsync(() => session.BeginLegacyMoveCartItemToStorage(cartIndex, amount));
        }

        public async Task<RoItemCommandResult> UseCartItemAsync(int cartIndex) {
            try {
                await session.BeginLegacyUseCartItem(cartIndex).ConfigureAwait(false);
                return new RoItemCommandResult(RoItemCommandStatus.Accepted, default, default, string.Empty);
            } catch (Exception exception) {
                return RoItemCommandResult.Failure(RoItemCommandStatus.SendFailed, exception.Message);
            }
        }

        public Task<RoEconomyCommandResult> CartOffAsync() {
            return RunEconomyAsync(() => session.BeginLegacyCartOff());
        }

        public Task<RoEconomyCommandResult> RemoveRentalOptionAsync() {
            return RunEconomyAsync(() => session.BeginLegacyCartOff());
        }

        public Task<RoAdvancedCommandResult> StartVendingAsync(string title) {
            return Task.FromResult(RoAdvancedCommandResult.Unsupported(
                "Use the Vending skill first; setup opens only after the server sends ZC_OPENSTORE."));
        }

        public Task<RoAdvancedCommandResult> OpenVendingStoreAsync(string title, RoVendingOpenStoreItem[] items) {
            return RunAdvancedAsync(() => session.BeginLegacyOpenVendingStore(title, items));
        }

        public Task<RoAdvancedCommandResult> CancelVendingSetupAsync(string title) {
            return RunAdvancedAsync(() => session.BeginLegacyCancelVendingSetup(title));
        }

        public Task<RoAdvancedCommandResult> CloseVendingStoreAsync() {
            return RunAdvancedAsync(() => session.BeginLegacyCloseVendingStore());
        }

        public Task<RoAdvancedCommandResult> RequestPlayerShopAsync(uint ownerAid) {
            return RunAdvancedAsync(() => session.BeginLegacyRequestPlayerShop(ownerAid));
        }

        public Task<RoAdvancedCommandResult> PurchaseFromPlayerShopAsync(uint ownerAid, uint uniqueId, RoPlayerShopPurchaseRequest[] items) {
            return RunAdvancedAsync(() => session.BeginLegacyPurchaseFromPlayerShop(ownerAid, uniqueId, items));
        }

        public Task<RoAdvancedCommandResult> FeedPetAsync() {
            return RunAdvancedAsync(() => session.BeginLegacyFeedPet());
        }

        private static async Task<RoSocialCommandResult> RunSocialAsync(Func<Task> action) {
            try {
                await action().ConfigureAwait(false);
                return RoSocialCommandResult.AcceptedResult();
            } catch (Exception exception) {
                return new RoSocialCommandResult(RoSocialCommandStatus.SendFailed, exception.Message);
            }
        }

        private static async Task<RoEconomyCommandResult> RunEconomyAsync(Func<Task> action) {
            try {
                await action().ConfigureAwait(false);
                return RoEconomyCommandResult.AcceptedResult();
            } catch (Exception exception) {
                return RoEconomyCommandResult.Failure(RoEconomyCommandStatus.SendFailed, exception.Message);
            }
        }

        private static async Task<RoAdvancedCommandResult> RunAdvancedAsync(Func<Task> action) {
            try {
                await action().ConfigureAwait(false);
                return RoAdvancedCommandResult.Accepted();
            } catch (Exception exception) {
                return new RoAdvancedCommandResult(RoAdvancedCommandStatus.SendFailed, exception.Message);
            }
        }
    }
}
