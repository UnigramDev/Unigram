//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Td.Api;
using WalletEngine;
using Windows.ApplicationModel;
using Windows.Storage;
using EngineSendMessage = WalletEngine.SendMessage;
// Both halves define a SendMessage - TDLib's is the function that sends a chat message - and this
// file is the one place they meet.
using File = System.IO.File;
using TdTonConnectSession = Telegram.Td.Api.TonConnectSession;
using TdTonWalletState = Telegram.Td.Api.TonWalletState;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// The wallet belonging to one Telegram account.
    /// </summary>
    /// <remarks>
    /// The only type that knows both halves exist.
    ///
    /// **TDLib owns the wallet.** Which wallet the account has, its balance and its history are
    /// server state: they arrive through <c>updateTonWalletState</c> and <c>getTonWalletTransactions</c>,
    /// and history is taken from there rather than from the chain because only TDLib can say which
    /// Telegram user is on the other side of a transfer.
    ///
    /// **The engine owns the key.** It turns a recovery phrase into a signing key, keeps it in
    /// protected storage, and signs transfers. It never broadcasts one: <c>sendTonWalletTransfer</c>
    /// does that, which is also the only way to get a relayer to pay the gas.
    ///
    /// So the engine's own view of the world - its snapshot, its refresh pump, its activity paging -
    /// is deliberately unused. Running it beside TDLib's would double every provider request to say
    /// the same thing twice, and the two would disagree while one of them was mid-refresh.
    /// </remarks>
    public sealed class WalletService : IWalletService
    {
        // From the engine's own defaults: DEFAULT_PROVIDER_REQUEST_TIMEOUT_MS and
        // DEFAULT_RESOLUTION_MARGIN_SECONDS in its config.rs. The validity has no default there;
        // 300 is what its examples and tests use.
        private const ulong RequestTimeoutMs = 15_000;
        private const ulong ResolutionMarginSeconds = 60;
        private const ulong SendValiditySeconds = 300;

        // TDLib caps this at 100.
        private const int ActivityPageSize = 50;

        // TDLib caps this at 20.
        private const int CollectiblesPageSize = 20;

        // How long to wait before asking the account about a transfer that has just left, and the
        // longest that wait grows to. A transfer settles in seconds, and the message it was sent as
        // expires in minutes, so the range is bounded on both ends by the thing being waited for.
        private static readonly TimeSpan ResolveFirstDelay = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan ResolveMaximumDelay = TimeSpan.FromSeconds(15);

        // How far a refresh will read to reach the newest transaction it already holds. Beyond
        // this, stitching the two halves together costs more than starting the history over - and
        // a wallet that saw this many transfers while the window was shut has a history the user
        // has not read anyway.
        private const int RefreshPageLimit = 5;

        // Mainnet, whatever the account is: the wallet of a test-server account is a mainnet
        // wallet like any other. Following Options.TestMode here derived a different address
        // altogether - the engine takes WALLET_SUBWALLET_ID_DEFAULT_TESTNET for testnet, so the
        // phrase produced a wallet the account had never heard of and every bind was refused -
        // and pointed the provider at testnet.toncenter, where the real one does not exist.
        private static Network DefaultNetwork => Network.Mainnet;

        private const uint DescriptorMagic = 0x4C41574Du; // "MWAL"
        private const uint ArchiveMagic = 0x4C415741u;    // "AWAL"
        private const int ArchiveVersion = 1;
        private const int DescriptorVersion = 2;

        private const uint RotationMagic = 0x544F524Du; // "MROT"
        private const int RotationVersion = 1;

        private static IReadOnlyList<string> _recoveryWords;

        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);

        private string _path;
        private string _suffix;
        private WalletPlatformHost _platform;
        private WalletLifecycle _lifecycle;

        private WalletStatuslessHost _transport;
        private WalletDescriptor _descriptor;

        // A key change this device has submitted and the chain has not shown yet. Until it
        // resolves, the wallet still signs with the old key - the contract has not accepted the
        // new one - so the descriptor is not switched and nothing else may spend the seqno this
        // rotation is using.
        private WalletRotation _rotation;

        // The public key the account reported when this device bound, which is the only thing that
        // changes under a rotation - see CanStillSign. Kept beside the descriptor, saved with it and
        // dropped with it.
        private byte[] _boundKey;

        // Wallets this device was the key for before the account moved on. Never emptied by a
        // rotation - see ArchiveDescriptorAsync for why.
        private readonly List<WalletArchiveRecord> _archive = new();
        private bool _archiveLoaded;

        // The ownership challenge's domain, normalized. See IsProofDomainAllowedAsync.
        private string _ownershipDomain;

        /// <summary>
        /// One past wallet, as stored. The balance and date are not stored: they are the chain's
        /// answer, read again each session, because either can move without this device.
        /// </summary>
        private sealed class WalletArchiveRecord
        {
            public string RecordId;
            public string Address;
            public byte[] PublicKey;
            public Network Network;
            public string SecretRef;
            public byte[] BoundKey;
            public long ArchivedDate;

            public BigInteger Balance;
            public int LastUsedDate;
        }
        private WalletClient _client;

        private TdTonWalletState _wallet;

        // What the account reports, in its order, and what this device has sent and is still
        // waiting on. Kept apart because only one of them is the server's to replace: a refresh
        // rebuilds the confirmed half and must leave a transfer in flight alone.
        private List<TonWalletTransaction> _confirmed = new List<TonWalletTransaction>();
        private List<TonWalletTransaction> _pending = new List<TonWalletTransaction>();

        // The two of them, as the view sees them. Rebuilt when either moves, never edited.
        private IReadOnlyList<TonWalletTransaction> _activity = Array.Empty<TonWalletTransaction>();
        private WalletResource _activityResource = WalletResource.Idle;
        private string _activityOffset = string.Empty;

        // Bumped wherever the history is abandoned rather than added to, which a view needs to know
        // to stop following the one before it.
        private int _activityGeneration;

        // The loop asking the account about transfers that have left but not landed, and the way to
        // stop it. One at a time: it walks whatever is pending each time round.
        private Task _resolver;
        private CancellationTokenSource _resolverCancellation;

        // The chain, watched directly, for as long as somebody is looking at the wallet.
        private WalletChainStream _stream;

        // The account's indexer trails the stream: a refresh run on the event itself can still
        // answer without the transaction. So an event is asked about again until the history
        // grows, which _activityGrowth counts, or these run out.
        private static readonly TimeSpan[] ChainEventFollowUps =
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(16),
            TimeSpan.FromSeconds(30)
        };

        private CancellationTokenSource _chainEventCancellation;
        private int _activityGrowth;

        // Whether a list has been read to its end, which only an empty next_offset says: nothing
        // read yet is not the end, and reporting it as one stops the list asking for a first page.
        private bool _activityEnd;
        private bool _collectiblesEnd;

        // The loads of each list, one at a time - see WalletPager.
        private readonly WalletPager _activityPager;
        private readonly WalletPager _collectiblesPager;

        private IReadOnlyList<TonNft> _collectibles = Array.Empty<TonNft>();
        private WalletResource _collectiblesResource = WalletResource.Idle;
        private string _collectiblesOffset = string.Empty;
        private int _collectiblesGeneration;

        public WalletService(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _activityPager = new WalletPager(LoadActivityCoreAsync);
            _collectiblesPager = new WalletPager(LoadCollectiblesCoreAsync);

            _aggregator.Subscribe<UpdateTonWalletState>(this, Handle)
                .Subscribe<UpdateTonWalletGaslessTransfersInfo>(Handle);
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

            // A test user and a production one can occupy the same session folder, so the files
            // have to say them apart. Not about networks - both wallets are mainnet - but about
            // keys: reading another account's secret is the one mistake with no recovery.
            _suffix = _clientService.Options.TestMode ? "_test" : string.Empty;

            Vault = new WalletVault(_path, _clientService.SessionId.ToString(), _suffix)
            {
                // The passcode is one setting for the whole app rather than one per account, which
                // is why it is the lifetime's and not this session's.
                Prompt = new WalletVaultPrompt(LifetimeService.Current.Passcode)
            };

            var secrets = new WalletSecretStore(Vault, _path, _suffix);
            var journal = new WalletJournalStore(Path.Combine(_path, "journal" + _suffix + ".bin"));

            _platform = new WalletPlatformHost(secrets, journal);
            _lifecycle = new WalletLifecycle(_platform);
        }

        /// <summary>
        /// The latest projection. Never null; <see cref="WalletState.None"/> until the account has
        /// a wallet.
        /// </summary>
        public WalletState State { get; private set; } = WalletState.None;

        /// <summary>
        /// What guards the secrets. Built by <see cref="EnsureStores"/>, so null until the first
        /// <see cref="RestoreAsync"/>.
        /// </summary>
        public WalletVault Vault { get; private set; }

        public WalletVault.WalletVaultLease CreateLease(INavigationService navigation, string reason = null)
        {
            return new WalletVault.WalletVaultLease(ResolveVaultAsync, navigation, reason);
        }

        // The vault is built with the stores, which may not exist yet when the operation that
        // will need it begins.
        private async Task<WalletVault> ResolveVaultAsync()
        {
            await _mutex.WaitAsync();
            try
            {
                EnsureStores();
                return Vault;
            }
            finally
            {
                _mutex.Release();
            }
        }

        /// <summary>
        /// Inflates the operation's lease, reporting a dismissed prompt the way every method here
        /// does: as <see cref="WalletAccessDeniedException"/>, which callers already treat as an
        /// answer rather than a fault.
        /// </summary>
        /// <remarks>
        /// **Never with <see cref="_mutex"/> held.** Inflating takes the vault gate, and an
        /// operation already holding the gate may be waiting for the mutex - so the two would wait
        /// on each other. Resolving the vault takes the mutex too, so on a lease not yet inflated
        /// the mistake hangs every time rather than occasionally.
        /// </remarks>
        private async Task EnsureLeaseAsync(WalletVault.WalletVaultLease lease)
        {
            try
            {
                await lease.EnsureAsync();
            }
            catch (WalletVaultException ex) when (ex.Failure == WalletVaultFailure.Cancelled)
            {
                throw new WalletAccessDeniedException();
            }
            catch (WalletVaultException ex) when (ex.Failure == WalletVaultFailure.Unenrolled)
            {
                // This operation still fails, but the next one binds again rather than meeting the
                // same dead key forever.
                await RecoverVaultAsync();
                throw;
            }
        }

        public async Task<bool> RecoverVaultAsync()
        {
            var vault = Vault;
            if (vault == null || await vault.IsUsableAsync())
            {
                return false;
            }

            Logger.Error("wallet vault can no longer be opened; dropping this device's copy");

            // The local copy is never the only one - the cloud backup or the written-down phrase
            // is - so giving it up is how the wallet gets back to a state it can bind from.
            await ForgetAsync(true);
            return true;
        }

        /// <summary>
        /// The 2048 BIP-39 words the engine validates against.
        /// </summary>
        /// <remarks>
        /// Copied verbatim out of the engine's own <c>wordlist_en.txt</c>, so that what the import
        /// screen accepts and what the engine accepts cannot drift apart. The engine now exports
        /// the list itself (<c>MnemonicWordlist</c>), which would remove the copy.
        /// </remarks>
        public static IReadOnlyList<string> RecoveryWords
        {
            get
            {
                if (_recoveryWords == null)
                {
                    var path = Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "Ton", "wordlist_en.txt");
                    _recoveryWords = File.ReadAllLines(path, Encoding.UTF8);
                }

                return _recoveryWords;
            }
        }

        public async Task<WalletState> RestoreAsync()
        {
            var changed = false;

            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                // Once per session. It is rebuilt from the file, so rereading it would throw away
                // the balances the chain has since filled in.
                if (!_archiveLoaded)
                {
                    _archiveLoaded = true;
                    LoadArchive();
                }

                // Before anything reads the descriptor: a rotation that was out when the app
                // closed decides which key the wallet signs with, and the answer is on the chain
                // rather than in either file.
                if (_rotation == null)
                {
                    TryLoadRotation(out _rotation);
                }

                if (_descriptor == null && TryLoadDescriptor(out _descriptor, out _boundKey))
                {
                    // The client is very likely already up as a watch-only one: the account's wallet
                    // arrives by update and does not wait to be asked for. Only a rebuild takes the
                    // key, because the config is fixed at construction.
                    await DetachAsync();

                    if (_wallet is { Address.Length: > 0 } && (!IsSameWallet(_descriptor, _wallet) || !CanStillSign(_wallet)))
                    {
                        // The stored key is for a wallet this account no longer has, or for the
                        // phrase it held before another device rotated it.
                        await ArchiveDescriptorAsync();
                    }

                    Attach();

                    SetState(Project());
                    changed = true;
                }
            }
            finally
            {
                _mutex.Release();
            }

            if (changed)
            {
                Raise();
            }

            // The first update almost certainly came and went before this service existed: it is
            // resolved lazily, and TDLib pushes the wallet as soon as the session starts. What it
            // pushed is in ClientService, which caches it like every other one-value update, and
            // reading it also asks for one if none has arrived yet.
            var reported = _clientService.TonWalletState;
            if (reported != null && _wallet == null)
            {
                await ApplyAsync(reported);
            }

            // Nor this one, and for the same reason: what is left on a wallet the account no longer
            // points at is the chain's answer, and nothing else waits on it.
            _ = RefreshArchiveAsync();

            // Nor this one: a key that turns out to be stale costs nothing until something signs,
            // and everything the wallet shows is readable either way.
            _ = VerifySigningKeyAsync();

            // A transfer left pending by a session that ended, or by a loop that gave up, is asked
            // about again from here.
            await _mutex.WaitAsync();
            try
            {
                if (_pending.Exists(x => x.State is TonWalletTransactionStatePending))
                {
                    EnsureResolver();
                }
            }
            finally
            {
                _mutex.Release();
            }

            return State;
        }

        public void Handle(UpdateTonWalletState update)
        {
            _ = ApplyAsync(update.State);
        }

        public void Handle(UpdateTonWalletGaslessTransfersInfo update)
        {
            _ = ApplyGaslessAsync();
        }

        /// <summary>
        /// Re-projects the state so that the new quota reaches whatever is showing it.
        /// </summary>
        /// <remarks>
        /// The value itself is already in <see cref="IClientService"/>, which caches it like every
        /// other one-value update; this only republishes the state it is part of. Nothing to say
        /// while there is no wallet: the state is <see cref="WalletState.None"/> either way.
        /// </remarks>
        private async Task ApplyGaslessAsync()
        {
            if (_wallet == null)
            {
                return;
            }

            await _mutex.WaitAsync();
            try
            {
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        private async Task ApplyAsync(TdTonWalletState wallet)
        {
            var reload = false;

            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                var previous = _wallet?.Address ?? string.Empty;
                var balance = _wallet?.GramAmount ?? 0;

                _wallet = wallet;

                // Before the staleness check below, and the order is the whole point: our own
                // rotation arrives here as a public key that does not match the one this device
                // bound, which is indistinguishable from someone else replacing the wallet unless
                // the rotation being waited for is settled first.
                var rotated = await SettleRotationAsync(wallet);

                // Nothing was seen before the first update, which is not the same as the wallet
                // having changed - and is the update that builds the client in the first place.
                var first = previous.Length == 0;
                var replaced = !first && !string.Equals(previous, wallet.Address, StringComparison.Ordinal);

                // The address outlives a key change: it is the hash of the wallet's initial state,
                // so rotating the phrase only rewrites the public key the contract holds, and the
                // account reports the same address it always had. The key this device stored has to
                // be measured against every update, not only against an address that moved.
                var stale = _descriptor != null
                    && wallet.Address.Length > 0
                    && (!IsSameWallet(_descriptor, wallet) || !CanStillSign(wallet));

                // A rotation is invisible from here except through these two keys, and either one
                // being absent makes it undetectable rather than absent - which is worth a line in
                // the log. Wallet state arrives a handful of times per session, not per frame.
                Logger.Info(string.Format("wallet key: account {0}, bound {1}, descriptor {2}",
                    wallet.PublicKey is { Length: > 0 } ? Convert.ToBase64String(wallet.PublicKey) : "none",
                    _boundKey is { Length: > 0 } ? Convert.ToBase64String(_boundKey) : "none",
                    _descriptor != null ? "yes" : "no"));

                // A settled rotation belongs here too: the client is built around the key it was
                // given, and the descriptor underneath it has just been swapped for the one that
                // holds the new phrase.
                if (first || replaced || stale || rotated)
                {
                    await DetachAsync();

                    if (replaced)
                    {
                        // A different wallet is a different journal and a different history. Nothing
                        // from the previous one survives the change.
                        _confirmed = new List<TonWalletTransaction>();
                        _pending = new List<TonWalletTransaction>();

                        // Whatever was in flight belonged to the wallet that is gone.
                        StopResolver();
                        _activityResource = WalletResource.Idle;
                        _activityOffset = string.Empty;
                        _activityEnd = false;
                        _activityGeneration++;
                        _activityPager.Reset();

                        _collectibles = Array.Empty<TonNft>();
                        _collectiblesResource = WalletResource.Idle;
                        _collectiblesOffset = string.Empty;
                        _collectiblesEnd = false;
                        _collectiblesGeneration++;
                        _collectiblesPager.Reset();

                        RebuildActivity();
                    }

                    if (stale)
                    {
                        // This device's key cannot sign for the wallet any more. Dropped the moment
                        // that becomes known rather than left to fail at the next transfer, where
                        // the only symptom would be a rejected message.
                        await ArchiveDescriptorAsync();
                    }

                    Attach();
                    reload = (first || replaced) && wallet.Address.Length > 0;
                }
                else if (wallet.GramAmount != balance && wallet.Address.Length > 0)
                {
                    // Grams moved, so something happened that the history has not heard about.
                    // Nothing announces a transaction - the balance changing is the announcement -
                    // and the refresh reads from the top only as far as the first row already held,
                    // which is usually one request that meets it immediately.
                    reload = true;
                }

                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();

            if (reload)
            {
                // The collectibles too: sending an NFT costs gas, and receiving one can carry a
                // forwarded amount.
                await Task.WhenAll(LoadActivityAsync(true), RefreshCollectiblesIfLoadedAsync());
            }
        }

        public async Task<WalletBindResult> BindAsync(IReadOnlyList<string> words, WalletVault.WalletVaultLease lease)
        {
            // Read again under the mutex; this only spares a prompt for a wallet that is not there.
            if (_wallet is not { Address.Length: > 0 })
            {
                return new WalletBindResult(WalletBindFailure.NoWallet);
            }

            // Storing the phrase is what makes the engine ask for the vault, and the engine has no
            // window of its own to ask in. Before the mutex, see EnsureLeaseAsync.
            await EnsureLeaseAsync(lease);

            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                var wallet = _wallet;
                if (wallet == null || wallet.Address.Length == 0)
                {
                    return new WalletBindResult(WalletBindFailure.NoWallet);
                }

                var array = new string[words.Count];

                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = words[i];
                }

                WalletDescriptor descriptor;

                // The engine derives the address from the phrase and stores the key on the way, so
                // a phrase for the wrong wallet has to be undone rather than refused up front.
                try
                {
                    descriptor = await _lifecycle.ImportWallet(new ImportWalletRequest(NewRecordId(), DefaultNetwork, array));
                }
                catch (Exception ex)
                {
                    // The words themselves: the engine validates the mnemonic and its checksum.
                    Logger.Error("wallet phrase refused: " + ex.Message);
                    return new WalletBindResult(WalletBindFailure.InvalidPhrase);
                }

                if (!IsSameWallet(descriptor, wallet))
                {
                    LogMismatch(descriptor, wallet, array);

                    await _lifecycle.DeleteWallet(descriptor);
                    return new WalletBindResult(WalletBindFailure.OtherWallet);
                }

                if (wallet.PublicKey is { Length: > 0 } reported && reported.Any(x => x != 0) && PhraseSigningKey(array) is byte[] signing && !signing.SequenceEqual(reported))
                {
                    // The anchor matches - it never changes - but the contract signs with another
                    // key now, so this device would sign messages the wallet refuses.
                    await _lifecycle.DeleteWallet(descriptor);
                    return new WalletBindResult(WalletBindFailure.OutdatedPhrase);
                }

                await DetachAsync();

                // What the account reports, which is what every later check compares against.
                // The server follows key changes on chain itself and rewrites this once one
                // confirms, so it is current even on a wallet somebody else has already rotated -
                // which it was not before, and this used to read the chain directly to get it.
                var bound = wallet.PublicKey;

                SaveDescriptor(descriptor, bound);

                _descriptor = descriptor;
                _boundKey = bound;

                Attach();
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
            return new WalletBindResult(State);
        }

        public async Task<IReadOnlyList<string>> ExportRecoveryPhraseAsync(string password)
        {
            // An empty password is TDLib's first attempt, answered with PASSWORD_MISSING when one
            // is needed, so it is passed through rather than refused here.
            var response = await _clientService.SendAsync(new GetTonWalletSecretPhrase(password ?? string.Empty));
            if (response is Text phrase)
            {
                return Split(phrase.TextValue);
            }

            // Which failure it was decides what the caller does next: a wrong password is worth
            // asking about again, and nothing else is.
            throw new WalletRequestException(response as Error);
        }

        public async Task<WalletBindResult> BindFromCloudAsync(string password, WalletVault.WalletVaultLease lease)
        {
            // Both halves throw what the caller has to tell apart: a wrong password comes out of
            // the export as WalletRequestException, and a phrase that derives another wallet out
            // of the bind.
            var words = await ExportRecoveryPhraseAsync(password);
            return await BindAsync(words, lease);
        }

        public async Task<IReadOnlyList<string>> RevealRecoveryPhraseAsync(WalletVault.WalletVaultLease lease)
        {
            if (_descriptor == null)
            {
                return null;
            }

            await EnsureLeaseAsync(lease);

            var descriptor = _descriptor;
            if (descriptor == null)
            {
                return null;
            }

            try
            {
                var phrase = await _lifecycle.RevealRecoveryPhrase(descriptor);
                return Split(phrase.Phrase);
            }
            catch (WalletLifecycleException.ProtectedSecretHost failed) when (failed.kind == ProtectedSecretHostErrorKind.Cancelled)
            {
                // The user dismissed the device prompt. That is an answer, not a fault, and it
                // must not be met by asking for the account password instead.
                throw new WalletAccessDeniedException();
            }
            catch (Exception ex)
            {
                // Anything else - no secret under that reference, storage that will not open - is
                // this device having nothing to give, which is the same answer as never having had
                // a key. The caller can still go to the cloud copy.
                Logger.Error("wallet secret unreadable: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Proves to the account that this device holds the wallet's key.
        /// </summary>
        /// <remarks>
        /// This is what replaced the private key the backup and replacement methods used to ask
        /// for: the account hands out a challenge, the key signs it here, and only the answer
        /// travels. The challenge is a `ton_proof` in all but name - payload and domain in, public
        /// key, timestamp and signature out - so the engine signs it with the same call TON Connect
        /// uses rather than with one of its own.
        ///
        /// The key in the answer is the **current signing** key, which differs from the
        /// descriptor's anchor after a rotation. That is why `replaceTonWallet` takes the anchor
        /// separately: the two are not interchangeable and the account needs both.
        ///
        /// Which wallet signs is the caller's to say. Enabling and disabling a backup are proved
        /// by the wallet they are about; a replacement is proved by the wallet being imported,
        /// which is not the current one and is not bound to anything yet.
        /// </remarks>
        private async Task<TonWalletOwnershipProof> ProveOwnershipAsync(WalletDescriptor descriptor, WalletVault.WalletVaultLease lease)
        {
            var lifecycle = _lifecycle;

            if (lifecycle == null || descriptor == null)
            {
                throw new WalletNotBoundException();
            }

            // Signing reads the wallet key. Before the challenge, which the prompt could otherwise
            // outlive.
            await EnsureLeaseAsync(lease);

            var response = await _clientService.SendAsync(new GetTonWalletOwnershipProofChallenge());
            if (response is not TonWalletOwnershipProofChallenge challenge)
            {
                throw new WalletRequestException(response as Error);
            }

            _ownershipDomain = NormalizeDomain(challenge.Domain);

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var signature = await lifecycle.SignTonConnectProof(
                new TonConnectProofSignRequest(descriptor, challenge.Domain, (ulong)timestamp, challenge.Payload));

            return new TonWalletOwnershipProof(signature.PublicKey, (int)timestamp, signature.Signature);
        }

        /// <summary>
        /// Puts the recovery phrase back in the Telegram cloud.
        /// </summary>
        /// <remarks>
        /// No account password any more: the proof is what the account now takes, and it is worth
        /// noticing that this is one prompt fewer rather than one more.
        /// </remarks>
        public async Task EnableBackupAsync(IReadOnlyList<string> words, WalletVault.WalletVaultLease lease)
        {
            if (_rotation != null)
            {
                // The phrase in hand stops signing once the rotation lands, so uploading it would
                // back up a wallet nobody can spend from.
                throw new WalletRotationPendingException();
            }

            var proof = await ProveOwnershipAsync(_descriptor, lease);

            var response = await _clientService.SendAsync(new EnableTonWalletBackup(string.Join(" ", words), proof));
            if (response is Error error)
            {
                throw new WalletRequestException(error);
            }
        }

        /// <summary>
        /// Takes the recovery phrase out of the Telegram cloud.
        /// </summary>
        /// <remarks>
        /// Proved rather than passworded. `disableTonWalletBackup` still exists and still takes the
        /// account password, but it is the weaker of the two: the password says who is asking and
        /// the proof says they hold the key, which is the thing actually at stake in giving up the
        /// only other copy of it.
        /// </remarks>
        public async Task DisableBackupAsync(WalletVault.WalletVaultLease lease)
        {
            var proof = await ProveOwnershipAsync(_descriptor, lease);

            var response = await _clientService.SendAsync(new DisableTonWalletBackupWithProof(proof));
            if (response is Error error)
            {
                throw new WalletRequestException(error);
            }
        }

        /// <summary>
        /// Replaces the account's wallet with one the user already has, from its recovery phrase.
        /// </summary>
        /// <remarks>
        /// The proof is signed by the **incoming** wallet rather than the current one - the account
        /// is being shown that whoever is asking holds the key to the wallet they are asking it to
        /// adopt. The anchor key goes alongside it because the address is derived from the anchor
        /// and the proof carries the signing key, which are the same only until the first rotation.
        ///
        /// The import is undone when the account refuses. A descriptor left behind is a phrase left
        /// in protected storage for a wallet this device does not have, which is the kind of thing
        /// that is only ever found much later.
        /// </remarks>
        public async Task ReplaceWalletAsync(string password, IReadOnlyList<string> words, WalletVault.WalletVaultLease lease)
        {
            var lifecycle = _lifecycle;
            if (lifecycle == null)
            {
                throw new WalletNotBoundException();
            }

            // Importing stores the phrase under the vault key.
            await EnsureLeaseAsync(lease);

            var imported = await lifecycle.ImportWallet(
                new ImportWalletRequest(NewRecordId(), DefaultNetwork, words.ToArray()));

            TonWalletOwnershipProof proof;

            try
            {
                proof = await ProveOwnershipAsync(imported, lease);

                var response = await _clientService.SendAsync(
                    new ReplaceTonWallet(password ?? string.Empty, imported.PublicKey, proof));

                if (response is Error error)
                {
                    throw new WalletRequestException(error);
                }
            }
            catch
            {
                await lifecycle.DeleteWallet(imported);
                throw;
            }

            // The account's wallet is the imported one from here on, so the descriptor this device
            // signs with has to follow it. Everything else - the balance, the history, the state -
            // arrives from the account as an update, the same way it does after any other change.
            await _mutex.WaitAsync();
            try
            {
                // The update announcing the new wallet can land before this answer or after it.
                // Either way the old wallet is archived rather than deleted, so both orders end in
                // the same place - and whatever is still on it stays reachable from its phrase.
                await ArchiveDescriptorAsync();

                if (_wallet is { Address.Length: > 0 } wallet && !IsSameWallet(imported, wallet))
                {
                    Logger.Error(string.Format("wallet replaced, but the account reports {0} rather than {1}", wallet.Address, imported.Address));
                }

                // The key the proof was signed with, not the descriptor's: for a phrase that has
                // been rotated the descriptor carries the anchor, and the contract holds the other.
                _descriptor = imported;
                _boundKey = proof.PublicKey;

                SaveDescriptor(_descriptor, _boundKey);

                // Attached afresh, because the client in place - if the update came first - was
                // attached watch-only, with no key to sign with.
                await DetachAsync();
                Attach();
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        public async Task<WalletTransferResult> SendAsync(string recipient, byte[] recipientPublicKey, long peerUserId, string peerDomain, BigInteger amountNanograms, string comment, bool isCommentPublic, bool allowGasless, int sendingId, WalletVault.WalletVaultLease lease)
        {
            var client = _client;
            if (client == null || _descriptor == null)
            {
                // Nothing here asks for a phrase: binding is a decision with UI attached, and the
                // caller is the one that can make it and then try again.
                throw new WalletNotBoundException();
            }

            // Covers the encrypted comment as well as the transfer: both sign with this wallet's
            // key, and the user confirming once is confirming the send they asked for.
            await EnsureLeaseAsync(lease);

            if (_rotation != null)
            {
                // A key change is out under this wallet's current seqno, and the contract accepts
                // one message per seqno. Sending now would either lose this transfer or the
                // rotation, and which one is not ours to pick.
                throw new WalletRotationPendingException();
            }

            if (HasUnsettledTransfer())
            {
                throw new WalletTransferInProgressException();
            }

            SendMessageBody body;

            // What the account and the history are told the comment is: the text itself, or for an
            // encrypted one the payload TDLib reports such comments as - never the plaintext, which
            // would go out as an encrypted comment that does not decrypt.
            var sentComment = comment ?? string.Empty;
            var isCommentEncrypted = false;

            if (string.IsNullOrEmpty(comment))
            {
                body = new SendMessageBody.Empty();
            }
            else if (isCommentPublic)
            {
                body = new SendMessageBody.Comment(comment);
            }
            else
            {
                // The engine reads the recipient's key off their contract, or takes the account's
                // for a wallet with no contract yet, and asks for this wallet's phrase - so an
                // encrypted comment is a signing operation of its own, and the body it hands back
                // is a cell rather than text.
                try
                {
                    var encrypted = await client.CreateEncryptedComment(new CreateEncryptedCommentRequest(recipient, comment, recipientPublicKey));
                    body = new SendMessageBody.RawPayload(encrypted);

                    var payload = WalletCommentBody.ToPayload(encrypted);
                    if (payload != null)
                    {
                        sentComment = payload;
                        isCommentEncrypted = true;
                    }
                    else
                    {
                        // The chain still carries it; only the account's copy goes without.
                        Logger.Error("wallet comment body could not be read back");
                        sentComment = string.Empty;
                    }
                }
                catch (WalletClientException.EncryptedCommentUnavailable ex)
                {
                    // There is no key to encrypt to: a frozen wallet, a contract that does not
                    // expose one, or an undeployed wallet whose key the account did not report.
                    // Nothing was signed and nothing was spent, so this is an answer rather than a
                    // fault - and the caller can do something about it, because the same comment
                    // can go publicly instead.
                    Logger.Error("wallet comment could not be encrypted: " + ex.diagnostic);
                    return WalletTransferResult.CommentUnavailable;
                }
            }

            // A wallet the account names a user for may not be deployed yet, and a bounceable
            // message to an undeployed one comes back instead of arriving.
            var message = new EngineSendMessage(
                recipient,
                new SendAmount.Exact(amountNanograms.ToString()),
                body,
                peerUserId == 0 && Bounceable(recipient),
                null);

            var intent = new SendIntent(new SendExpiration.EngineDefault(), new[] { message });
            var prepared = await client.PrepareTransfer(new PrepareTransferRequest(NewRecordId(), intent));

            // Both messages cover the same seqno and expiry, so offering both is offering the server
            // a choice, not two transfers: whichever it broadcasts, the other can never also land.
            var external = Bytes(prepared.ExternalBoc);
            var gasless = allowGasless ? Bytes(prepared.InternalBoc) : Array.Empty<byte>();

#if DEBUG
            if (DebugSpoilTransfers)
            {
                external = Spoil(external);
                gasless = Spoil(gasless);
            }
#endif

            // At the top of the history before the request leaves, because the account holds it
            // open until the transfer is final and the caller is not made to wait for that. TDLib
            // reports transactions, and this is not one yet.
            await _mutex.WaitAsync();
            try
            {
                AddPending(recipient, peerUserId, peerDomain, amountNanograms, sentComment, isCommentEncrypted, allowGasless, prepared.OperationId, prepared.ValidUntil);
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();

            _ = CompleteTransferAsync(prepared.OperationId, _clientService.SendAsync(new SendTonWalletTransfer(peerUserId, recipient, (long)amountNanograms, sentComment, isCommentEncrypted, sendingId, external, gasless)));

            return WalletTransferResult.Sent;
        }

        /// <summary>
        /// Settles the row SendAsync added, once the account answers the transfer.
        /// </summary>
        /// <remarks>
        /// The row is looked up rather than held: the resolver can settle it first - the engine may
        /// see the transfer on the chain, or its expiry pass, while the account is still holding
        /// the request - and a replaced wallet drops it altogether. Nothing awaits this, so nothing
        /// may escape it.
        /// </remarks>
        private async Task CompleteTransferAsync(string operationId, Task<Object> request)
        {
            try
            {
#if DEBUG
                if (DebugSpoilTransfers)
                {
                    await Task.Delay(2000);
                }
#endif

                var response = await request;

                await _mutex.WaitAsync();
                try
                {
                    var index = _pending.FindIndex(x => x.Id == operationId);
                    if (index < 0)
                    {
                        return;
                    }

                    var pending = _pending[index];
                    var items = new List<TonWalletTransaction>(_pending);

                    if (response is TonWalletTransferResult result && result.Transaction != null)
                    {
                        // The same row the history returns, so there is nothing left to poll for.
                        items.RemoveAt(index);
                        _pending = items;

                        Succeed(pending.Id, result.Transaction);
                        AddConfirmed(result.Transaction);
                    }
                    else if (response is TonWalletTransferResult accepted)
                    {
                        // Accepted but not yet final when the account stopped waiting. A row the
                        // resolver already failed on expiry stays failed: it can no longer land.
                        if (pending.State is not TonWalletTransactionStatePending state)
                        {
                            return;
                        }

                        var type = pending.Type is TonWalletTransactionTypeTransfer transfer
                            ? new TonWalletTransactionTypeTransfer(transfer.Amount, accepted.IsGasless, transfer.Comment, transfer.IsCommentEncrypted)
                            : pending.Type;

                        items[index] = With(pending, new TonWalletTransactionStatePending(state.OperationId, MessageHash(accepted.MsgHash), state.ExpirationDate), type);
                        _pending = items;

                        RebuildActivity();
                        EnsureResolver();
                    }
                    else if (response is Error error && error.Code >= 400 && error.Code < 500)
                    {
                        Logger.Error(string.Format("wallet transfer refused: {0} {1}", error.Code, error.Message));

                        items[index] = With(pending, new TonWalletTransactionStateFailed(), pending.Type);
                        _pending = items;

                        RebuildActivity();
                    }
                    else
                    {
                        // A timeout or a server fault says nothing about whether the message was
                        // broadcast, so the row stays pending until the chain or its expiry says.
                        // Calling it failed would invite sending the same money again.
                        var unknown = response as Error;
                        Logger.Error(string.Format("wallet transfer outcome unknown: {0} {1}", unknown?.Code, unknown?.Message));

                        EnsureResolver();
                        return;
                    }

                    SetState(Project());
                }
                finally
                {
                    _mutex.Release();
                }

                Raise();
            }
            catch (Exception ex)
            {
                Logger.Error("wallet transfer sent but not recorded: " + ex.Message);
            }
        }

        private bool HasUnsettledTransfer()
        {
            return _pending.Exists(x => x.State is TonWalletTransactionStatePending);
        }

        private static TonWalletTransaction With(TonWalletTransaction transaction, TonWalletTransactionState state, TonWalletTransactionType type)
        {
            return new TonWalletTransaction(
                transaction.Id,
                transaction.PeerAddress,
                transaction.PeerUserId,
                transaction.PeerDomain,
                transaction.Date,
                transaction.FeeAmount,
                state,
                type);
        }

        public async Task<BigInteger?> EstimateFeeAsync(string recipient, BigInteger amountNanograms, string comment, bool isCommentPublic)
        {
            var client = _client;
            if (client == null || string.IsNullOrEmpty(recipient) || amountNanograms <= BigInteger.Zero)
            {
                return null;
            }

            try
            {
                // A private comment is priced by a stand-in of the size it will encrypt to, never
                // by its text: the emulation runs on Toncenter. Encrypting the real one is a
                // signing operation, and a user-presence prompt per keystroke is not worth it.
                var body = string.IsNullOrEmpty(comment)
                    ? new SendMessageBody.Empty()
                    : (SendMessageBody)new SendMessageBody.Comment(isCommentPublic ? comment : EncryptedCommentStandIn(comment));

                var message = new EngineSendMessage(
                    recipient,
                    new SendAmount.Exact(amountNanograms.ToString()),
                    body,
                    Bounceable(recipient),
                    null);

                var intent = new SendIntent(new SendExpiration.EngineDefault(), new[] { message });
                var preview = await client.PreviewSend(new SendPreviewRequest(intent));

                // What this wallet's own transaction is charged, not the trace total: the rest of
                // the trace is the recipient's side, and they pay for it out of what they receive.
                return BigInteger.Parse(preview.Emulation.WalletFeesNanograms);
            }
            catch (Exception ex)
            {
                // An estimate nobody can be given is a line that is not shown, never a failure:
                // the transfer itself does not depend on this having worked.
                Logger.Error("wallet fee could not be estimated: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Text that encodes to as many bytes as <paramref name="comment"/> will once encrypted.
        /// </summary>
        /// <remarks>
        /// The encrypted format is the 32-byte key XOR and the 16-byte message key, then the text
        /// behind 16 to 31 bytes of padding that brings it to a multiple of 16. The opcode in front
        /// is four bytes either way.
        /// </remarks>
        private static string EncryptedCommentStandIn(string comment)
        {
            var length = Encoding.UTF8.GetByteCount(comment);
            var padded = (length + 16 + 15) / 16 * 16;

            return new string('0', 32 + 16 + padded);
        }

        // How long a prepared phrase update stays submittable. The user writes the phrase down and
        // passes the test between preparing and committing, so this has to outlast that; it is
        // also how long a submitted change that never lands blocks the wallet before it expires.
        private static readonly TimeSpan PhraseUpdateWindow = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Generates the phrase that will replace the current one, and signs the key change that
        /// makes it so - without submitting anything or storing the phrase.
        /// </summary>
        /// <remarks>
        /// Split from <see cref="CommitRecoveryPhraseUpdateAsync"/> so the phrase can be shown and
        /// tested first: until it is committed the wallet is untouched, and a user who walks away
        /// leaves nothing behind. The engine generates the phrase and signs the message in one
        /// call, so the signature has to be made now, valid for <see cref="PhraseUpdateWindow"/>.
        /// </remarks>
        public async Task<WalletPhraseUpdate> PrepareRecoveryPhraseUpdateAsync(WalletVault.WalletVaultLease lease)
        {
            var client = _client;

            if (client == null || _descriptor == null || _wallet is not { Address.Length: > 0 })
            {
                throw new WalletNotBoundException();
            }

            // The message is signed by the key it replaces.
            await EnsureLeaseAsync(lease);

            if (_rotation != null)
            {
                throw new WalletRotationPendingException();
            }

            var validUntil = (ulong)DateTimeOffset.UtcNow.Add(PhraseUpdateWindow).ToUnixTimeSeconds();

            var prepared = await client.PrepareKeyRotation(
                new PrepareKeyRotationRequest(validUntil, KeyRotationMessageKind.External));

            return new WalletPhraseUpdate(Split(prepared.ReplacementRecoveryPhrase.Phrase), prepared);
        }

        /// <summary>
        /// Submits a phrase update prepared by <see cref="PrepareRecoveryPhraseUpdateAsync"/>.
        /// </summary>
        /// <remarks>
        /// The order is the whole of the safety here. The replacement phrase is stored **before**
        /// the message is submitted, because a key change that lands while the phrase behind it is
        /// nowhere leaves a wallet nobody can sign for. The descriptor is switched **after** the
        /// chain shows the new key, because until then the old one is still what the contract
        /// accepts.
        /// </remarks>
        public async Task CommitRecoveryPhraseUpdateAsync(WalletPhraseUpdate update, WalletVault.WalletVaultLease lease)
        {
            var client = _client;
            var wallet = _wallet;

            if (client == null || _descriptor == null || wallet is not { Address.Length: > 0 })
            {
                throw new WalletNotBoundException();
            }

            // Storing the phrase writes it under the vault key.
            await EnsureLeaseAsync(lease);

            if (_rotation != null)
            {
                throw new WalletRotationPendingException();
            }

            var prepared = update.Prepared;

            // Refused here rather than by the chain: an expired message would be stored as a
            // pending rotation and block the wallet until the update that never comes.
            if (update.IsExpired)
            {
                throw new WalletRotationFailedException("the prepared key change has expired");
            }

            var replacement = await _lifecycle.ImportWallet(
                new ImportWalletRequest(NewRecordId(), DefaultNetwork, update.Words.ToArray()));

            // A rotation preserves the anchor, and the address is derived from it, so this holds by
            // construction. If it ever did not, what was just stored would be the key to somewhere
            // else and the wallet would be unreachable.
            if (!IsSameWallet(replacement, wallet))
            {
                LogMismatch(replacement, wallet, update.Words.ToArray());

                await _lifecycle.DeleteWallet(replacement);
                throw new WalletRotationFailedException("the replacement phrase derives another address");
            }

            var rotation = new WalletRotation(replacement, prepared.NewPublicKey, prepared.ValidUntil);

            await _mutex.WaitAsync();
            try
            {
                _rotation = rotation;
                SaveRotation(rotation);
            }
            finally
            {
                _mutex.Release();
            }

            try
            {
                await client.SendBoc(new SendBocRequest(
                    NewRecordId(), false, prepared.SignedBoc, prepared.Seqno, prepared.ValidUntil));
            }
            catch (WalletClientException.SubmissionUnknown ex)
            {
                // The change may still land, so the phrase behind it has to stay: the account's
                // update settles it if it does, the expiry if it does not.
                Logger.Error("wallet key rotation submission unknown: " + ex.diagnostic);
            }
            catch (WalletClientException ex)
            {
                // Nothing was signed away: the message never reached the chain, the contract still
                // holds the old key, and the phrase just stored is the one to forget.
                Logger.Error("wallet key rotation refused: " + ex.Message);

                await DiscardRotationAsync(rotation);
                throw new WalletRotationFailedException(ex.Message);
            }

            // Nothing to wait for. The account reports the new key once the change confirms, and
            // the update that carries it settles this.
        }

        /// <summary>
        /// Settles a submitted key change against what the account reports.
        /// </summary>
        /// <remarks>
        /// Nothing is polled and nothing is read from the chain. The server watches
        /// <c>change_wallet_key</c> itself, re-reads the key once the transaction confirms, and
        /// sends the state on - so the account's report is both the answer and the notification,
        /// and it says the same thing whichever client performed the rotation.
        ///
        /// Called with <see cref="_mutex"/> held, from the update that carries the key.
        /// </remarks>
        /// <returns>
        /// Whether the descriptor was swapped, which the caller has to rebuild the client around.
        /// </returns>
        private async Task<bool> SettleRotationAsync(TdTonWalletState wallet)
        {
            var rotation = _rotation;
            if (rotation == null)
            {
                return false;
            }

            if (wallet.PublicKey is { Length: > 0 } reported && reported.SequenceEqual(rotation.PublicKey))
            {
                var previous = _descriptor;

                _descriptor = rotation.Descriptor;
                _boundKey = rotation.PublicKey;

                SaveDescriptor(_descriptor, _boundKey);

                _rotation = null;
                DeleteRotation();

                // Last, and only once what replaces it is on disk: the old phrase is dead weight
                // the moment the contract stops accepting its key, but deleting it before the new
                // descriptor is saved would leave a crash with neither.
                if (previous != null)
                {
                    await DeleteQuietlyAsync(previous);
                }

                return true;
            }

            if ((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds() > rotation.ValidUntil)
            {
                // The signature the message carried can no longer be included, so the key it would
                // have set can never arrive. The old phrase is still the wallet's, and nothing was
                // swapped - so there is nothing to rebuild.
                _rotation = null;
                DeleteRotation();

                await DeleteQuietlyAsync(rotation.Descriptor);
            }

            return false;
        }

        private async Task DiscardRotationAsync(WalletRotation rotation)
        {
            await _mutex.WaitAsync();
            try
            {
                if (_rotation == rotation)
                {
                    _rotation = null;
                    DeleteRotation();
                }
            }
            finally
            {
                _mutex.Release();
            }

            await DeleteQuietlyAsync(rotation.Descriptor);
        }

        /// <summary>
        /// Forgets a phrase the engine is holding, for the paths where failing to is not worth
        /// failing the operation over.
        /// </summary>
        private async Task DeleteQuietlyAsync(WalletDescriptor descriptor)
        {
            try
            {
                await _lifecycle.DeleteWallet(descriptor);
            }
            catch (Exception ex)
            {
                // A phrase left in protected storage under a record nothing points at. It opens
                // nothing on its own and the next bind writes over it.
                Logger.Error("wallet phrase could not be forgotten: " + ex.Message);
            }
        }

        public async Task<BigInteger?> EstimateKeyRotationFeeAsync(WalletVault.WalletVaultLease lease)
        {
            var client = _client;
            if (client == null || _descriptor == null)
            {
                // Not the same as having no estimate: without a key here there is nothing to
                // rotate, so the caller has to bind rather than show a cost for something that
                // cannot happen.
                throw new WalletNotBoundException();
            }

            // Preparing one reads the phrase - the key-change message is signed by the key it
            // replaces - so this costs a confirmation, unless the operation already gave one.
            // Outside the catch below: a refusal is the caller's answer, not a missing estimate.
            await EnsureLeaseAsync(lease);

            try
            {
                // Long enough that the emulation is against a message that would still be valid,
                // short enough to be meaningless afterwards: nothing here is kept.
                var validUntil = (ulong)DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds();

                var prepared = await client.PrepareKeyRotation(
                    new PrepareKeyRotationRequest(validUntil, KeyRotationMessageKind.External));

                var preview = await client.PreviewSendBoc(new SendBocRequest(
                    NewRecordId(), false, prepared.SignedBoc, prepared.Seqno, prepared.ValidUntil));

                return BigInteger.Parse(preview.Emulation.WalletFeesNanograms);
            }
            catch (Exception ex)
            {
                // An estimate nobody can be given is a line that is not shown. The caller decides
                // what to do without one; it must not read as "free".
                Logger.Error("wallet key rotation fee could not be estimated: " + ex.Message);
                return null;
            }
        }

        public async Task<TonWalletTransaction> GetTransactionAsync(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId))
            {
                return null;
            }

            await _mutex.WaitAsync();
            try
            {
                foreach (var item in _activity)
                {
                    if (string.Equals(item.Id, transactionId, StringComparison.Ordinal))
                    {
                        return item;
                    }
                }
            }
            finally
            {
                _mutex.Release();
            }

            var response = await _clientService.SendAsync(new GetTonWalletTransaction(transactionId));
            return response as TonWalletTransaction;
        }

        public async Task<TdTonConnectSession> GetSessionAsync(long sessionId)
        {
            // Through the pending requests because there is no getter for one session, and this one
            // reads nothing protected: the session is public state, and only deriving its key is not.
            var response = await _clientService.SendAsync(new GetTonConnectSessionPendingRequests(sessionId));
            return response is TonConnectRequests requests ? requests.Session : null;
        }

        public async Task<WalletRequest> GetRequestAsync(long messageId, MessageTonConnectRequest message, WalletVault.WalletVaultLease lease)
        {
            // The stores are built on the first restore, so nothing below exists until it has run.
            // A request can arrive in a chat long before the wallet has been opened in this
            // session, which is exactly when this is reached.
            await RestoreAsync();

            var client = _client;
            var lifecycle = _lifecycle;
            var descriptor = _descriptor;

            if (client == null || lifecycle == null || descriptor == null)
            {
                throw new WalletNotBoundException();
            }

            // Deriving the session reads the wallet key, so this is behind a confirmation - and
            // it has to come before the sheet rather than on its Confirm button, because without it
            // the request cannot be decrypted and there is nothing to show. The sheet keeps the
            // lease afterwards, so answering does not ask again.
            await EnsureLeaseAsync(lease);

            var response = await _clientService.SendAsync(new GetTonConnectSessionPendingRequests(message.SessionId));
            if (response is not TonConnectRequests pending)
            {
                // Answered by another device, or expired while the chat was open. Either way there
                // is nothing left to decide.
                return null;
            }

            var request = pending.Requests.FirstOrDefault(x => x.MessageId == messageId);
            if (request == null)
            {
                return null;
            }

            // Derived rather than stored, the same way connecting derives it: the session key comes
            // from the wallet key, the dApp id and the server nonce, so any device of this account
            // arrives at it.
            using var derived = await DeriveAsync(lifecycle, descriptor, pending.Session);
            if (derived == null)
            {
                return null;
            }

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var decrypted = derived.DecryptRequest(request.Body, now);

            // Answered here rather than shown: there is nothing for the user to decide, and a
            // request nobody answers leaves the dApp waiting until its own timeout.
            if (decrypted.Request is not TonConnectIncomingRequest.SendTransaction send)
            {
                var (rejected, body) = UnsupportedAnswer(derived, decrypted.Request);
                await ClaimAndAnswerAsync(message.SessionId, messageId, RequestId(decrypted.Request), message.TraceId, rejected, body);
                return null;
            }

            var preview = await client.PreviewTonConnect(send.Request);
            var message0 = preview.Messages.FirstOrDefault();

            if (message0 == null)
            {
                await ClaimAndAnswerAsync(message.SessionId, messageId, send.Id, message.TraceId, true,
                    derived.EncryptError(send.Id, TonConnectRpcErrorCode.BadRequest, "No messages"));
                return null;
            }

            var messages = new List<WalletRequestMessage>(preview.Messages.Length);
            var total = BigInteger.Zero;

            foreach (var item in preview.Messages)
            {
                var amount = Amount(item.Amount);
                total += amount;

                // A raw body that reads as a plain comment is shown as one, the way the chain
                // will show it; anything else is shown as the payload it is.
                var raw = (item.Body as SendMessageBody.RawPayload)?.Boc;
                var comment = item.Body is SendMessageBody.Comment text
                    ? text.Text
                    : WalletCommentBody.TextFromBoc(raw);

                messages.Add(new WalletRequestMessage(item.Destination, amount, comment, comment == null ? raw : null, item.StateInit != null));
            }

            return new WalletRequest(message.SessionId, messageId, send.Id, message.TraceId)
            {
                Name = message.DappName,
                Domain = Domain(pending.Session),
                Recipient = message0.Destination,
                Nanograms = total,
                Messages = messages,
                FeeNanograms = BigInteger.Parse(preview.Emulation.WalletFeesNanograms),
                Actions = Actions(preview.Emulation, descriptor.Address),
                ExpirationDate = request.ExpirationDate
            };
        }

        // The opcode of a contract handing back what a message left over.
        private const uint ExcessOpcode = 0xD53276DB;

        /// <summary>
        /// The emulation, as rows: what each action in the trace does, seen from this wallet.
        /// </summary>
        /// <remarks>
        /// Toncenter reports each action with its details as a JSON object -
        /// <c>{source, destination, value, comment, encrypted}</c> for a transfer, value in
        /// nanograms as a string. The direction is not among them: it is whether *our* address is
        /// the destination, which is the one thing only this side knows.
        ///
        /// The address each row shows is the other end of that leg - where it came from for a
        /// deposit, where it goes for a withdrawal. On a transfer that returns change to the same
        /// wallet both legs name the same address, which is why the two rows can look alike.
        ///
        /// Every kind gets a row: a contract call or an operation Toncenter could not name is
        /// exactly what the user most needs to see before signing. Only Toncenter's own "unknown"
        /// is left out, which says nothing a row could.
        /// </remarks>
        private static IReadOnlyList<WalletRequestAction> Actions(SendEmulation emulation, string ours)
        {
            var items = new List<WalletRequestAction>();

            foreach (var action in emulation.Actions)
            {
                if (string.Equals(action.Kind, "unknown", StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(action.DetailsJson);
                    var root = document.RootElement;

                    var source = Text(root, "source");
                    var destination = Text(root, "destination");

                    var deposit = IsSameAddress(destination, ours);
                    var direction = deposit ? WalletRequestDirection.Deposit : WalletRequestDirection.Withdraw;

                    BigInteger.TryParse(Text(root, "value"), out var value);

                    string label;

                    switch (action.Kind)
                    {
                        case "ton_transfer":
                            label = deposit ? Strings.WalletIncomingTransfer : Strings.WalletOutgoingTransfer;
                            break;
                        case "call_contract":
                            label = Opcode(root) == ExcessOpcode ? Strings.WalletExcess : Strings.WalletContractCall;
                            break;
                        case "contract_deploy":
                            // Moves nothing of its own; the address is the contract being made.
                            label = Strings.WalletContractDeploy;
                            direction = WalletRequestDirection.Withdraw;
                            value = BigInteger.Zero;
                            destination ??= action.Accounts.FirstOrDefault();
                            break;
                        default:
                            // An extra currency or a token: whatever the value is, it is not in
                            // nanograms, so it is not shown as grams.
                            label = Strings.WalletUnknownOperation;
                            value = BigInteger.Zero;
                            break;
                    }

                    items.Add(new WalletRequestAction(
                        direction == WalletRequestDirection.Deposit ? source : destination,
                        direction,
                        value,
                        Comment(root),
                        label));
                }
                catch (Exception ex)
                {
                    Logger.Error("wallet emulation action could not be read: " + ex.Message);
                }
            }

            return items;
        }

        private static string Text(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        /// <summary>
        /// The opcode of a contract call, which Toncenter gives as a "0x" hex string or a number.
        /// </summary>
        private static uint? Opcode(JsonElement root)
        {
            if (!root.TryGetProperty("opcode", out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                return text != null
                    && text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    && uint.TryParse(text.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
                    ? hex
                    : null;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                if (value.TryGetUInt32(out var number))
                {
                    return number;
                }

                if (value.TryGetInt32(out var signed))
                {
                    return unchecked((uint)signed);
                }
            }

            return null;
        }

        /// <summary>
        /// What was written on a transfer, where it is there to be read.
        /// </summary>
        /// <remarks>
        /// An encrypted comment is only readable by the two parties, and the emulator is neither -
        /// it reports the flag and nothing else. Saying so is better than an empty line, which
        /// would read as no comment at all.
        /// </remarks>
        private static string Comment(JsonElement root)
        {
            if (root.TryGetProperty("encrypted", out var encrypted) && encrypted.ValueKind == JsonValueKind.True)
            {
                return "[encrypted comment]";
            }

            return root.TryGetProperty("comment", out var comment) && comment.ValueKind == JsonValueKind.String
                ? comment.GetString()
                : null;
        }

        /// <summary>
        /// The host the dApp's manifest names, which connecting required to be the one it was
        /// fetched from.
        /// </summary>
        private static string Domain(TdTonConnectSession session)
        {
            if (session?.Manifest is not TonConnectManifestInfo info)
            {
                return string.Empty;
            }

            return Common.WalletHelper.TonConnectHost(info.Url) ?? string.Empty;
        }

        /// <summary>
        /// The nanograms an engine send amount stands for, or zero where it names no fixed value.
        /// </summary>
        private static BigInteger Amount(SendAmount amount)
        {
            return amount is SendAmount.Exact exact && BigInteger.TryParse(exact.Nanograms, out var value)
                ? value
                : BigInteger.Zero;
        }

        public async Task<bool> AnswerRequestAsync(WalletRequest request, bool accept, WalletVault.WalletVaultLease lease)
        {
            var client = _client;
            var lifecycle = _lifecycle;
            var descriptor = _descriptor;

            if (client == null || lifecycle == null || descriptor == null)
            {
                throw new WalletNotBoundException();
            }

            // Before the claim, like everything else that can stop this: a claim this device then
            // fails to follow with an answer locks the other devices out and leaves the dApp
            // waiting.
            await EnsureLeaseAsync(lease);

            // Read again rather than held from the sheet: the session is what encrypts the answer,
            // and keeping a decrypted request alive across a screen is keeping a spend authorized
            // for as long as somebody looks at it.
            var response = await _clientService.SendAsync(new GetTonConnectSessionPendingRequests(request.SessionId));
            if (response is not TonConnectRequests pending)
            {
                return false;
            }

            var current = pending.Requests.FirstOrDefault(x => x.MessageId == request.MessageId);
            if (current == null)
            {
                return false;
            }

            using var derived = await DeriveAsync(lifecycle, descriptor, pending.Session);
            if (derived == null)
            {
                return false;
            }

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var decrypted = derived.DecryptRequest(current.Body, now);

            if (decrypted.Request is not TonConnectIncomingRequest.SendTransaction send)
            {
                // Expired since the sheet opened, which the engine reports as unsupported.
                var (rejected, error) = UnsupportedAnswer(derived, decrypted.Request);
                return await ClaimAndAnswerAsync(request.SessionId, request.MessageId, request.DappRequestId, request.TraceId, rejected, error);
            }

            if (!accept)
            {
                return await ClaimAndAnswerAsync(request.SessionId, request.MessageId, request.DappRequestId, request.TraceId, true,
                    derived.EncryptError(request.DappRequestId, TonConnectRpcErrorCode.UserDeclined, "Declined"));
            }

            // Both leave the request unclaimed and the sheet up: they pass on their own.
            if (_rotation != null)
            {
                throw new WalletRotationPendingException();
            }

            if (HasUnsettledTransfer())
            {
                throw new WalletTransferInProgressException();
            }

            // Submitted by the engine rather than the account: sendTonWalletTransfer describes one
            // transfer to one peer, and a dApp intent can carry several messages. Prepared - signed -
            // before the claim, so a refusal here can still be answered as one.
            PreparedTransfer prepared;

            try
            {
                prepared = await client.PrepareTransfer(new PrepareTransferRequest(NewRecordId(), send.Request.Intent));
            }
            catch (Exception ex) when (ex is not WalletAccessDeniedException)
            {
                Logger.Error("ton connect transfer could not be prepared: " + ex.Message);
                return await ClaimAndAnswerAsync(request.SessionId, request.MessageId, request.DappRequestId, request.TraceId, true,
                    derived.EncryptError(request.DappRequestId, TonConnectRpcErrorCode.Unknown, "The transfer could not be sent"));
            }

            // At the tap rather than when the sheet opened: it is atomic across devices, and
            // whichever gets here first is the one that answers. The server then edits the service
            // message so the others show the outcome. Losing it discards the signed message unsent.
            if (!await ClaimAsync(request.SessionId, request.MessageId, request.DappRequestId, false))
            {
                return false;
            }

            byte[] body;

            try
            {
                await client.SendBoc(new SendBocRequest(
                    prepared.OperationId, false, prepared.ExternalBoc, prepared.Seqno, prepared.ValidUntil));

                // A TON Connect answer is the signed BOC, not a receipt for it.
                body = derived.EncryptSendSuccess(request.DappRequestId, prepared.ExternalBoc);
            }
            catch (WalletClientException.SubmissionUnknown ex)
            {
                // It may land, and telling the dApp it failed would invite a second spend.
                Logger.Error("ton connect transfer submission unknown: " + ex.diagnostic);
                body = derived.EncryptSendSuccess(request.DappRequestId, prepared.ExternalBoc);
            }
            catch (WalletClientException ex)
            {
                // Nothing left, so the dApp is told so rather than handed a message that was
                // never broadcast.
                Logger.Error("ton connect transfer refused: " + ex.Message);
                body = derived.EncryptError(request.DappRequestId, TonConnectRpcErrorCode.Unknown, "The transfer could not be sent");
            }

            // The transfer has already left by now, so a failure here is the dApp not being told
            // rather than nothing having happened.
            await SendAnswerAsync(request.SessionId, request.MessageId, request.TraceId, body);
            return true;
        }

        private async Task<bool> ClaimAsync(long sessionId, long messageId, string dappRequestId, bool rejected)
        {
            var claim = await _clientService.SendAsync(new ClaimTonConnectRequest(sessionId, messageId, dappRequestId, rejected));
            if (claim is Error error)
            {
                // TONCONNECT_REQUEST_ALREADY_CLAIMED, or it expired while the sheet was open.
                Logger.Error(string.Format("ton connect request could not be claimed: {0} {1}", error.Code, error.Message));
                return false;
            }

            return true;
        }

        private async Task SendAnswerAsync(long sessionId, long messageId, string traceId, byte[] body)
        {
            var answer = await _clientService.SendAsync(new AnswerTonConnectRequest(sessionId, messageId, traceId, body));
            if (answer is Error failed)
            {
                Logger.Error(string.Format("ton connect request could not be answered: {0} {1}", failed.Code, failed.Message));
            }
        }

        private async Task<bool> ClaimAndAnswerAsync(long sessionId, long messageId, string dappRequestId, string traceId, bool rejected, byte[] body)
        {
            if (!await ClaimAsync(sessionId, messageId, dappRequestId, rejected))
            {
                return false;
            }

            await SendAnswerAsync(sessionId, messageId, traceId, body);
            return true;
        }

        /// <summary>
        /// The answer to a request no sheet is shown for: a dApp disconnecting is acknowledged,
        /// anything else is refused with the reason the protocol has for it.
        /// </summary>
        private static (bool Rejected, byte[] Body) UnsupportedAnswer(TonConnectDerivedSession derived, TonConnectIncomingRequest request)
        {
            return request switch
            {
                TonConnectIncomingRequest.Disconnect disconnect => (false, derived.EncryptDisconnectSuccess(disconnect.Id)),
                TonConnectIncomingRequest.Unsupported unsupported => (true, derived.EncryptError(unsupported.Id, unsupported.ErrorCode, unsupported.ErrorMessage)),
                _ => (true, derived.EncryptError(RequestId(request), TonConnectRpcErrorCode.MethodNotSupported, "Method not supported"))
            };
        }

        private static string RequestId(TonConnectIncomingRequest request)
        {
            return request switch
            {
                TonConnectIncomingRequest.SendTransaction x => x.Id,
                TonConnectIncomingRequest.SignMessage x => x.Id,
                TonConnectIncomingRequest.Disconnect x => x.Id,
                TonConnectIncomingRequest.Unsupported x => x.Id,
                TonConnectIncomingRequest.SignData x => x.Id,
                _ => string.Empty
            };
        }

        public async Task<WalletConnectResult> ConnectAsync(TdTonConnectSession session, TonConnectConnectRequest request, string domain, string traceId, WalletVault.WalletVaultLease lease)
        {
            var lifecycle = _lifecycle;
            var descriptor = _descriptor;

            if (lifecycle == null || descriptor == null || session == null)
            {
                return new WalletConnectResult(null);
            }

            await EnsureLeaseAsync(lease);

            if (_rotation != null)
            {
                // The engine says so outright on SignTonConnectProof: a proof signed with a key the
                // contract is about to stop accepting is one the dApp would be told to trust and
                // the chain would then reject.
                throw new WalletRotationPendingException();
            }

            if (Payload(request) != null && !await IsProofDomainAllowedAsync(domain))
            {
                // Before anything is registered: binding the session key is one-way.
                Logger.Error("ton connect proof refused for domain " + domain);
                return new WalletConnectResult(null);
            }

            // Derived, not stored: every device of this account arrives at the same key from the
            // wallet key, the dApp's id and the server's nonce, so any of them can answer for the
            // session without a key ever being sent anywhere. Reading the wallet key is what makes
            // this ask for user presence.
            using var derived = await DeriveAsync(lifecycle, descriptor, session);
            if (derived == null)
            {
                return new WalletConnectResult(null);
            }

            // Registering W is what earns the challenge, and it is one-way: the server binds the
            // key to the session and it can never be replaced.
            var response = await _clientService.SendAsync(new SetTonConnectSessionWalletClientId(session.Id, derived.PublicKeyHex()));
            if (response is not TonConnectChallenge challenge)
            {
                return new WalletConnectResult(response as Error);
            }

            var account = lifecycle.TonConnectAccount(descriptor);
            TonConnectProofReply proof = null;

            if (Payload(request) is string payload)
            {
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                var signature = await SignProofAsync(lifecycle, descriptor, domain, timestamp, payload);
                if (signature == null)
                {
                    // Asked for and not produced. Connecting anyway would look like success and
                    // leave the dApp unable to sign the user in.
                    return new WalletConnectResult(null);
                }

                // The proof carries the key that signed it, which after a rotation is not the
                // anchor key the address and StateInit are built from - and the reply beside it
                // has to name the same one.
                account = account with { PublicKey = signature.PublicKey };

                // The same timestamp, domain and payload that were signed: the dApp rebuilds the
                // digest from these, and anything that drifts makes the proof fail.
                proof = new TonConnectProofReply((ulong)timestamp, domain, payload, signature.Signature);
            }

            if (RequestedNetwork(request) is string network)
            {
                account = account with { Network = network };
            }

            var body = derived.EncryptConnectEvent((ulong)challenge.EventId, account, proof, Device());

            var result = await _clientService.SendAsync(new SendTonConnectSessionConnectResult(
                session.Id, derived.OpenChallenge(challenge.Challenge), false, body, traceId ?? string.Empty));

            return result is Ok
                ? WalletConnectResult.Connected
                : new WalletConnectResult(result as Error);
        }

        /// <summary>
        /// Whether a dApp may have a <c>ton_proof</c> signed for this domain.
        /// </summary>
        /// <remarks>
        /// A proof for the ownership challenge's domain is byte for byte the proof the account
        /// takes for its wallet operations; only the domain tells the two apart, so a dApp that
        /// names it - or telegram.org - is refused. Fails closed when the domain cannot be learned.
        /// </remarks>
        private async Task<bool> IsProofDomainAllowedAsync(string domain)
        {
            var normalized = NormalizeDomain(domain);
            if (normalized.Length == 0 || normalized == "telegram.org")
            {
                return false;
            }

            if (_ownershipDomain == null)
            {
                var response = await _clientService.SendAsync(new GetTonWalletOwnershipProofChallenge());
                if (response is not TonWalletOwnershipProofChallenge challenge)
                {
                    return false;
                }

                _ownershipDomain = NormalizeDomain(challenge.Domain);
            }

            return normalized != _ownershipDomain;
        }

        private static string NormalizeDomain(string domain)
        {
            return (domain ?? string.Empty).TrimEnd('.').ToLowerInvariant();
        }

        public async Task<bool> DeclineConnectAsync(TdTonConnectSession session, WalletConnectRefusal refusal, string traceId)
        {
            var lifecycle = _lifecycle;
            var descriptor = _descriptor;
            var vault = Vault;

            if (lifecycle == null || descriptor == null || vault == null || session == null)
            {
                return false;
            }

            // No window, so the vault opens only where it would not have asked anyway.
            using var lease = vault.CreateLease(null);

            try
            {
                await lease.EnsureAsync();
            }
            catch (WalletVaultException)
            {
                return false;
            }

            try
            {
                using var derived = await DeriveAsync(lifecycle, descriptor, session);
                if (derived == null)
                {
                    return false;
                }

                var response = await _clientService.SendAsync(new SetTonConnectSessionWalletClientId(session.Id, derived.PublicKeyHex()));
                if (response is not TonConnectChallenge challenge)
                {
                    return false;
                }

                var code = refusal switch
                {
                    WalletConnectRefusal.ManifestNotFound => TonConnectConnectErrorCode.ManifestNotFound,
                    WalletConnectRefusal.ManifestContent => TonConnectConnectErrorCode.ManifestContent,
                    _ => TonConnectConnectErrorCode.UserDeclined
                };

                var body = derived.EncryptConnectError((ulong)challenge.EventId, code, code == TonConnectConnectErrorCode.UserDeclined ? "Declined" : "Manifest unusable");

                var result = await _clientService.SendAsync(new SendTonConnectSessionConnectResult(
                    session.Id, derived.OpenChallenge(challenge.Challenge), true, body, traceId ?? string.Empty));

                return result is Ok;
            }
            catch (Exception ex)
            {
                // Nothing awaits a refusal, so nothing may escape it.
                Logger.Error("ton connect refusal could not be sent: " + ex.Message);
                return false;
            }
        }

        public async Task<IReadOnlyList<TdTonConnectSession>> GetConnectedAppsAsync()
        {
            var response = await _clientService.SendAsync(new GetTonConnectSessions());
            if (response is not TonConnectSessions sessions)
            {
                return Array.Empty<TdTonConnectSession>();
            }

            var connected = new List<TdTonConnectSession>(sessions.Sessions.Count);

            foreach (var session in sessions.Sessions)
            {
                if (session.State is TonConnectSessionStateReady)
                {
                    connected.Add(session);
                }
            }

            return connected;
        }

        public async Task<bool> DisconnectAppAsync(TdTonConnectSession session, WalletVault.WalletVaultLease lease)
        {
            var lifecycle = _lifecycle;
            var descriptor = _descriptor;

            if (lifecycle == null || descriptor == null || session == null)
            {
                throw new WalletNotBoundException();
            }

            await EnsureLeaseAsync(lease);

            var next = await _clientService.SendAsync(new GetTonConnectSessionNextEventId(session.Id));
            if (next is not TonConnectSessionEventId eventId)
            {
                Logger.Error("ton connect disconnect event could not be numbered: " + (next as Error)?.Message);
                return false;
            }

            using var derived = await DeriveAsync(lifecycle, descriptor, session);
            if (derived == null)
            {
                // Registered with another key, so this device cannot speak for the session.
                return false;
            }

            var body = derived.EncryptDisconnectEvent((ulong)eventId.EventId);

            var response = await _clientService.SendAsync(new DisconnectTonConnectSession(session.Id, body));
            if (response is Error error)
            {
                Logger.Error(string.Format("ton connect session could not be disconnected: {0} {1}", error.Code, error.Message));
                return false;
            }

            return true;
        }

        private async Task<TonConnectDerivedSession> DeriveAsync(WalletLifecycle lifecycle, WalletDescriptor descriptor, TdTonConnectSession session)
        {
            try
            {
                return await lifecycle.DeriveTonConnectSession(new TonConnectDerivedSessionRequest(descriptor, session.DappClientId, session.Nonce));
            }
            catch (WalletLifecycleException.ProtectedSecretHost failed) when (failed.kind == ProtectedSecretHostErrorKind.Cancelled)
            {
                // The device prompt was dismissed. An answer, not a fault.
                throw new WalletAccessDeniedException();
            }
            catch (Exception ex)
            {
                // A wallet key that does not match the one the session was registered with is the
                // ordinary case here: this device simply does not take part.
                Logger.Error("ton connect session could not be derived: " + ex.Message);
                return null;
            }
        }

        private static async Task<TonConnectProofSignature> SignProofAsync(WalletLifecycle lifecycle, WalletDescriptor descriptor, string domain, long timestamp, string payload)
        {
            try
            {
                return await lifecycle.SignTonConnectProof(new TonConnectProofSignRequest(descriptor, domain ?? string.Empty, (ulong)timestamp, payload));
            }
            catch (WalletLifecycleException.ProtectedSecretHost failed) when (failed.kind == ProtectedSecretHostErrorKind.Cancelled)
            {
                throw new WalletAccessDeniedException();
            }
            catch (Exception ex)
            {
                Logger.Error("ton connect proof could not be signed: " + ex.Message);
                return null;
            }
        }

        /// <summary>The dApp's <c>ton_proof</c> challenge, or null if it did not ask for one.</summary>
        private static string Payload(TonConnectConnectRequest request)
        {
            foreach (var item in request.Items)
            {
                if (item is TonConnectConnectItemProof proof)
                {
                    return proof.Payload ?? string.Empty;
                }
            }

            return null;
        }

        /// <summary>The network the dApp asked to be told about, where it named one.</summary>
        private static string RequestedNetwork(TonConnectConnectRequest request)
        {
            foreach (var item in request.Items)
            {
                if (item is TonConnectConnectItemAddress address && address.Network.Length > 0)
                {
                    return address.Network;
                }
            }

            return null;
        }

        private static TonConnectDevice Device()
        {
            var version = Package.Current.Id.Version;

            return new TonConnectDevice(TonConnectDevicePlatform.Windows, "Unigram",
                string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build));
        }

        public async Task<string> ResolveDnsAsync(string name)
        {
            var client = _client;
            return client != null ? await client.ResolveDns(name) : null;
        }

        public async Task<string> DecryptCommentAsync(TonWalletTransaction transaction, string encryptedBody, WalletVault.WalletVaultLease lease)
        {
            var client = _client;
            if (client == null || string.IsNullOrEmpty(encryptedBody) || transaction?.Type is not TonWalletTransactionTypeTransfer transfer || !transfer.IsCommentEncrypted)
            {
                return null;
            }

            if (_descriptor == null)
            {
                throw new WalletNotBoundException();
            }

            await EnsureLeaseAsync(lease);

            // TDLib reports the encrypted payload, the engine takes the message body it belongs to.
            var body = WalletCommentBody.FromPayload(encryptedBody);
            if (body == null)
            {
                return null;
            }

            // Whoever encrypted the comment salted the authentication tag with their own address, so
            // on a transfer we sent that is ours, not the peer's. Both sides derive the same secret
            // either way - the payload carries the two public keys xored together - but the tag is
            // checked against the sender alone.
            var sender = transfer.Amount < 0
                ? _wallet?.Address
                : transaction.PeerAddress;

            if (string.IsNullOrEmpty(sender))
            {
                return null;
            }

            try
            {
                return await client.DecryptComment(new DecryptCommentRequest(sender, body));
            }
            catch (PanicException ex)
            {
                // A body the engine cannot lift fails inside the argument conversion, which it
                // reports by panicking rather than by returning an error. Caught here so one bad
                // string cannot take a window down.
                Logger.Error("wallet comment body is not a BOC: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Drops this device's key when it has stopped being the wallet's, secret and all.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="ForgetAsync"/> a failure here is only logged. The user did not ask for
        /// this and has nothing to retry: the key is already dead, and leaving it in place would
        /// leave the wallet claiming it can sign. The caller detaches before and attaches after,
        /// the client being built around the key it was given.
        /// </remarks>
        /// <summary>
        /// Asks the chain which key the wallet signs with, and drops this device's if it is not the
        /// one.
        /// </summary>
        /// <remarks>
        /// A backstop now rather than the mechanism. The server follows <c>change_wallet_key</c> on
        /// chain and rewrites <c>tonWalletState.public_key</c> when it confirms, so a rotation done
        /// anywhere reaches this device as an ordinary state update and is caught where staleness
        /// is checked. What is left here covers the window before the server has noticed.
        ///
        /// Once per session, and never blocking: a stale key costs nothing until something signs,
        /// and the balance and history are readable without one.
        /// </remarks>
        private async Task VerifySigningKeyAsync()
        {
            var wallet = _wallet;

            // A rotation of our own is the one case where the chain disagreeing with the bound
            // key is expected rather than a wallet taken over elsewhere. Settling it belongs to the
            // update that reports the new key, so there is nothing to do here but stay out of the
            // way until it arrives or expires.
            if (_rotation != null)
            {
                return;
            }

            var descriptor = _descriptor;
            if (descriptor == null || wallet == null || wallet.Address.Length == 0 || _boundKey is not { Length: > 0 } bound)
            {
                return;
            }

            // ReplaceTonWallet answers before the update that moves the account to the new address,
            // and until then the chain would be asked for the key of the wallet that was replaced.
            if (!IsSameWallet(descriptor, wallet))
            {
                return;
            }

            var reported = await ChainPublicKeyAsync(wallet.Address);
            if (reported == null || bound.SequenceEqual(reported))
            {
                return;
            }

            Logger.Warning(string.Format("wallet key rotated: chain {0}, bound {1}",
                Convert.ToBase64String(reported),
                Convert.ToBase64String(bound)));

            await _mutex.WaitAsync();
            try
            {
                // The wallet may have been rebound, replaced, or forgotten while the request was
                // out. Only the key this answer is about is dropped.
                if (_descriptor != descriptor || _boundKey == null || !_boundKey.SequenceEqual(bound))
                {
                    return;
                }

                await DetachAsync();
                await ArchiveDescriptorAsync();

                Attach();
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        /// <summary>
        /// The key the wallet contract itself holds, or null if the chain could not be asked.
        /// </summary>
        /// <summary>
        /// Asks the chain what is left on each archived wallet, and when it last moved.
        /// </summary>
        /// <remarks>
        /// Two requests per wallet, and there is normally none: this runs when the archive gains an
        /// entry and once per restore. The records are mutated in place rather than rebuilt - they
        /// are written only here, and reading a torn pair would cost a wrong number for one refresh
        /// rather than anything durable.
        /// </remarks>
        private async Task RefreshArchiveAsync()
        {
            List<WalletArchiveRecord> records;

            await _mutex.WaitAsync();
            try
            {
                records = new List<WalletArchiveRecord>(_archive);
            }
            finally
            {
                _mutex.Release();
            }

            var changed = false;

            foreach (var record in records)
            {
                if (await ChainBalanceAsync(record.Address) is BigInteger balance && balance != record.Balance)
                {
                    record.Balance = balance;
                    changed = true;
                }

                var date = await ChainLastActivityAsync(record.Address);
                if (date > 0 && date != record.LastUsedDate)
                {
                    record.LastUsedDate = date;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            await _mutex.WaitAsync();
            try
            {
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        private async Task<BigInteger?> ChainBalanceAsync(string address)
        {
            try
            {
                var query = string.Format("address={0}&include_boc=false", Uri.EscapeDataString(address));
                var response = await _clientService.SendAsync(new SendTonCenterApiRequest("/api/v3/accountStates", new TonCenterApiRequestTypeGet(query)));

                if (response is Text text)
                {
                    using var document = JsonDocument.Parse(text.TextValue);

                    if (document.RootElement.TryGetProperty("accounts", out var accounts)
                        && accounts.GetArrayLength() > 0
                        && accounts[0].TryGetProperty("balance", out var balance)
                        && BigInteger.TryParse(balance.GetString(), out var value))
                    {
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("wallet balance could not be read: " + ex.Message);
            }

            return null;
        }

        private async Task<int> ChainLastActivityAsync(string address)
        {
            try
            {
                var query = string.Format("account={0}&limit=1&sort=desc", Uri.EscapeDataString(address));
                var response = await _clientService.SendAsync(new SendTonCenterApiRequest("/api/v3/transactions", new TonCenterApiRequestTypeGet(query)));

                if (response is Text text)
                {
                    using var document = JsonDocument.Parse(text.TextValue);

                    if (document.RootElement.TryGetProperty("transactions", out var transactions)
                        && transactions.GetArrayLength() > 0
                        && transactions[0].TryGetProperty("now", out var now)
                        && now.TryGetInt32(out var date))
                    {
                        return date;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("wallet activity could not be read: " + ex.Message);
            }

            return 0;
        }

        private async Task<byte[]> ChainPublicKeyAsync(string address)
        {
            try
            {
                var payload = string.Format("{{\"address\":\"{0}\",\"method\":\"get_public_key\",\"stack\":[]}}", address);
                var response = await _clientService.SendAsync(new SendTonCenterApiRequest("/api/v3/runGetMethod", new TonCenterApiRequestTypePost(payload)));

                if (response is Text text && TryReadPublicKey(text.TextValue, out var key))
                {
                    return key;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("wallet public key could not be read: " + ex.Message);
            }

            return null;
        }

        /// <summary>
        /// The 32-byte key out of a <c>get_public_key</c> answer, which reports it as an integer -
        /// so a key with leading zero bytes comes back short and is padded back out here.
        /// </summary>
        private static bool TryReadPublicKey(string json, out byte[] key)
        {
            key = null;

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("exit_code", out var exit) && exit.TryGetInt32(out var code) && code != 0)
                {
                    return false;
                }

                if (!root.TryGetProperty("stack", out var stack) || stack.ValueKind != JsonValueKind.Array || stack.GetArrayLength() == 0)
                {
                    return false;
                }

                if (!stack[0].TryGetProperty("value", out var value) || value.GetString() is not string text)
                {
                    return false;
                }

                var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? text.Substring(2)
                    : text;

                if (digits.Length == 0 || digits.Length > 64)
                {
                    return false;
                }

                digits = digits.PadLeft(64, '0');
                key = new byte[32];

                for (int i = 0; i < key.Length; i++)
                {
                    key[i] = Convert.ToByte(digits.Substring(i * 2, 2), 16);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("wallet public key could not be parsed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Files the wallet this device holds the key for, and stops being it.
        /// </summary>
        /// <remarks>
        /// **The secret is deliberately not deleted.** The account points at one wallet at a time,
        /// and when it is pointed somewhere else the old one keeps whatever is on it - reachable
        /// only with the phrase stored here. Deleting it, which is what this used to do, loses
        /// those funds permanently and with no way back.
        ///
        /// Called under the lock, and only for a change this device did not make. The explicit
        /// delete is <see cref="ForgetAsync"/>, and that one really does delete.
        /// </remarks>
        private Task ArchiveDescriptorAsync()
        {
            var descriptor = _descriptor;
            if (descriptor == null)
            {
                return Task.CompletedTask;
            }

            if (!_archive.Exists(x => string.Equals(x.RecordId, descriptor.RecordId, StringComparison.Ordinal)))
            {
                _archive.Add(new WalletArchiveRecord
                {
                    RecordId = descriptor.RecordId,
                    Address = descriptor.Address,
                    PublicKey = descriptor.PublicKey,
                    Network = descriptor.Network,
                    SecretRef = descriptor.SecretRef.Value,
                    BoundKey = _boundKey,
                    ArchivedDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

                SaveArchive();

                Logger.Info(string.Format("wallet archived: {0}", descriptor.Address));

                // What is on it is the account's business no longer, so the chain is the only thing
                // that can say. Not awaited: the entry exists either way and prices itself later.
                _ = RefreshArchiveAsync();
            }

            DeleteDescriptor();

            _descriptor = null;
            _boundKey = null;

            return Task.CompletedTask;
        }

        public async Task DeleteWalletAsync(string password)
        {
            var response = await _clientService.SendAsync(new DeleteTonWallet(password ?? string.Empty));
            if (response is Error error)
            {
                throw new WalletRequestException(error);
            }

            // Only once the account has agreed. The new wallet arrives by update, and this device
            // holds no key for it - which is the same position as a fresh install, and the same
            // one the account is now in.
            await ForgetAsync();
        }

        public Task ForgetAsync()
        {
            return ForgetAsync(false);
        }

        private async Task ForgetAsync(bool vaultLost)
        {
            await _mutex.WaitAsync();
            try
            {
                EnsureStores();

                if (!_archiveLoaded)
                {
                    _archiveLoaded = true;
                    LoadArchive();
                }

                _rotation = null;
                DeleteRotation();

                var descriptor = _descriptor;
                if (descriptor != null)
                {
                    await DetachAsync();
                    await _lifecycle.DeleteWallet(descriptor);

                    // Only after the secret is gone, so a failure above leaves a wallet that can
                    // still be opened rather than an orphaned secret with nothing pointing at it.
                    DeleteDescriptor();

                    _descriptor = null;
                    _boundKey = null;
                }

                // Whether or not there was a descriptor: a bind cancelled after the vault was
                // enrolled leaves one behind, and it would go on asking for Hello on a device that
                // holds nothing. Kept while the archive has entries, whose phrases are encrypted
                // under it and are the only way to the funds left on those wallets - unless it can
                // no longer be opened, in which case nothing it encrypts is coming back.
                if (vaultLost || _archive.Count == 0)
                {
                    Vault?.Delete();
                }

                if (descriptor == null)
                {
                    return;
                }

                // Back to watching it. The wallet is still the account's, and everything but signing
                // still works.
                Attach();
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        public void StartWatching()
        {
            _stream ??= new WalletChainStream(_clientService, () => State.Address, OnChainChanged, OnChainEvent);

            // Nothing was watching, so nothing saw what happened in the meantime. Not left to the
            // stream's own first event, which never comes if the socket cannot connect.
            if (_stream.Start())
            {
                OnChainChanged();
            }
        }

        // Read from the UI thread and written under _mutex from wherever the service resumes, so
        // it has a lock of its own rather than borrowing that one.
        private readonly Dictionary<string, string> _predecessors = new(StringComparer.Ordinal);

        public string PredecessorOf(string transactionId)
        {
            lock (_predecessors)
            {
                return _predecessors.TryGetValue(transactionId, out var id) ? id : null;
            }
        }

        // Kept for the session: the list keys a transaction by its predecessor for as long as it
        // holds it, and a pending row is rare enough that this never grows to matter.
        private void Succeed(string pendingId, TonWalletTransaction transaction)
        {
            lock (_predecessors)
            {
                _predecessors[transaction.Id] = pendingId;
            }
        }

        public void StopWatching()
        {
            _stream?.Stop();
            Interlocked.Exchange(ref _chainEventCancellation, null)?.Cancel();
        }

        /// <summary>
        /// Something happened on the chain. What it was comes from the account, which is the only
        /// thing that knows who was on the other side.
        /// </summary>
        /// <remarks>
        /// A catch-up rather than a refresh of its own: it is called when the window opens and when
        /// the stream connects, which is when nothing is known to have happened, so a refresh
        /// already running or just done answers it. Events go through OnChainEvent.
        /// </remarks>
        private void OnChainChanged()
        {
            _ = _activityPager.CatchUpAsync();

            if (_collectiblesResource.Phase != WalletResourcePhase.Idle || _collectibles.Count > 0)
            {
                _ = _collectiblesPager.CatchUpAsync();
            }
        }

        /// <remarks>
        /// A newer event restarts the follow-ups rather than adding its own beside them.
        /// </remarks>
        private void OnChainEvent()
        {
            var source = new CancellationTokenSource();
            Interlocked.Exchange(ref _chainEventCancellation, source)?.Cancel();

            _ = FollowChainEventAsync(source.Token);
        }

        private async Task FollowChainEventAsync(CancellationToken cancellationToken)
        {
            try
            {
                var growth = Volatile.Read(ref _activityGrowth);

                await Task.WhenAll(LoadActivityAsync(true), RefreshCollectiblesIfLoadedAsync());

                foreach (var delay in ChainEventFollowUps)
                {
                    if (Volatile.Read(ref _activityGrowth) != growth)
                    {
                        return;
                    }

                    await Task.Delay(delay, cancellationToken);
                    await Task.WhenAll(LoadActivityAsync(true), RefreshCollectiblesIfLoadedAsync());
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error("wallet chain event could not be followed: " + ex.Message);
            }
        }

        public async Task RefreshAsync()
        {
            _clientService.Send(new LoadTonWalletState());

            await Task.WhenAll(LoadActivityAsync(true), RefreshCollectiblesIfLoadedAsync());
        }

        public Task LoadMoreActivityAsync()
        {
            return LoadActivityAsync(false);
        }

        public Task LoadMoreCollectiblesAsync()
        {
            return LoadCollectiblesAsync(false);
        }

        // Not before their first page: this runs on every chain event.
        private Task RefreshCollectiblesIfLoadedAsync()
        {
            return _collectiblesResource.Phase == WalletResourcePhase.Idle && _collectibles.Count == 0
                ? Task.CompletedTask
                : LoadCollectiblesAsync(true);
        }

        /// <summary>
        /// The list the view sees: what this device is still waiting on, above what the account
        /// reports.
        /// </summary>
        /// <remarks>
        /// A new list rather than an edit, so that a state already handed out keeps describing the
        /// history it was made from.
        /// </remarks>
        private void RebuildActivity()
        {
            if (_pending.Count == 0)
            {
                _activity = _confirmed;
                return;
            }

            var items = new List<TonWalletTransaction>(_pending.Count + _confirmed.Count);

            items.AddRange(_pending);
            items.AddRange(_confirmed);

            _activity = items;
        }

        /// <summary>
        /// Puts a transfer this device just sent at the top of the history, where it stays until
        /// the chain settles it.
        /// </summary>
        /// <remarks>
        /// The engine's operation id is its identity for as long as it has none: a transaction id
        /// only exists once there is a transaction, and the message hash only once the account has
        /// answered - which is why the row starts without one, and the resolver skips it until then.
        /// </remarks>
        private void AddPending(string recipient, long peerUserId, string peerDomain, BigInteger amountNanograms, string comment, bool isCommentEncrypted, bool isGasless, string operationId, ulong validUntil)
        {
            var pending = new TonWalletTransaction(
                operationId,
                recipient,
                peerUserId,
                peerDomain ?? string.Empty,
                (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                0,
                new TonWalletTransactionStatePending(operationId, string.Empty, (int)validUntil),
                new TonWalletTransactionTypeTransfer(
                    -(long)amountNanograms,
                    isGasless,
                    comment ?? string.Empty,
                    isCommentEncrypted));

            var items = new List<TonWalletTransaction>(_pending.Count + 1) { pending };
            items.AddRange(_pending);

            _pending = items;

            RebuildActivity();
            EnsureResolver();
        }

        /// <summary>
        /// Puts a transfer the account has already settled at the top of the history.
        /// </summary>
        /// <remarks>
        /// Called under the lock. The row is the account's own, so nothing is invented here and
        /// nothing has to be reconciled later. The guard is for the history having been refreshed
        /// between the send and this, and already holding it.
        /// </remarks>
        private void AddConfirmed(TonWalletTransaction transaction)
        {
            if (_confirmed.Exists(x => x.Id == transaction.Id))
            {
                return;
            }

            var items = new List<TonWalletTransaction>(_confirmed.Count + 1) { transaction };
            items.AddRange(_confirmed);

            _confirmed = items;

            RebuildActivity();
        }

        /// <summary>
        /// Starts the loop that settles pending transfers, if it is not already running.
        /// </summary>
        /// <remarks>
        /// Called under the lock, with something pending. The loop ends on its own once nothing is.
        /// </remarks>
        private void StopResolver()
        {
            _resolverCancellation?.Cancel();
            _resolverCancellation = null;
            _resolver = null;
        }

        private void EnsureResolver()
        {
            if (_resolver != null)
            {
                return;
            }

            var source = new CancellationTokenSource();

            _resolverCancellation = source;
            _resolver = ResolvePendingAsync(source);
        }

        /// <summary>
        /// Asks the account about each transfer that has left, until it has landed or can no longer.
        /// </summary>
        /// <remarks>
        /// Polled rather than pushed: <c>getTonWalletTransactionByMsgHash</c> answers with an error
        /// until the transaction is final, and nothing updates to say that it has become one.
        ///
        /// The waiting grows because the answer is worth having quickly and worth little later: a
        /// transfer settles in seconds, and one that has not by then is one the user has stopped
        /// watching.
        /// </remarks>
        private async Task ResolvePendingAsync(CancellationTokenSource cancellationSource)
        {
            var cancellationToken = cancellationSource.Token;
            var delay = ResolveFirstDelay;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    List<TonWalletTransaction> waiting;

                    await _mutex.WaitAsync();
                    try
                    {
                        waiting = _pending.FindAll(x => x.State is TonWalletTransactionStatePending);
                    }
                    finally
                    {
                        _mutex.Release();
                    }

                    if (waiting.Count == 0)
                    {
                        // Nothing left to settle. A later transfer starts the loop again.
                        return;
                    }

                    // The account is asked first: it answers with the transaction itself as soon as
                    // the transfer is final, which is both the row we want and cheaper than reading
                    // the chain. An error from it means "not yet" and never means "never", so
                    // whatever it does not settle still goes to the engine below.
                    if (await ResolveByMessageHashAsync(waiting))
                    {
                        Raise();

                        await _mutex.WaitAsync();
                        try
                        {
                            waiting = _pending.FindAll(x => x.State is TonWalletTransactionStatePending);
                        }
                        finally
                        {
                            _mutex.Release();
                        }

                        if (waiting.Count == 0)
                        {
                            return;
                        }
                    }

                    SendSnapshot snapshot = null;

                    try
                    {
                        // One call for the wallet, not one per row: the engine settles the send it
                        // holds, and a wallet has one in flight at a time.
                        snapshot = await ResolveSendAsync();

                        if (await ApplyResolutionAsync(snapshot, waiting))
                        {
                            Raise();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Nothing observes this task, so an escaping exception would leave the
                        // transfer pending for the rest of the session with nothing said.
                        Logger.Error("wallet transfer could not be resolved: " + ex.Message);
                    }

                    delay = NextDelay(delay, snapshot?.Resolution?.RetryAfterHintMs);
                }
            }
            finally
            {
                // However it ended, the next transfer - or the next time the wallet is opened - gets
                // to start a new one.
                await _mutex.WaitAsync();
                try
                {
                    if (_resolverCancellation == cancellationSource)
                    {
                        _resolver = null;
                        _resolverCancellation = null;
                    }
                }
                finally
                {
                    _mutex.Release();
                }
            }
        }

        /// <summary>
        /// How long to wait before asking again: what the engine suggested, or twice the last wait.
        /// </summary>
        /// <remarks>
        /// The hint is guidance for a view, not a deadline, so it is bounded by the same maximum as
        /// the doubling - a hint of an hour would otherwise leave the row saying nothing for one.
        /// </remarks>
        private static TimeSpan NextDelay(TimeSpan delay, ulong? hint)
        {
            if (hint > 0)
            {
                return TimeSpan.FromMilliseconds(Math.Min(hint.Value, (ulong)ResolveMaximumDelay.TotalMilliseconds));
            }

            return delay + delay < ResolveMaximumDelay
                ? delay + delay
                : ResolveMaximumDelay;
        }

        /// <summary>
        /// Asks the account whether any pending transfer has become a transaction.
        /// </summary>
        /// <remarks>
        /// One request per row rather than one for the wallet, because the message hash is what
        /// identifies a transfer here and each row has its own. The answer is the finished
        /// transaction, so a row that lands is replaced by it rather than dropped and re-read.
        /// </remarks>
        private async Task<bool> ResolveByMessageHashAsync(List<TonWalletTransaction> waiting)
        {
            List<(string Id, TonWalletTransaction Transaction)> landed = null;

            foreach (var pending in waiting)
            {
                if (pending.State is not TonWalletTransactionStatePending state || string.IsNullOrEmpty(state.MsgHash))
                {
                    continue;
                }

                var response = await _clientService.SendAsync(new GetTonWalletTransactionByMsgHash(state.MsgHash));
                if (response is TonWalletTransaction transaction)
                {
                    Logger.Info(string.Format("wallet transfer landed: {0} is {1}", state.MsgHash, transaction.Id));

                    landed ??= new List<(string, TonWalletTransaction)>();
                    landed.Add((pending.Id, transaction));
                }
            }

            if (landed == null)
            {
                return false;
            }

            await _mutex.WaitAsync();
            try
            {
                var items = new List<TonWalletTransaction>(_pending);

                foreach (var (id, _) in landed)
                {
                    var index = items.FindIndex(x => x.Id == id);
                    if (index >= 0)
                    {
                        items.RemoveAt(index);
                    }
                }

                // Before the rows are added, because a settled row takes the place the pending one
                // was holding and RebuildActivity reads both lists.
                _pending = items;

                foreach (var (id, transaction) in landed)
                {
                    Succeed(id, transaction);
                    AddConfirmed(transaction);
                }

                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            return true;
        }

        /// <summary>
        /// Asks the engine to settle the send it is holding, against the chain.
        /// </summary>
        /// <remarks>
        /// Idempotent, and it reads no protected secret: the message was signed when it was sent,
        /// and this only looks for evidence of what became of it. Nothing to ask while the wallet
        /// is watch-only or between clients.
        /// </remarks>
        private async Task<SendSnapshot> ResolveSendAsync()
        {
            var client = _client;
            return client != null ? await client.ResolvePending() : null;
        }

        /// <summary>
        /// Turns what the engine now knows into rows: a transfer that landed becomes the
        /// transaction it is, and one that never can becomes a failure.
        /// </summary>
        /// <remarks>
        /// A confirmed transfer is not built from the resolution: the account is what knows who was
        /// on the other side and what it cost, so the row is dropped and the history re-read from
        /// the top, where the transaction now is.
        ///
        /// The expiry is the second half of this, and the only half for a row the engine has no
        /// operation for - one that outlived a restart, or a wallet that was rebuilt underneath it.
        /// </remarks>
        private async Task<bool> ApplyResolutionAsync(SendSnapshot snapshot, List<TonWalletTransaction> waiting)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var landed = false;
            var changed = false;

            await _mutex.WaitAsync();
            try
            {
                var items = new List<TonWalletTransaction>(_pending);

                foreach (var pending in waiting)
                {
                    if (pending.State is not TonWalletTransactionStatePending state)
                    {
                        continue;
                    }

                    var index = items.FindIndex(x => x.Id == pending.Id);
                    if (index < 0)
                    {
                        // Gone while the request was out - another wallet, or a history that
                        // started over.
                        continue;
                    }

                    var resolved = snapshot != null
                        && string.Equals(snapshot.OperationId, state.OperationId, StringComparison.Ordinal)
                        && IsTerminal(snapshot.Phase);

                    if (resolved && snapshot.Phase == SendPhase.Confirmed)
                    {
                        Logger.Info(string.Format("wallet transfer confirmed: {0}", snapshot.Resolution?.TransactionHash));

                        items.RemoveAt(index);

                        landed = true;
                        changed = true;
                    }
                    else if (resolved || state.ExpirationDate <= now)
                    {
                        Logger.Error(string.Format("wallet transfer failed: {0} {1}", snapshot?.Phase, snapshot?.ErrorMessage));

                        // The row stays, saying so: it is this device's record of something the
                        // account never heard about.
                        items[index] = new TonWalletTransaction(
                            pending.Id,
                            pending.PeerAddress,
                            pending.PeerUserId,
                            pending.PeerDomain,
                            pending.Date,
                            pending.FeeAmount,
                            new TonWalletTransactionStateFailed(),
                            pending.Type);

                        changed = true;
                    }
                }

                if (changed)
                {
                    _pending = items;

                    RebuildActivity();
                    SetState(Project());
                }
            }
            finally
            {
                _mutex.Release();
            }

            if (landed)
            {
                // Outside the lock, and after the row is gone: the refresh reads down from the top
                // until it meets something already held, so the transaction takes the place the row
                // was occupying.
                await LoadActivityAsync(true);
            }

            return changed;
        }

        /// <summary>
        /// Whether the engine has stopped waiting for this send, one way or the other.
        /// </summary>
        private static bool IsTerminal(SendPhase phase)
        {
            return phase is SendPhase.Confirmed
                or SendPhase.Replaced
                or SendPhase.SequenceNumberConsumed
                or SendPhase.Expired
                or SendPhase.Superseded
                or SendPhase.Failed
                or SendPhase.Cancelled;
        }

        private Task LoadActivityAsync(bool reset)
        {
            return reset ? _activityPager.RefreshAsync() : _activityPager.LoadMoreAsync();
        }

        private async Task LoadActivityCoreAsync(bool reset)
        {
            string offset;
            string address;
            HashSet<string> known;

            await _mutex.WaitAsync();
            try
            {
                if (_wallet == null || _wallet.Address.Length == 0)
                {
                    return;
                }

                address = _wallet.Address;

                if (!reset && _activityEnd)
                {
                    // An empty next_offset was TDLib saying there is nothing older.
                    return;
                }

                offset = reset ? string.Empty : _activityOffset;

                // Where the refresh stops reading. The pager keeps a second load out, but a send
                // settling can still add to the confirmed half meanwhile, so the write-back checks
                // against the list as it is then rather than this.
                known = new HashSet<string>(_confirmed.Count, StringComparer.Ordinal);

                foreach (var item in _confirmed)
                {
                    known.Add(item.Id);
                }

                _activityResource = WalletResource.Loading;
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();

            var error = reset
                ? await RefreshActivityAsync(address, known)
                : await AppendActivityAsync(address, offset);

            await _mutex.WaitAsync();
            try
            {
                _activityResource = error == null
                    ? WalletResource.Ready
                    : new WalletResource(WalletResourcePhase.Failed, error.Message, true);

                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();

            if (error == null && known.Count == 0)
            {
                // The first page of history, so the collectibles' first page follows it.
                _ = LoadFirstCollectiblesAsync();
            }
        }

        private Task LoadFirstCollectiblesAsync()
        {
            return _collectiblesResource.Phase == WalletResourcePhase.Idle && _collectibles.Count == 0
                ? LoadCollectiblesAsync(false)
                : Task.CompletedTask;
        }

        /// <summary>
        /// Reads one page of older transactions onto the end of the history.
        /// </summary>
        private async Task<Error> AppendActivityAsync(string address, string offset)
        {
            var response = await _clientService.SendAsync(new GetTonWalletTransactions(null, offset, ActivityPageSize));
            if (response is not TonWalletTransactions transactions)
            {
                return response as Error;
            }

            await _mutex.WaitAsync();
            try
            {
                if (!IsCurrentWallet(address))
                {
                    return null;
                }

                var items = new List<TonWalletTransaction>(_confirmed);
                var held = HeldIds();

                // By id, because a page repeats the transaction its offset was taken at.
                foreach (var item in transactions.Transactions)
                {
                    if (held.Add(item.Id))
                    {
                        items.Add(item);
                    }
                }

                _confirmed = items;
                _activityOffset = transactions.NextOffset;
                _activityEnd = transactions.NextOffset.Length == 0;

                RebuildActivity();
            }
            finally
            {
                _mutex.Release();
            }

            return null;
        }

        /// <summary>
        /// Whether the wallet a load started for is still the account's, so its pages may be
        /// written back. Called under the lock.
        /// </summary>
        private bool IsCurrentWallet(string address)
        {
            return string.Equals(_wallet?.Address, address, StringComparison.Ordinal);
        }

        private HashSet<string> HeldIds()
        {
            var held = new HashSet<string>(_confirmed.Count, StringComparer.Ordinal);

            foreach (var item in _confirmed)
            {
                held.Add(item.Id);
            }

            return held;
        }

        /// <summary>
        /// Brings the history up to date from the top, keeping every page already read.
        /// </summary>
        /// <remarks>
        /// A history only grows at the top, so a refresh is what is new above what we hold rather
        /// than a new history: it reads down from the newest transaction until it meets one already
        /// held, and puts what it read in front. Nothing paged in is thrown away, and how far the
        /// list has read - the tail offset - does not move, so a list that had reached the end is
        /// still at the end.
        ///
        /// Only where the join cannot be reached, <see cref="RefreshPageLimit"/> pages down, does
        /// the history start over: keeping both halves would leave a hole in the middle with
        /// nothing to mark it.
        /// </remarks>
        private async Task<Error> RefreshActivityAsync(string address, HashSet<string> known)
        {
            var fetched = new List<TonWalletTransaction>();
            var offset = string.Empty;
            var tail = string.Empty;

            // Nothing held is nothing to meet: the first load reads one page and that page is the
            // history.
            var joined = known.Count == 0;

            for (int page = 0; page < RefreshPageLimit; page++)
            {
                var response = await _clientService.SendAsync(new GetTonWalletTransactions(null, offset, ActivityPageSize));
                if (response is not TonWalletTransactions transactions)
                {
                    return response as Error;
                }

                foreach (var item in transactions.Transactions)
                {
                    // The server's order is stable, so the first transaction already held is the
                    // join: everything under it is held as well.
                    if (known.Contains(item.Id))
                    {
                        joined = true;
                        break;
                    }

                    fetched.Add(item);
                }

                tail = transactions.NextOffset;
                offset = tail;

                if (joined || tail.Length == 0)
                {
                    break;
                }
            }

            await _mutex.WaitAsync();
            try
            {
                if (!IsCurrentWallet(address))
                {
                    // Replaced while the pages were out: they belong to the wallet before.
                    return null;
                }

                if (fetched.Count > 0)
                {
                    Interlocked.Increment(ref _activityGrowth);
                }

                if (joined)
                {
                    var held = HeldIds();
                    var items = new List<TonWalletTransaction>(fetched.Count + _confirmed.Count);

                    // A send that settled while the pages were out is already held, at the top.
                    foreach (var item in fetched)
                    {
                        if (!held.Contains(item.Id))
                        {
                            items.Add(item);
                        }
                    }

                    items.AddRange(_confirmed);

                    _confirmed = items;

                    // The page just read is also the end of what has been read only on a first
                    // load. Otherwise the end is where it already was.
                    if (known.Count == 0)
                    {
                        _activityOffset = tail;
                        _activityEnd = tail.Length == 0;
                    }
                }
                else
                {
                    _confirmed = fetched;
                    _activityOffset = tail;
                    _activityEnd = tail.Length == 0;
                    _activityGeneration++;
                }

                RebuildActivity();
            }
            finally
            {
                _mutex.Release();
            }

            return null;
        }

        /// <remarks>
        /// Unlike the history, NFTs do not only arrive at the top - one sent away leaves from
        /// wherever it was - so a refresh compares against the first page instead of stitching.
        /// </remarks>
        private Task LoadCollectiblesAsync(bool reset)
        {
            return reset ? _collectiblesPager.RefreshAsync() : _collectiblesPager.LoadMoreAsync();
        }

        private async Task LoadCollectiblesCoreAsync(bool reset)
        {
            string offset;
            int generation;

            await _mutex.WaitAsync();
            try
            {
                if (_wallet == null || _wallet.Address.Length == 0)
                {
                    return;
                }

                if (!reset && _collectiblesEnd)
                {
                    return;
                }

                offset = reset ? string.Empty : _collectiblesOffset;
                generation = _collectiblesGeneration;

                _collectiblesResource = WalletResource.Loading;
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();

            var response = await _clientService.SendAsync(new GetTonWalletNfts(offset, CollectiblesPageSize));

            await _mutex.WaitAsync();
            try
            {
                if (generation != _collectiblesGeneration)
                {
                    // A page for the wallet before the switch.
                }
                else if (response is TonNfts page)
                {
                    if (reset)
                    {
                        ApplyFirstCollectiblesPage(page);
                    }
                    else
                    {
                        AppendCollectibles(page);
                    }

                    _collectiblesResource = WalletResource.Ready;
                }
                else
                {
                    _collectiblesResource = new WalletResource(WalletResourcePhase.Failed, (response as Error)?.Message, true);
                }

                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        /// <summary>
        /// Always called with <see cref="_mutex"/> held.
        /// </summary>
        private void AppendCollectibles(TonNfts page)
        {
            var items = new List<TonNft>(_collectibles.Count + page.Nfts.Count);
            var known = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in _collectibles)
            {
                items.Add(item);
                known.Add(item.Address);
            }

            foreach (var item in page.Nfts)
            {
                if (known.Add(item.Address))
                {
                    items.Add(item);
                }
            }

            _collectibles = items;
            _collectiblesOffset = page.NextOffset;
            _collectiblesEnd = page.NextOffset.Length == 0;
        }

        /// <summary>
        /// Always called with <see cref="_mutex"/> held.
        /// </summary>
        private void ApplyFirstCollectiblesPage(TonNfts page)
        {
            var held = _collectibles;
            var fresh = page.Nfts;

            // Over the overlap only: a held list shorter than a page was the whole collection.
            var overlap = Math.Min(held.Count, fresh.Count);
            var same = true;

            for (int i = 0; same && i < overlap; i++)
            {
                same = string.Equals(held[i].Address, fresh[i].Address, StringComparison.Ordinal);
            }

            // Keep the pages read beyond the first only if there is still something after it.
            if (same && held.Count > fresh.Count && page.NextOffset.Length > 0)
            {
                var items = new List<TonNft>(held.Count);

                items.AddRange(fresh);

                for (int i = fresh.Count; i < held.Count; i++)
                {
                    items.Add(held[i]);
                }

                _collectibles = items;
                return;
            }

            _collectibles = Array.Empty<TonNft>();
            AppendCollectibles(page);

            if (!same)
            {
                _collectiblesGeneration++;
            }
        }

        public async Task ShutdownAsync()
        {
            await _mutex.WaitAsync();
            try
            {
                StopResolver();

                await DetachAsync();
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        /// <summary>
        /// Builds the engine client for the wallet the account holds. Always called with
        /// <see cref="_mutex"/> held.
        /// </summary>
        /// <remarks>
        /// Built from what TDLib reports - the address and the public key - so it exists whether or
        /// not this device holds the key. A null <c>local_secret_ref</c> is a public-key-only wallet
        /// in the engine's own words: reads, DNS and send previews all work, and only signing fails,
        /// with <c>LocalSigningUnavailable</c>. That is what lets the wallet be opened, watched and
        /// priced without ever asking for a recovery phrase.
        ///
        /// Rebuilt rather than mutated when the binding changes: the config is fixed at
        /// construction, and a client that acquired a key mid-flight would have a journal keyed to
        /// the record id it started with.
        /// </remarks>
        private void Attach()
        {
            var wallet = _wallet;
            if (wallet == null || wallet.Address.Length == 0 || _client != null)
            {
                return;
            }

            var descriptor = _descriptor;
            var network = DefaultNetwork;

            var config = new WalletClientConfig(
                // The address doubles as the record id while unbound: the journal is per wallet, and
                // there is no descriptor to take an id from until one is stored.
                descriptor?.RecordId ?? wallet.Address,
                wallet.Address,
                wallet.PublicKey,
                descriptor?.SecretRef,
                network,
                SendValiditySeconds,
                ResolutionMarginSeconds,
                // A null DNS root leaves the engine on its own per-network default, which is the
                // real root contract for mainnet and testnet - it does not disable resolution.
                new ProviderConfig(BaseUrl(network), null, RequestTimeoutMs));

            // One host per client, never one per service: request ids are allocated by the client
            // and restart at 1 for each new one, so a host outliving its client carries that
            // client's cancellations into the next one's id space - and a cancellation recorded for
            // an id that has already completed makes the next request under that id fail before it
            // is ever sent.
            _transport = new WalletStatuslessHost(_clientService);
            _client = WalletClient.NewStatusless(config, _transport, _platform);
        }

        private async Task DetachAsync()
        {
            var client = _client;

            _client = null;
            _transport = null;

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
        /// Always called with <see cref="_mutex"/> held.
        /// </summary>
        private WalletState Project()
        {
            var wallet = _wallet;
            if (wallet == null || (wallet.Address.Length == 0 && !wallet.IsBeingCreated))
            {
                return WalletState.None;
            }

            return new WalletState(
                wallet.Address,
                wallet.GramAmount,
                wallet.Address.Length > 0,
                _descriptor != null,
                wallet.IsBeingCreated,
                wallet.IsBackupEnabled,
                wallet.CanEnableBackup,
                wallet.CanExportPhrase,
                AppSettings.WalletCurrency,
                CurrencyRate(AppSettings.WalletCurrency),
                _activity,
                _activityResource,
                !_activityEnd,
                _activityGeneration,
                _collectibles,
                _collectiblesResource,
                !_collectiblesEnd,
                _collectiblesGeneration,
                _clientService.TonWalletGaslessTransfersInfo,
                ProjectArchive());
        }

        /// <summary>
        /// The archived wallets worth showing, which is the ones that are somewhere else.
        /// </summary>
        /// <remarks>
        /// An entry whose address is the wallet in use is a key rotation on the same contract: the
        /// phrase is kept in case that reading is wrong, but there is no second balance to show and
        /// listing it would report the current one twice.
        /// </remarks>
        private IReadOnlyList<WalletArchivedWallet> ProjectArchive()
        {
            List<WalletArchivedWallet> items = null;

            foreach (var record in _archive)
            {
                if (_wallet != null && IsSameAddress(record.Address, _wallet.Address))
                {
                    continue;
                }

                items ??= new List<WalletArchivedWallet>();
                items.Add(new WalletArchivedWallet(record.Address, record.Balance, record.LastUsedDate));
            }

            return items ?? WalletState.NoArchive;
        }

        /// <summary>
        /// Shows amounts in another currency from now on.
        /// </summary>
        /// <remarks>
        /// The rates are fetched here rather than by the caller so that the state carries both
        /// halves at once: a currency whose rate has not arrived would price a balance at one to
        /// one, which reads as a real number and is not one.
        /// </remarks>
        public async Task SetCurrencyAsync(string currency)
        {
            if (string.IsNullOrEmpty(currency))
            {
                return;
            }

            AppSettings.WalletCurrency = currency;

            await _mutex.WaitAsync();
            try
            {
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();
        }

        /// <summary>
        /// How many of the chosen currency one dollar buys. One for USD, and zero while the rates
        /// have not arrived - which is not a rate of one, and the difference is the whole point:
        /// dollars wearing another currency's name is a wrong number, and a view that knows it has
        /// nothing yet can say so instead.
        /// </summary>
        private double CurrencyRate(string currency)
        {
            return _clientService.ExchangeRate(currency);
        }

        /// <summary>
        /// Whether a stored key signs for the wallet the account holds.
        /// </summary>
        /// <remarks>
        /// On the public key where there is one: the same 32 bytes on both sides, while an address
        /// has several user-facing spellings - bounceable or not, URL-safe or not - that TDLib and
        /// the engine need not choose between the same way. The address is the fallback, in raw
        /// form, for a wallet TDLib has reported without a key.
        /// </remarks>
        /// <summary>
        /// Whether the descriptor and the account name the same wallet.
        /// </summary>
        /// <remarks>
        /// By address, deliberately. The descriptor's own public key is the rotation anchor, which
        /// the address is derived from and which a rotation preserves, while the key the account
        /// reports is the one the contract signs with today. Comparing those two would call the
        /// right phrase the wrong one for every wallet that has ever rotated.
        /// </remarks>
        /// <summary>
        /// Whether two addresses name the same account, whichever form each is written in.
        /// </summary>
        private static bool IsSameAddress(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    WalletEngineMethods.ParseTonAddress(left).Raw,
                    WalletEngineMethods.ParseTonAddress(right).Raw,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.Error("wallet address could not be compared: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Whether a transfer to this address should bounce back when the destination rejects it.
        /// </summary>
        /// <remarks>
        /// **The address carries the answer, and honouring it is what stops people losing money.**
        /// TEP-2 gives a user-friendly address a bounceable flag, and the two prefixes are how it
        /// reads on screen: `EQ...` is bounceable and `UQ...` is not. Wallets hand out `UQ`,
        /// contracts - NFTs, tokens, exchanges - hand out `EQ`, precisely so that a call they
        /// cannot honour returns the value instead of swallowing it.
        ///
        /// So this follows the address rather than the account behind it. In particular an `EQ`
        /// address stays bounceable **even when the destination is not deployed yet**: that is the
        /// case the flag exists for, and sending to it unbounced is how the funds are lost.
        ///
        /// A raw `workchain:hex` address carries no flag, and bounceable is the safe reading of
        /// silence - a bounce costs the fees, not the transfer.
        /// </remarks>
        private static bool Bounceable(string address)
        {
            try
            {
                return WalletEngineMethods.ParseTonAddress(address).Format is not TonAddressFormat.UserFriendly friendly
                    || friendly.Bounceable;
            }
            catch (Exception ex)
            {
                // Unparseable here means the transfer is about to fail anyway, and the engine is
                // the one that should say so.
                Logger.Error("wallet address could not be read for its bounce flag: " + ex.Message);
                return true;
            }
        }

        private static bool IsSameWallet(WalletDescriptor descriptor, TdTonWalletState wallet)
        {
            try
            {
                var mine = WalletEngineMethods.ParseTonAddress(descriptor.Address);
                var theirs = WalletEngineMethods.ParseTonAddress(wallet.Address);

                return string.Equals(mine.Raw, theirs.Raw, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Logger.Error("wallet address could not be compared: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Whether the key this device bound is still the key the wallet signs with.
        /// </summary>
        /// <remarks>
        /// A rotation replaces the contract's key and leaves the address alone, so nothing in the
        /// descriptor can answer this - the anchor is invariant by design. What answers it is the
        /// key the account reported when this device bound, against the one it reports now.
        ///
        /// Unknown on either side counts as yes. A missing key is not evidence of a rotation, and
        /// the cost of guessing wrong is deleting a key the user then has to type back in.
        /// </remarks>
        private bool CanStillSign(TdTonWalletState wallet)
        {
            if (_boundKey is not { Length: > 0 } bound || wallet.PublicKey is not { Length: > 0 } reported)
            {
                return true;
            }

            return bound.SequenceEqual(reported);
        }

        /// <summary>
        /// The key a phrase signs with, or null when the engine cannot say.
        /// </summary>
        /// <remarks>
        /// The engine exports only the anchor. A 12-word phrase signs with its anchor; each half of
        /// a 24-word one is a valid 12-word phrase of its own, and the second half's anchor is the
        /// 24-word phrase's signing key.
        /// </remarks>
        private static byte[] PhraseSigningKey(string[] words)
        {
            try
            {
                var half = words.Length == 24 ? words.Skip(12) : words;
                return WalletEngineMethods.RotationMnemonicPublicKey(string.Join(" ", half));
            }
            catch (Exception ex)
            {
                Logger.Error("wallet phrase signing key could not be derived: " + ex.GetType().Name);
                return null;
            }
        }

        /// <summary>
        /// Says what the account holds and what the phrase derived, for the one failure here that
        /// is not the user mistyping.
        /// </summary>
        /// <remarks>
        /// The anchor key is in it because a rotation mnemonic carries two: if the account's key
        /// is the anchor rather than the one the wallet derives from, this line is what says so.
        /// Public keys and addresses only - the phrase never goes near a log.
        /// </remarks>
        private static void LogMismatch(WalletDescriptor descriptor, TdTonWalletState wallet, string[] words)
        {
            string anchor;

            try
            {
                anchor = Convert.ToBase64String(WalletEngineMethods.RotationMnemonicPublicKey(string.Join(" ", words)));
            }
            catch (Exception ex)
            {
                anchor = ex.GetType().Name;
            }

            Logger.Error(string.Format("wallet bind mismatch: account {0} / {1}, derived {2} / {3}, anchor {4}",
                wallet.Address,
                wallet.PublicKey != null ? Convert.ToBase64String(wallet.PublicKey) : "none",
                descriptor.Address,
                descriptor.PublicKey != null ? Convert.ToBase64String(descriptor.PublicKey) : "none",
                anchor));
        }

        /// <summary>
        /// The message hash as <c>getTonWalletTransactionByMsgHash</c> wants it.
        /// </summary>
        /// <remarks>
        /// The field is bytes and the method takes a string, which reads like an encoding is
        /// missing - it is not. The bytes are already the text: the standard base64 of the hash,
        /// in ASCII. Encoding them again produced a hash of a hash, and the account answered Not
        /// Found to every poll it was given.
        /// </remarks>
        private static string MessageHash(byte[] msgHash)
        {
            return Encoding.UTF8.GetString(msgHash);
        }

        // The engine hands BOCs over as standard padded base64; TDLib wants the bytes.
        private static byte[] Bytes(string boc)
        {
            return Convert.FromBase64String(boc);
        }

#if DEBUG
        // Lets SendAsync be exercised end to end without spending: the account still receives
        // the request and answers it, but with a refusal.
        private static readonly bool DebugSpoilTransfers = true;

        // One byte short of the length the header declares, so no bag-of-cells deserializer will
        // read it, the account's or a node's. A flipped byte in the body is not as safe: one
        // that landed in the destination address or the state init is outside what the wallet
        // signs.
        private static byte[] Spoil(byte[] boc)
        {
            return boc.Length > 0 ? boc.AsSpan(0, boc.Length - 1).ToArray() : boc;
        }
#endif

        // Split on any whitespace: the words are what callers show, check and compare, never the
        // sentence.
        private static IReadOnlyList<string> Split(string phrase)
        {
            return phrase.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        // Setting and announcing are separate because the announcement must not happen under
        // _mutex: a subscriber that called back into the service would deadlock on a semaphore
        // that is not reentrant.
        private void SetState(WalletState state)
        {
            State = state;
        }

        private void Raise()
        {
            _aggregator.Publish(new UpdateWalletState(State));
        }

        /// <summary>
        /// What the engine builds its provider URLs against.
        /// </summary>
        /// <remarks>
        /// Only the path of these ever reaches the network - TDLib decides which host answers - but
        /// the engine still needs a base to build against, and mainnet and testnet must not share
        /// one in case the transport ever changes back.
        /// </remarks>
        private static string BaseUrl(Network network)
        {
            return network == Network.Testnet
                ? "https://testnet.toncenter.com"
                : "https://toncenter.com";
        }

        // Not the Telegram account id, deliberately. Forgetting a wallet does not clear its
        // journal, so a reused id would hand the next one the previous one's pending send.
        private static string NewRecordId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private string DescriptorPath => Path.Combine(_path, "descriptor" + _suffix + ".bin");

        private string ArchivePath => Path.Combine(_path, "archive" + _suffix + ".bin");

        /// <summary>
        /// Reads the archive, which is a list of what <see cref="SaveDescriptor"/> writes plus the
        /// date each one stopped being the wallet.
        /// </summary>
        private void LoadArchive()
        {
            _archive.Clear();

            try
            {
                if (!File.Exists(ArchivePath))
                {
                    return;
                }

                using var stream = File.OpenRead(ArchivePath);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                var magic = reader.ReadUInt32();
                var version = reader.ReadInt32();

                if (magic != ArchiveMagic || version < 1 || version > ArchiveVersion)
                {
                    return;
                }

                var count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    _archive.Add(new WalletArchiveRecord
                    {
                        RecordId = reader.ReadString(),
                        Address = reader.ReadString(),
                        PublicKey = reader.ReadBytes(reader.ReadInt32()),
                        Network = reader.ReadInt32() == 1 ? Network.Testnet : Network.Mainnet,
                        SecretRef = reader.ReadString(),
                        BoundKey = reader.ReadBytes(reader.ReadInt32()),
                        ArchivedDate = reader.ReadInt64()
                    });
                }
            }
            catch (Exception ex)
            {
                // Left in place rather than rewritten: a file that cannot be read is still the only
                // record of those phrases, and overwriting it is the one unrecoverable mistake here.
                Logger.Error("wallet archive unreadable: " + ex.Message);
            }
        }

        private void SaveArchive()
        {
            try
            {
                Directory.CreateDirectory(_path);

                using var stream = File.Create(ArchivePath);
                using var writer = new BinaryWriter(stream, Encoding.UTF8);

                writer.Write(ArchiveMagic);
                writer.Write(ArchiveVersion);
                writer.Write(_archive.Count);

                foreach (var record in _archive)
                {
                    writer.Write(record.RecordId);
                    writer.Write(record.Address);
                    writer.Write(record.PublicKey?.Length ?? 0);

                    if (record.PublicKey != null)
                    {
                        writer.Write(record.PublicKey);
                    }

                    writer.Write(record.Network == Network.Testnet ? 1 : 0);
                    writer.Write(record.SecretRef ?? string.Empty);
                    writer.Write(record.BoundKey?.Length ?? 0);

                    if (record.BoundKey != null)
                    {
                        writer.Write(record.BoundKey);
                    }

                    writer.Write(record.ArchivedDate);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("wallet archive could not be saved: " + ex.Message);
            }
        }

        private bool TryLoadDescriptor(out WalletDescriptor descriptor, out byte[] boundKey)
        {
            descriptor = null;
            boundKey = null;

            try
            {
                if (!File.Exists(DescriptorPath))
                {
                    return false;
                }

                using var stream = File.OpenRead(DescriptorPath);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                var magic = reader.ReadUInt32();
                var version = reader.ReadInt32();

                if (magic != DescriptorMagic || version < 1 || version > DescriptorVersion)
                {
                    return false;
                }

                var recordId = reader.ReadString();
                var address = reader.ReadString();
                var publicKey = reader.ReadBytes(reader.ReadInt32());
                var network = reader.ReadInt32() == 1 ? Network.Testnet : Network.Mainnet;
                var secretRef = reader.ReadString();

                // Version 1 predates the rotation check and holds no key of its own. The anchor
                // stands in for it, which is right for every wallet that version could have bound:
                // before a first rotation the two are the same key, and after one the bind would
                // have refused the phrase.
                boundKey = version >= 2 ? reader.ReadBytes(reader.ReadInt32()) : publicKey;

                descriptor = new WalletDescriptor(recordId, address, publicKey, network, new ProtectedSecretRef(secretRef));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("wallet descriptor unreadable: " + ex.Message);
                return false;
            }
        }

        private void SaveDescriptor(WalletDescriptor descriptor, byte[] boundKey)
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

            writer.Write(boundKey?.Length ?? 0);

            if (boundKey != null)
            {
                writer.Write(boundKey);
            }
        }

        private string RotationPath => Path.Combine(_path, "rotation" + _suffix + ".bin");

        private void SaveRotation(WalletRotation rotation)
        {
            Directory.CreateDirectory(_path);

            using var stream = File.Create(RotationPath);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            writer.Write(RotationMagic);
            writer.Write(RotationVersion);
            writer.Write(rotation.Descriptor.RecordId);
            writer.Write(rotation.Descriptor.Address);
            writer.Write(rotation.Descriptor.PublicKey.Length);
            writer.Write(rotation.Descriptor.PublicKey);
            writer.Write(rotation.Descriptor.Network == Network.Testnet ? 1 : 0);
            writer.Write(rotation.Descriptor.SecretRef.Value);
            writer.Write(rotation.PublicKey.Length);
            writer.Write(rotation.PublicKey);
            writer.Write(rotation.ValidUntil);
        }

        private bool TryLoadRotation(out WalletRotation rotation)
        {
            rotation = null;

            try
            {
                if (!File.Exists(RotationPath))
                {
                    return false;
                }

                using var stream = File.OpenRead(RotationPath);
                using var reader = new BinaryReader(stream, Encoding.UTF8);

                if (reader.ReadUInt32() != RotationMagic || reader.ReadInt32() != RotationVersion)
                {
                    return false;
                }

                var recordId = reader.ReadString();
                var address = reader.ReadString();
                var publicKey = reader.ReadBytes(reader.ReadInt32());
                var network = reader.ReadInt32() == 1 ? Network.Testnet : Network.Mainnet;
                var secretRef = reader.ReadString();

                var descriptor = new WalletDescriptor(recordId, address, publicKey, network, new ProtectedSecretRef(secretRef));

                rotation = new WalletRotation(descriptor, reader.ReadBytes(reader.ReadInt32()), reader.ReadUInt64());
                return true;
            }
            catch (Exception ex)
            {
                // Unreadable is the same as absent: the rotation either landed, in which case the
                // chain says so on the next check, or it did not and the old phrase still signs.
                Logger.Error("wallet rotation record could not be read: " + ex.Message);
                return false;
            }
        }

        private void DeleteRotation()
        {
            try
            {
                if (File.Exists(RotationPath))
                {
                    File.Delete(RotationPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("wallet rotation record could not be deleted: " + ex.Message);
            }
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
    /// A submitted key change that the chain has not shown yet.
    /// </summary>
    /// <remarks>
    /// Durable, because the phrase it names is already in protected storage and the message it
    /// names is already out: forgetting this between runs would leave a wallet whose key is about
    /// to change and a device that does not know which phrase opens it.
    /// </remarks>
    internal sealed class WalletRotation
    {
        public WalletRotation(WalletDescriptor descriptor, byte[] publicKey, ulong validUntil)
        {
            Descriptor = descriptor;
            PublicKey = publicKey;
            ValidUntil = validUntil;
        }

        /// <summary>The replacement phrase, already stored.</summary>
        public WalletDescriptor Descriptor { get; }

        /// <summary>The key the contract holds once the change lands, and how it is recognised.</summary>
        public byte[] PublicKey { get; }

        /// <summary>After this, the signature it was made under can no longer be included.</summary>
        public ulong ValidUntil { get; }
    }

    /// <summary>
    /// A phrase update that has been prepared but not submitted: the words to show, and the signed
    /// message that makes them the wallet's. Dropping it abandons the update.
    /// </summary>
    public sealed class WalletPhraseUpdate
    {
        internal WalletPhraseUpdate(IReadOnlyList<string> words, PreparedKeyRotation prepared)
        {
            Words = words;
            Prepared = prepared;
        }

        // Room for what still has to happen before the message reaches the chain - disabling the
        // backup, storing the phrase - so that a check passed now still holds at submission.
        private static readonly TimeSpan Margin = TimeSpan.FromMinutes(1);

        public IReadOnlyList<string> Words { get; }

        internal PreparedKeyRotation Prepared { get; }

        /// <summary>
        /// Whether the signed message can no longer be relied on to be accepted. Once it is, the
        /// words open nothing and must not be committed, nor anything done on their account.
        /// </summary>
        public bool IsExpired => (ulong)DateTimeOffset.UtcNow.Add(Margin).ToUnixTimeSeconds() >= Prepared.ValidUntil;
    }

    /// <summary>
    /// Something was asked of the wallet while a key change of its own was still out.
    /// </summary>
    /// <remarks>
    /// The contract accepts one message per sequence number and the rotation is using this one, so
    /// a transfer sent now would cost either itself or the rotation - and which is not ours to
    /// pick. It resolves in a block or two, or expires.
    /// </remarks>
    public sealed class WalletRotationPendingException : Exception
    {
        public WalletRotationPendingException()
            : base("A key change is already out for this wallet.")
        {
        }
    }

    /// <summary>
    /// A transfer from this wallet has left and not yet settled.
    /// </summary>
    /// <remarks>
    /// A second one would be signed under the same sequence number, so at most one of the two can
    /// land - and if the first already has, an unsettled row that reads as failed is how a user
    /// ends up sending the same money twice.
    /// </remarks>
    public sealed class WalletTransferInProgressException : Exception
    {
        public WalletTransferInProgressException()
            : base("A transfer from this wallet has not settled yet.")
        {
        }
    }

    /// <summary>
    /// A key change could not be made, and nothing about the wallet was altered.
    /// </summary>
    public sealed class WalletRotationFailedException : Exception
    {
        public WalletRotationFailedException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// The user refused the prompt that guards the signing key.
    /// </summary>
    public sealed class WalletAccessDeniedException : Exception
    {
        public WalletAccessDeniedException()
            : base("The user dismissed the prompt.")
        {
        }
    }

    /// <summary>
    /// TDLib refused a wallet request, and the error is carried so the caller can tell a wrong
    /// password from a wallet with nothing to give.
    /// </summary>
    public sealed class WalletRequestException : Exception
    {
        public WalletRequestException(Error error)
            : base(error?.Message ?? "the request was not accepted")
        {
            Error = error;
        }

        public Error Error { get; }

        public bool IsInvalidPassword => Error != null && Error.MessageEquals(ErrorType.PASSWORD_HASH_INVALID);

        public bool IsPasswordMissing => Error != null && Error.MessageEquals(ErrorType.PASSWORD_MISSING);
    }

    /// <summary>
    /// An operation needed the signing key and this device does not have it.
    /// </summary>
    /// <remarks>
    /// Thrown rather than handled, because acquiring the key is a decision with UI attached - the
    /// phrase either comes out of the cloud backup behind the account password, or the user types
    /// it - and the service cannot make either happen. Catch it, bind, and try the operation again.
    /// </remarks>
    public sealed class WalletNotBoundException : Exception
    {
        public WalletNotBoundException()
            : base("The wallet is not bound on this device.")
        {
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
