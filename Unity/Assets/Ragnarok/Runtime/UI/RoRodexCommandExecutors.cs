using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    public interface IRoRodexCommandExecutor {
        Task<RoEconomyCommandResult> OpenRodexAsync();
        Task<RoEconomyCommandResult> RefreshRodexAsync(ulong mailId = 0, ulong returnMailId = 0, ulong accountMailId = 0);
        Task<RoEconomyCommandResult> CloseRodexAsync();
        Task<RoEconomyCommandResult> ReadRodexAsync(byte openType, ulong mailId);
        Task<RoEconomyCommandResult> NextRodexAsync(byte openType, ulong mailId);
        Task<RoEconomyCommandResult> DeleteRodexAsync(byte openType, ulong mailId);
        Task<RoEconomyCommandResult> RetrieveRodexZenyAsync(byte openType, ulong mailId);
        Task<RoEconomyCommandResult> RetrieveRodexItemsAsync(byte openType, ulong mailId);
        Task<RoEconomyCommandResult> OpenWriteRodexAsync(string receiverName);
        Task<RoEconomyCommandResult> CancelWriteRodexAsync();
        Task<RoEconomyCommandResult> CheckRodexReceiverAsync(string receiverName);
        Task<RoEconomyCommandResult> AddRodexItemAsync(int bagIndex, int amount);
        Task<RoEconomyCommandResult> RemoveRodexItemAsync(int bagIndex, int amount);
        Task<RoEconomyCommandResult> SendRodexMailAsync(string receiverName, string senderName, long zeny, string title, string body, uint receiverCharacterId);
    }
    public sealed class RoLegacyRodexCommandExecutor : IRoRodexCommandExecutor {
        private readonly RoLoginShellSession session;

        public RoLegacyRodexCommandExecutor(RoLoginShellSession session) {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public Task<RoEconomyCommandResult> OpenRodexAsync() => RunAsync(() => session.BeginLegacyOpenRodex());
        public Task<RoEconomyCommandResult> RefreshRodexAsync(ulong mailId = 0, ulong returnMailId = 0, ulong accountMailId = 0) => RunAsync(() => session.BeginLegacyRefreshRodex(mailId, returnMailId, accountMailId));
        public Task<RoEconomyCommandResult> CloseRodexAsync() => RunAsync(() => session.BeginLegacyCloseRodex());
        public Task<RoEconomyCommandResult> ReadRodexAsync(byte openType, ulong mailId) => RunAsync(() => session.BeginLegacyReadRodex(openType, mailId));
        public Task<RoEconomyCommandResult> NextRodexAsync(byte openType, ulong mailId) => RunAsync(() => session.BeginLegacyNextRodex(openType, mailId));
        public Task<RoEconomyCommandResult> DeleteRodexAsync(byte openType, ulong mailId) => RunAsync(() => session.BeginLegacyDeleteRodex(openType, mailId));
        public Task<RoEconomyCommandResult> RetrieveRodexZenyAsync(byte openType, ulong mailId) => RunAsync(() => session.BeginLegacyRetrieveRodexZeny(openType, mailId));
        public Task<RoEconomyCommandResult> RetrieveRodexItemsAsync(byte openType, ulong mailId) => RunAsync(() => session.BeginLegacyRetrieveRodexItems(openType, mailId));
        public Task<RoEconomyCommandResult> OpenWriteRodexAsync(string receiverName) => RunAsync(() => session.BeginLegacyOpenWriteRodex(receiverName));
        public Task<RoEconomyCommandResult> CancelWriteRodexAsync() => RunAsync(() => session.BeginLegacyCancelWriteRodex());
        public Task<RoEconomyCommandResult> CheckRodexReceiverAsync(string receiverName) => RunAsync(() => session.BeginLegacyCheckRodexReceiver(receiverName));
        public Task<RoEconomyCommandResult> AddRodexItemAsync(int bagIndex, int amount) => RunAsync(() => session.BeginLegacyAddRodexItem(bagIndex, amount));
        public Task<RoEconomyCommandResult> RemoveRodexItemAsync(int bagIndex, int amount) => RunAsync(() => session.BeginLegacyRemoveRodexItem(bagIndex, amount));
        public Task<RoEconomyCommandResult> SendRodexMailAsync(string receiverName, string senderName, long zeny, string title, string body, uint receiverCharacterId) {
            if (string.IsNullOrWhiteSpace(receiverName)) {
                return Task.FromResult(RoEconomyCommandResult.Failure(RoEconomyCommandStatus.InvalidTarget, "Rodex receiver is required."));
            }

            if (receiverCharacterId == 0) {
                return Task.FromResult(RoEconomyCommandResult.Failure(RoEconomyCommandStatus.InvalidTarget, "Rodex receiver must be validated before sending."));
            }

            return RunAsync(() => session.BeginLegacySendRodexMail(receiverName, senderName, zeny, title, body, receiverCharacterId));
        }

        private static async Task<RoEconomyCommandResult> RunAsync(Func<Task> action) {
            try {
                await action().ConfigureAwait(false);
                return RoEconomyCommandResult.AcceptedResult();
            } catch (Exception exception) {
                return RoEconomyCommandResult.Failure(RoEconomyCommandStatus.SendFailed, exception.Message);
            }
        }
    }
}
