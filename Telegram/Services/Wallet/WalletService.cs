//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletEngine;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// The wallet belonging to one Telegram account.
    /// </summary>
    /// <remarks>
    /// The only type that knows the engine exists. Everything above it sees <see cref="WalletState"/>
    /// and nothing generated.
    ///
    /// It deliberately holds no state of its own beyond the current projection: the engine already
    /// is an immutable, revisioned store, and keeping a second mutable copy beside it is what made
    /// the previous attempt hard to follow. What this owns is the pump that turns
    /// <c>WaitForChange</c> into an event.
    ///
    /// Changes are announced as <see cref="UpdateWalletState"/> through the event aggregator, the
    /// same way every other change in the app travels. They are published from the engine's pump
    /// thread: a service outlives any one window and Unigram can have several, so there is no
    /// single dispatcher for it to marshal to, and subscribers hop for themselves.
    /// </remarks>
    public sealed class WalletService : IWalletService
    {
        // From the engine's own defaults: DEFAULT_PROVIDER_REQUEST_TIMEOUT_MS and
        // DEFAULT_RESOLUTION_MARGIN_SECONDS in its config.rs. The validity has no default there;
        // 300 is what its examples and tests use.
        private const ulong RequestTimeoutMs = 15_000;
        private const ulong ResolutionMarginSeconds = 60;
        private const ulong SendValiditySeconds = 300;

        // The TON network follows the Telegram one: a client on the test DC has no business
        // moving real coins, and there is no separate UI for choosing. Deliberately not
        // overridable - which chain a wallet is created on decides where its funds can ever be
        // reached from, and that is not something a debug switch should be able to change.
        private Network DefaultNetwork =>
            _clientService.Options.TestMode ? Network.Testnet : Network.Mainnet;

        private const uint DescriptorMagic = 0x4C41574Du; // "MWAL"
        private const int DescriptorVersion = 1;

        private static IReadOnlyList<string> _recoveryWords;

        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);

        private string _path;
        private string _suffix;
        private WalletPlatformHost _platform;
        private WalletLifecycle _lifecycle;

        private WalletHttpHost _http;
        private WalletDescriptor _descriptor;
        private WalletClient _client;
        private CancellationTokenSource _pump;

        public WalletService(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;
        }

        /// <summary>
        /// Creates the stores, once, on first use.
        /// </summary>
        /// <remarks>
        /// Not in the constructor, because the paths depend on <c>Options.TestMode</c> and TDLib has
        /// not necessarily reported its options by the time this service is resolved. Freezing the
        /// wrong suffix there would point a production session at the test files, or the reverse.
        ///
        /// Always called with <see cref="_mutex"/> held.
        /// </remarks>
        private void EnsureStores()
        {
            if (_lifecycle != null)
            {
                return;
            }

            // Beside TDLib's own database for this session rather than off in a wallet\ tree of its
            // own: the two belong to the same account and should be found, backed up and deleted
            // together.
            _path = Path.Combine(
                ApplicationData.Current.LocalFolder.Path,
                _clientService.SessionId.ToString(),
                "wallet");

            // Test and production data sit in the same folder because the session id is the same,
            // so the files have to say them apart. TDLib does not do this for its own database, but
            // a wallet holds keys and must never read the wrong network's.
            _suffix = _clientService.Options.TestMode ? "_test" : string.Empty;

            var secrets = new WalletSecretStore(_path, _suffix);
            var journal = new WalletJournalStore(Path.Combine(_path, "journal" + _suffix + ".bin"));

            _platform = new WalletPlatformHost(secrets, journal);
            _lifecycle = new WalletLifecycle(_platform);
        }

        /// <summary>
        /// The latest projection. Never null; <see cref="WalletState.None"/> until a wallet exists.
        /// </summary>
        public WalletState State { get; private set; } = WalletState.None;

        /// <summary>
        /// The 2048 BIP-39 words the engine validates against.
        /// </summary>
        /// <remarks>
        /// Copied verbatim out of the engine's own <c>wordlist_en.txt</c>, so that what the import
        /// screen accepts and what the engine accepts cannot drift apart.
        /// </remarks>
        public static IReadOnlyList<string> RecoveryWords
        {
            get
            {
                if (_recoveryWords == null)
                {
                    var path = Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "Ton", "wordlist_en.txt");
                    _recoveryWords = File.ReadAllLines(path, Encoding.UTF8);
                }

                return _recoveryWords;
            }
        }

        /// <summary>
        /// Loads the stored wallet, if there is one, and starts tracking it.
        /// </summary>
        public async Task<WalletState> RestoreAsync()
        {
            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                if (_client != null || !TryLoadDescriptor(out var descriptor))
                {
                    return State;
                }

                await AttachAsync(descriptor);
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
            return State;
        }

        public async Task<IReadOnlyList<string>> CreateAsync()
        {
            IReadOnlyList<string> words;

            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                var created = await _lifecycle.CreateWallet(new CreateWalletRequest(NewRecordId(), DefaultNetwork));

                // The one moment the phrase is in hand. Reading it back afterwards goes through
                // protected storage and prompts the user, which is absurd for a wallet they just
                // created and were about to be shown the words for.
                words = Split(created.RecoveryPhrase);
                await AdoptAsync(created.Descriptor);
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
            return words;
        }

        public async Task<WalletState> ImportAsync(IList<string> words)
        {
            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                var array = new string[words.Count];
                words.CopyTo(array, 0);

                var descriptor = await _lifecycle.ImportWallet(new ImportWalletRequest(NewRecordId(), DefaultNetwork, array));
                await AdoptAsync(descriptor);
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
            return State;
        }

        /// <summary>
        /// Returns the recovery phrase after the store has authorized access to it.
        /// </summary>
        /// <remarks>
        /// The caller owns the only copy the app can see. Show it, then drop it: a string cannot be
        /// cleared, so it must not reach logs, errors, analytics or saved state.
        /// </remarks>
        public async Task<IReadOnlyList<string>> RevealRecoveryPhraseAsync()
        {
            var descriptor = _descriptor;
            if (descriptor == null)
            {
                return null;
            }

            var phrase = await _lifecycle.RevealRecoveryPhrase(descriptor);
            return Split(phrase);
        }

        // Split once here rather than at every call site, and on any whitespace: the words are what
        // callers show, check and compare, never the sentence.
        private static IReadOnlyList<string> Split(RecoveryPhrase phrase)
        {
            return phrase.Phrase.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        public async Task DeleteAsync()
        {
            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                var descriptor = _descriptor;
                if (descriptor == null)
                {
                    return;
                }

                await DetachAsync();
                await _lifecycle.DeleteWallet(descriptor);

                // Only after the secret is gone, so a failure above leaves a wallet that can still
                // be opened rather than an orphaned secret with nothing pointing at it.
                DeleteDescriptor();

                _descriptor = null;
                SetState(WalletState.None);
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        public async Task RefreshAsync()
        {
            var client = _client;
            if (client != null)
            {
                await client.Refresh();
            }
        }

        public async Task LoadMoreActivityAsync()
        {
            var client = _client;
            if (client != null)
            {
                // Returns Skipped during a refresh, during another page load, or when there is no
                // advancing cursor. That is not a failure and the snapshot says so either way.
                await client.LoadMoreActivity();
            }
        }

        public async Task ShutdownAsync()
        {
            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                await DetachAsync();
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        private async Task<WalletState> AdoptAsync(WalletDescriptor descriptor)
        {
            // Importing or creating over an existing wallet would otherwise leave the previous
            // client running, pump and all, with nothing left holding a reference to it.
            await DetachAsync();

            SaveDescriptor(descriptor);
            return await AttachAsync(descriptor);
        }

        private async Task<WalletState> AttachAsync(WalletDescriptor descriptor)
        {
            if (_client != null)
            {
                return State;
            }

            var config = new WalletClientConfig(
                descriptor.RecordId,
                descriptor.Address,
                descriptor.PublicKey,
                descriptor.SecretRef,
                descriptor.Network,
                SendValiditySeconds,
                ResolutionMarginSeconds,
                new ProviderConfig(BaseUrl(descriptor.Network), RequestTimeoutMs));

            _descriptor = descriptor;

            // One host per client, never one per service, for two independent reasons. Request ids
            // are allocated by the client and restart at 1 for each new one, so a host outliving
            // its client carries that client's cancellations into the next one's id space - and a
            // cancellation recorded for an id that has already completed makes the next request
            // under that id fail before it is ever sent. The host will also take IClientService
            // once requests go out through TDLib again, and that is per user.
            _http = new WalletHttpHost();
            _client = new WalletClient(config, _http, _platform);

            var state = Project(_client.Snapshot());

            _pump = new CancellationTokenSource();
            _ = PumpAsync(_client, _pump.Token);
            _ = RecoverAsync(_client);

            SetState(state);
            return State;
        }

        /// <summary>
        /// Drives startup recovery, and loads the first real snapshot with it.
        /// </summary>
        /// <remarks>
        /// Refresh, not resolvePending: the engine runs its own best-effort resolvePending inside
        /// refresh - "a client has no runtime of its own, so startup recovery is driven by" the
        /// host calling it (refresh.rs). Calling resolvePending here as well raced that one, and
        /// whichever lost got SendAlreadyInProgress.
        ///
        /// Best-effort by design: the README marks startup recovery as failure-ignored, because a
        /// send that cannot be settled now stays in the journal until the chain decides it. The
        /// task is still observed rather than discarded, or a failure becomes an unobserved
        /// exception with no obvious origin.
        /// </remarks>
        private static async Task RecoverAsync(WalletClient client)
        {
            try
            {
                await client.Refresh();
            }
            catch (Exception ex)
            {
                Logger.Error("wallet startup refresh failed: " + ex.Message);
            }
        }

        private async Task DetachAsync()
        {
            var client = _client;
            var pump = _pump;

            _client = null;
            _pump = null;
            _http = null;

            if (pump != null)
            {
                pump.Cancel();
                pump.Dispose();
            }

            if (client != null)
            {
                try
                {
                    await client.Shutdown();
                }
                catch (Exception ex)
                {
                    Logger.Error("wallet shutdown failed: " + ex.Message);
                }

                client.Dispose();
            }
        }

        /// <summary>
        /// Turns the engine's pull-based change signal into an event.
        /// </summary>
        private async Task PumpAsync(WalletClient client, CancellationToken cancellationToken)
        {
            var revision = State.Revision;

            while (!cancellationToken.IsCancellationRequested)
            {
                WalletSnapshot snapshot;

                try
                {
                    snapshot = await client.WaitForChange(revision);
                }
                catch (Exception)
                {
                    // Shutdown releases every waiter with an error, which is the loop's exit and
                    // not a fault worth reporting.
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                revision = snapshot.Revision;
                SetState(Project(snapshot));
                Raise();
            }
        }

        private static WalletState Project(WalletSnapshot snapshot)
        {
            var account = snapshot.Account;
            var activity = snapshot.Activity;

            var items = new WalletActivityItem[activity.Items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = Project(activity.Items[i]);
            }

            return new WalletState(
                snapshot.Revision,
                snapshot.Address,
                Project(account?.Status ?? AccountStatus.Unknown),
                account != null ? Parse(account.BalanceNanograms) : BigInteger.Zero,
                account != null,
                Project(snapshot.AccountResource),
                items,
                Project(activity.Resource),
                activity.HasMore);
        }

        private static WalletActivityItem Project(ActivityItem item)
        {
            return new WalletActivityItem(
                item.Id,
                item.TransactionHash,
                DateTimeOffset.FromUnixTimeSeconds((long)item.Timestamp).UtcDateTime,
                item.Direction == ActivityDirection.Sent
                    ? WalletActivityDirection.Sent
                    : WalletActivityDirection.Received,
                Parse(item.AmountNanograms),
                item.Counterparty);
        }

        private static WalletResource Project(ResourceState resource)
        {
            var phase = resource.Phase switch
            {
                ResourcePhase.Loading => WalletResourcePhase.Loading,
                ResourcePhase.Ready => WalletResourcePhase.Ready,
                ResourcePhase.Failed => WalletResourcePhase.Failed,
                _ => WalletResourcePhase.Idle
            };

            var error = resource.Error;
            if (error == null)
            {
                return phase == WalletResourcePhase.Idle
                    ? WalletResource.Idle
                    : new WalletResource(phase, null, false);
            }

            // RetryAdvice.None means retrying produces the same failure, so a view that offers a
            // retry button on it is lying to the user.
            return new WalletResource(phase, error.DeveloperMessage, error.Retry != RetryAdvice.None);
        }

        private static WalletAccountStatus Project(AccountStatus status)
        {
            return status switch
            {
                AccountStatus.Nonexistent => WalletAccountStatus.Nonexistent,
                AccountStatus.Uninitialized => WalletAccountStatus.Uninitialized,
                AccountStatus.Active => WalletAccountStatus.Active,
                AccountStatus.Frozen => WalletAccountStatus.Frozen,
                _ => WalletAccountStatus.Unknown
            };
        }

        private static BigInteger Parse(string nanograms)
        {
            return BigInteger.TryParse(nanograms, out var value) ? value : BigInteger.Zero;
        }

        // Setting and announcing are separate because the announcement must not happen under
        // _mutex: a subscriber that called back into the service would deadlock on a semaphore
        // that is not reentrant. The engine keeps the same discipline with its own state lock.
        private void SetState(WalletState state)
        {
            State = state;
        }

        private void Raise()
        {
            _aggregator.Publish(new UpdateWalletState(State));
        }

        private static string BaseUrl(Network network)
        {
            return network == Network.Testnet
                ? "https://testnet.toncenter.com"
                : "https://toncenter.com";
        }

        // Not the Telegram account id, deliberately. Deleting a wallet does not clear its journal,
        // so a reused id would hand the next wallet the previous one's pending send.
        private static string NewRecordId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private string DescriptorPath => Path.Combine(_path, "descriptor" + _suffix + ".bin");

        private bool TryLoadDescriptor(out WalletDescriptor descriptor)
        {
            descriptor = null;

            try
            {
                if (!File.Exists(DescriptorPath))
                {
                    return false;
                }

                using var stream = File.OpenRead(DescriptorPath);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                if (reader.ReadUInt32() != DescriptorMagic || reader.ReadInt32() != DescriptorVersion)
                {
                    return false;
                }

                var recordId = reader.ReadString();
                var address = reader.ReadString();
                var publicKey = reader.ReadBytes(reader.ReadInt32());
                var network = reader.ReadInt32() == 1 ? Network.Testnet : Network.Mainnet;
                var secretRef = reader.ReadString();

                descriptor = new WalletDescriptor(recordId, address, publicKey, network, new ProtectedSecretRef(secretRef));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("wallet descriptor unreadable: " + ex.Message);
                return false;
            }
        }

        private void SaveDescriptor(WalletDescriptor descriptor)
        {
            Directory.CreateDirectory(_path);

            using var stream = File.Create(DescriptorPath);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            writer.Write(DescriptorMagic);
            writer.Write(DescriptorVersion);
            writer.Write(descriptor.RecordId);
            writer.Write(descriptor.Address);
            writer.Write(descriptor.PublicKey.Length);
            writer.Write(descriptor.PublicKey);
            writer.Write(descriptor.Network == Network.Testnet ? 1 : 0);
            writer.Write(descriptor.SecretRef.Value);
        }

        private void DeleteDescriptor()
        {
            try
            {
                if (File.Exists(DescriptorPath))
                {
                    File.Delete(DescriptorPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("wallet descriptor could not be deleted: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// The wallet's state changed. Carries the new state, so a subscriber never has to read it
    /// back off the service and cannot observe a newer one than the update it is handling.
    /// </summary>
    public sealed class UpdateWalletState
    {
        public UpdateWalletState(WalletState state)
        {
            State = state;
        }

        public WalletState State { get; }
    }
}
