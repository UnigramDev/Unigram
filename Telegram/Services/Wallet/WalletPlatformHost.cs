//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using WalletEngine;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Adapts <see cref="IProtectedSecretStore"/> and <see cref="IWalletJournal"/> to the engine's
    /// platform host.
    /// </summary>
    /// <remarks>
    /// This type holds no state and makes no decisions: it exists so that the two stores can be
    /// written and tested against app-level contracts instead of generated ones. All it adds is
    /// translation, in both directions.
    ///
    /// The engine calls these from its own threads and never holds its wallet-state lock across
    /// the call, so implementations may do real I/O — but must not touch the UI thread.
    /// </remarks>
    internal sealed class WalletPlatformHost : IWalletPlatformHost
    {
        private readonly IProtectedSecretStore _secrets;
        private readonly IWalletJournal _journal;

        public WalletPlatformHost(IProtectedSecretStore secrets, IWalletJournal journal)
        {
            _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        }

        public async Task<byte[]> ReadProtectedSecret(ProtectedSecretRead request)
        {
            try
            {
                return await _secrets.ReadAsync(request.SecretRef.Value, request.Prompt);
            }
            catch (ProtectedSecretException ex)
            {
                throw Secret(ex.Failure, ex.Message);
            }
            catch (Exception ex)
            {
                throw Secret(ProtectedSecretFailure.Other, Describe(ex));
            }
        }

        public async Task StoreProtectedSecret(ProtectedSecretStore request)
        {
            try
            {
                await _secrets.WriteAsync(request.SecretRef.Value, request.Bytes, request.RequireUserPresence);
            }
            catch (ProtectedSecretException ex)
            {
                throw Secret(ex.Failure, ex.Message);
            }
            catch (Exception ex)
            {
                throw Secret(ProtectedSecretFailure.Other, Describe(ex));
            }
        }

        public async Task DeleteProtectedSecret(ProtectedSecretRef secretRef)
        {
            try
            {
                await _secrets.DeleteAsync(secretRef.Value);
            }
            catch (ProtectedSecretException ex)
            {
                throw Secret(ex.Failure, ex.Message);
            }
            catch (Exception ex)
            {
                throw Secret(ProtectedSecretFailure.Other, Describe(ex));
            }
        }

        public async Task<JournalRecord> LoadJournal(JournalKey key)
        {
            try
            {
                var entry = await _journal.LoadAsync(key.RecordId, key.Slot);
                return entry.HasValue
                    ? new JournalRecord(entry.Value.Version, entry.Value.Payload)
                    : null;
            }
            catch (WalletJournalException ex)
            {
                throw Journal(ex.Failure, ex.Message);
            }
            catch (Exception ex)
            {
                throw Journal(WalletJournalFailure.Other, Describe(ex));
            }
        }

        public async Task<JournalCompareExchangeResult> CompareExchangeJournal(JournalCompareExchange mutation)
        {
            try
            {
                var result = await _journal.CompareExchangeAsync(
                    mutation.Key.RecordId,
                    mutation.Key.Slot,
                    mutation.ExpectedVersion,
                    new WalletJournalEntry(mutation.Replacement.Version, mutation.Replacement.Payload));

                var current = result.Current.HasValue
                    ? new JournalRecord(result.Current.Value.Version, result.Current.Value.Payload)
                    : null;

                return new JournalCompareExchangeResult(result.Applied, current);
            }
            catch (WalletJournalException ex)
            {
                throw Journal(ex.Failure, ex.Message);
            }
            catch (Exception ex)
            {
                throw Journal(WalletJournalFailure.Other, Describe(ex));
            }
        }

        private static ProtectedSecretHostException Secret(ProtectedSecretFailure failure, string diagnostic)
        {
            return new ProtectedSecretHostException.Failed(Map(failure), diagnostic);
        }

        private static JournalHostException Journal(WalletJournalFailure failure, string diagnostic)
        {
            return new JournalHostException.Failed(Map(failure), diagnostic);
        }

        private static ProtectedSecretHostErrorKind Map(ProtectedSecretFailure failure)
        {
            return failure switch
            {
                ProtectedSecretFailure.NotFound => ProtectedSecretHostErrorKind.NotFound,
                ProtectedSecretFailure.AuthenticationFailed => ProtectedSecretHostErrorKind.AuthenticationFailed,
                ProtectedSecretFailure.Cancelled => ProtectedSecretHostErrorKind.Cancelled,
                ProtectedSecretFailure.Unavailable => ProtectedSecretHostErrorKind.Unavailable,
                ProtectedSecretFailure.PolicyViolation => ProtectedSecretHostErrorKind.PolicyViolation,
                _ => ProtectedSecretHostErrorKind.Other
            };
        }

        private static JournalHostErrorKind Map(WalletJournalFailure failure)
        {
            return failure switch
            {
                WalletJournalFailure.Unavailable => JournalHostErrorKind.Unavailable,
                WalletJournalFailure.CorruptData => JournalHostErrorKind.CorruptData,
                WalletJournalFailure.Cancelled => JournalHostErrorKind.Cancelled,
                _ => JournalHostErrorKind.Other
            };
        }

        // The engine records this string and may surface it. Exception messages can carry file
        // paths but never secret bytes, so the type name plus the message is safe; anything
        // richer would risk leaking a mnemonic through a storage failure.
        private static string Describe(Exception ex)
        {
            return ex.GetType().Name + ": " + ex.Message;
        }
    }
}
