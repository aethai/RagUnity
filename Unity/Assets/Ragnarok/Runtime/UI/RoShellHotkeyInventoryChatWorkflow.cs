using System;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Data;
using Ragnarok.Gameplay;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal sealed class RoShellHotkeyInventoryChatWorkflow {
        private readonly object sync;
        private readonly Action throwIfDisposedLocked;
        private readonly Action throwIfBusyLocked;
        private readonly Func<RoLiveMapSession> requireOpenMapSessionLocked;
        private readonly Func<RoGameplaySnapshot> gameplaySnapshotLocked;
        private readonly Func<int> localActorAidLocked;
        private readonly Func<uint> localActorIdLocked;
        private readonly Func<string> selectedCharacterNameLocked;
        private readonly Func<int> legacyTextCodePageLocked;
        private readonly RoGameplayState gameplayState;
        private readonly RoShellLiveSendExecutor liveSendExecutor;
        private readonly RoShellActionLockState actionLockState;
        private readonly Action gameplayChangedLocked;
        private readonly Action<string> setStatusLocked;
        private readonly Action<int, int, int> targetSkillSent;

        public RoShellHotkeyInventoryChatWorkflow(
            object sync,
            Action throwIfDisposedLocked,
            Action throwIfBusyLocked,
            Func<RoLiveMapSession> requireOpenMapSessionLocked,
            Func<RoGameplaySnapshot> gameplaySnapshotLocked,
            Func<int> localActorAidLocked,
            Func<uint> localActorIdLocked,
            Func<string> selectedCharacterNameLocked,
            Func<int> legacyTextCodePageLocked,
            RoGameplayState gameplayState,
            RoShellLiveSendExecutor liveSendExecutor,
            RoShellActionLockState actionLockState,
            Action gameplayChangedLocked,
            Action<string> setStatusLocked,
            Action<int, int, int> targetSkillSent = null) {
            this.sync = sync ?? throw new ArgumentNullException(nameof(sync));
            this.throwIfDisposedLocked = throwIfDisposedLocked ?? throw new ArgumentNullException(nameof(throwIfDisposedLocked));
            this.throwIfBusyLocked = throwIfBusyLocked ?? throw new ArgumentNullException(nameof(throwIfBusyLocked));
            this.requireOpenMapSessionLocked = requireOpenMapSessionLocked ?? throw new ArgumentNullException(nameof(requireOpenMapSessionLocked));
            this.gameplaySnapshotLocked = gameplaySnapshotLocked ?? throw new ArgumentNullException(nameof(gameplaySnapshotLocked));
            this.localActorAidLocked = localActorAidLocked ?? throw new ArgumentNullException(nameof(localActorAidLocked));
            this.localActorIdLocked = localActorIdLocked ?? throw new ArgumentNullException(nameof(localActorIdLocked));
            this.selectedCharacterNameLocked = selectedCharacterNameLocked ?? throw new ArgumentNullException(nameof(selectedCharacterNameLocked));
            this.legacyTextCodePageLocked = legacyTextCodePageLocked ?? throw new ArgumentNullException(nameof(legacyTextCodePageLocked));
            this.gameplayState = gameplayState ?? throw new ArgumentNullException(nameof(gameplayState));
            this.liveSendExecutor = liveSendExecutor ?? throw new ArgumentNullException(nameof(liveSendExecutor));
            this.actionLockState = actionLockState ?? throw new ArgumentNullException(nameof(actionLockState));
            this.gameplayChangedLocked = gameplayChangedLocked ?? throw new ArgumentNullException(nameof(gameplayChangedLocked));
            this.setStatusLocked = setStatusLocked ?? throw new ArgumentNullException(nameof(setStatusLocked));
            this.targetSkillSent = targetSkillSent;
        }

        public Task BeginUseHotkey(int hotkeyIndex, int targetAid) {
            RoLiveMapSession liveMapSession;
            RoGameplayHotkeyAction action;
            byte[] packet;
            int resolvedSkillTargetAid = 0;

            lock (sync) {
                throwIfDisposedLocked();
                throwIfBusyLocked();
                liveMapSession = requireOpenMapSessionLocked();

                RoGameplaySnapshot gameplay = gameplaySnapshotLocked();
                int localAid = localActorAidLocked();
                packet = RoShellGameplayCommands.BuildHotkey(gameplay, hotkeyIndex, localAid, targetAid, out action);

                if (action.Kind == RoGameplayActionKind.UseSkill) {
                    resolvedSkillTargetAid = RoGameplayActionResolver.ResolveHotkeySkillTargetAid(
                        gameplay,
                        action.SkillId,
                        localAid,
                        targetAid);
                    actionLockState.ThrowIfLocked(DateTime.UtcNow);
                }

                setStatusLocked(RoShellStatusTextFormatter.FormatHotkeyActionStatus(action, "sending"));
            }

            return liveSendExecutor.CompletePreparedSend(liveMapSession, packet, requestStartedUtc => {
                setStatusLocked(RoShellStatusTextFormatter.FormatHotkeyActionStatus(action, "sent"));
                if (action.Kind == RoGameplayActionKind.UseSkill) {
                    actionLockState.Apply(
                        RoMapEntryDrainResult.Empty,
                        localActorIdLocked(),
                        new RoLocalActionLockRequest(
                            RoGameplayActionLockKind.Skill,
                            RoGameplayActionLockResolver.DefaultSkillFallbackMilliseconds,
                            $"hotkey skill {action.SkillId}"),
                        requestStartedUtc);
                    targetSkillSent?.Invoke(action.SkillId, action.SkillLevel, resolvedSkillTargetAid);
                }
            });
        }

        public Task BeginSetHotkey(int hotkeyIndex, RoShortcutKeyEntry shortcut) {
            RoLiveMapSession liveMapSession;
            int tab;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                throwIfBusyLocked();
                liveMapSession = requireOpenMapSessionLocked();

                packet = RoShellGameplayCommands.BuildSetHotkey(gameplaySnapshotLocked(), hotkeyIndex, shortcut, out tab);
                setStatusLocked(RoShellStatusTextFormatter.FormatShortcutChangeStatus(hotkeyIndex, shortcut, "setting"));
            }

            return liveSendExecutor.CompletePreparedSend(liveMapSession, packet, _ => {
                gameplayState.ApplyShortcutKeyChange(tab, hotkeyIndex, shortcut);
                gameplayChangedLocked();
                setStatusLocked(RoShellStatusTextFormatter.FormatShortcutChangeStatus(hotkeyIndex, shortcut, "set"));
            });
        }

        public Task BeginUseInventoryItem(int bagIndex) {
            RoLiveMapSession liveMapSession;
            RoItemInstance item;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                throwIfBusyLocked();
                liveMapSession = requireOpenMapSessionLocked();

                packet = RoShellGameplayCommands.BuildUseInventoryItem(
                    gameplaySnapshotLocked(),
                    bagIndex,
                    localActorAidLocked(),
                    out item);

                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "using"));
            }

            return liveSendExecutor.CompletePreparedSend(liveMapSession, packet, _ => {
                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "used"));
            });
        }

        public Task BeginUseCartItem(int cartIndex) {
            RoLiveMapSession liveMapSession;
            RoItemInstance item;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                throwIfBusyLocked();
                liveMapSession = requireOpenMapSessionLocked();

                packet = RoShellGameplayCommands.BuildUseCartItem(
                    gameplaySnapshotLocked(),
                    cartIndex,
                    localActorAidLocked(),
                    default,
                    out item);

                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "using"));
            }

            return liveSendExecutor.CompletePreparedSend(liveMapSession, packet, _ => {
                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "used"));
            });
        }

        public Task BeginDropInventoryItem(int bagIndex, int count) {
            RoLiveMapSession liveMapSession;
            RoItemInstance item;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                throwIfBusyLocked();
                liveMapSession = requireOpenMapSessionLocked();

                packet = RoShellGameplayCommands.BuildDropInventoryItem(gameplaySnapshotLocked(), bagIndex, count, out item);
                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "dropping"));
            }

            return liveSendExecutor.CompletePreparedSend(liveMapSession, packet, _ => {
                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "sent drop request for"));
            });
        }

        public Task BeginWearInventoryItem(int bagIndex) {
            RoLiveMapSession liveMapSession;
            RoItemInstance item;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                throwIfBusyLocked();
                liveMapSession = requireOpenMapSessionLocked();

                packet = RoShellGameplayCommands.BuildWearInventoryItem(gameplaySnapshotLocked(), bagIndex, out item);
                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "equipping"));
            }

            return liveSendExecutor.CompletePreparedSend(liveMapSession, packet, _ => {
                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "sent equip request for"));
            });
        }

        public Task BeginTakeoffInventoryItem(int bagIndex) {
            RoLiveMapSession liveMapSession;
            RoItemInstance item;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                throwIfBusyLocked();
                liveMapSession = requireOpenMapSessionLocked();

                packet = RoShellGameplayCommands.BuildTakeoffInventoryItem(gameplaySnapshotLocked(), bagIndex, out item);
                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "taking off"));
            }

            return liveSendExecutor.CompletePreparedSend(liveMapSession, packet, _ => {
                setStatusLocked(RoShellStatusTextFormatter.FormatInventoryItemStatus(item, "sent takeoff request for"));
            });
        }

        public Task BeginSendChatMessage(string message) {
            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                liveMapSession = requireOpenMapSessionLocked();

                Encoding encoding = RoShellTextEncoding.Resolve(legacyTextCodePageLocked());
                packet = RoShellGameplayCommands.BuildChatMessage(selectedCharacterNameLocked(), message, encoding);
                setStatusLocked("sending chat");
            }

            try {
                liveMapSession.Send(packet);
            } catch (ObjectDisposedException ex) {
                throw new InvalidOperationException("Map session is not open.", ex);
            }

            lock (sync) {
                setStatusLocked("sent chat");
            }

            return Task.CompletedTask;
        }

        public Task BeginSendPrivateMessage(string receiverName, string message) {
            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                liveMapSession = requireOpenMapSessionLocked();

                Encoding encoding = RoShellTextEncoding.Resolve(legacyTextCodePageLocked());
                packet = RoShellGameplayCommands.BuildPrivateMessage(receiverName, message, encoding);
                setStatusLocked("sending 1:1 chat");
            }

            try {
                liveMapSession.Send(packet);
            } catch (ObjectDisposedException ex) {
                throw new InvalidOperationException("Map session is not open.", ex);
            }

            lock (sync) {
                setStatusLocked("sent 1:1 chat");
            }

            return Task.CompletedTask;
        }

        public Task BeginSendPartyChatMessage(string message) {
            return BeginSendGroupChatMessage(
                message,
                "sending party chat",
                "sent party chat",
                (characterName, chatText, encoding) => RoShellGameplayCommands.BuildPartyChatMessage(characterName, chatText, encoding));
        }

        public Task BeginSendGuildChatMessage(string message) {
            return BeginSendGroupChatMessage(
                message,
                "sending guild chat",
                "sent guild chat",
                (characterName, chatText, encoding) => RoShellGameplayCommands.BuildGuildChatMessage(characterName, chatText, encoding));
        }

        public Task BeginSendClanChatMessage(string message) {
            return BeginSendGroupChatMessage(
                message,
                "sending clan chat",
                "sent clan chat",
                (characterName, chatText, encoding) => RoShellGameplayCommands.BuildClanChatMessage(characterName, chatText, encoding));
        }

        private Task BeginSendGroupChatMessage(
            string message,
            string sendingStatus,
            string sentStatus,
            Func<string, string, Encoding, byte[]> buildPacket) {
            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                throwIfDisposedLocked();
                liveMapSession = requireOpenMapSessionLocked();

                Encoding encoding = RoShellTextEncoding.Resolve(legacyTextCodePageLocked());
                packet = buildPacket(selectedCharacterNameLocked(), message, encoding);
                setStatusLocked(sendingStatus);
            }

            try {
                liveMapSession.Send(packet);
            } catch (ObjectDisposedException ex) {
                throw new InvalidOperationException("Map session is not open.", ex);
            }

            lock (sync) {
                setStatusLocked(sentStatus);
            }

            return Task.CompletedTask;
        }
    }
}
