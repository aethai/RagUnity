using System;
using Ragnarok.Gameplay;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal delegate bool RoShellTryGetOpenMapSession(out RoLiveMapSession liveMapSession);

    internal sealed class RoShellDirectGameplaySender {
        private readonly object sync;
        private readonly Func<bool> isDisposedLocked;
        private readonly Func<RoLiveMapSession, bool> isCurrentSessionLocked;
        private readonly RoShellTryGetOpenMapSession tryGetOpenSessionLocked;
        private readonly RoShellLiveSendExecutor liveSendExecutor;
        private readonly RoShellActionLockState actionLockState;
        private readonly Func<uint> localActorIdLocked;
        private readonly Action<string> setStatusLocked;

        public RoShellDirectGameplaySender(
            object sync,
            Func<bool> isDisposedLocked,
            Func<RoLiveMapSession, bool> isCurrentSessionLocked,
            RoShellTryGetOpenMapSession tryGetOpenSessionLocked,
            RoShellLiveSendExecutor liveSendExecutor,
            RoShellActionLockState actionLockState,
            Func<uint> localActorIdLocked,
            Action<string> setStatusLocked) {
            this.sync = sync ?? throw new ArgumentNullException(nameof(sync));
            this.isDisposedLocked = isDisposedLocked ?? throw new ArgumentNullException(nameof(isDisposedLocked));
            this.isCurrentSessionLocked = isCurrentSessionLocked ?? throw new ArgumentNullException(nameof(isCurrentSessionLocked));
            this.tryGetOpenSessionLocked = tryGetOpenSessionLocked ?? throw new ArgumentNullException(nameof(tryGetOpenSessionLocked));
            this.liveSendExecutor = liveSendExecutor ?? throw new ArgumentNullException(nameof(liveSendExecutor));
            this.actionLockState = actionLockState ?? throw new ArgumentNullException(nameof(actionLockState));
            this.localActorIdLocked = localActorIdLocked ?? throw new ArgumentNullException(nameof(localActorIdLocked));
            this.setStatusLocked = setStatusLocked ?? throw new ArgumentNullException(nameof(setStatusLocked));
        }

        public bool TrySendMoveRequest(int x, int y, int direction) {
            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                if (!tryGetOpenSessionLocked(out liveMapSession)) {
                    return false;
                }

                try {
                    packet = liveMapSession.BuildMoveRequest(x, y, direction);
                } catch (ArgumentException) {
                    return false;
                } catch (InvalidOperationException) {
                    return false;
                }
            }

            return liveSendExecutor.TrySend(liveMapSession, packet);
        }

        public bool TrySendAttackRequest(int targetAid, bool repeatAttack) {
            if (targetAid <= 0) {
                return false;
            }

            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                if (!tryGetOpenSessionLocked(out liveMapSession) ||
                    actionLockState.GetRemainingMilliseconds(DateTime.UtcNow) > 0) {
                    return false;
                }

                try {
                    packet = liveMapSession.BuildAttackRequest((uint) targetAid, repeatAttack);
                } catch (ArgumentException) {
                    return false;
                } catch (InvalidOperationException) {
                    return false;
                }
            }

            if (!liveSendExecutor.TrySend(liveMapSession, packet)) {
                return false;
            }

            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    actionLockState.Apply(
                        RoMapEntryDrainResult.Empty,
                        localActorIdLocked(),
                        new RoLocalActionLockRequest(
                            RoGameplayActionLockKind.Attack,
                            RoGameplayActionLockResolver.DefaultAttackFallbackMilliseconds,
                            $"attack target {targetAid}"),
                        DateTime.UtcNow);
                    setStatusLocked($"sent attack request for target {targetAid}");
                }
            }

            return true;
        }

        public bool TrySendPickupGroundItemRequest(int itemAid) {
            if (itemAid <= 0) {
                return false;
            }

            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                if (!tryGetOpenSessionLocked(out liveMapSession) ||
                    actionLockState.GetRemainingMilliseconds(DateTime.UtcNow) > 0) {
                    return false;
                }

                try {
                    packet = liveMapSession.BuildPickupGroundItemRequest((uint) itemAid);
                } catch (ArgumentException) {
                    return false;
                } catch (InvalidOperationException) {
                    return false;
                }
            }

            if (!liveSendExecutor.TrySend(liveMapSession, packet)) {
                return false;
            }

            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    actionLockState.Apply(
                        RoMapEntryDrainResult.Empty,
                        localActorIdLocked(),
                        new RoLocalActionLockRequest(
                            RoGameplayActionLockKind.Pickup,
                            RoGameplayActionLockResolver.DefaultPickupFallbackMilliseconds,
                            $"pickup item {itemAid}"),
                        DateTime.UtcNow);
                    setStatusLocked($"sent pickup request for item {itemAid}");
                }
            }

            return true;
        }

        /// <summary>A packet built by the caller, sent as it is while the map session is open (no action lock).</summary>
        public bool TrySendRawPacket(byte[] packet) {
            RoLiveMapSession liveMapSession;
            lock (sync) {
                if (packet == null || !tryGetOpenSessionLocked(out liveMapSession)) {
                    return false;
                }
            }

            return liveSendExecutor.TrySend(liveMapSession, packet);
        }

        public bool TrySendTargetSkillRequest(int skillId, int skillLevel, int targetAid) {
            if (skillId <= 0 || skillLevel <= 0 || targetAid <= 0) {
                return false;
            }

            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                if (!tryGetOpenSessionLocked(out liveMapSession) ||
                    actionLockState.GetRemainingMilliseconds(DateTime.UtcNow) > 0) {
                    return false;
                }

                try {
                    packet = liveMapSession.BuildTargetSkillRequest(skillLevel, skillId, (uint) targetAid);
                } catch (ArgumentException) {
                    return false;
                } catch (InvalidOperationException) {
                    return false;
                }
            }

            if (!liveSendExecutor.TrySend(liveMapSession, packet)) {
                return false;
            }

            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    actionLockState.Apply(
                        RoMapEntryDrainResult.Empty,
                        localActorIdLocked(),
                        new RoLocalActionLockRequest(
                            RoGameplayActionLockKind.Skill,
                            RoGameplayActionLockResolver.DefaultSkillFallbackMilliseconds,
                            $"skill {skillId} target {targetAid}"),
                        DateTime.UtcNow);
                    setStatusLocked($"sent skill {skillId} request for target {targetAid}");
                }
            }

            return true;
        }

        public bool TrySendActorNameRequest(uint actorId) {
            if (actorId == 0) {
                return false;
            }

            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                if (!tryGetOpenSessionLocked(out liveMapSession)) {
                    return false;
                }

                try {
                    packet = liveMapSession.BuildActorNameRequest(actorId);
                } catch (ArgumentException) {
                    return false;
                } catch (InvalidOperationException) {
                    return false;
                }
            }

            if (!liveSendExecutor.TrySend(liveMapSession, packet)) {
                return false;
            }

            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    setStatusLocked($"sent name request for actor {actorId}");
                }
            }

            return true;
        }

        public bool TrySendSitStandRequest(bool sit) {
            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                if (!tryGetOpenSessionLocked(out liveMapSession) ||
                    actionLockState.GetRemainingMilliseconds(DateTime.UtcNow) > 0) {
                    return false;
                }

                try {
                    packet = liveMapSession.BuildSitStandRequest(sit);
                } catch (ArgumentException) {
                    return false;
                } catch (InvalidOperationException) {
                    return false;
                }
            }

            if (!liveSendExecutor.TrySend(liveMapSession, packet)) {
                return false;
            }

            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    setStatusLocked(sit ? "sent sit request" : "sent stand request");
                }
            }

            return true;
        }

        public bool TrySendRestartRequest(byte type) {
            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                if (!tryGetOpenSessionLocked(out liveMapSession)) {
                    return false;
                }

                try {
                    packet = liveMapSession.BuildRestartRequest(type);
                } catch (ArgumentException) {
                    return false;
                } catch (InvalidOperationException) {
                    return false;
                }
            }

            if (!liveSendExecutor.TrySend(liveMapSession, packet)) {
                return false;
            }

            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    setStatusLocked(type == 0 ? "sent save point restart request" : "sent character select restart request");
                }
            }

            return true;
        }

        public bool TrySendStandingResurrectionRequest() {
            RoLiveMapSession liveMapSession;
            byte[] packet;

            lock (sync) {
                if (!tryGetOpenSessionLocked(out liveMapSession)) {
                    return false;
                }

                try {
                    packet = liveMapSession.BuildStandingResurrectionRequest();
                } catch (ArgumentException) {
                    return false;
                } catch (InvalidOperationException) {
                    return false;
                }
            }

            if (!liveSendExecutor.TrySend(liveMapSession, packet)) {
                return false;
            }

            lock (sync) {
                if (!isDisposedLocked() && isCurrentSessionLocked(liveMapSession)) {
                    setStatusLocked("sent standing resurrection request");
                }
            }

            return true;
        }
    }
}
