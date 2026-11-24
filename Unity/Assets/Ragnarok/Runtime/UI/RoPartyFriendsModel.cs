using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ragnarok.Gameplay;
using Ragnarok.Network;
using Ragnarok.World;
using UnityEngine;

namespace Ragnarok.UI {
    /// <summary>The two lists of the Party / Friend window (UIMessengerGroupWnd 0x22): its Friend and Party radios.</summary>
    public enum RoPartyFriendsTab {
        Friends = 0,
        Party = 1
    }

    /// <summary>
    /// The non-UI side of the classic Party / Friend windows (<see cref="RoClassicPartyFriends"/>): the session's party and
    /// friend lists (the own row completed from the status), the leader / online rules, the lock and the tab (per character),
    /// the members detached to small windows, the two questions from the server (ZC_PARTY_JOIN_REQ and ZC_REQ_ADD_FRIENDS,
    /// each answered once) and the party / friend requests, gated like every legacy social command (in the map, no
    /// transition, no action lock).
    /// </summary>
    public sealed class RoPartyFriendsModel {
        private const string PrefsRoot = "Ragnarok.UI.PartyFriends";
        private const string LockPrefsKey = "Ragnarok.UI.PartyFriends.Lock";
        private const string TabPrefsKey = "Ragnarok.UI.PartyFriends.Tab";
        private const float WorldLookupRetrySeconds = 0.5f;

        private readonly HashSet<uint> detachedMembers = new HashSet<uint>();
        private readonly HashSet<ulong> answeredFriendRequests = new HashSet<ulong>();
        private readonly HashSet<ulong> pendingFriendRequestsScratch = new HashSet<ulong>();
        private RoLoginShellSession session;
        private IRoSocialEconomyCommandExecutor commands;
        private RoOnlineWorldController worldController;
        private float nextWorldLookupTime;
        private RoPartyFriendsTab activeTab = RoPartyFriendsTab.Party;
        private bool locked;
        private string commandStatus = string.Empty;
        private uint partyInviteSeen;   // the last invitation a question was opened for
        private uint partyInviteOpen;   // the invitation the open question answers (0: none)

        public RoLoginShellSession Session => session;
        public IRoSocialEconomyCommandExecutor Commands => commands;
        public RoPartyFriendsTab ActiveTab => activeTab;
        public bool Locked => locked;
        public string LastCommandStatus => commandStatus ?? string.Empty;
        public IReadOnlyCollection<uint> DetachedMembers => detachedMembers;
        public uint LocalAccountId => session != null ? session.LocalActorId : 0;

        /// <summary>The invitation the open UIJoinPartyAcceptWnd answers (0: none).</summary>
        public uint OpenPartyInviteId => partyInviteOpen;

        /// <summary>The party and friend lists moved (ZC party / friend packets, the own HP, level and job).</summary>
        public int Sequence => session != null
            ? session.GetFunctionalWindowChangeSequence(RoFunctionalWindowChange.Party | RoFunctionalWindowChange.Friends)
            : 0;

        /// <summary>
        /// The session the lists and requests use; another session (a new login) forgets the detached members and the
        /// questions and reads the character's lock and tab. Without an executor the session's legacy social paths send.
        /// </summary>
        public void Bind(RoLoginShellSession loginSession, IRoSocialEconomyCommandExecutor executor = null) {
            bool changed = !ReferenceEquals(session, loginSession);
            session = loginSession;
            if (executor != null) {
                commands = executor;
            } else if (changed || commands == null) {
                commands = loginSession != null ? new RoLegacySocialEconomyCommandExecutor(loginSession) : null;
            }

            if (!changed) return;
            Reset();
            commandStatus = string.Empty;
            LoadPrefs();
        }

        /// <summary>Leaving the map: no detached members, no question remembered.</summary>
        public void Reset() {
            detachedMembers.Clear();
            answeredFriendRequests.Clear();
            partyInviteSeen = 0;
            partyInviteOpen = 0;
        }

        // ---- the tab and the lock (per character) ----

        public void ShowTab(RoPartyFriendsTab tab) {
            activeTab = tab;
            PlayerPrefs.SetInt(CharacterPrefsPrefix() + "." + TabPrefsKey, (int) activeTab);
            PlayerPrefs.Save();
        }

        public void ToggleLock() {
            locked = !locked;
            PlayerPrefs.SetInt(CharacterPrefsPrefix() + "." + LockPrefsKey, locked ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void LoadPrefs() {
            string key = CharacterPrefsPrefix();
            locked = PlayerPrefs.GetInt(key + "." + LockPrefsKey, 0) != 0;
            activeTab = (RoPartyFriendsTab) Mathf.Clamp(PlayerPrefs.GetInt(key + "." + TabPrefsKey, (int) RoPartyFriendsTab.Party), 0, 1);
        }

        private string CharacterPrefsPrefix() => CharacterPrefsPrefix(session != null ? session.SelectedCharacterName : null);

        private static string CharacterPrefsPrefix(string characterName) {
            if (string.IsNullOrWhiteSpace(characterName)) characterName = "Default";
            return PrefsRoot + "." + characterName.Trim().Replace(' ', '_');
        }

        // ---- Friend Setup (UIFriendOptionWnd 0x44: client-side options, no packet) ----

        /// <summary>The three Friend Setup options, for the character and as the default of the others.</summary>
        public void SaveFriendSetup(bool openStranger, bool openFriend, bool alarm) {
            string key = CharacterPrefsPrefix();
            PlayerPrefs.SetInt(key + ".FriendSetup.OpenStranger", openStranger ? 1 : 0);
            PlayerPrefs.SetInt(key + ".FriendSetup.OpenFriend", openFriend ? 1 : 0);
            PlayerPrefs.SetInt(key + ".FriendSetup.Alarm", alarm ? 1 : 0);
            PlayerPrefs.SetInt(PrefsRoot + ".FriendSetup.OpenStranger", openStranger ? 1 : 0);
            PlayerPrefs.SetInt(PrefsRoot + ".FriendSetup.OpenFriend", openFriend ? 1 : 0);
            PlayerPrefs.SetInt(PrefsRoot + ".FriendSetup.Alarm", alarm ? 1 : 0);
            PlayerPrefs.Save();
            commandStatus = "Friend setup saved.";
        }

        public bool LoadFriendSetupPreference(string preferenceName, bool defaultValue) =>
            LoadFriendSetupPreference(session != null ? session.SelectedCharacterName : null, preferenceName, defaultValue);

        /// <summary>A Friend Setup option (OpenStranger / OpenFriend / Alarm) of the character, else the last saved one.</summary>
        public static bool LoadFriendSetupPreference(string characterName, string preferenceName, bool defaultValue) {
            if (string.IsNullOrWhiteSpace(preferenceName)) return defaultValue;
            int fallback = defaultValue ? 1 : 0;
            string characterKey = CharacterPrefsPrefix(characterName) + ".FriendSetup." + preferenceName;
            if (PlayerPrefs.HasKey(characterKey)) return PlayerPrefs.GetInt(characterKey, fallback) != 0;
            return PlayerPrefs.GetInt(PrefsRoot + ".FriendSetup." + preferenceName, fallback) != 0;
        }

        // ---- the lists ----

        /// <summary>The session's party, the own row's HP, job and level taken from the status (fresher than the party packets).</summary>
        public RoPartyRuntimeSnapshot ResolvePartySnapshot() {
            RoPartyRuntimeSnapshot party = session != null
                ? session.PartySnapshot()
                : new RoPartyRuntimeSnapshot(string.Empty, 0, string.Empty, false, string.Empty, 0, 0, 0, 0, Array.Empty<RoPartyMemberRuntimeSnapshot>());
            return EnrichLocalPartyMember(party);
        }

        private RoPartyRuntimeSnapshot EnrichLocalPartyMember(RoPartyRuntimeSnapshot party) {
            if (session == null || party.Members == null || party.Members.Length == 0 || session.LocalActorId == 0) {
                return party;
            }

            RoPlayerStatusSnapshot status = session.BasicInfoSnapshot().Status ?? RoPlayerStatusSnapshot.Empty;
            if (status.MaxHp <= 0 && status.BaseLevel <= 0 && status.JobId <= 0) {
                return party;
            }

            RoPartyMemberRuntimeSnapshot[] members = null;
            for (int i = 0; i < party.Members.Length; i++) {
                RoPartyMemberRuntimeSnapshot member = party.Members[i];
                if (member.AccountId != session.LocalActorId) {
                    continue;
                }

                RoPartyMemberRuntimeSnapshot enriched = member;
                if (status.MaxHp > 0) {
                    enriched = enriched.WithHp(status.Hp, status.MaxHp);
                }

                int classId = status.JobId > 0 ? status.JobId : enriched.ClassId;
                int baseLevel = status.BaseLevel > 0 ? status.BaseLevel : enriched.BaseLevel;
                if (classId != enriched.ClassId || baseLevel != enriched.BaseLevel) {
                    enriched = enriched.WithJobAndLevel(classId, baseLevel);
                }

                if (enriched.Equals(member)) {
                    return party;
                }

                members = new RoPartyMemberRuntimeSnapshot[party.Members.Length];
                Array.Copy(party.Members, members, party.Members.Length);
                members[i] = enriched;
                break;
            }

            if (members == null) {
                return party;
            }

            return new RoPartyRuntimeSnapshot(
                party.PartyName,
                party.IsInParty,
                party.LeaderAccountId,
                party.RefuseJoinMessages,
                party.PendingInvitationId,
                party.PendingInvitationName,
                party.HasLastInvitationAnswer,
                party.LastInvitationAnswerCharacterName,
                party.LastInvitationAnswerCode,
                party.ExpOption,
                party.ItemPickupRule,
                party.ItemDivisionRule,
                members);
        }

        public void ResolveFriendSnapshot(List<RoFriendRuntimeSnapshot> friends) {
            if (friends == null) return;
            if (session != null) session.FriendsSnapshot(friends);
            else friends.Clear();
        }

        /// <summary>The own character leads: the party's leader AID, else the own row's role 0.</summary>
        public bool IsLocalLeader(RoPartyRuntimeSnapshot party) {
            uint localActorId = LocalAccountId;
            if (localActorId == 0) return false;
            if (party.LeaderAccountId != 0) return party.LeaderAccountId == localActorId;
            if (party.Members == null) return false;
            for (int i = 0; i < party.Members.Length; i++) {
                if (party.Members[i].AccountId == localActorId) return party.Members[i].Role == 0;
            }

            return false;
        }

        public static bool IsPartyLeaderMember(RoPartyRuntimeSnapshot party, RoPartyMemberRuntimeSnapshot member) =>
            party.LeaderAccountId != 0 ? party.LeaderAccountId == member.AccountId : member.Role == 0;

        public static bool IsInParty(RoPartyRuntimeSnapshot party) => party.IsInParty || (party.Members != null && party.Members.Length > 0);

        public static bool IsOnlinePartyMember(RoPartyMemberRuntimeSnapshot member) => member.State == 0 && !member.IsDead;

        public static ulong FriendKey(uint accountId, uint characterId) => ((ulong) accountId << 32) | characterId;

        /// <summary>The member's actor is in the client's view (the player object for oneself).</summary>
        public bool IsInView(uint accountId) {
            if (accountId == LocalAccountId) return true;
            RoOnlineWorldController world = ResolveWorldController();
            return world != null && world.State.TryGetActorByAccountId(accountId, out _);
        }

        private RoOnlineWorldController ResolveWorldController() {
            if (worldController != null) return worldController;
            float now = Time.unscaledTime;
            if (now < nextWorldLookupTime) return null;
            nextWorldLookupTime = now + WorldLookupRetrySeconds;
            worldController = UnityEngine.Object.FindFirstObjectByType<RoOnlineWorldController>();
            return worldController;
        }

        // ---- the small party windows ----

        /// <summary>An online member gets a small window (not while locked).</summary>
        public bool Detach(RoPartyMemberRuntimeSnapshot member) {
            if (member.AccountId == 0 || locked || !IsOnlinePartyMember(member)) return false;
            return detachedMembers.Add(member.AccountId);
        }

        public bool IsDetached(uint accountId) => detachedMembers.Contains(accountId);

        public bool RemoveDetached(uint accountId) => detachedMembers.Remove(accountId);

        // ---- the questions from the server ----

        /// <summary>
        /// ZC_PARTY_JOIN_REQ (0x02c6): a new invitation opens the question (or replaces the open one); the invitation gone
        /// (the party state cleared) closes it. Returns 1 to show the question, -1 to close it, 0 for nothing.
        /// </summary>
        public int FollowPartyInvite(RoPartyRuntimeSnapshot party) {
            uint id = party.PendingInvitationId;
            if (id != 0 && id != partyInviteSeen) {
                partyInviteSeen = id;
                partyInviteOpen = id;
                return 1;
            }

            if (id == 0) {
                partyInviteSeen = 0;
                if (partyInviteOpen != 0) {
                    partyInviteOpen = 0;
                    return -1;
                }
            }

            return 0;
        }

        /// <summary>OK / Cancel of UIJoinPartyAcceptWnd: CZ_PARTY_JOIN_REQ_ACK {GRID, 1 / 0}, once per question.</summary>
        public Task AnswerPartyInvite(bool accept) {
            uint id = partyInviteOpen;
            partyInviteOpen = 0;
            return id != 0 ? RespondPartyInviteAsync(id, accept) : Task.CompletedTask;
        }

        /// <summary>
        /// ZC_REQ_ADD_FRIENDS (0x0207): the newest request not answered yet (the answered ones the server no longer lists
        /// are forgotten).
        /// </summary>
        public bool TryGetPendingFriendRequest(List<RoFriendRuntimeSnapshot> friends, out RoFriendRuntimeSnapshot pending) {
            pending = default;
            bool found = false;
            pendingFriendRequestsScratch.Clear();
            if (friends != null) {
                foreach (RoFriendRuntimeSnapshot f in friends) {
                    if (!f.HasPendingRequest || f.AccountId == 0) continue;
                    ulong key = FriendKey(f.AccountId, f.CharacterId);
                    pendingFriendRequestsScratch.Add(key);
                    if (answeredFriendRequests.Contains(key)) continue;
                    pending = f;
                    found = true;
                }
            }

            answeredFriendRequests.IntersectWith(pendingFriendRequestsScratch);
            pendingFriendRequestsScratch.Clear();
            return found;
        }

        /// <summary>OK / Cancel of UIJoinFriendAcceptWnd: CZ_ACK_REQ_ADD_FRIENDS {AID, GID, 1 / 0}, once per request.</summary>
        public Task AnswerFriendRequest(RoFriendRuntimeSnapshot request, bool accept) {
            if (!answeredFriendRequests.Add(FriendKey(request.AccountId, request.CharacterId))) return Task.CompletedTask;
            return RespondFriendRequestAsync(request, accept);
        }

        // ---- the requests ----

        public async Task RespondPartyInviteAsync(uint invitationId, bool accept) {
            if (!TryBeginCommand()) return;
            SetCommandStatus(accept ? "Accepting party invite..." : "Declining party invite...");
            SetCommandStatus(await ExecuteSocialAsync(
                commands.RespondPartyInviteAsync(invitationId, accept),
                accept ? "Party invite accepted." : "Party invite declined."));
        }

        public async Task RespondFriendRequestAsync(RoFriendRuntimeSnapshot friend, bool accept) {
            if (!TryBeginCommand()) return;
            SetCommandStatus(accept ? "Accepting friend request..." : "Declining friend request...");
            SetCommandStatus(await ExecuteSocialAsync(
                commands.RespondFriendRequestAsync(friend.AccountId, friend.CharacterId, accept),
                accept ? "Friend request accepted." : "Friend request declined."));
        }

        public async Task LeavePartyAsync() {
            if (!TryBeginCommand()) return;
            SetCommandStatus("Leaving party...");
            SetCommandStatus(await ExecuteSocialAsync(commands.LeavePartyAsync(), "Party leave request sent."));
        }

        public async Task RemoveFriendAsync(RoFriendRuntimeSnapshot friend) {
            if (!TryBeginCommand()) return;
            SetCommandStatus("Removing friend...");
            SetCommandStatus(await ExecuteSocialAsync(commands.RemoveFriendAsync(friend.AccountId, friend.CharacterId), "Friend remove request sent."));
        }

        public async Task ExpelPartyMemberAsync(RoPartyMemberRuntimeSnapshot member) {
            if (!TryBeginCommand()) return;
            SetCommandStatus("Expelling party member...");
            SetCommandStatus(await ExecuteSocialAsync(commands.ExpelPartyMemberAsync(member.AccountId, member.Name), "Party expel request sent."));
        }

        public async Task ChangePartyLeaderAsync(RoPartyMemberRuntimeSnapshot member) {
            if (!TryBeginCommand()) return;
            SetCommandStatus("Changing party leader...");
            SetCommandStatus(await ExecuteSocialAsync(commands.ChangePartyLeaderAsync(member.AccountId), "Party leader change request sent."));
        }

        /// <summary>CZ_MAKE_GROUP2 (Create Party's OK).</summary>
        public async Task CreatePartyAsync(string partyName, byte pickupRule, byte divisionRule) {
            if (!TryBeginCommand()) return;
            if (string.IsNullOrWhiteSpace(partyName)) {
                SetCommandStatus("Enter a party name.");
                return;
            }

            SetCommandStatus("Creating party...");
            SetCommandStatus(await ExecuteSocialAsync(commands.CreatePartyAsync(partyName.Trim(), pickupRule, divisionRule), "Party create request sent."));
        }

        /// <summary>CZ_PARTY_JOIN_REQ by name (Party Invitation's OK).</summary>
        public async Task InviteAsync(string characterName) {
            if (!TryBeginCommand()) return;
            if (string.IsNullOrWhiteSpace(characterName)) {
                SetCommandStatus("Enter a character name to invite.");
                return;
            }

            SetCommandStatus("Sending party invite...");
            SetCommandStatus(await ExecuteSocialAsync(commands.InvitePartyAsync(characterName.Trim()), "Party invite sent."));
        }

        /// <summary>mode->Msg(0x3b, name): CZ_PARTY_JOIN_REQ by name from a friend row, sent as it is (no gate).</summary>
        public void InviteByName(string name) {
            if (commands != null && !string.IsNullOrWhiteSpace(name)) _ = commands.InvitePartyAsync(name.Trim());
        }

        /// <summary>CZ_GROUPINFO_CHANGE_V2 (Party Setup's OK).</summary>
        public async Task UpdatePartyOptionsAsync(uint expOption, byte pickupRule, byte divisionRule) {
            if (!TryBeginCommand()) return;
            SetCommandStatus("Updating party setup...");
            SetCommandStatus(await ExecuteSocialAsync(commands.UpdatePartyOptionsAsync(expOption, pickupRule, divisionRule), "Party setup request sent."));
        }

        private bool TryBeginCommand() {
            if (!CanSendCommand(out string reason)) {
                SetCommandStatus(reason);
                return false;
            }

            return true;
        }

        private bool CanSendCommand(out string reason) {
            if (session == null) {
                reason = "No active session.";
                return false;
            }

            if (commands == null) {
                reason = "Command route is not available.";
                return false;
            }

            RoShellCommandStateSnapshot snapshot = session.CommandStateSnapshot();
            if (snapshot.Stage != RoLoginShellStage.MapEntered) {
                reason = "Enter the map before using this workflow.";
                return false;
            }

            if (snapshot.IsBusy || snapshot.IsMapTransitionPending) {
                reason = "Finish the current transition first.";
                return false;
            }

            if (snapshot.IsActionLocked) {
                reason = string.IsNullOrWhiteSpace(snapshot.ActionLockReason)
                    ? "Action is still cooling down."
                    : snapshot.ActionLockReason;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static async Task<string> ExecuteSocialAsync(Task<RoSocialCommandResult> task, string acceptedStatus) {
            RoSocialCommandResult result = await task;
            return result.Accepted
                ? acceptedStatus
                : string.IsNullOrWhiteSpace(result.Message) ? result.Status.ToString() : result.Message;
        }

        private void SetCommandStatus(string value) => commandStatus = value ?? string.Empty;
    }
}
