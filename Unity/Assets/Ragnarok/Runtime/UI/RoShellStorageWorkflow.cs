using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellStorageWorkflow {
        private readonly RoShellGameplayPacketWorkflow gameplayPacketWorkflow;
        private readonly Func<RoServerProfile> profileLocked;

        public RoShellStorageWorkflow(
            RoShellGameplayPacketWorkflow gameplayPacketWorkflow,
            Func<RoServerProfile> profileLocked) {
            this.gameplayPacketWorkflow = gameplayPacketWorkflow ?? throw new ArgumentNullException(nameof(gameplayPacketWorkflow));
            this.profileLocked = profileLocked ?? throw new ArgumentNullException(nameof(profileLocked));
        }

        public Task BeginLegacyMoveInventoryItemToStorage(int bagIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildMoveInventoryItemToStorage(bagIndex, amount, profileLocked()),
                $"moving bag {bagIndex} to storage",
                $"sent storage deposit for bag {bagIndex}");
        }

        public Task BeginLegacyMoveStorageItemToInventory(int storageIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildMoveStorageItemToInventory(storageIndex, amount, profileLocked()),
                $"moving storage {storageIndex} to inventory",
                $"sent storage withdraw for {storageIndex}");
        }

        public Task BeginLegacyMoveStorageItemToCart(int storageIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildMoveStorageItemToCart(storageIndex, amount, profileLocked()),
                $"moving storage {storageIndex} to cart",
                $"sent storage to cart transfer for {storageIndex}");
        }

        public Task BeginLegacyCloseStorage() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildCloseStorage(profileLocked()),
                "closing storage",
                "closed storage");
        }
    }
}
