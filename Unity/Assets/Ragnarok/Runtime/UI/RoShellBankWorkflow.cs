using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellBankWorkflow {
        private readonly RoShellGameplayPacketWorkflow gameplayPacketWorkflow;
        private readonly Func<int> aidLocked;
        private readonly Func<RoServerProfile> profileLocked;

        public RoShellBankWorkflow(
            RoShellGameplayPacketWorkflow gameplayPacketWorkflow,
            Func<int> aidLocked,
            Func<RoServerProfile> profileLocked) {
            this.gameplayPacketWorkflow = gameplayPacketWorkflow ?? throw new ArgumentNullException(nameof(gameplayPacketWorkflow));
            this.aidLocked = aidLocked ?? throw new ArgumentNullException(nameof(aidLocked));
            this.profileLocked = profileLocked ?? throw new ArgumentNullException(nameof(profileLocked));
        }

        public Task BeginLegacyOpenBank() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildOpenBank(aidLocked(), profileLocked()),
                "opening bank",
                "sent bank open request");
        }

        public Task BeginLegacyCloseBank() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildCloseBank(aidLocked(), profileLocked()),
                "closing bank",
                "sent bank close request");
        }

        public Task BeginLegacyCheckBank() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildCheckBank(aidLocked(), profileLocked()),
                "checking bank",
                "sent bank balance check");
        }

        public Task BeginLegacyDepositBankZeny(int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildDepositBankZeny(aidLocked(), amount, profileLocked()),
                "depositing bank zeny",
                "sent bank deposit");
        }

        public Task BeginLegacyWithdrawBankZeny(int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildWithdrawBankZeny(aidLocked(), amount, profileLocked()),
                "withdrawing bank zeny",
                "sent bank withdraw");
        }
    }
}
