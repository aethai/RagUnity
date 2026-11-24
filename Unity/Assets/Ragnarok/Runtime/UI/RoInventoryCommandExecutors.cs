using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    public interface IRoInventoryCommandExecutor {
        Task<RoItemCommandResult> UseInventoryItemAsync(int bagIndex);
        Task<RoItemCommandResult> DropInventoryItemAsync(int bagIndex, int count);
        Task<RoItemCommandResult> EquipInventoryItemAsync(int bagIndex, uint position = 0);
        Task<RoItemCommandResult> UnequipInventoryItemAsync(int bagIndex);
        Task<RoItemCommandResult> RequestCardCompositionListAsync(int cardBagIndex);
        Task<RoItemCommandResult> InsertCardAsync(int cardBagIndex, int equipBagIndex);
        Task<RoItemCommandResult> IdentifyItemAsync(int bagIndex);
        Task<RoItemCommandResult> CancelIdentifyItemAsync();
        Task<RoItemCommandResult> RequestMakingArrowAsync(uint itemId);
        Task<RoItemCommandResult> SetEquipmentConfigAsync(int config, int value);
    }
    public sealed class RoLegacyInventoryCommandExecutor : IRoInventoryCommandExecutor {
        private readonly RoLoginShellSession session;

        public RoLegacyInventoryCommandExecutor(RoLoginShellSession session) {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task<RoItemCommandResult> UseInventoryItemAsync(int bagIndex) {
            await session.BeginLegacyUseInventoryItem(bagIndex).ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> DropInventoryItemAsync(int bagIndex, int count) {
            await session.BeginLegacyDropInventoryItem(bagIndex, count).ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> EquipInventoryItemAsync(int bagIndex, uint position = 0) {
            await session.BeginLegacyWearInventoryItem(bagIndex).ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> UnequipInventoryItemAsync(int bagIndex) {
            await session.BeginLegacyTakeoffInventoryItem(bagIndex).ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> RequestCardCompositionListAsync(int cardBagIndex) {
            await session.BeginLegacyRequestCardCompositionList(cardBagIndex).ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> InsertCardAsync(int cardBagIndex, int equipBagIndex) {
            await session.BeginLegacyInsertCard(cardBagIndex, equipBagIndex).ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> IdentifyItemAsync(int bagIndex) {
            await session.BeginLegacyIdentifyItem(bagIndex).ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> CancelIdentifyItemAsync() {
            await session.BeginLegacyCancelIdentifyItem().ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> RequestMakingArrowAsync(uint itemId) {
            await session.BeginLegacyMakeArrow(itemId).ConfigureAwait(false);
            return Accepted();
        }

        public async Task<RoItemCommandResult> SetEquipmentConfigAsync(int config, int value) {
            await session.BeginLegacySetEquipmentConfig(config, value).ConfigureAwait(false);
            return Accepted();
        }

        private static RoItemCommandResult Accepted() {
            return new RoItemCommandResult(RoItemCommandStatus.Accepted, default, default, string.Empty);
        }
    }
}
