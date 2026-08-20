//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// The wallet of one Telegram account. One account has at most one wallet, so there is nothing
    /// here to select between.
    /// </summary>
    /// <remarks>
    /// Nothing on this interface is a generated type, deliberately: it is the line the engine does
    /// not cross.
    /// </remarks>
    public interface IWalletService
    {
        WalletState State { get; }

        /// <summary>
        /// Loads the stored wallet, if any, and begins tracking it. Safe to call more than once.
        /// </summary>
        Task<WalletState> RestoreAsync();

        /// <summary>
        /// Creates a wallet and returns its recovery words.
        /// </summary>
        /// <remarks>
        /// The words come back from creation because that is the only point they are available
        /// without re-authorizing: reading them later goes through protected storage and prompts
        /// the user. Show them, have them confirmed, then drop them - a string cannot be cleared.
        /// The new state arrives through <see cref="State"/> and the usual update.
        /// </remarks>
        Task<IReadOnlyList<string>> CreateAsync();

        /// <summary>
        /// Imports an existing wallet from its recovery words. The engine validates them and throws
        /// if they are not a wallet.
        /// </summary>
        Task<WalletState> ImportAsync(IList<string> words);

        /// <summary>
        /// Returns the recovery phrase, after storage has authorized access to it. Show it and drop
        /// it: a string cannot be cleared once created.
        /// </summary>
        Task<IReadOnlyList<string>> RevealRecoveryPhraseAsync();

        /// <summary>
        /// Deletes the wallet and its secret. The recovery phrase is the only way back.
        /// </summary>
        Task DeleteAsync();

        Task RefreshAsync();

        /// <summary>
        /// Loads the next older page of history. A no-op while a refresh or another page load is in
        /// flight, or when there is nothing older.
        /// </summary>
        Task LoadMoreActivityAsync();

        /// <summary>
        /// Stops tracking and releases the engine's client. A send past its commit boundary is
        /// allowed to finish first.
        /// </summary>
        Task ShutdownAsync();
    }
}
