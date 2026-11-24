using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public sealed class RoCharFlowController : IDisposable {
        private static readonly TimeSpan OptionalCharacterPreludeQuietPeriod = TimeSpan.FromMilliseconds(25);
        private static readonly TimeSpan OptionalCharacterPreludeMaxDrain = TimeSpan.FromMilliseconds(250);

        private readonly RoClientRuntimeOptions options;
        private readonly RoProfiledOutboundPackets outboundPackets;
        private readonly bool supportsCharacterKeepAlive;
        private readonly object keepAliveSync = new object();
        private RoConnection connection;
        private RoPacketDispatcher dispatcher;
        private RoCharFlowResult currentCharacters;
        private CancellationTokenSource characterKeepAliveCancellation;
        private Task characterKeepAliveTask;
        private bool disposed;

        public RoCharFlowController(RoClientRuntimeOptions options) {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            RoRuntimeFlowTasks.RequireCharRuntimeProfile(options.PacketProfile);
            outboundPackets = options.CreateOutboundPackets();
            supportsCharacterKeepAlive = outboundPackets.TryResolveDefinition(RoOutboundPacketKind.CzPing, out _);
            State = RoCharFlowState.Idle;
        }

        public event EventHandler<RoCharFlowStateChangedEventArgs> StateChanged;

        public RoCharFlowState State { get; private set; }

        public async Task<RoCharFlowResult> LoadCharactersAsync(
            RoLoginFlowResult login,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            if (login == null) {
                throw new ArgumentNullException(nameof(login));
            }

            await ResetConnectionAsync().ConfigureAwait(false);
            connection = options.CreateConnection(login.CharacterEndpoint);
            dispatcher = new RoPacketDispatcher(connection);

            TaskCompletionSource<RoHcAcceptEnter2> slotLimitsTask =
                RoRuntimeFlowTasks.CreateCompletionSource<RoHcAcceptEnter2>();
            TaskCompletionSource<RoHcAcceptEnter> characterListTask =
                RoRuntimeFlowTasks.CreateCompletionSource<RoHcAcceptEnter>();
            TaskCompletionSource<RoHcNotifyCharList> pageInfoTask =
                RoRuntimeFlowTasks.CreateCompletionSource<RoHcNotifyCharList>();
            TaskCompletionSource<RoHcBlockCharacter> blockedTask =
                RoRuntimeFlowTasks.CreateCompletionSource<RoHcBlockCharacter>();
            TaskCompletionSource<RoHcSecondPasswordLogin> secondPasswordTask =
                RoRuntimeFlowTasks.CreateCompletionSource<RoHcSecondPasswordLogin>();
            var preludeSync = new object();
            int preludeSequence = 0;
            bool hasLatestSlotLimits = false;
            bool hasLatestCharacterList = false;
            bool hasLatestPageInfo = false;
            bool hasLatestBlocked = false;
            bool hasLatestSecondPassword = false;
            RoHcAcceptEnter2 latestSlotLimits = default;
            RoHcAcceptEnter latestCharacterList = default;
            RoHcNotifyCharList latestPageInfo = default;
            RoHcBlockCharacter latestBlocked = default;
            RoHcSecondPasswordLogin latestSecondPassword = default;
            var packetTasks = new List<object> {
                slotLimitsTask,
                characterListTask,
                pageInfoTask,
                blockedTask,
                secondPasswordTask
            };

            EventHandler<RoTransportErrorEventArgs> errorHandler = (sender, eventArgs) => {
                FailCharacterListTasks(packetTasks, eventArgs.Exception);
            };
            EventHandler<RoTransportStateChangedEventArgs> stateHandler = (sender, eventArgs) => {
                if (eventArgs.CurrentState == RoTransportConnectionState.Disconnected &&
                    State != RoCharFlowState.Ready &&
                    State != RoCharFlowState.Completed) {
                    FailCharacterListTasks(
                        packetTasks,
                        new RoNetworkProtocolException("Character connection disconnected before character list completed."));
                }
            };
            EventHandler<RoPacketUnhandledEventArgs> unhandledHandler = (sender, eventArgs) => {
                FailCharacterListTasks(
                    packetTasks,
                    new RoNetworkProtocolException($"Unexpected char-server packet 0x{eventArgs.Frame.PacketId:x4}."));
            };

            var subscriptions = new List<IDisposable> {
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter2, frame => Complete(slotLimitsTask, () => {
                    RoHcAcceptEnter2 parsed = RoCharacterPacketParser.ParseHcAcceptEnter2(frame.Bytes, options.TextEncoding);
                    lock (preludeSync) {
                        latestSlotLimits = parsed;
                        hasLatestSlotLimits = true;
                        preludeSequence++;
                    }

                    return parsed;
                })),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter, frame => Complete(characterListTask, () => {
                    RoHcAcceptEnter parsed = ParseAcceptEnterForProfile(frame);
                    lock (preludeSync) {
                        latestCharacterList = parsed;
                        hasLatestCharacterList = true;
                        preludeSequence++;
                    }

                    return parsed;
                })),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter20240619, frame => Complete(characterListTask, () => {
                    RoHcAcceptEnter parsed = RoCharacterPacketParser.ParseHcAcceptEnter20240619(frame.Bytes, options.TextEncoding);
                    lock (preludeSync) {
                        latestCharacterList = parsed;
                        hasLatestCharacterList = true;
                        preludeSequence++;
                    }

                    return parsed;
                })),
                dispatcher.Subscribe(RoPacketIds.HcAckCharInfoPerPage, frame => Complete(characterListTask, () => {
                    RoHcAcceptEnter parsed = ParseAckCharInfoAsAcceptEnter(frame);
                    lock (preludeSync) {
                        latestCharacterList = parsed;
                        hasLatestCharacterList = true;
                        preludeSequence++;
                    }

                    return parsed;
                })),
                dispatcher.Subscribe(RoPacketIds.HcNotifyCharList, frame => Complete(pageInfoTask, () => {
                    RoHcNotifyCharList parsed = RoCharacterPacketParser.ParseHcNotifyCharList(frame.Bytes);
                    lock (preludeSync) {
                        latestPageInfo = parsed;
                        hasLatestPageInfo = true;
                        preludeSequence++;
                    }

                    return parsed;
                })),
                dispatcher.Subscribe(RoPacketIds.HcBlockCharacter, frame => Complete(blockedTask, () => {
                    RoHcBlockCharacter parsed = RoCharacterPacketParser.ParseHcBlockCharacter(frame.Bytes, options.TextEncoding);
                    lock (preludeSync) {
                        latestBlocked = parsed;
                        hasLatestBlocked = true;
                        preludeSequence++;
                    }

                    return parsed;
                })),
                dispatcher.Subscribe(RoPacketIds.HcSecondPasswordLogin, frame => Complete(secondPasswordTask, () => {
                    RoHcSecondPasswordLogin parsed = RoCharacterPacketParser.ParseHcSecondPasswordLogin(frame.Bytes);
                    lock (preludeSync) {
                        latestSecondPassword = parsed;
                        hasLatestSecondPassword = true;
                        preludeSequence++;
                    }

                    return parsed;
                }))
            };

            connection.Error += errorHandler;
            connection.ConnectionStateChanged += stateHandler;
            dispatcher.UnhandledPacket += unhandledHandler;

            bool loaded = false;
            try {
                SetState(RoCharFlowState.Connecting);
                await connection.ConnectAsync(login.CharacterEndpoint, cancellationToken).ConfigureAwait(false);

                Task<byte[]> accountAckBytesTask = connection.ReadRawAsync(
                    4,
                    "CHAR_SERVER_ACCOUNT_ACK",
                    cancellationToken);

                SetState(RoCharFlowState.RequestingCharList);
                await connection
                    .SendAsync(
                        outboundPackets.BuildChEnter(
                            login.Login.AccountId,
                            login.Login.LoginId1,
                            login.Login.LoginId2,
                            login.Login.Sex),
                        cancellationToken)
                    .ConfigureAwait(false);

                byte[] accountAckBytes = await RoRuntimeFlowTasks
                    .WaitWithTimeoutAsync(
                        accountAckBytesTask,
                        options.OperationTimeout,
                        "CHAR_SERVER_ACCOUNT_ACK",
                        cancellationToken)
                    .ConfigureAwait(false);
                RoCharServerAccountAck accountAck = RoCharacterPacketParser.ParseCharServerAccountAck(accountAckBytes);
                if (accountAck.AccountId != login.Login.AccountId) {
                    throw new RoNetworkProtocolException(
                        $"Char-server account ack {accountAck.AccountId} does not match login account {login.Login.AccountId}.");
                }

                RoHcAcceptEnter awaitedCharacterList = await AwaitCharacterPacketAsync(
                    characterListTask.Task,
                    "HC_ACCEPT_ENTER/HC_ACCEPT_ENTER_20240619",
                    cancellationToken).ConfigureAwait(false);
                RoHcAcceptEnter characterList = awaitedCharacterList;
                RoHcAcceptEnter2 slotLimits;
                RoHcNotifyCharList pageInfo;
                RoHcBlockCharacter blocked;
                RoHcSecondPasswordLogin secondPassword;

                if (options.PacketProfile.PacketVersion == 20200401) {
                    slotLimits = await AwaitCharacterPacketAsync(
                        slotLimitsTask.Task,
                        "HC_ACCEPT_ENTER2",
                        cancellationToken).ConfigureAwait(false);
                    await DrainOptionalCharacterPreludeAsync(
                        () => {
                            lock (preludeSync) {
                                return preludeSequence;
                            }
                        },
                        cancellationToken).ConfigureAwait(false);

                    lock (preludeSync) {
                        if (hasLatestCharacterList) {
                            characterList = latestCharacterList;
                        }

                        pageInfo = hasLatestPageInfo
                            ? latestPageInfo
                            : new RoHcNotifyCharList(ResolveCharacterSlotCount(characterList, slotLimits));
                        blocked = hasLatestBlocked ? latestBlocked : default;
                        secondPassword = hasLatestSecondPassword ? latestSecondPassword : default;
                    }
                } else {
                    await DrainOptionalCharacterPreludeAsync(
                        () => {
                            lock (preludeSync) {
                                return preludeSequence;
                            }
                        },
                        cancellationToken).ConfigureAwait(false);

                    int pageRequestCount;
                    lock (preludeSync) {
                        pageRequestCount = hasLatestPageInfo
                            ? NormalizeCharacterListPageRequestCount(latestPageInfo.TotalSlots)
                            : 0;
                    }

                    if (pageRequestCount > 0) {
                        await RequestCharacterListPagesAsync(pageRequestCount, cancellationToken).ConfigureAwait(false);
                        await DrainOptionalCharacterPreludeAsync(
                            () => {
                                lock (preludeSync) {
                                    return preludeSequence;
                                }
                            },
                            cancellationToken).ConfigureAwait(false);
                    }

                    lock (preludeSync) {
                        if (hasLatestCharacterList) {
                            characterList = latestCharacterList;
                        }

                        slotLimits = hasLatestSlotLimits ? latestSlotLimits : default;
                        pageInfo = hasLatestPageInfo ? latestPageInfo : new RoHcNotifyCharList(characterList.Characters.Length);
                        blocked = hasLatestBlocked ? latestBlocked : default;
                        secondPassword = hasLatestSecondPassword ? latestSecondPassword : default;
                    }
                }

                currentCharacters = new RoCharFlowResult(
                    login,
                    new RoInitialCharacterSnapshot(
                        accountAck,
                        slotLimits,
                        characterList,
                        pageInfo,
                        blocked,
                        secondPassword));
                loaded = true;
                SetState(RoCharFlowState.Ready);
                StartCharacterKeepAlive();
                return currentCharacters;
            } catch (Exception exception) {
                SetState(RoCharFlowState.Failed, exception);
                throw;
            } finally {
                if (dispatcher != null) {
                    dispatcher.UnhandledPacket -= unhandledHandler;
                }

                if (connection != null) {
                    connection.Error -= errorHandler;
                    connection.ConnectionStateChanged -= stateHandler;
                }

                DisposeAll(subscriptions);
                if (!loaded) {
                    await ResetConnectionAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task RequestCharacterListPagesAsync(int pageCount, CancellationToken cancellationToken) {
            for (int i = 0; i < pageCount; i++) {
                await connection
                    .SendAsync(outboundPackets.BuildChCharListRequest(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async Task<RoCharSelectFlowResult> SelectCharacterAsync(
            int characterSlot,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            if (State != RoCharFlowState.Ready || currentCharacters == null || connection == null || dispatcher == null) {
                throw new InvalidOperationException("Character list must be loaded before selecting a character.");
            }

            RoCharacterInfo selectedCharacter = FindCharacterBySlot(currentCharacters.Characters.Characters, characterSlot);
            if (selectedCharacter == null) {
                throw new RoNetworkProtocolException($"Character slot {characterSlot} was not found in the current character list.");
            }

            TaskCompletionSource<RoHcNotifyZoneServer2> handoffTask =
                RoRuntimeFlowTasks.CreateCompletionSource<RoHcNotifyZoneServer2>();
            bool selected = false;
            EventHandler<RoTransportErrorEventArgs> errorHandler = (sender, eventArgs) => {
                handoffTask.TrySetException(eventArgs.Exception);
            };
            EventHandler<RoTransportStateChangedEventArgs> stateHandler = (sender, eventArgs) => {
                if (eventArgs.CurrentState == RoTransportConnectionState.Disconnected &&
                    State != RoCharFlowState.Completed) {
                    handoffTask.TrySetException(new RoNetworkProtocolException(
                        "Character connection disconnected before map handoff completed."));
                }
            };
            EventHandler<RoPacketUnhandledEventArgs> unhandledHandler = (sender, eventArgs) => {
                handoffTask.TrySetException(new RoNetworkProtocolException(
                    $"Unexpected char-select packet 0x{eventArgs.Frame.PacketId:x4}."));
            };

            var subscriptions = new List<IDisposable> {
                dispatcher.Subscribe(RoPacketIds.HcNotifyZoneServer, frame => Complete(handoffTask, () => RoCharacterPacketParser.ParseHcNotifyZoneServer(frame.Bytes, options.TextEncoding))),
                dispatcher.Subscribe(RoPacketIds.HcNotifyZoneServer2, frame => Complete(handoffTask, () => RoCharacterPacketParser.ParseHcNotifyZoneServer(frame.Bytes, options.TextEncoding))),
                dispatcher.Subscribe(RoPacketIds.HcRefuseEnter, frame => HandleRefuseEnter(handoffTask, frame)),
                dispatcher.Subscribe(RoPacketIds.HcSecondPasswordLogin, frame => HandleSelectSecondPassword(handoffTask, frame)),
                dispatcher.Subscribe(RoPacketIds.HcNotifyAccessibleMapName, frame => HandleAccessibleMapSelection(handoffTask, frame, characterSlot, cancellationToken))
            };

            connection.Error += errorHandler;
            connection.ConnectionStateChanged += stateHandler;
            dispatcher.UnhandledPacket += unhandledHandler;

            try {
                await StopCharacterKeepAliveAsync().ConfigureAwait(false);
                SetState(RoCharFlowState.SelectingCharacter);
                await connection
                    .SendAsync(outboundPackets.BuildChSelectChar(characterSlot), cancellationToken)
                    .ConfigureAwait(false);

                RoHcNotifyZoneServer2 handoff = await RoRuntimeFlowTasks
                    .WaitWithTimeoutAsync(
                        handoffTask.Task,
                        options.OperationTimeout,
                        "HC_NOTIFY_ZONESVR2",
                        cancellationToken)
                    .ConfigureAwait(false);

                SetState(RoCharFlowState.Completed);
                selected = true;
                return new RoCharSelectFlowResult(currentCharacters, selectedCharacter, handoff);
            } catch (Exception exception) {
                SetState(RoCharFlowState.Failed, exception);
                throw;
            } finally {
                dispatcher.UnhandledPacket -= unhandledHandler;
                connection.Error -= errorHandler;
                connection.ConnectionStateChanged -= stateHandler;
                DisposeAll(subscriptions);
                if (!selected) {
                    await ResetConnectionAsync().ConfigureAwait(false);
                } else {
                    await CloseConnectionAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<RoCharCreateFlowResult> CreateCharacterAsync(
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            if (State != RoCharFlowState.Ready || currentCharacters == null || connection == null || dispatcher == null) {
                throw new InvalidOperationException("Character list must be loaded before creating a character.");
            }

            TaskCompletionSource<RoCharCreatePacketResult> createTask =
                RoRuntimeFlowTasks.CreateCompletionSource<RoCharCreatePacketResult>();

            EventHandler<RoTransportErrorEventArgs> errorHandler = (sender, eventArgs) => {
                createTask.TrySetException(eventArgs.Exception);
            };
            EventHandler<RoTransportStateChangedEventArgs> stateHandler = (sender, eventArgs) => {
                if (eventArgs.CurrentState == RoTransportConnectionState.Disconnected &&
                    State == RoCharFlowState.CreatingCharacter) {
                    createTask.TrySetException(new RoNetworkProtocolException(
                        "Character connection disconnected before character creation completed."));
                }
            };
            EventHandler<RoPacketUnhandledEventArgs> unhandledHandler = (sender, eventArgs) => {
                createTask.TrySetException(new RoNetworkProtocolException(
                    $"Unexpected character-create packet 0x{eventArgs.Frame.PacketId:x4}."));
            };

            string requestedName = characterName.Trim();
            bool allowCharacterListCompletion = false;
            var subscriptions = new List<IDisposable> {
                dispatcher.Subscribe(RoPacketIds.HcAcceptMakeChar, frame => CompleteCreateAccept(createTask, frame)),
                dispatcher.Subscribe(RoPacketIds.HcAcceptMakeChar20201007, frame => CompleteCreateAccept(createTask, frame)),
                dispatcher.Subscribe(RoPacketIds.HcRefuseMakeChar, frame => HandleCreateRefuse(createTask, frame)),
                dispatcher.Subscribe(RoPacketIds.HcSecondPasswordLogin, frame => HandleCreateSecondPassword(createTask, frame)),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter, frame => {
                    if (allowCharacterListCompletion) {
                        CompleteCreateFromCharacterList(createTask, frame, requestedName, characterSlot);
                    }
                }),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter20240619, frame => {
                    if (allowCharacterListCompletion) {
                        CompleteCreateFromCharacterList(createTask, frame, requestedName, characterSlot);
                    }
                }),
                dispatcher.Subscribe(RoPacketIds.HcAckCharInfoPerPage, frame => {
                    if (allowCharacterListCompletion) {
                        CompleteCreateFromCharacterList(createTask, frame, requestedName, characterSlot);
                    }
                }),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter2, frame => { }),
                dispatcher.Subscribe(RoPacketIds.HcNotifyCharList, frame => { }),
                dispatcher.Subscribe(RoPacketIds.HcBlockCharacter, frame => { })
            };

            connection.Error += errorHandler;
            connection.ConnectionStateChanged += stateHandler;
            dispatcher.UnhandledPacket += unhandledHandler;

            bool created = false;
            try {
                await StopCharacterKeepAliveAsync().ConfigureAwait(false);
                SetState(RoCharFlowState.CreatingCharacter);
                await connection
                    .SendAsync(
                        outboundPackets.BuildChMakeChar(
                            requestedName,
                            characterSlot,
                            hairColor,
                            hairStyle,
                            startJob,
                            sex ?? currentCharacters.Login.Login.Sex,
                            options.TextEncoding),
                        cancellationToken)
                    .ConfigureAwait(false);

                RoCharCreatePacketResult createResult;
                try {
                    createResult = await RoRuntimeFlowTasks
                        .WaitWithTimeoutAsync(
                            createTask.Task,
                            options.OperationTimeout,
                            "HC_ACCEPT_MAKECHAR/HC_REFUSE_MAKECHAR",
                            cancellationToken)
                        .ConfigureAwait(false);
                } catch (TimeoutException) {
                    allowCharacterListCompletion = true;
                    await connection
                        .SendAsync(outboundPackets.BuildChCharListRequest(), cancellationToken)
                        .ConfigureAwait(false);
                    createResult = await RoRuntimeFlowTasks
                        .WaitWithTimeoutAsync(
                            createTask.Task,
                            options.OperationTimeout,
                            "character list refresh after CH_MAKE_CHAR",
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                createResult = NormalizeCreateResult(createResult, requestedName, characterSlot);
                currentCharacters = BuildCreatedCharacterFlow(currentCharacters, createResult);
                created = true;
                SetState(RoCharFlowState.Ready);
                StartCharacterKeepAlive();
                return new RoCharCreateFlowResult(currentCharacters, createResult.CreatedCharacter);
            } catch (Exception exception) {
                SetState(RoCharFlowState.Failed, exception);
                throw;
            } finally {
                dispatcher.UnhandledPacket -= unhandledHandler;
                connection.Error -= errorHandler;
                connection.ConnectionStateChanged -= stateHandler;
                DisposeAll(subscriptions);
                if (!created) {
                    await ResetConnectionAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<RoCharDeleteReserveFlowResult> ReserveDeleteCharacterAsync(
            int characterId,
            CancellationToken cancellationToken = default) {
            RoHcDeleteChar3Reserved reservation = await RunDeleteMutationAsync(
                characterId,
                () => outboundPackets.BuildChDeleteChar3Reserved(characterId),
                RoPacketIds.HcDeleteChar3Reserved,
                frame => RoCharacterPacketParser.ParseHcDeleteChar3Reserved(frame.Bytes),
                "HC_DELETE_CHAR3_RESERVED",
                response => response.IsSuccess,
                cancellationToken).ConfigureAwait(false);
            return new RoCharDeleteReserveFlowResult(currentCharacters, reservation);
        }

        public async Task<RoCharDeleteConfirmFlowResult> ConfirmDeleteCharacterAsync(
            int characterId,
            string birthdateYyMmDd,
            CancellationToken cancellationToken = default) {
            RoHcDeleteChar3 confirmation = await RunDeleteMutationAsync(
                characterId,
                () => outboundPackets.BuildChDeleteChar3(characterId, birthdateYyMmDd),
                RoPacketIds.HcDeleteChar3,
                frame => RoCharacterPacketParser.ParseHcDeleteChar3(frame.Bytes),
                "HC_DELETE_CHAR3",
                response => response.IsSuccess,
                cancellationToken).ConfigureAwait(false);
            return new RoCharDeleteConfirmFlowResult(currentCharacters, confirmation);
        }

        /// <summary>
        /// The delete of this client (langtype 1): CH_DELETE_CHAR2 0x01fb with the deletion password (the account's
        /// e-mail), answered by HC_ACCEPT_DELETECHAR 0x6f or HC_REFUSE_DELETECHAR 0x70; an accepted delete reloads the list.
        /// </summary>
        public async Task<RoCharDeleteFlowResult> DeleteCharacterAsync(
            int characterId,
            string deletionKey,
            CancellationToken cancellationToken = default) {
            RoHcDeleteChar answer = await RunDeleteMutationAsync(
                characterId,
                () => outboundPackets.BuildChDeleteChar2(characterId, deletionKey, options.TextEncoding),
                RoPacketIds.HcAcceptDeleteChar,
                frame => RoCharacterPacketParser.ParseHcAcceptDeleteChar(frame.Bytes),
                "HC_ACCEPT_DELETECHAR / HC_REFUSE_DELETECHAR",
                response => response.IsSuccess,
                cancellationToken,
                RoPacketIds.HcRefuseDeleteChar,
                frame => RoCharacterPacketParser.ParseHcRefuseDeleteChar(frame.Bytes)).ConfigureAwait(false);
            return new RoCharDeleteFlowResult(currentCharacters, answer);
        }

        public async Task<RoCharDeleteCancelFlowResult> CancelDeleteCharacterAsync(
            int characterId,
            CancellationToken cancellationToken = default) {
            RoHcDeleteChar3Cancel cancellation = await RunDeleteMutationAsync(
                characterId,
                () => outboundPackets.BuildChDeleteChar3Cancel(characterId),
                RoPacketIds.HcDeleteChar3Cancel,
                frame => RoCharacterPacketParser.ParseHcDeleteChar3Cancel(frame.Bytes),
                "HC_DELETE_CHAR3_CANCEL",
                response => response.IsSuccess,
                cancellationToken).ConfigureAwait(false);
            return new RoCharDeleteCancelFlowResult(currentCharacters, cancellation);
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            ResetConnectionAsync().GetAwaiter().GetResult();
        }

        private Task<T> AwaitCharacterPacketAsync<T>(
            Task<T> packetTask,
            string packetName,
            CancellationToken cancellationToken) {
            return RoRuntimeFlowTasks.WaitWithTimeoutAsync(
                packetTask,
                options.OperationTimeout,
                packetName,
                cancellationToken);
        }

        private async Task DrainOptionalCharacterPreludeAsync(
            Func<int> getPreludeSequence,
            CancellationToken cancellationToken) {
            if (getPreludeSequence == null) {
                throw new ArgumentNullException(nameof(getPreludeSequence));
            }

            TimeSpan maxDrain = options.OperationTimeout < OptionalCharacterPreludeMaxDrain
                ? options.OperationTimeout
                : OptionalCharacterPreludeMaxDrain;
            if (maxDrain <= TimeSpan.Zero) {
                return;
            }

            DateTime deadline = DateTime.UtcNow + maxDrain;
            int previousSequence = getPreludeSequence();
            while (true) {
                TimeSpan remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) {
                    return;
                }

                TimeSpan delay = remaining < OptionalCharacterPreludeQuietPeriod
                    ? remaining
                    : OptionalCharacterPreludeQuietPeriod;
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

                int currentSequence = getPreludeSequence();
                if (currentSequence == previousSequence) {
                    return;
                }

                previousSequence = currentSequence;
            }
        }

        private async Task<TResponse> RunDeleteMutationAsync<TResponse>(
            int characterId,
            Func<byte[]> buildPacket,
            ushort responsePacketId,
            Func<RoPacketFrame, TResponse> parseResponse,
            string responsePacketName,
            Func<TResponse, bool> isSuccess,
            CancellationToken cancellationToken,
            ushort alternateResponsePacketId = 0,
            Func<RoPacketFrame, TResponse> parseAlternateResponse = null) {
            ThrowIfDisposed();

            if (characterId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(characterId));
            }

            if (State != RoCharFlowState.Ready || currentCharacters == null || connection == null || dispatcher == null) {
                throw new InvalidOperationException("Character list must be loaded before deleting a character.");
            }

            if (FindCharacterById(currentCharacters.Characters.Characters, characterId) == null) {
                throw new RoNetworkProtocolException($"Character id {characterId} was not found in the current character list.");
            }

            TaskCompletionSource<TResponse> responseTask = RoRuntimeFlowTasks.CreateCompletionSource<TResponse>();
            EventHandler<RoTransportErrorEventArgs> errorHandler = (sender, eventArgs) => {
                responseTask.TrySetException(eventArgs.Exception);
            };
            EventHandler<RoTransportStateChangedEventArgs> stateHandler = (sender, eventArgs) => {
                if (eventArgs.CurrentState == RoTransportConnectionState.Disconnected &&
                    State == RoCharFlowState.DeletingCharacter) {
                    responseTask.TrySetException(new RoNetworkProtocolException(
                        "Character connection disconnected before character delete completed."));
                }
            };
            EventHandler<RoPacketUnhandledEventArgs> unhandledHandler = (sender, eventArgs) => {
                responseTask.TrySetException(new RoNetworkProtocolException(
                    $"Unexpected character-delete packet 0x{eventArgs.Frame.PacketId:x4}."));
            };

            var subscriptions = new List<IDisposable> {
                dispatcher.Subscribe(responsePacketId, frame => Complete(responseTask, () => parseResponse(frame))),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter, frame => { }),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter20240619, frame => { }),
                dispatcher.Subscribe(RoPacketIds.HcAckCharInfoPerPage, frame => { }),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter2, frame => { }),
                dispatcher.Subscribe(RoPacketIds.HcNotifyCharList, frame => { }),
                dispatcher.Subscribe(RoPacketIds.HcBlockCharacter, frame => { })
            };
            if (alternateResponsePacketId != 0 && parseAlternateResponse != null) {
                subscriptions.Add(dispatcher.Subscribe(alternateResponsePacketId, frame => Complete(responseTask, () => parseAlternateResponse(frame))));
            }

            connection.Error += errorHandler;
            connection.ConnectionStateChanged += stateHandler;
            dispatcher.UnhandledPacket += unhandledHandler;

            bool handled = false;
            try {
                await StopCharacterKeepAliveAsync().ConfigureAwait(false);
                SetState(RoCharFlowState.DeletingCharacter);
                await connection
                    .SendAsync(buildPacket(), cancellationToken)
                    .ConfigureAwait(false);

                TResponse response = await RoRuntimeFlowTasks
                    .WaitWithTimeoutAsync(
                        responseTask.Task,
                        options.OperationTimeout,
                        responsePacketName,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (isSuccess(response)) {
                    currentCharacters = await RefreshCharacterListAfterMutationAsync(cancellationToken).ConfigureAwait(false);
                }

                handled = true;
                SetState(RoCharFlowState.Ready);
                StartCharacterKeepAlive();
                return response;
            } catch (Exception exception) {
                SetState(RoCharFlowState.Failed, exception);
                throw;
            } finally {
                dispatcher.UnhandledPacket -= unhandledHandler;
                connection.Error -= errorHandler;
                connection.ConnectionStateChanged -= stateHandler;
                DisposeAll(subscriptions);
                if (!handled) {
                    await ResetConnectionAsync().ConfigureAwait(false);
                }
            }
        }

        private static int ResolveCharacterSlotCount(
            RoHcAcceptEnter characterList,
            RoHcAcceptEnter2 slotLimits) {
            if (characterList.TotalSlots > 0) {
                return characterList.TotalSlots;
            }

            if (slotLimits.TotalSlots > 0) {
                return slotLimits.TotalSlots;
            }

            return characterList.Characters.Length;
        }

        private async Task<RoCharFlowResult> RefreshCharacterListAfterMutationAsync(CancellationToken cancellationToken) {
            TaskCompletionSource<RoHcAcceptEnter> characterListTask =
                RoRuntimeFlowTasks.CreateCompletionSource<RoHcAcceptEnter>();
            var subscriptions = new List<IDisposable> {
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter, frame => Complete(characterListTask, () => ParseAcceptEnterForProfile(frame))),
                dispatcher.Subscribe(RoPacketIds.HcAcceptEnter20240619, frame => Complete(characterListTask, () => RoCharacterPacketParser.ParseHcAcceptEnter20240619(frame.Bytes, options.TextEncoding))),
                dispatcher.Subscribe(RoPacketIds.HcAckCharInfoPerPage, frame => Complete(characterListTask, () => ParseAckCharInfoAsAcceptEnter(frame)))
            };

            try {
                await connection
                    .SendAsync(outboundPackets.BuildChCharListRequest(), cancellationToken)
                    .ConfigureAwait(false);

                RoHcAcceptEnter characterList = await RoRuntimeFlowTasks
                    .WaitWithTimeoutAsync(
                        characterListTask.Task,
                        options.OperationTimeout,
                        "character list refresh after delete-character mutation",
                        cancellationToken)
                    .ConfigureAwait(false);
                return BuildRefreshedCharacterFlow(currentCharacters, characterList);
            } finally {
                DisposeAll(subscriptions);
            }
        }

        private void HandleRefuseEnter(TaskCompletionSource<RoHcNotifyZoneServer2> completion, RoPacketFrame frame) {
            try {
                RoHcRefuseEnter refuse = RoCharacterPacketParser.ParseHcRefuseEnter(frame.Bytes);
                completion.TrySetException(new RoNetworkProtocolException(
                    $"Character select refused with error code {refuse.ErrorCode}."));
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private void HandleSelectSecondPassword(TaskCompletionSource<RoHcNotifyZoneServer2> completion, RoPacketFrame frame) {
            try {
                RoHcSecondPasswordLogin secondPassword = RoCharacterPacketParser.ParseHcSecondPasswordLogin(frame.Bytes);
                if (!secondPassword.IsPassedOrDisabled) {
                    completion.TrySetException(new RoNetworkProtocolException(
                        $"Character select requires unsupported second password result {secondPassword.Result}."));
                }
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private void CompleteCreateAccept(
            TaskCompletionSource<RoCharCreatePacketResult> completion,
            RoPacketFrame frame) {
            Complete(
                completion,
                () => new RoCharCreatePacketResult(
                    RoCharacterPacketParser.ParseHcAcceptMakeCharForProfile(frame.Bytes, options.TextEncoding)));
        }

        private void CompleteCreateFromCharacterList(
            TaskCompletionSource<RoCharCreatePacketResult> completion,
            RoPacketFrame frame,
            string characterName,
            int characterSlot) {
            try {
                RoHcAcceptEnter characterList = ParseCharacterListFrame(frame);
                RoCharacterInfo character = FindCharacterByName(characterList.Characters, characterName);
                if (character != null) {
                    completion.TrySetResult(new RoCharCreatePacketResult(
                        new RoHcAcceptMakeChar(character),
                        characterList,
                        hasCharacterList: true));
                }
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private void HandleCreateRefuse(
            TaskCompletionSource<RoCharCreatePacketResult> completion,
            RoPacketFrame frame) {
            try {
                RoHcRefuseMakeChar refuse = RoCharacterPacketParser.ParseHcRefuseMakeChar(frame.Bytes);
                completion.TrySetException(new RoNetworkProtocolException(
                    $"Character creation refused with error code {refuse.ErrorCode}."));
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private void HandleCreateSecondPassword(
            TaskCompletionSource<RoCharCreatePacketResult> completion,
            RoPacketFrame frame) {
            try {
                RoHcSecondPasswordLogin secondPassword = RoCharacterPacketParser.ParseHcSecondPasswordLogin(frame.Bytes);
                if (!secondPassword.IsPassedOrDisabled) {
                    completion.TrySetException(new RoNetworkProtocolException(
                        $"Character creation requires unsupported second password result {secondPassword.Result}."));
                }
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private RoHcAcceptEnter ParseAckCharInfoAsAcceptEnter(RoPacketFrame frame) {
            RoHcAckCharInfoPerPage page = options.PacketProfile.PacketVersion == 20240619
                ? RoCharacterPacketParser.ParseHcAckCharInfoPerPage20240619(frame.Bytes, options.TextEncoding)
                : RoCharacterPacketParser.ParseHcAckCharInfoPerPage(frame.Bytes, options.TextEncoding);
            return new RoHcAcceptEnter(page.PacketLength, 0, 0, 0, string.Empty, page.Characters);
        }

        private RoHcAcceptEnter ParseCharacterListFrame(RoPacketFrame frame) {
            switch (frame.PacketId) {
                case RoPacketIds.HcAcceptEnter:
                    return ParseAcceptEnterForProfile(frame);
                case RoPacketIds.HcAcceptEnter20240619:
                    return RoCharacterPacketParser.ParseHcAcceptEnter20240619(frame.Bytes, options.TextEncoding);
                case RoPacketIds.HcAckCharInfoPerPage:
                    return ParseAckCharInfoAsAcceptEnter(frame);
                default:
                    throw new ArgumentException(
                        $"Packet 0x{frame.PacketId:x4} is not a character list packet.",
                        nameof(frame));
            }
        }

        private RoHcAcceptEnter ParseAcceptEnterForProfile(RoPacketFrame frame) {
            return options.PacketProfile.PacketVersion == 20240619
                ? RoCharacterPacketParser.ParseHcAcceptEnter20240619Legacy(frame.Bytes, options.TextEncoding)
                : RoCharacterPacketParser.ParseHcAcceptEnter(frame.Bytes, options.TextEncoding);
        }

        private void HandleAccessibleMapSelection(
            TaskCompletionSource<RoHcNotifyZoneServer2> completion,
            RoPacketFrame frame,
            int characterSlot,
            CancellationToken cancellationToken) {
            try {
                RoHcNotifyAccessibleMapName maps = RoCharacterPacketParser.ParseHcNotifyAccessibleMapName(frame.Bytes, options.TextEncoding);
                int mapIndex = maps.FindFirstAvailableMapIndex();
                if (mapIndex < 0) {
                    completion.TrySetException(new RoNetworkProtocolException(
                        "Character select requested an accessible map, but none are available."));
                    return;
                }

                _ = SendAccessibleMapSelectionAsync(completion, characterSlot, mapIndex, cancellationToken);
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private async Task SendAccessibleMapSelectionAsync(
            TaskCompletionSource<RoHcNotifyZoneServer2> completion,
            int characterSlot,
            int mapIndex,
            CancellationToken cancellationToken) {
            try {
                await connection
                    .SendAsync(outboundPackets.BuildChSelectAccessibleMapName(characterSlot, mapIndex), cancellationToken)
                    .ConfigureAwait(false);
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private async Task ResetConnectionAsync() {
            currentCharacters = null;
            await StopCharacterKeepAliveAsync().ConfigureAwait(false);
            await CloseConnectionAsync().ConfigureAwait(false);

            if (!disposed && State != RoCharFlowState.Failed) {
                SetState(RoCharFlowState.Idle);
            }
        }

        private async Task CloseConnectionAsync() {
            if (dispatcher != null) {
                dispatcher.Dispose();
                dispatcher = null;
            }

            if (connection != null) {
                RoConnection previous = connection;
                connection = null;
                await previous.CloseAsync().ConfigureAwait(false);
                previous.Dispose();
            }
        }

        private void SetState(RoCharFlowState nextState, Exception exception = null) {
            if (State == nextState) {
                return;
            }

            RoCharFlowState previous = State;
            State = nextState;
            StateChanged?.Invoke(this, new RoCharFlowStateChangedEventArgs(previous, nextState, exception));
        }

        private void StartCharacterKeepAlive() {
            if (!supportsCharacterKeepAlive || connection == null || currentCharacters == null) {
                return;
            }

            lock (keepAliveSync) {
                if (characterKeepAliveTask != null && !characterKeepAliveTask.IsCompleted) {
                    return;
                }

                characterKeepAliveCancellation?.Dispose();
                characterKeepAliveCancellation = new CancellationTokenSource();
                characterKeepAliveTask = RunCharacterKeepAliveAsync(characterKeepAliveCancellation.Token);
            }
        }

        private async Task StopCharacterKeepAliveAsync() {
            CancellationTokenSource cancellation;
            Task keepAliveTask;
            lock (keepAliveSync) {
                cancellation = characterKeepAliveCancellation;
                keepAliveTask = characterKeepAliveTask;
                characterKeepAliveCancellation = null;
                characterKeepAliveTask = null;
            }

            if (cancellation == null) {
                return;
            }

            cancellation.Cancel();
            try {
                if (keepAliveTask != null) {
                    await keepAliveTask.ConfigureAwait(false);
                }
            } catch (OperationCanceledException) {
            } finally {
                cancellation.Dispose();
            }
        }

        private async Task RunCharacterKeepAliveAsync(CancellationToken cancellationToken) {
            while (!cancellationToken.IsCancellationRequested) {
                await Task.Delay(options.CharacterKeepAliveInterval, cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested ||
                    State != RoCharFlowState.Ready ||
                    connection == null ||
                    currentCharacters == null ||
                    connection.State != RoTransportConnectionState.Connected) {
                    continue;
                }

                try {
                    await connection
                        .SendAsync(
                            outboundPackets.BuildCzPing(currentCharacters.Login.Login.AccountId),
                            cancellationToken)
                        .ConfigureAwait(false);
                } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                    return;
                } catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) {
                    return;
                } catch {
                    return;
                }
            }
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoCharFlowController));
            }
        }

        private static void Complete<T>(TaskCompletionSource<T> completion, Func<T> parse) {
            try {
                completion.TrySetResult(parse());
            } catch (Exception exception) {
                completion.TrySetException(exception);
            }
        }

        private static void FailCharacterListTasks(List<object> tasks, Exception exception) {
            for (int i = 0; i < tasks.Count; i++) {
                if (tasks[i] is TaskCompletionSource<RoHcAcceptEnter2> slotLimits) {
                    slotLimits.TrySetException(exception);
                } else if (tasks[i] is TaskCompletionSource<RoHcAcceptEnter> characterList) {
                    characterList.TrySetException(exception);
                } else if (tasks[i] is TaskCompletionSource<RoHcNotifyCharList> pageInfo) {
                    pageInfo.TrySetException(exception);
                } else if (tasks[i] is TaskCompletionSource<RoHcBlockCharacter> blocked) {
                    blocked.TrySetException(exception);
                } else if (tasks[i] is TaskCompletionSource<RoHcSecondPasswordLogin> secondPassword) {
                    secondPassword.TrySetException(exception);
                }
            }
        }

        private static T CompletedOrDefault<T>(Task<T> task, T defaultValue = default) {
            return task.Status == TaskStatus.RanToCompletion ? task.Result : defaultValue;
        }

        private static void DisposeAll(List<IDisposable> subscriptions) {
            for (int i = 0; i < subscriptions.Count; i++) {
                subscriptions[i].Dispose();
            }
        }

        private static RoCharacterInfo FindCharacterBySlot(RoCharacterInfo[] characters, int slot) {
            for (int i = 0; i < characters.Length; i++) {
                if (characters[i] != null && characters[i].Slot == slot) {
                    return characters[i];
                }
            }

            return null;
        }

        private static RoCharacterInfo FindCharacterByName(RoCharacterInfo[] characters, string name) {
            if (characters == null || string.IsNullOrWhiteSpace(name)) {
                return null;
            }

            for (int i = 0; i < characters.Length; i++) {
                if (characters[i] != null &&
                    string.Equals(characters[i].Name, name, StringComparison.OrdinalIgnoreCase)) {
                    return characters[i];
                }
            }

            return null;
        }

        private static RoCharacterInfo FindCharacterById(RoCharacterInfo[] characters, int characterId) {
            if (characters == null || characterId <= 0) {
                return null;
            }

            for (int i = 0; i < characters.Length; i++) {
                if (characters[i] != null && characters[i].CharacterId == characterId) {
                    return characters[i];
                }
            }

            return null;
        }

        private static int NormalizeCharacterListPageRequestCount(int totalPages) {
            if (totalPages <= 0) {
                return 0;
            }

            return totalPages > 32 ? 32 : totalPages;
        }

        private static RoCharCreatePacketResult NormalizeCreateResult(
            RoCharCreatePacketResult createResult,
            string requestedName,
            int requestedSlot) {
            RoCharacterInfo character = createResult.CreatedCharacter.Character;
            if (character == null) {
                return createResult;
            }

            string normalizedName = (requestedName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(normalizedName)) {
                character.Name = normalizedName;
            }

            character.Slot = ClampCharacterSlot(requestedSlot);
            return new RoCharCreatePacketResult(
                new RoHcAcceptMakeChar(character),
                createResult.CharacterList,
                createResult.HasCharacterList);
        }

        private static byte ClampCharacterSlot(int slot) {
            if (slot <= 0) {
                return 0;
            }

            return slot >= byte.MaxValue ? byte.MaxValue : (byte) slot;
        }

        private static RoCharFlowResult BuildCreatedCharacterFlow(
            RoCharFlowResult previous,
            RoCharCreatePacketResult createResult) {
            RoInitialCharacterSnapshot previousSnapshot = previous.Characters;
            RoHcAcceptEnter characterList = createResult.HasCharacterList
                ? createResult.CharacterList
                : UpsertCharacter(previousSnapshot.CharacterList, createResult.CreatedCharacter.Character);
            var nextSnapshot = new RoInitialCharacterSnapshot(
                previousSnapshot.AccountAck,
                previousSnapshot.SlotLimits,
                characterList,
                previousSnapshot.PageInfo,
                previousSnapshot.BlockedCharacters,
                previousSnapshot.SecondPassword);
            return new RoCharFlowResult(previous.Login, nextSnapshot);
        }

        private static RoCharFlowResult BuildRefreshedCharacterFlow(
            RoCharFlowResult previous,
            RoHcAcceptEnter characterList) {
            RoInitialCharacterSnapshot previousSnapshot = previous.Characters;
            var nextSnapshot = new RoInitialCharacterSnapshot(
                previousSnapshot.AccountAck,
                previousSnapshot.SlotLimits,
                characterList,
                previousSnapshot.PageInfo,
                previousSnapshot.BlockedCharacters,
                previousSnapshot.SecondPassword);
            return new RoCharFlowResult(previous.Login, nextSnapshot);
        }

        private static RoHcAcceptEnter UpsertCharacter(RoHcAcceptEnter previousList, RoCharacterInfo character) {
            RoCharacterInfo[] existing = previousList.Characters ?? Array.Empty<RoCharacterInfo>();
            var characters = new RoCharacterInfo[existing.Length];
            Array.Copy(existing, characters, existing.Length);

            for (int i = 0; i < characters.Length; i++) {
                if (IsSameCharacter(characters[i], character)) {
                    characters[i] = character;
                    return new RoHcAcceptEnter(
                        previousList.PacketLength,
                        previousList.TotalSlots,
                        previousList.PremiumStart,
                        previousList.PremiumEnd,
                        previousList.Extension,
                        characters);
                }
            }

            var appended = new RoCharacterInfo[characters.Length + 1];
            Array.Copy(characters, appended, characters.Length);
            appended[appended.Length - 1] = character;
            return new RoHcAcceptEnter(
                previousList.PacketLength,
                previousList.TotalSlots,
                previousList.PremiumStart,
                previousList.PremiumEnd,
                previousList.Extension,
                appended);
        }

        private static bool IsSameCharacter(RoCharacterInfo left, RoCharacterInfo right) {
            if (left == null || right == null) {
                return false;
            }

            if (left.CharacterId != 0 && right.CharacterId != 0 && left.CharacterId == right.CharacterId) {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(left.Name) &&
                string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return left.Slot == right.Slot;
        }

        private readonly struct RoCharCreatePacketResult {
            public RoCharCreatePacketResult(
                RoHcAcceptMakeChar createdCharacter,
                RoHcAcceptEnter characterList = default,
                bool hasCharacterList = false) {
                CreatedCharacter = createdCharacter;
                CharacterList = characterList;
                HasCharacterList = hasCharacterList;
            }

            public RoHcAcceptMakeChar CreatedCharacter { get; }
            public RoHcAcceptEnter CharacterList { get; }
            public bool HasCharacterList { get; }
        }
    }
}
