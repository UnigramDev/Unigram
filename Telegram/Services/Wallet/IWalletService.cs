//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// The wallet of one Telegram account. One account has at most one wallet, so there is nothing
    /// here to select between.
    /// </summary>
    /// <remarks>
    /// Two systems sit behind this: TDLib owns the wallet as the account knows it - which wallet
    /// exists, its balance, its history, and the broadcasting of anything signed - and the engine
    /// owns the key material and everything derived from it. Neither is visible from here, and no
    /// generated type from either appears on this interface.
    /// </remarks>
    public interface IWalletService
    {
        WalletState State { get; }

        /// <summary>
        /// Begins tracking the account's wallet, and picks up a signing key if one was stored
        /// earlier. Safe to call more than once.
        /// </summary>
        /// <remarks>
        /// Never asks for anything. The address, the balance and the history all come from the
        /// account, and the engine works from the public key alone - so the wallet can be opened,
        /// watched and priced on a device that has never seen the recovery phrase.
        /// </remarks>
        Task<WalletState> RestoreAsync();

        /// <summary>
        /// Binds this device to the account's wallet using its recovery phrase, and keeps the
        /// signing key in protected storage.
        /// </summary>
        /// <remarks>
        /// Only needed for what actually signs - see <see cref="WalletNotBoundException"/> - so it
        /// belongs to the moment the user asks for such an action, not to opening the wallet.
        ///
        /// Refuses a phrase that derives a different address than the one the account holds: a
        /// phrase that is valid but belongs to another wallet would otherwise sign messages the
        /// server will reject, long after the mistake was made.
        ///
        /// Answers with the state it left the wallet in, or with the reason it left it alone: a
        /// phrase that is not one, or one that belongs to another wallet.
        /// </remarks>
        Task<WalletBindResult> BindAsync(IReadOnlyList<string> words);

        /// <summary>
        /// Binds using the phrase held in the Telegram cloud, which needs only the account
        /// password. Throws <see cref="WalletNotBoundException"/> when the phrase cannot be read
        /// back, and the user has to type it instead.
        /// </summary>
        Task<WalletBindResult> BindFromCloudAsync(string password);

        /// <summary>
        /// Returns the recovery phrase from the Telegram cloud backup. Requires the account
        /// password - an empty string where the account has none, which is what TDLib asks for -
        /// and only works while <see cref="WalletState.CanExportPhrase"/> is set.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="WalletRequestException"/> when TDLib refuses; its
        /// <c>IsInvalidPassword</c> is the one worth asking the user about again.
        ///
        /// The caller owns the only copy the app can see. Show it, then drop it: a string cannot be
        /// cleared, so it must not reach logs, errors, analytics or saved state.
        /// </remarks>
        Task<IReadOnlyList<string>> ExportRecoveryPhraseAsync(string password);

        /// <summary>
        /// Returns the recovery phrase from local protected storage, after the device prompt has
        /// authorized access. Same handling rules as <see cref="ExportRecoveryPhraseAsync"/>.
        /// </summary>
        /// <remarks>
        /// Null means this device has nothing to give - no key, or storage that will not open - so
        /// the cloud copy is worth trying next. A dismissed prompt is different and comes back as
        /// <see cref="WalletAccessDeniedException"/>: the user was asked and said no, and asking
        /// them for their account password instead would be answering a refusal with a demand.
        /// </remarks>
        Task<IReadOnlyList<string>> RevealRecoveryPhraseAsync();

        /// <summary>
        /// Stores the recovery phrase in the Telegram cloud, behind the account password.
        /// </summary>
        Task EnableBackupAsync(string password, IReadOnlyList<string> words);

        /// <summary>
        /// Removes the cloud copy of the recovery phrase, leaving the phrase itself as the only way
        /// back into the wallet.
        /// </summary>
        Task DisableBackupAsync(string password);

        /// <summary>
        /// Signs a transfer here and has TDLib broadcast it. Throws
        /// <see cref="WalletNotBoundException"/> when this device holds no key.
        /// </summary>
        /// <param name="recipient">A raw or user-facing address. Names are resolved by
        /// <see cref="ResolveDnsAsync"/> first, deliberately not here.</param>
        /// <param name="peerUserId">The Telegram user on the other side, or zero where the address
        /// is all there is. Only the caller knows this, and the row that appears in the history the
        /// moment the transfer leaves is drawn from it - so without it a transfer to a friend reads
        /// as a transfer to a string of characters until the account reports it.</param>
        /// <param name="peerDomain">The <c>.ton</c> name the address was reached through, if it
        /// was. Same reason.</param>
        /// <param name="allowGasless">Whether a relayer may pay the gas. The server decides whether
        /// one does; the result says what happened.</param>
        /// <param name="isCommentPublic">
        /// Whether the comment travels in the clear. Encrypted is the default and costs a little
        /// more: the engine reads the recipient's key off the chain and signs the body with this
        /// wallet's, so only the two of them can read it - and only a wallet contract that exposes
        /// its key can be sent one.
        /// </param>
        Task<WalletTransferResult> SendAsync(string recipient, long peerUserId, string peerDomain, BigInteger amountNanograms, string comment, bool isCommentPublic, bool allowGasless);

        /// <summary>
        /// What the network would charge for a transfer, in nanograms, or null when it cannot be
        /// said - no wallet, no recipient yet, or the estimate did not come back.
        /// </summary>
        /// <remarks>
        /// An emulation against a recent block rather than a rule of thumb, so it costs a round
        /// trip and is worth asking for only once the amount has settled.
        /// </remarks>
        Task<BigInteger?> EstimateFeeAsync(string recipient, BigInteger amountNanograms, string comment);

        /// <summary>
        /// Resolves a <c>.ton</c> name to an address, or null when it resolves to nothing. Needs no
        /// key: resolution is a read.
        /// </summary>
        Task<string> ResolveDnsAsync(string name);

        /// <summary>
        /// Decrypts the comment of a history entry that carries one. Needs the signing key.
        /// </summary>
        /// <param name="encryptedBody">
        /// The encrypted payload, which is what TDLib puts in <c>comment</c> when
        /// <c>is_comment_encrypted</c> is set. The engine decrypts the message body it belongs to,
        /// so it is wrapped back into one by <see cref="WalletCommentBody"/> on the way.
        /// </param>
        Task<string> DecryptCommentAsync(TonWalletTransaction transaction, string encryptedBody);

        /// <summary>
        /// Forgets the signing key on this device. The wallet itself, and its cloud backup, are
        /// untouched - the account still has it and another device can still spend from it.
        /// </summary>
        /// <remarks>
        /// Deleting the account's wallet is a different operation (<c>deleteTonWallet</c>, which
        /// replaces it with a new one and needs the account password), and is deliberately not
        /// offered until there is a flow that can ask for that password.
        /// </remarks>
        Task ForgetAsync();

        /// <summary>
        /// Shows amounts in another currency from now on, and remembers it. The choice is the
        /// app's rather than the account's, so it follows the user across their sessions here.
        /// </summary>
        Task SetCurrencyAsync(string currency);

        /// <summary>
        /// Every currency TDLib quotes, with what each is worth in USD. For the picker.
        /// </summary>
        Task<IReadOnlyList<CurrencyExchangeRate>> GetCurrencyRatesAsync();

        /// <summary>
        /// Reloads state and the first page of history.
        /// </summary>
        Task RefreshAsync();

        /// <summary>
        /// Starts watching the chain, or notes another watcher; every call is paired with
        /// <see cref="StopWatching"/>.
        /// </summary>
        /// <remarks>
        /// A transaction is not announced: nothing updates to say the history has changed, and the
        /// balance moving is the only sign the account gives. Watching the chain is what turns that
        /// into something immediate - at the cost of a socket held open, which is why it runs only
        /// while the wallet is on screen.
        /// </remarks>
        void StartWatching();

        void StopWatching();

        /// <summary>
        /// Loads the next older page of history. A no-op while another page load is in flight, or
        /// when there is nothing older.
        /// </summary>
        Task LoadMoreActivityAsync();

        /// <summary>
        /// Stops tracking and releases the engine's client.
        /// </summary>
        Task ShutdownAsync();
    }
}
