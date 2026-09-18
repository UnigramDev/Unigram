//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using WalletEngine;
using Windows.Storage;
// Both halves define a SendMessage - TDLib's is the function that sends a chat message - and this
// file is the one place they meet.
using File = System.IO.File;
using EngineSendMessage = WalletEngine.SendMessage;
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

        // Set when a refresh is asked for while one is already running, and honoured when that one
        // finishes. Dropping it would lose exactly the update that asked - a transfer landing while
        // the list was being read.
        private bool _activityQueued;

        // Mainnet, whatever the account is: the wallet of a test-server account is a mainnet
        // wallet like any other. Following Options.TestMode here derived a different address
        // altogether - the engine takes WALLET_SUBWALLET_ID_DEFAULT_TESTNET for testnet, so the
        // phrase produced a wallet the account had never heard of and every bind was refused -
        // and pointed the provider at testnet.toncenter, where the real one does not exist.
        private static Network DefaultNetwork => Network.Mainnet;

        private const uint DescriptorMagic = 0x4C41574Du; // "MWAL"
        private const int DescriptorVersion = 2;

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

        // The public key the account reported when this device bound, which is the only thing that
        // changes under a rotation - see CanStillSign. Kept beside the descriptor, saved with it and
        // dropped with it.
        private byte[] _boundKey;


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

        // Fetched once and kept: the rates move slowly, the list is every currency there is, and
        // the card asks for one of them on every balance change.
        private IReadOnlyList<CurrencyExchangeRate> _rates;
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
        private bool _activityLoading;

        public WalletService(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;

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

            var secrets = new WalletSecretStore(_path, _suffix);
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
                        await ForgetDescriptorAsync();
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

            // Not awaited: the card shows grams the moment the state arrives, and the price beside
            // them follows when the rates do.
            _ = LoadRatesAsync();

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

                if (first || replaced || stale)
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
                        _activityGeneration++;

                        RebuildActivity();
                    }

                    if (stale)
                    {
                        // This device's key cannot sign for the wallet any more. Dropped the moment
                        // that becomes known rather than left to fail at the next transfer, where
                        // the only symptom would be a rejected message.
                        await ForgetDescriptorAsync();
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
                await LoadActivityAsync(true);
            }
        }

        public async Task<WalletBindResult> BindAsync(IReadOnlyList<string> words)
        {
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

                await DetachAsync();

                // The baseline comes from the chain, because that is what it will be compared
                // against: the account keeps reporting the key the wallet was created with, so
                // recording that one on an already-rotated wallet would make the next check drop the
                // key that was just bound.
                //
                // The account's key stands in when the chain cannot be reached. It is the same value
                // on a wallet that has never rotated, and on one that has, the first check that does
                // reach the chain asks for the phrase once more and settles it.
                var bound = await ChainPublicKeyAsync(wallet.Address) ?? wallet.PublicKey;

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
            // An empty password is what TDLib asks for when the account has none, so it is passed
            // through rather than refused here.
            var response = await _clientService.SendAsync(new GetTonWalletSecretPhrase(password ?? string.Empty));
            if (response is Text phrase)
            {
                return Split(phrase.TextValue);
            }

            // Which failure it was decides what the caller does next: a wrong password is worth
            // asking about again, and nothing else is.
            throw new WalletRequestException(response as Error);
        }

        public async Task<WalletBindResult> BindFromCloudAsync(string password)
        {
            // Both halves throw what the caller has to tell apart: a wrong password comes out of
            // the export as WalletRequestException, and a phrase that derives another wallet out
            // of the bind.
            var words = await ExportRecoveryPhraseAsync(password);
            return await BindAsync(words);
        }

        public async Task<IReadOnlyList<string>> RevealRecoveryPhraseAsync()
        {
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

        public async Task EnableBackupAsync(string password, IReadOnlyList<string> words)
        {
            await _clientService.SendAsync(new EnableTonWalletBackup(password ?? string.Empty, string.Join(" ", words)));
        }

        public async Task DisableBackupAsync(string password)
        {
            await _clientService.SendAsync(new DisableTonWalletBackup(password ?? string.Empty));
        }

        public async Task<WalletTransferResult> SendAsync(string recipient, long peerUserId, string peerDomain, BigInteger amountNanograms, string comment, bool isCommentPublic, bool allowGasless)
        {
            var client = _client;
            if (client == null || _descriptor == null)
            {
                // Nothing here asks for a phrase: binding is a decision with UI attached, and the
                // caller is the one that can make it and then try again.
                throw new WalletNotBoundException();
            }

            SendMessageBody body;

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
                // The engine reads the recipient's key off their contract and asks for this
                // wallet's phrase, so an encrypted comment is a signing operation of its own -
                // and the body it hands back is a cell rather than text.
                body = new SendMessageBody.RawPayload(await client.CreateEncryptedComment(new CreateEncryptedCommentRequest(recipient, comment)));
            }

            // Bounce is off because a transfer to an address that cannot accept it should leave the
            // funds there rather than return them minus the fees, which is what wallets do.
            var message = new EngineSendMessage(
                recipient,
                new SendAmount.Exact(amountNanograms.ToString()),
                body,
                false,
                null);

            var intent = new SendIntent(new SendExpiration.EngineDefault(), new[] { message });
            var prepared = await client.PrepareTransfer(new PrepareTransferRequest(NewRecordId(), intent));

            // Both messages cover the same seqno and expiry, so offering both is offering the server
            // a choice, not two transfers: whichever it broadcasts, the other can never also land.
            var response = await _clientService.SendAsync(new SendTonWalletTransfer(
                Bytes(prepared.ExternalBoc),
                allowGasless ? Bytes(prepared.InternalBoc) : Array.Empty<byte>()));

            if (response is not TonWalletTransferResult result)
            {
                // Handed back rather than thrown: a refusal is an answer, not a fault, and the
                // caller is the one that knows how to say it.
                var error = response as Error;

                Logger.Error(string.Format("wallet transfer refused: {0} {1}", error?.Code, error?.Message));
                return new WalletTransferResult(error);
            }

            // Everything past this point is bookkeeping over a transfer that has already left. It
            // must not be able to fail the send: a caller told that this failed would send it again,
            // and the money is already gone.
            try
            {
                // At the top of the history before the caller is told anything, because the account
                // has no idea any of this happened: TDLib reports transactions, and this is not one.
                await _mutex.WaitAsync();
                try
                {
                    if (result.Transaction != null)
                    {
                        // The account held the request open until the transfer was included and
                        // answered with the transaction itself - the same row the history returns -
                        // so there is nothing to settle and nothing to poll for.
                        AddConfirmed(result.Transaction);
                    }
                    else
                    {
                        AddPending(recipient, peerUserId, peerDomain, amountNanograms, comment, result.IsGasless, prepared.OperationId, result.MsgHash, prepared.ValidUntil);
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

            return new WalletTransferResult(
                MessageHash(result.MsgHash),
                result.IsGasless,
                result.Transaction);
        }

        public async Task<BigInteger?> EstimateFeeAsync(string recipient, BigInteger amountNanograms, string comment)
        {
            var client = _client;
            if (client == null || string.IsNullOrEmpty(recipient) || amountNanograms <= BigInteger.Zero)
            {
                return null;
            }

            try
            {
                // The comment goes in as plain text even when the transfer will encrypt it.
                // Encrypting one is a signing operation - the engine reads the recipient's key and
                // asks for this wallet's phrase - and a user-presence prompt per keystroke is not a
                // price worth paying for the few forward-fee nanograms the larger cell would add.
                var body = string.IsNullOrEmpty(comment)
                    ? new SendMessageBody.Empty()
                    : (SendMessageBody)new SendMessageBody.Comment(comment);

                var message = new EngineSendMessage(
                    recipient,
                    new SendAmount.Exact(amountNanograms.ToString()),
                    body,
                    false,
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

        public async Task<string> ResolveDnsAsync(string name)
        {
            var client = _client;
            return client != null ? await client.ResolveDns(name) : null;
        }

        public async Task<string> DecryptCommentAsync(TonWalletTransaction transaction, string encryptedBody)
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
        /// The account cannot answer this. Telegram knows the wallet by the key its address is
        /// derived from, and a rotation is an on-chain operation performed by whichever client did
        /// it - one whose whole point, when it follows the cloud backup being turned off, is that
        /// the server no longer holds a key that can spend. So <c>tonWalletState.public_key</c>
        /// stays as it was and the contract is the only authority left.
        ///
        /// Once per session, and never blocking: a stale key costs nothing until something signs,
        /// and the balance and history are readable without one.
        /// </remarks>
        private async Task VerifySigningKeyAsync()
        {
            var wallet = _wallet;

            if (_descriptor == null || wallet == null || wallet.Address.Length == 0 || _boundKey is not { Length: > 0 } bound)
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
                // The wallet may have been rebound, or forgotten, while the request was out. Only
                // the key this answer is about is dropped.
                if (_boundKey == null || !_boundKey.SequenceEqual(bound))
                {
                    return;
                }

                await DetachAsync();
                await ForgetDescriptorAsync();

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

        private async Task ForgetDescriptorAsync()
        {
            var descriptor = _descriptor;
            if (descriptor == null)
            {
                return;
            }

            try
            {
                await _lifecycle.DeleteWallet(descriptor);
            }
            catch (Exception ex)
            {
                Logger.Error("wallet secret could not be deleted: " + ex.Message);
            }

            DeleteDescriptor();

            _descriptor = null;
            _boundKey = null;
        }

        public async Task ForgetAsync()
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
                _boundKey = null;

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
            _stream ??= new WalletChainStream(_clientService, () => State.Address, OnChainChanged);
            _stream.Start();
        }

        public void StopWatching()
        {
            _stream?.Stop();
        }

        /// <summary>
        /// Something happened on the chain. What it was comes from the account, which is the only
        /// thing that knows who was on the other side.
        /// </summary>
        /// <remarks>
        /// Not awaited, and not guarded here: a refresh already running takes this as a second one
        /// to do when it finishes, and a burst of frames collapses into that single repeat.
        /// </remarks>
        private void OnChainChanged()
        {
            _ = LoadActivityAsync(true);
        }

        public async Task RefreshAsync()
        {
            _clientService.Send(new LoadTonWalletState());
            await LoadActivityAsync(true);
        }

        public Task LoadMoreActivityAsync()
        {
            return LoadActivityAsync(false);
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
        /// The message hash is its identity for as long as it has none: a transaction id only
        /// exists once there is a transaction, and there is not one yet.
        /// </remarks>
        private void AddPending(string recipient, long peerUserId, string peerDomain, BigInteger amountNanograms, string comment, bool isGasless, string operationId, byte[] msgHash, ulong validUntil)
        {
            var hash = MessageHash(msgHash);

            var pending = new TonWalletTransaction(
                hash,
                recipient,
                peerUserId,
                peerDomain ?? string.Empty,
                (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                new TonWalletTransactionStatePending(operationId, hash, (int)validUntil),
                new TonWalletTransactionTypeTransfer(
                    -(long)amountNanograms,
                    0,
                    isGasless,
                    comment ?? string.Empty,
                    false));

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

                foreach (var (_, transaction) in landed)
                {
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

        private async Task LoadActivityAsync(bool reset)
        {
            string offset;
            HashSet<string> known;

            await _mutex.WaitAsync();
            try
            {
                if (_wallet == null || _wallet.Address.Length == 0)
                {
                    return;
                }

                if (_activityLoading)
                {
                    _activityQueued |= reset;
                    return;
                }

                if (!reset && _activityOffset.Length == 0 && _confirmed.Count > 0)
                {
                    // An empty next_offset is TDLib saying there is nothing older.
                    return;
                }

                offset = reset ? string.Empty : _activityOffset;

                // Taken here, under the lock, and carried into the request: nothing else adds to
                // the confirmed half, and the loading flag keeps a second load out until this one
                // has written its pages back.
                known = new HashSet<string>(_confirmed.Count, StringComparer.Ordinal);

                foreach (var item in _confirmed)
                {
                    known.Add(item.Id);
                }

                _activityLoading = true;
                _activityResource = WalletResource.Loading;
                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();

            var error = reset
                ? await RefreshActivityAsync(known)
                : await AppendActivityAsync(offset, known);

            bool queued;

            await _mutex.WaitAsync();
            try
            {
                _activityLoading = false;
                _activityResource = error == null
                    ? WalletResource.Ready
                    : new WalletResource(WalletResourcePhase.Failed, error.Message, true);

                queued = _activityQueued;
                _activityQueued = false;

                SetState(Project());
            }
            finally
            {
                _mutex.Release();
            }

            Raise();

            if (queued)
            {
                await LoadActivityAsync(true);
            }
        }

        /// <summary>
        /// Reads one page of older transactions onto the end of the history.
        /// </summary>
        private async Task<Error> AppendActivityAsync(string offset, HashSet<string> known)
        {
            var response = await _clientService.SendAsync(new GetTonWalletTransactions(null, offset, ActivityPageSize));
            if (response is not TonWalletTransactions transactions)
            {
                return response as Error;
            }

            var items = new List<TonWalletTransaction>(_confirmed);

            // By id, because a page repeats the transaction its offset was taken at.
            foreach (var item in transactions.Transactions)
            {
                if (known.Add(item.Id))
                {
                    items.Add(item);
                }
            }

            _confirmed = items;
            _activityOffset = transactions.NextOffset;

            RebuildActivity();
            return null;
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
        private async Task<Error> RefreshActivityAsync(HashSet<string> known)
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

            if (joined)
            {
                var items = new List<TonWalletTransaction>(fetched.Count + _confirmed.Count);

                items.AddRange(fetched);
                items.AddRange(_confirmed);

                _confirmed = items;

                // The page just read is also the end of what has been read only on a first load.
                // Otherwise the end is where it already was.
                if (known.Count == 0)
                {
                    _activityOffset = tail;
                }
            }
            else
            {
                _confirmed = fetched;
                _activityOffset = tail;
                _activityGeneration++;
            }

            RebuildActivity();
            return null;
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
                _activityOffset.Length > 0,
                _activityGeneration,
                _clientService.TonWalletGaslessTransfersInfo);
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

            await LoadRatesAsync();

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
        /// Every currency TDLib quotes, and what each is worth in USD.
        /// </summary>
        public async Task<IReadOnlyList<CurrencyExchangeRate>> GetCurrencyRatesAsync()
        {
            await LoadRatesAsync();
            return _rates;
        }

        private async Task LoadRatesAsync()
        {
            if (_rates != null)
            {
                return;
            }

            var response = await _clientService.SendAsync(new GetCurrencyExchangeRates());
            if (response is CurrencyExchangeRates rates)
            {
                _rates = rates.Rates;

                // The state was projected before these arrived, so it carries the rate that stands
                // in for a missing one - which is 1, and reads as dollars wearing another currency's
                // name. Everything showing a converted amount has to be told they are here.
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
        }

        /// <summary>
        /// How many of the chosen currency one dollar buys. One for USD, and zero while the rates
        /// have not arrived - which is not a rate of one, and the difference is the whole point:
        /// dollars wearing another currency's name is a wrong number, and a view that knows it has
        /// nothing yet can say so instead.
        /// </summary>
        private double CurrencyRate(string currency)
        {
            if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            var rates = _rates;
            if (rates == null)
            {
                return 0;
            }

            foreach (var rate in rates)
            {
                if (string.Equals(rate.Currency, currency, StringComparison.OrdinalIgnoreCase) && rate.Rate > 0)
                {
                    return rate.Rate;
                }
            }

            // They arrived and this currency is not among them, which the picker cannot produce -
            // it offers what the rates offer. Dollars are the honest fallback.
            return 1;
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
