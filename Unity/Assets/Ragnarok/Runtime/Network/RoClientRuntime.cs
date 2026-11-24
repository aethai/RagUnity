using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public sealed class RoClientRuntime : IDisposable {
        private bool disposed;

        public RoClientRuntime(RoClientRuntimeOptions options) {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Login = new RoLoginFlowController(options);
            Character = new RoCharFlowController(options);

            Login.StateChanged += HandleLoginStateChanged;
            Character.StateChanged += HandleCharStateChanged;
        }

        public event EventHandler<RoLoginFlowStateChangedEventArgs> LoginStateChanged;
        public event EventHandler<RoCharFlowStateChangedEventArgs> CharacterStateChanged;

        public RoClientRuntimeOptions Options { get; }
        public RoLoginFlowController Login { get; }
        public RoCharFlowController Character { get; }

        public Task<RoLoginFlowResult> LoginAsync(
            string account,
            string password,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return Login.LoginAsync(account, password, cancellationToken);
        }

        public Task<RoCharFlowResult> LoadCharactersAsync(
            RoLoginFlowResult login,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return Character.LoadCharactersAsync(login, cancellationToken);
        }

        public Task<RoCharSelectFlowResult> SelectCharacterAsync(
            int characterSlot,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return Character.SelectCharacterAsync(characterSlot, cancellationToken);
        }

        public Task<RoCharCreateFlowResult> CreateCharacterAsync(
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return Character.CreateCharacterAsync(
                characterName,
                characterSlot,
                hairColor,
                hairStyle,
                startJob,
                sex,
                cancellationToken);
        }

        public Task<RoCharDeleteReserveFlowResult> ReserveDeleteCharacterAsync(
            int characterId,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return Character.ReserveDeleteCharacterAsync(characterId, cancellationToken);
        }

        public Task<RoCharDeleteConfirmFlowResult> ConfirmDeleteCharacterAsync(
            int characterId,
            string birthdateYyMmDd,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return Character.ConfirmDeleteCharacterAsync(characterId, birthdateYyMmDd, cancellationToken);
        }

        public Task<RoCharDeleteFlowResult> DeleteCharacterAsync(
            int characterId,
            string deletionKey,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return Character.DeleteCharacterAsync(characterId, deletionKey, cancellationToken);
        }

        public Task<RoCharDeleteCancelFlowResult> CancelDeleteCharacterAsync(
            int characterId,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();
            return Character.CancelDeleteCharacterAsync(characterId, cancellationToken);
        }

        public async Task<RoLoginSessionResult> LoginAndLoadCharactersAsync(
            string account,
            string password,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            RoLoginFlowResult login = await LoginAsync(account, password, cancellationToken).ConfigureAwait(false);
            RoCharFlowResult characters = await LoadCharactersAsync(login, cancellationToken).ConfigureAwait(false);
            return new RoLoginSessionResult(
                Options.ServerProfile,
                login.Login,
                login.SelectedCharacterServer,
                characters.Characters);
        }

        public async Task<RoLoginCreateCharacterResult> LoginLoadAndCreateCharacterAsync(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            RoLoginFlowResult login = await LoginAsync(account, password, cancellationToken).ConfigureAwait(false);
            await LoadCharactersAsync(login, cancellationToken).ConfigureAwait(false);
            RoCharCreateFlowResult created = await CreateCharacterAsync(
                characterName,
                characterSlot,
                hairColor,
                hairStyle,
                startJob,
                sex,
                cancellationToken).ConfigureAwait(false);
            return new RoLoginCreateCharacterResult(
                Options.ServerProfile,
                login.Login,
                login.SelectedCharacterServer,
                created.CharacterFlow.Characters,
                created.CreatedCharacter,
                default);
        }

        public async Task<RoCharSelectFlowResult> LoginLoadAndSelectCharacterAsync(
            string account,
            string password,
            int characterSlot,
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            RoLoginFlowResult login = await LoginAsync(account, password, cancellationToken).ConfigureAwait(false);
            await LoadCharactersAsync(login, cancellationToken).ConfigureAwait(false);
            return await SelectCharacterAsync(characterSlot, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            Login.StateChanged -= HandleLoginStateChanged;
            Character.StateChanged -= HandleCharStateChanged;
            Character.Dispose();
        }

        private void HandleLoginStateChanged(object sender, RoLoginFlowStateChangedEventArgs eventArgs) {
            LoginStateChanged?.Invoke(this, eventArgs);
        }

        private void HandleCharStateChanged(object sender, RoCharFlowStateChangedEventArgs eventArgs) {
            CharacterStateChanged?.Invoke(this, eventArgs);
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoClientRuntime));
            }
        }
    }
}
