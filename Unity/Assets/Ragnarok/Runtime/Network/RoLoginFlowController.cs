using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public sealed class RoLoginFlowController {
        private readonly RoClientRuntimeOptions options;
        private readonly RoProfiledOutboundPackets outboundPackets;

        public RoLoginFlowController(RoClientRuntimeOptions options) {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            RoRuntimeFlowTasks.RequireLoginRuntimeProfile(options.PacketProfile);
            outboundPackets = options.CreateOutboundPackets();
            State = RoLoginFlowState.Idle;
        }

        public event EventHandler<RoLoginFlowStateChangedEventArgs> StateChanged;

        public RoLoginFlowState State { get; private set; }

        public async Task<RoLoginFlowResult> LoginAsync(
            string account,
            string password,
            CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(account)) {
                throw new ArgumentException("Account is required.", nameof(account));
            }

            using (RoConnection connection = options.CreateConnection(options.ServerProfile.Login))
            using (var dispatcher = new RoPacketDispatcher(connection)) {
                TaskCompletionSource<RoLoginFlowResult> loginResponse =
                    RoRuntimeFlowTasks.CreateCompletionSource<RoLoginFlowResult>();
                EventHandler<RoTransportErrorEventArgs> errorHandler = (sender, eventArgs) => {
                    loginResponse.TrySetException(eventArgs.Exception);
                };
                EventHandler<RoTransportStateChangedEventArgs> stateHandler = (sender, eventArgs) => {
                    if (eventArgs.CurrentState == RoTransportConnectionState.Disconnected &&
                        State != RoLoginFlowState.Completed &&
                        !loginResponse.Task.IsCompleted) {
                        loginResponse.TrySetException(new RoNetworkProtocolException("Login connection disconnected before login completed."));
                    }
                };
                EventHandler<RoPacketUnhandledEventArgs> unhandledHandler = (sender, eventArgs) => {
                    loginResponse.TrySetException(new RoNetworkProtocolException(
                        $"Unexpected login packet 0x{eventArgs.Frame.PacketId:x4}."));
                };

                connection.Error += errorHandler;
                connection.ConnectionStateChanged += stateHandler;
                dispatcher.UnhandledPacket += unhandledHandler;

                dispatcher.Subscribe(RoPacketIds.AcAcceptLogin3, frame => HandleAcceptLogin(loginResponse, frame));
                dispatcher.Subscribe(RoPacketIds.AcRefuseLoginR2, frame => HandleRefuseLogin(loginResponse, frame));
                dispatcher.Subscribe(RoPacketIds.AcRefuseLogin20240619, frame => HandleRefuseLogin(loginResponse, frame));
                dispatcher.Subscribe(RoPacketIds.ScNotifyBan, frame => HandleBan(loginResponse, frame));

                try {
                    SetState(RoLoginFlowState.Connecting);
                    await connection.ConnectAsync(options.ServerProfile.Login, cancellationToken).ConfigureAwait(false);

                    if (RoServerSignature.Required) {
                        await CheckServerSignatureAsync(connection, dispatcher, loginResponse.Task, cancellationToken).ConfigureAwait(false);
                    }

                    SetState(RoLoginFlowState.Authenticating);
                    await connection
                        .SendAsync(outboundPackets.BuildCaLogin(account, RoLauncherTicket.ResolvePassword(account, password ?? string.Empty)), cancellationToken)
                        .ConfigureAwait(false);

                    SetState(RoLoginFlowState.AwaitingCharServer);
                    RoLoginFlowResult result = await RoRuntimeFlowTasks
                        .WaitWithTimeoutAsync(
                            loginResponse.Task,
                            options.OperationTimeout,
                            "AC_ACCEPT_LOGIN3",
                            cancellationToken)
                        .ConfigureAwait(false);

                    SetState(RoLoginFlowState.Completed);
                    return result;
                } catch (Exception exception) {
                    SetState(RoLoginFlowState.Failed, exception);
                    throw;
                } finally {
                    dispatcher.UnhandledPacket -= unhandledHandler;
                    connection.ConnectionStateChanged -= stateHandler;
                    connection.Error -= errorHandler;
                    await connection.CloseAsync().ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// RoServerSignature: the challenge before CA_LOGIN; a server without the signature closes the connection (that fails
        /// <paramref name="loginTask"/>) or answers nothing - either way this client refuses to go on.
        /// </summary>
        private async Task CheckServerSignatureAsync(RoConnection connection, RoPacketDispatcher dispatcher, Task loginTask,
            CancellationToken cancellationToken) {
            byte[] challenge = RoServerSignature.NewChallenge();
            var reply = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (dispatcher.Subscribe(RoServerSignature.ReplyId, frame => reply.TrySetResult(frame.Bytes))) {
                await connection.SendAsync(RoServerSignature.BuildRequest(challenge), cancellationToken).ConfigureAwait(false);
                Task first = await Task.WhenAny(reply.Task, loginTask, Task.Delay(options.OperationTimeout, cancellationToken))
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (first != reply.Task) {
                    _ = loginTask.Exception; // observed: the refusal below is what the player sees
                    throw new RoNetworkProtocolException(RoServerSignature.RefusedMessage);
                }

                RoServerSignature.Check(challenge, reply.Task.Result);
            }
        }

        private void HandleAcceptLogin(TaskCompletionSource<RoLoginFlowResult> completion, RoPacketFrame frame) {
            try {
                RoAcAcceptLogin3 login = RoLoginPacketParser.ParseAcAcceptLogin3(frame.Bytes, options.TextEncoding);
                if (login.Servers.Length == 0) {
                    completion.TrySetException(new RoNetworkProtocolException(
                        "Login succeeded, but no character servers were returned."));
                    return;
                }

                completion.TrySetResult(new RoLoginFlowResult(
                    options.ServerProfile,
                    options.PacketProfile,
                    login,
                    login.Servers[0]));
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private void HandleRefuseLogin(TaskCompletionSource<RoLoginFlowResult> completion, RoPacketFrame frame) {
            try {
                RoAcRefuseLogin refuse = RoLoginPacketParser.ParseAcRefuseLogin(frame.Bytes, options.TextEncoding);
                completion.TrySetException(new RoNetworkProtocolException(
                    $"Login refused with error code {refuse.ErrorCode}."));
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private void HandleBan(TaskCompletionSource<RoLoginFlowResult> completion, RoPacketFrame frame) {
            try {
                RoScNotifyBan ban = RoMapPacketParser.ParseScNotifyBan(frame.Bytes);
                completion.TrySetException(new RoNetworkProtocolException(
                    $"Login rejected by server with auth result code {ban.ErrorCode}."));
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private void SetState(RoLoginFlowState nextState, Exception exception = null) {
            if (State == nextState) {
                return;
            }

            RoLoginFlowState previous = State;
            State = nextState;
            StateChanged?.Invoke(this, new RoLoginFlowStateChangedEventArgs(previous, nextState, exception));
        }
    }
}
