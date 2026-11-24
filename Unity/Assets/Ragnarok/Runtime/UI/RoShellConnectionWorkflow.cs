using System;
using System.Text;
using System.Threading.Tasks;
using Ragnarok.Effects;
using Ragnarok.Network;

namespace Ragnarok.UI {
    internal readonly struct RoShellLoginRequest {
        public RoShellLoginRequest(string account, string password) {
            if (string.IsNullOrWhiteSpace(account)) {
                throw new ArgumentException("Account is required.", nameof(account));
            }

            Account = account;
            Password = password ?? string.Empty;
        }

        public string Account { get; }
        public string Password { get; }
    }

    internal readonly struct RoShellCharacterRequest {
        public RoShellCharacterRequest(
            string account,
            string password,
            string characterName,
            int characterSlot,
            ushort hairColor,
            ushort hairStyle,
            int startJob,
            byte? sex) {
            if (string.IsNullOrWhiteSpace(account)) {
                throw new ArgumentException("Account is required.", nameof(account));
            }

            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            Account = account;
            Password = password ?? string.Empty;
            CharacterName = characterName.Trim();
            CharacterSlot = characterSlot;
            HairColor = hairColor;
            HairStyle = hairStyle;
            StartJob = startJob;
            Sex = sex;
        }

        public string Account { get; }
        public string Password { get; }
        public string CharacterName { get; }
        public int CharacterSlot { get; }
        public ushort HairColor { get; }
        public ushort HairStyle { get; }
        public int StartJob { get; }
        public byte? Sex { get; }
    }

    internal sealed class RoShellConnectionWorkflow {
        private readonly IRoPacketTraceSink traceSink;
        private readonly int timeoutMilliseconds;
        private readonly RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck;
        private readonly RoShellRuntimeConnectionWorkflow runtimeWorkflow;
        private readonly bool enableVerboseNetworkTraceMessages;

        public RoShellConnectionWorkflow(
            IRoPacketTraceSink traceSink,
            int timeoutMilliseconds,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null,
            bool enableVerboseNetworkTraceMessages = false) {
            this.traceSink = traceSink;
            this.timeoutMilliseconds = timeoutMilliseconds <= 0 ? 5000 : timeoutMilliseconds;
            this.loadRuntimeMapBeforeAck = loadRuntimeMapBeforeAck;
            this.enableVerboseNetworkTraceMessages = enableVerboseNetworkTraceMessages;
            runtimeWorkflow = new RoShellRuntimeConnectionWorkflow(traceSink, this.timeoutMilliseconds, enableVerboseNetworkTraceMessages);
        }

        public RoLoginSessionResult LoadCharacters(RoServerProfile profile, RoShellLoginRequest request) {
            return runtimeWorkflow.LoadCharacters(profile, request);
        }

        public RoShellCharacterRuntimeSession OpenCharacterRuntime(RoServerProfile profile, RoShellLoginRequest request) {
            return runtimeWorkflow.OpenCharacterRuntime(profile, request);
        }

        /// <summary>The login alone: AC_ACCEPT_LOGIN's character servers, kept until one is chosen (UISelectServerWnd 0x2).</summary>
        public RoShellPendingLogin Login(RoServerProfile profile, RoShellLoginRequest request) {
            return runtimeWorkflow.Login(profile, request);
        }

        /// <summary>The chosen character server of a pending login and its character list.</summary>
        public RoShellCharacterRuntimeSession OpenCharacterRuntime(RoShellPendingLogin pending, int serverIndex) {
            return runtimeWorkflow.OpenCharacterRuntime(pending, serverIndex);
        }

        public RoLoginEnterMapResult EnterMap(RoServerProfile profile, RoShellCharacterRequest request) {
            return CreateClient(profile).LoginCreateCharacterSelectEnterMapLoadInitialStateAndOpenSession(
                request.Account,
                request.Password,
                request.CharacterName,
                request.CharacterSlot,
                request.HairColor,
                request.HairStyle,
                request.StartJob,
                request.Sex,
                loadRuntimeMapBeforeAck);
        }

        public RoLoginEnterMapResult EnterMap(
            RoServerProfile profile,
            RoShellCharacterRequest request,
            RoShellCharacterRuntimeSession runtimeSession) {
            if (runtimeSession == null) {
                throw new ArgumentNullException(nameof(runtimeSession));
            }

            if (!runtimeSession.Matches(profile, request)) {
                throw new InvalidOperationException("Cached character runtime session does not match the requested account.");
            }

            RoCharSelectFlowResult selection = runtimeSession.SelectCharacterForMapEntry(request.CharacterSlot);
            return CreateClient(profile).EnterMapFromHandoff(selection, loadRuntimeMapBeforeAck);
        }

        public RoLoginCreateCharacterResult CreateCharacter(RoServerProfile profile, RoShellCharacterRequest request) {
            using (RoShellCharacterRuntimeSession runtimeSession =
                   runtimeWorkflow.OpenCharacterRuntime(profile, request.ToLoginRequest())) {
                return runtimeSession.CreateCharacter(request);
            }
        }

        private RoLoginSessionClient CreateClient(RoServerProfile profile) {
            RoServerProfile currentProfile = profile.PacketVersion == 0
                ? RoServerProfile.LocalRathena20200401()
                : profile;
            IRoPacketProfile packetProfile = RoPacketProfiles.ForServerProfile(currentProfile);

            return new RoLoginSessionClient(
                currentProfile,
                packetProfile.CreateRegistry(),
                packetProfile.CreatePacketTransform(),
                traceSink: traceSink,
                timeoutMilliseconds: timeoutMilliseconds,
                enableVerboseTraceMessages: enableVerboseNetworkTraceMessages);
        }
    }

    internal sealed class RoShellRuntimeConnectionWorkflow {
        private readonly IRoPacketTraceSink traceSink;
        private readonly int timeoutMilliseconds;
        private readonly bool enableVerboseNetworkTraceMessages;

        public RoShellRuntimeConnectionWorkflow(
            IRoPacketTraceSink traceSink,
            int timeoutMilliseconds,
            bool enableVerboseNetworkTraceMessages = false) {
            this.traceSink = traceSink;
            this.timeoutMilliseconds = timeoutMilliseconds <= 0 ? 5000 : timeoutMilliseconds;
            this.enableVerboseNetworkTraceMessages = enableVerboseNetworkTraceMessages;
        }

        public RoLoginSessionResult LoadCharacters(RoServerProfile profile, RoShellLoginRequest request) {
            using (RoShellCharacterRuntimeSession runtimeSession = OpenCharacterRuntime(profile, request)) {
                return runtimeSession.ToLoginSessionResult();
            }
        }

        public RoShellCharacterRuntimeSession OpenCharacterRuntime(RoServerProfile profile, RoShellLoginRequest request) {
            RoServerProfile currentProfile = NormalizeProfile(profile);
            IRoPacketProfile packetProfile = RoPacketProfiles.ForServerProfile(currentProfile);
            var options = new RoClientRuntimeOptions(
                currentProfile,
                packetProfile,
                endpoint => new RoTcpTransport(),
                traceSink: traceSink,
                operationTimeout: TimeSpan.FromMilliseconds(timeoutMilliseconds),
                textEncoding: ResolveEncoding(currentProfile.LegacyTextCodePage),
                enableVerboseNetworkTraceMessages: enableVerboseNetworkTraceMessages);

            var runtime = new RoClientRuntime(options);
            try {
                RoLoginFlowResult login = runtime
                    .LoginAsync(request.Account, request.Password)
                    .GetAwaiter()
                    .GetResult();
                RoCharFlowResult characters = runtime
                    .LoadCharactersAsync(login)
                    .GetAwaiter()
                    .GetResult();
                return new RoShellCharacterRuntimeSession(currentProfile, request, runtime, login, characters);
            } catch {
                runtime.Dispose();
                throw;
            }
        }

        public RoShellPendingLogin Login(RoServerProfile profile, RoShellLoginRequest request) {
            RoServerProfile currentProfile = NormalizeProfile(profile);
            IRoPacketProfile packetProfile = RoPacketProfiles.ForServerProfile(currentProfile);
            var options = new RoClientRuntimeOptions(
                currentProfile,
                packetProfile,
                endpoint => new RoTcpTransport(),
                traceSink: traceSink,
                operationTimeout: TimeSpan.FromMilliseconds(timeoutMilliseconds),
                textEncoding: ResolveEncoding(currentProfile.LegacyTextCodePage),
                enableVerboseNetworkTraceMessages: enableVerboseNetworkTraceMessages);

            var runtime = new RoClientRuntime(options);
            try {
                RoLoginFlowResult login = runtime
                    .LoginAsync(request.Account, request.Password)
                    .GetAwaiter()
                    .GetResult();
                return new RoShellPendingLogin(currentProfile, request, runtime, login);
            } catch {
                runtime.Dispose();
                throw;
            }
        }

        public RoShellCharacterRuntimeSession OpenCharacterRuntime(RoShellPendingLogin pending, int serverIndex) {
            if (pending == null) {
                throw new ArgumentNullException(nameof(pending));
            }

            RoLoginFlowResult login = pending.WithServer(serverIndex);
            RoClientRuntime runtime = pending.TakeRuntime();
            try {
                RoCharFlowResult characters = runtime
                    .LoadCharactersAsync(login)
                    .GetAwaiter()
                    .GetResult();
                return new RoShellCharacterRuntimeSession(pending.Profile, pending.Request, runtime, login, characters);
            } catch {
                runtime.Dispose();
                throw;
            }
        }

        private static RoServerProfile NormalizeProfile(RoServerProfile profile) {
            return profile.PacketVersion == 0
                ? RoServerProfile.LocalRathena20200401()
                : profile;
        }

        private static Encoding ResolveEncoding(int codePage) {
            try {
                return Encoding.GetEncoding(codePage);
            } catch (ArgumentException) {
                return Encoding.ASCII;
            } catch (NotSupportedException) {
                return Encoding.ASCII;
            }
        }
    }

    /// <summary>
    /// A login between AC_ACCEPT_LOGIN and the character server the player picks: the runtime that logged in and the
    /// server list. The runtime moves to the character session once a server is chosen; disposing drops it otherwise.
    /// </summary>
    internal sealed class RoShellPendingLogin : IDisposable {
        private readonly RoLoginFlowResult login;
        private RoClientRuntime runtime;

        public RoShellPendingLogin(RoServerProfile profile, RoShellLoginRequest request, RoClientRuntime runtime, RoLoginFlowResult login) {
            Profile = profile;
            Request = request;
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.login = login ?? throw new ArgumentNullException(nameof(login));
        }

        public RoServerProfile Profile { get; }
        public RoShellLoginRequest Request { get; }
        public RoLoginCharServerInfo[] Servers => login.Login.Servers ?? Array.Empty<RoLoginCharServerInfo>();

        /// <summary>AC_ACCEPT_LOGIN 0x0ac4's web auth token (the web API's AuthToken).</summary>
        public string WebAuthToken => login.Login.WebAuthToken ?? string.Empty;

        /// <summary>The login with <paramref name="index"/> as its character server.</summary>
        public RoLoginFlowResult WithServer(int index) {
            RoLoginCharServerInfo[] servers = Servers;
            if (index < 0 || index >= servers.Length) {
                throw new ArgumentOutOfRangeException(nameof(index), "No character server " + index + " in the login's list of " + servers.Length + ".");
            }

            return new RoLoginFlowResult(login.Profile, login.PacketProfile, login.Login, servers[index]);
        }

        public RoClientRuntime TakeRuntime() {
            RoClientRuntime taken = runtime ?? throw new ObjectDisposedException(nameof(RoShellPendingLogin));
            runtime = null;
            return taken;
        }

        public void Dispose() {
            runtime?.Dispose();
            runtime = null;
        }
    }

    internal sealed class RoShellCharacterRuntimeSession : IDisposable {
        private readonly RoServerProfile profile;
        private readonly RoShellLoginRequest request;
        private readonly RoClientRuntime runtime;
        private RoLoginFlowResult login;
        private RoCharFlowResult characters;
        private int? selectedMapEntrySlot;
        private RoCharSelectFlowResult selectedMapEntry;
        private bool disposed;

        public RoShellCharacterRuntimeSession(
            RoServerProfile profile,
            RoShellLoginRequest request,
            RoClientRuntime runtime,
            RoLoginFlowResult login,
            RoCharFlowResult characters) {
            this.profile = profile;
            this.request = request;
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.login = login ?? throw new ArgumentNullException(nameof(login));
            this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        }

        public RoCharFlowResult Characters => characters;

        public bool Matches(RoServerProfile candidateProfile, RoShellCharacterRequest candidateRequest) {
            return Matches(candidateProfile, candidateRequest.ToLoginRequest());
        }

        public bool Matches(RoServerProfile candidateProfile, RoShellLoginRequest candidateRequest) {
            RoServerProfile normalizedProfile = NormalizeProfile(candidateProfile);
            return profile.Equals(normalizedProfile) &&
                string.Equals(request.Account, candidateRequest.Account, StringComparison.Ordinal) &&
                string.Equals(request.Password, candidateRequest.Password, StringComparison.Ordinal);
        }

        public RoLoginSessionResult ToLoginSessionResult() {
            ThrowIfDisposed();
            return new RoLoginSessionResult(
                profile,
                login.Login,
                login.SelectedCharacterServer,
                characters.Characters);
        }

        public RoCharSelectFlowResult SelectCharacterForMapEntry(int characterSlot) {
            ThrowIfDisposed();
            if (selectedMapEntry != null) {
                if (selectedMapEntrySlot == characterSlot) {
                    return selectedMapEntry;
                }

                throw new InvalidOperationException("A different character slot has already been selected for map entry.");
            }

            selectedMapEntry = runtime
                .SelectCharacterAsync(characterSlot)
                .GetAwaiter()
                .GetResult();
            selectedMapEntrySlot = characterSlot;
            characters = selectedMapEntry.CharacterFlow;
            return selectedMapEntry;
        }

        public RoLoginCreateCharacterResult CreateCharacter(RoShellCharacterRequest characterRequest) {
            ThrowIfDisposed();
            RoCharCreateFlowResult created = runtime
                .CreateCharacterAsync(
                    characterRequest.CharacterName,
                    characterRequest.CharacterSlot,
                    characterRequest.HairColor,
                    characterRequest.HairStyle,
                    characterRequest.StartJob,
                    characterRequest.Sex)
                .GetAwaiter()
                .GetResult();
            characters = created.CharacterFlow;
            ClearSelectedMapEntry();
            return new RoLoginCreateCharacterResult(
                profile,
                login.Login,
                login.SelectedCharacterServer,
                characters.Characters,
                created.CreatedCharacter,
                default);
        }

        public RoCharDeleteReserveFlowResult ReserveDeleteCharacter(int characterId) {
            ThrowIfDisposed();
            RoCharDeleteReserveFlowResult result = runtime
                .ReserveDeleteCharacterAsync(characterId)
                .GetAwaiter()
                .GetResult();
            characters = result.CharacterFlow;
            ClearSelectedMapEntry();
            return result;
        }

        public RoCharDeleteConfirmFlowResult ConfirmDeleteCharacter(int characterId, string birthdateYyMmDd) {
            ThrowIfDisposed();
            RoCharDeleteConfirmFlowResult result = runtime
                .ConfirmDeleteCharacterAsync(characterId, birthdateYyMmDd)
                .GetAwaiter()
                .GetResult();
            characters = result.CharacterFlow;
            ClearSelectedMapEntry();
            return result;
        }

        public RoCharDeleteFlowResult DeleteCharacter(int characterId, string deletionKey) {
            ThrowIfDisposed();
            RoCharDeleteFlowResult result = runtime
                .DeleteCharacterAsync(characterId, deletionKey)
                .GetAwaiter()
                .GetResult();
            characters = result.CharacterFlow;
            ClearSelectedMapEntry();
            return result;
        }

        public RoCharDeleteCancelFlowResult CancelDeleteCharacter(int characterId) {
            ThrowIfDisposed();
            RoCharDeleteCancelFlowResult result = runtime
                .CancelDeleteCharacterAsync(characterId)
                .GetAwaiter()
                .GetResult();
            characters = result.CharacterFlow;
            ClearSelectedMapEntry();
            return result;
        }

        public bool TryCreateResultFromCurrentCharacters(
            RoShellCharacterRequest characterRequest,
            out RoLoginCreateCharacterResult result) {
            ThrowIfDisposed();
            result = null;
            if (!TryFindRequestedCharacter(characters != null ? characters.Characters : null, characterRequest, out RoCharacterInfo character)) {
                return false;
            }

            result = new RoLoginCreateCharacterResult(
                profile,
                login.Login,
                login.SelectedCharacterServer,
                characters.Characters,
                new RoHcAcceptMakeChar(character),
                default);
            return true;
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            runtime.Dispose();
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoShellCharacterRuntimeSession));
            }
        }

        private void ClearSelectedMapEntry() {
            selectedMapEntry = null;
            selectedMapEntrySlot = null;
        }

        private static RoServerProfile NormalizeProfile(RoServerProfile profile) {
            return profile.PacketVersion == 0
                ? RoServerProfile.LocalRathena20200401()
                : profile;
        }

        private static bool TryFindRequestedCharacter(
            RoInitialCharacterSnapshot snapshot,
            RoShellCharacterRequest characterRequest,
            out RoCharacterInfo character) {
            character = null;
            if (snapshot == null ||
                snapshot.Characters == null ||
                string.IsNullOrWhiteSpace(characterRequest.CharacterName)) {
                return false;
            }

            for (int i = 0; i < snapshot.Characters.Length; i++) {
                RoCharacterInfo candidate = snapshot.Characters[i];
                if (candidate != null &&
                    candidate.Slot == characterRequest.CharacterSlot &&
                    string.Equals(candidate.Name, characterRequest.CharacterName, StringComparison.OrdinalIgnoreCase)) {
                    character = candidate;
                    return true;
                }
            }

            return false;
        }
    }

    internal static class RoShellCharacterRequestExtensions {
        public static RoShellLoginRequest ToLoginRequest(this RoShellCharacterRequest request) {
            return new RoShellLoginRequest(request.Account, request.Password);
        }
    }
}
