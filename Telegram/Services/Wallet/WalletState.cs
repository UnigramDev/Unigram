//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// How far along one part of the wallet is. Each resource loads and fails on its own: a refresh
    /// can return activity while the balance request failed, and the view has to be able to say so.
    /// </summary>
    public enum WalletResourcePhase
    {
        Idle,
        Loading,
        Ready,
        Failed
    }

    /// <summary>
    /// The state of one resource, and why it failed if it did.
    /// </summary>
    public sealed class WalletResource
    {
        public static readonly WalletResource Idle = new WalletResource(WalletResourcePhase.Idle, null, false);

        public WalletResource(WalletResourcePhase phase, string message, bool canRetry)
        {
            Phase = phase;
            Message = message;
            CanRetry = canRetry;
        }

        public WalletResourcePhase Phase { get; }

        /// <summary>
        /// The engine's developer message. Diagnostic, not something to show a user as-is.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Whether the engine considers retrying safe. False means retrying will fail the same way,
        /// or is not safe to repeat.
        /// </summary>
        public bool CanRetry { get; }

        public bool IsFailed => Phase == WalletResourcePhase.Failed;

        public bool IsLoading => Phase == WalletResourcePhase.Loading;
    }

    /// <summary>
    /// The account's state on chain. <see cref="Uninitialized"/> is not the same as empty: a wallet
    /// that has been funded but has never sent anything holds its balance and has no contract
    /// deployed yet.
    /// </summary>
    public enum WalletAccountStatus
    {
        Nonexistent,
        Uninitialized,
        Active,
        Frozen,
        Unknown
    }

    public enum WalletActivityDirection
    {
        Sent,
        Received
    }

    /// <summary>
    /// One entry of wallet history.
    /// </summary>
    public sealed class WalletActivityItem
    {
        public WalletActivityItem(string id, string transactionHash, DateTime date, WalletActivityDirection direction, BigInteger amountNanograms, string counterparty)
        {
            Id = id;
            TransactionHash = transactionHash;
            Date = date;
            Direction = direction;
            AmountNanograms = amountNanograms;
            Counterparty = counterparty;
        }

        /// <summary>
        /// Stable across refreshes, which is what pagination merges on.
        /// </summary>
        public string Id { get; }

        public string TransactionHash { get; }

        public DateTime Date { get; }

        public WalletActivityDirection Direction { get; }

        public BigInteger AmountNanograms { get; }

        /// <summary>
        /// The other side, or null when the engine could not attribute one.
        /// </summary>
        public string Counterparty { get; }
    }

    /// <summary>
    /// What the wallet looks like right now, projected out of the engine's snapshot.
    /// </summary>
    /// <remarks>
    /// Immutable, and replaced wholesale on every change: the engine's snapshots are immutable and
    /// revisioned, and mirroring that here is what keeps the two from drifting.
    ///
    /// Nothing generated appears on this type. The wallet views bind with <c>{Binding}</c>, which
    /// needs <c>[GeneratedBindableCustomProperty]</c> under AOT - an attribute that cannot be added
    /// to generated code.
    /// </remarks>
    public sealed class WalletState
    {
        private static readonly WalletActivityItem[] NoActivity = new WalletActivityItem[0];

        /// <summary>
        /// No wallet exists for this account. The starting state, and where deletion returns to.
        /// </summary>
        public static readonly WalletState None = new WalletState();

        private WalletState()
        {
            Account = WalletResource.Idle;
            ActivityResource = WalletResource.Idle;
            Activity = NoActivity;
        }

        public WalletState(
            ulong revision,
            string address,
            WalletAccountStatus status,
            BigInteger balanceNanograms,
            bool synchronized,
            WalletResource account,
            IReadOnlyList<WalletActivityItem> activity,
            WalletResource activityResource,
            bool hasMoreActivity)
        {
            Revision = revision;
            Address = address;
            Status = status;
            BalanceNanograms = balanceNanograms;
            IsSynchronized = synchronized;
            Account = account;
            Activity = activity;
            ActivityResource = activityResource;
            HasMoreActivity = hasMoreActivity;
            HasWallet = true;
        }

        /// <summary>
        /// The engine's snapshot revision this was projected from. Monotonic, and only meaningful
        /// against the same wallet.
        /// </summary>
        public ulong Revision { get; }

        public bool HasWallet { get; }

        /// <summary>
        /// The wallet's address in its user-facing form, or null when there is no wallet.
        /// </summary>
        public string Address { get; }

        public WalletAccountStatus Status { get; }

        /// <summary>
        /// The balance in nanograms. Nanograms rather than grams because that is what the chain
        /// and the engine deal in, and <see cref="BigInteger"/> because the protocol permits values
        /// far beyond <see cref="long"/> - TDLib's own gram fields are int53, which caps at about
        /// nine million TON.
        /// </summary>
        public BigInteger BalanceNanograms { get; }

        /// <summary>
        /// Whether the balance has been read from a provider yet. False means "not known", which is
        /// not the same as zero and must not be shown as zero - check <see cref="Account"/> to tell
        /// "still loading" from "the request failed".
        /// </summary>
        public bool IsSynchronized { get; }

        /// <summary>
        /// Whether the balance is loading, ready or failed.
        /// </summary>
        public WalletResource Account { get; }

        /// <summary>
        /// History, oldest page appended as it is loaded. Never null.
        /// </summary>
        public IReadOnlyList<WalletActivityItem> Activity { get; }

        public WalletResource ActivityResource { get; }

        /// <summary>
        /// Whether an older page exists. Loading it is <c>LoadMoreActivityAsync</c>.
        /// </summary>
        public bool HasMoreActivity { get; }
    }
}
