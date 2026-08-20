//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Why a journal operation failed. Mirrors the engine's classification one for one.
    /// </summary>
    public enum WalletJournalFailure
    {
        Unavailable,
        CorruptData,
        Cancelled,
        Other
    }

    public sealed class WalletJournalException : Exception
    {
        public WalletJournalException(WalletJournalFailure failure, string message)
            : base(message)
        {
            Failure = failure;
        }

        public WalletJournalFailure Failure { get; }
    }

    /// <summary>
    /// One journal entry: an opaque engine payload carried under a compare-and-swap version.
    /// </summary>
    public readonly struct WalletJournalEntry
    {
        public WalletJournalEntry(ulong version, byte[] payload)
        {
            Version = version;
            Payload = payload;
        }

        /// <summary>
        /// The engine's version for this slot. Positive, and only ever moved by the engine.
        /// </summary>
        public ulong Version { get; }

        /// <summary>
        /// Opaque engine bytes. The store must preserve them exactly; nothing here may parse,
        /// normalize or re-encode them.
        /// </summary>
        public byte[] Payload { get; }
    }

    /// <summary>
    /// The outcome of a compare-and-exchange.
    /// </summary>
    public readonly struct WalletJournalExchange
    {
        private WalletJournalExchange(bool applied, WalletJournalEntry? current)
        {
            Applied = applied;
            Current = current;
        }

        public static WalletJournalExchange Replaced(WalletJournalEntry current)
        {
            return new WalletJournalExchange(true, current);
        }

        /// <summary>
        /// The expected version did not match. <paramref name="current"/> is what the slot holds
        /// now, which the engine needs in order to reconcile.
        /// </summary>
        public static WalletJournalExchange Rejected(WalletJournalEntry? current)
        {
            return new WalletJournalExchange(false, current);
        }

        public bool Applied { get; }

        public WalletJournalEntry? Current { get; }
    }

    /// <summary>
    /// Durable storage for the engine's journal: the record of sends that have crossed their
    /// commit boundary and must be resolved even if the app is killed mid-flight.
    /// </summary>
    /// <remarks>
    /// Losing or reordering a journal write can double-spend, so the two rules below are the whole
    /// contract:
    /// <list type="bullet">
    /// <item>a write must be durable before it returns, not merely queued;</item>
    /// <item>compare and replace must happen in one transaction, never read-then-write.</item>
    /// </list>
    /// Entries are keyed by the wallet's <c>recordId</c> and a slot name the engine chooses. The
    /// payload is opaque and must survive byte for byte.
    /// </remarks>
    public interface IWalletJournal
    {
        /// <summary>
        /// Reads the current entry for a slot, or null when the slot has never been written.
        /// </summary>
        /// <exception cref="WalletJournalException" />
        Task<WalletJournalEntry?> LoadAsync(string recordId, string slot);

        /// <summary>
        /// Replaces the slot's entry only if its version is <paramref name="expectedVersion"/> —
        /// or if the slot is empty and <paramref name="expectedVersion"/> is null — and reports
        /// what the slot holds afterwards.
        /// </summary>
        /// <exception cref="WalletJournalException" />
        Task<WalletJournalExchange> CompareExchangeAsync(string recordId, string slot, ulong? expectedVersion, WalletJournalEntry replacement);
    }
}
