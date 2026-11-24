using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Network {
    public enum RoLoginFlowState {
        Idle = 0,
        Connecting = 1,
        Authenticating = 2,
        AwaitingCharServer = 3,
        Completed = 4,
        Failed = 5
    }

    public enum RoCharFlowState {
        Idle = 0,
        Connecting = 1,
        RequestingCharList = 2,
        Ready = 3,
        SelectingCharacter = 4,
        Completed = 5,
        Failed = 6,
        CreatingCharacter = 7,
        DeletingCharacter = 8
    }

    public sealed class RoLoginFlowStateChangedEventArgs : EventArgs {
        public RoLoginFlowStateChangedEventArgs(RoLoginFlowState previousState, RoLoginFlowState currentState, Exception exception = null) {
            PreviousState = previousState;
            CurrentState = currentState;
            Exception = exception;
        }

        public RoLoginFlowState PreviousState { get; }
        public RoLoginFlowState CurrentState { get; }
        public Exception Exception { get; }
    }

    public sealed class RoCharFlowStateChangedEventArgs : EventArgs {
        public RoCharFlowStateChangedEventArgs(RoCharFlowState previousState, RoCharFlowState currentState, Exception exception = null) {
            PreviousState = previousState;
            CurrentState = currentState;
            Exception = exception;
        }

        public RoCharFlowState PreviousState { get; }
        public RoCharFlowState CurrentState { get; }
        public Exception Exception { get; }
    }

    public sealed class RoClientRuntimeOptions {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DefaultCharacterKeepAliveInterval = TimeSpan.FromSeconds(10);

        public RoClientRuntimeOptions(
            RoServerProfile serverProfile,
            IRoPacketProfile packetProfile,
            Func<RoServerEndpoint, IRoTransport> transportFactory,
            IRoPacketTraceSink traceSink = null,
            IRoPacketCaptureSink captureSink = null,
            TimeSpan? operationTimeout = null,
            Encoding textEncoding = null,
            TimeSpan? characterKeepAliveInterval = null,
            bool enableVerboseNetworkTraceMessages = false) {
            ServerProfile = serverProfile;
            PacketProfile = packetProfile ?? throw new ArgumentNullException(nameof(packetProfile));
            TransportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
            TraceSink = traceSink;
            CaptureSink = captureSink;
            OperationTimeout = operationTimeout ?? DefaultTimeout;
            TextEncoding = textEncoding ?? Encoding.ASCII;
            CharacterKeepAliveInterval = characterKeepAliveInterval ?? DefaultCharacterKeepAliveInterval;
            EnableVerboseNetworkTraceMessages = enableVerboseNetworkTraceMessages;

            if (OperationTimeout <= TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(operationTimeout), "Operation timeout must be positive.");
            }

            if (CharacterKeepAliveInterval <= TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(characterKeepAliveInterval), "Character keepalive interval must be positive.");
            }
        }

        public RoServerProfile ServerProfile { get; }
        public IRoPacketProfile PacketProfile { get; }
        public Func<RoServerEndpoint, IRoTransport> TransportFactory { get; }
        public IRoPacketTraceSink TraceSink { get; }
        public IRoPacketCaptureSink CaptureSink { get; }
        public TimeSpan OperationTimeout { get; }
        public Encoding TextEncoding { get; }
        public TimeSpan CharacterKeepAliveInterval { get; }
        public bool EnableVerboseNetworkTraceMessages { get; }

        public RoProfiledOutboundPackets CreateOutboundPackets() {
            return new RoProfiledOutboundPackets(PacketProfile);
        }

        public RoConnection CreateConnection(RoServerEndpoint endpoint) {
            IRoTransport transport = TransportFactory(endpoint);
            if (transport == null) {
                throw new InvalidOperationException($"Transport factory returned null for endpoint {endpoint}.");
            }

            return new RoConnection(
                transport,
                ServerProfile,
                PacketProfile.CreateRegistry(),
                PacketProfile.CreatePacketTransform(),
                TraceSink,
                CaptureSink,
                enableVerboseTraceMessages: EnableVerboseNetworkTraceMessages);
        }
    }

    public sealed class RoLoginFlowResult {
        public RoLoginFlowResult(
            RoServerProfile profile,
            IRoPacketProfile packetProfile,
            RoAcAcceptLogin3 login,
            RoLoginCharServerInfo selectedCharacterServer) {
            Profile = profile;
            PacketProfile = packetProfile ?? throw new ArgumentNullException(nameof(packetProfile));
            Login = login;
            SelectedCharacterServer = selectedCharacterServer;
        }

        public RoServerProfile Profile { get; }
        public IRoPacketProfile PacketProfile { get; }
        public RoAcAcceptLogin3 Login { get; }
        public RoLoginCharServerInfo SelectedCharacterServer { get; }

        public RoServerEndpoint CharacterEndpoint {
            get {
                if (!string.IsNullOrWhiteSpace(SelectedCharacterServer.Address) &&
                    SelectedCharacterServer.Port > 0) {
                    return RoServerEndpoint.PreferProfileHostForLoopback(
                        new RoServerEndpoint(SelectedCharacterServer.Address, SelectedCharacterServer.Port),
                        Profile.Character);
                }

                return Profile.Character;
            }
        }
    }

    public sealed class RoCharFlowResult {
        public RoCharFlowResult(RoLoginFlowResult login, RoInitialCharacterSnapshot characters) {
            Login = login ?? throw new ArgumentNullException(nameof(login));
            Characters = characters ?? throw new ArgumentNullException(nameof(characters));
        }

        public RoLoginFlowResult Login { get; }
        public RoInitialCharacterSnapshot Characters { get; }
    }

    public sealed class RoCharCreateFlowResult {
        public RoCharCreateFlowResult(RoCharFlowResult characterFlow, RoHcAcceptMakeChar createdCharacter) {
            CharacterFlow = characterFlow ?? throw new ArgumentNullException(nameof(characterFlow));
            CreatedCharacter = createdCharacter;
        }

        public RoCharFlowResult CharacterFlow { get; }
        public RoHcAcceptMakeChar CreatedCharacter { get; }
        public RoCharacterInfo Character => CreatedCharacter.Character;
    }

    public sealed class RoCharDeleteReserveFlowResult {
        public RoCharDeleteReserveFlowResult(RoCharFlowResult characterFlow, RoHcDeleteChar3Reserved reservation) {
            CharacterFlow = characterFlow ?? throw new ArgumentNullException(nameof(characterFlow));
            Reservation = reservation;
        }

        public RoCharFlowResult CharacterFlow { get; }
        public RoHcDeleteChar3Reserved Reservation { get; }
        public bool IsSuccess => Reservation.IsSuccess;
    }

    public sealed class RoCharDeleteConfirmFlowResult {
        public RoCharDeleteConfirmFlowResult(RoCharFlowResult characterFlow, RoHcDeleteChar3 confirmation) {
            CharacterFlow = characterFlow ?? throw new ArgumentNullException(nameof(characterFlow));
            Confirmation = confirmation;
        }

        public RoCharFlowResult CharacterFlow { get; }
        public RoHcDeleteChar3 Confirmation { get; }
        public bool IsSuccess => Confirmation.IsSuccess;
    }

    public sealed class RoCharDeleteFlowResult {
        public RoCharDeleteFlowResult(RoCharFlowResult characterFlow, RoHcDeleteChar answer) {
            CharacterFlow = characterFlow ?? throw new ArgumentNullException(nameof(characterFlow));
            Answer = answer;
        }

        public RoCharFlowResult CharacterFlow { get; }
        public RoHcDeleteChar Answer { get; }
        public bool IsSuccess => Answer.IsSuccess;
    }

    public sealed class RoCharDeleteCancelFlowResult {
        public RoCharDeleteCancelFlowResult(RoCharFlowResult characterFlow, RoHcDeleteChar3Cancel cancellation) {
            CharacterFlow = characterFlow ?? throw new ArgumentNullException(nameof(characterFlow));
            Cancellation = cancellation;
        }

        public RoCharFlowResult CharacterFlow { get; }
        public RoHcDeleteChar3Cancel Cancellation { get; }
        public bool IsSuccess => Cancellation.IsSuccess;
    }

    public sealed class RoCharSelectFlowResult {
        public RoCharSelectFlowResult(
            RoCharFlowResult characterFlow,
            RoCharacterInfo selectedCharacter,
            RoHcNotifyZoneServer2 mapHandoff) {
            CharacterFlow = characterFlow ?? throw new ArgumentNullException(nameof(characterFlow));
            SelectedCharacter = selectedCharacter ?? throw new ArgumentNullException(nameof(selectedCharacter));
            MapHandoff = mapHandoff;
        }

        public RoCharFlowResult CharacterFlow { get; }
        public RoCharacterInfo SelectedCharacter { get; }
        public RoHcNotifyZoneServer2 MapHandoff { get; }
    }

    internal static class RoRuntimeFlowTasks {
        public static System.Threading.Tasks.TaskCompletionSource<T> CreateCompletionSource<T>() {
            return new System.Threading.Tasks.TaskCompletionSource<T>(
                System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public static void RequireLoginRuntimeProfile(IRoPacketProfile packetProfile) {
            RoPacketProfileCapabilities capabilities = GetCapabilities(packetProfile);
            RequireRuntimeCapability(
                packetProfile,
                "Login runtime",
                capabilities.SupportsLoginRuntimeInbound,
                capabilities.LoginRuntimePacketIds);
        }

        public static void RequireCharRuntimeProfile(IRoPacketProfile packetProfile) {
            RoPacketProfileCapabilities capabilities = GetCapabilities(packetProfile);
            RequireRuntimeCapability(
                packetProfile,
                "Char runtime",
                capabilities.SupportsCharRuntimeInbound,
                capabilities.CharRuntimePacketIds);
        }

        private static RoPacketProfileCapabilities GetCapabilities(IRoPacketProfile packetProfile) {
            if (packetProfile == null) {
                throw new ArgumentNullException(nameof(packetProfile));
            }

            return packetProfile.Capabilities ?? RoPacketProfileCapabilities.None;
        }

        private static void RequireRuntimeCapability(
            IRoPacketProfile packetProfile,
            string runtimeName,
            bool supported,
            ushort[] requiredPacketIds) {
            if (packetProfile == null) {
                throw new ArgumentNullException(nameof(packetProfile));
            }

            runtimeName = string.IsNullOrWhiteSpace(runtimeName) ? "Runtime" : runtimeName;
            if (!supported) {
                throw new NotSupportedException(
                    $"{runtimeName} inbound is not enabled for profile '{packetProfile.Name}' packetver {packetProfile.PacketVersion}.");
            }

            RoPacketRegistry registry = packetProfile.CreateRegistry();
            var missing = new List<string>();
            for (int i = 0; i < requiredPacketIds.Length; i++) {
                ushort packetId = requiredPacketIds[i];
                if (!registry.TryGet(packetId, out RoPacketDefinition definition) ||
                    definition.Direction == RoPacketDirection.ClientToServer) {
                    missing.Add($"0x{packetId:x4}");
                }
            }

            if (missing.Count > 0) {
                throw new NotSupportedException(
                    $"{runtimeName} profile '{packetProfile.Name}' packetver {packetProfile.PacketVersion} is missing required inbound packet definitions: {string.Join(", ", missing)}.");
            }
        }

        public static async System.Threading.Tasks.Task<T> WaitWithTimeoutAsync<T>(
            System.Threading.Tasks.Task<T> task,
            TimeSpan timeout,
            string operationName,
            System.Threading.CancellationToken cancellationToken) {
            using (var delayCancellation = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
                System.Threading.Tasks.Task delay = System.Threading.Tasks.Task.Delay(timeout, delayCancellation.Token);
                System.Threading.Tasks.Task completed = await System.Threading.Tasks.Task
                    .WhenAny(task, delay)
                    .ConfigureAwait(false);

                if (completed == task) {
                    delayCancellation.Cancel();
                    return await task.ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Timed out while waiting for {operationName}.");
            }
        }
    }
}
