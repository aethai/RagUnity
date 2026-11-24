using System;
using System.Threading.Tasks;
using Ragnarok.Diagnostics;
using Ragnarok.Network;
using UnityEngine;

namespace Ragnarok.UI {
    internal sealed class RoShellRodexWorkflow {
        private readonly RoShellGameplayPacketWorkflow gameplayPacketWorkflow;
        private readonly Func<int> codePageLocked;
        private readonly Func<RoServerProfile> profileLocked;

        public RoShellRodexWorkflow(
            RoShellGameplayPacketWorkflow gameplayPacketWorkflow,
            Func<int> codePageLocked,
            Func<RoServerProfile> profileLocked) {
            this.gameplayPacketWorkflow = gameplayPacketWorkflow ?? throw new ArgumentNullException(nameof(gameplayPacketWorkflow));
            this.codePageLocked = codePageLocked ?? throw new ArgumentNullException(nameof(codePageLocked));
            this.profileLocked = profileLocked ?? throw new ArgumentNullException(nameof(profileLocked));
        }

        public Task BeginLegacyOpenRodex() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexOpen(profileLocked()),
                "opening Rodex",
                "sent Rodex open request");
        }

        public Task BeginLegacyRefreshRodex(ulong mailId, ulong returnMailId, ulong accountMailId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexRefresh(mailId, returnMailId, accountMailId, profileLocked()),
                "refreshing Rodex",
                "sent Rodex refresh request");
        }

        public Task BeginLegacyCloseRodex() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexClose(profileLocked()),
                "closing Rodex",
                "sent Rodex close request");
        }

        public Task BeginLegacyReadRodex(byte openType, ulong mailId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexRead(openType, mailId, profileLocked()),
                "reading Rodex mail",
                "sent Rodex read request");
        }

        public Task BeginLegacyNextRodex(byte openType, ulong mailId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexNext(openType, mailId, profileLocked()),
                "loading Rodex page",
                "sent Rodex next request");
        }

        public Task BeginLegacyDeleteRodex(byte openType, ulong mailId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexDelete(openType, mailId, profileLocked()),
                "deleting Rodex mail",
                "sent Rodex delete request");
        }

        public Task BeginLegacyRetrieveRodexZeny(byte openType, ulong mailId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexGetZeny(openType, mailId, profileLocked()),
                "retrieving Rodex zeny",
                "sent Rodex zeny retrieve request");
        }

        public Task BeginLegacyRetrieveRodexItems(byte openType, ulong mailId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexGetItems(openType, mailId, profileLocked()),
                "retrieving Rodex items",
                "sent Rodex item retrieve request");
        }

        public Task BeginLegacyOpenWriteRodex(string receiverName) {
            return gameplayPacketWorkflow.Begin(
                _ => {
                    byte[] packet = RoShellGameplayCommands.BuildRodexOpenWrite(receiverName, codePageLocked(), profileLocked());
                    if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                        ushort opcode = packet != null && packet.Length >= 2 ? (ushort) (packet[0] | (packet[1] << 8)) : (ushort) 0;
                        RoRuntimeDiagnostics.IncrementDebugLogLine();
                        Debug.Log("RODEX write open send opcode=0x" + opcode.ToString("x4") +
                            " len=" + (packet != null ? packet.Length.ToString() : "0") +
                            " receiver='" + (receiverName ?? string.Empty) + "'");
                    }

                    return packet;
                },
                "opening Rodex writer",
                "sent Rodex write open request");
        }

        public Task BeginLegacyCancelWriteRodex() {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexCancelWrite(profileLocked()),
                "closing Rodex writer",
                "sent Rodex write cancel request");
        }

        public Task BeginLegacyCheckRodexReceiver(string receiverName) {
            return gameplayPacketWorkflow.Begin(
                _ => {
                    byte[] packet = RoShellGameplayCommands.BuildRodexCheckReceiver(receiverName, codePageLocked(), profileLocked());
                    if (RoRuntimeDiagnostics.ShouldLogVerboseDiagnostics) {
                        ushort opcode = packet != null && packet.Length >= 2 ? (ushort) (packet[0] | (packet[1] << 8)) : (ushort) 0;
                        RoRuntimeDiagnostics.IncrementDebugLogLine();
                        Debug.Log("RODEX validate send opcode=0x" + opcode.ToString("x4") +
                            " len=" + (packet != null ? packet.Length.ToString() : "0") +
                            " receiver='" + (receiverName ?? string.Empty) + "'");
                    }

                    return packet;
                },
                "checking Rodex receiver",
                "sent Rodex receiver check");
        }

        public Task BeginLegacyAddRodexItem(int bagIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexAddItem(bagIndex, amount, profileLocked()),
                "adding Rodex item",
                "sent Rodex add item request");
        }

        public Task BeginLegacyRemoveRodexItem(int bagIndex, int amount) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexRemoveItem(bagIndex, amount, profileLocked()),
                "removing Rodex item",
                "sent Rodex remove item request");
        }

        public Task BeginLegacySendRodexMail(string receiverName, string senderName, long zeny, string title, string body, uint receiverCharacterId) {
            return gameplayPacketWorkflow.Begin(
                _ => RoShellGameplayCommands.BuildRodexSendMail(
                    receiverName,
                    senderName,
                    zeny,
                    title,
                    body,
                    receiverCharacterId,
                    codePageLocked(),
                    profileLocked()),
                "sending Rodex mail",
                "sent Rodex mail");
        }
    }
}
