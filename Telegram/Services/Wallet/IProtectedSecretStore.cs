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
    /// Why a protected-secret operation failed.
    /// </summary>
    /// <remarks>
    /// These mirror the engine's own classification one for one, so that the store never has to
    /// reference a generated type and the host adapter can map without interpreting.
    /// </remarks>
    public enum ProtectedSecretFailure
    {
        NotFound,
        AuthenticationFailed,
        Cancelled,
        Unavailable,
        PolicyViolation,
        Other
    }

    public sealed class ProtectedSecretException : Exception
    {
        public ProtectedSecretException(ProtectedSecretFailure failure, string message)
            : base(message)
        {
            Failure = failure;
        }

        public ProtectedSecretFailure Failure { get; }
    }

    /// <summary>
    /// Protected storage for wallet secrets.
    /// </summary>
    /// <remarks>
    /// The engine hands over mnemonic bytes and expects them back byte for byte. Nothing here may
    /// log a secret, and no failure may be reported as success: the engine treats a short or empty
    /// read as a valid secret and derives the wrong wallet from it.
    /// </remarks>
    public interface IProtectedSecretStore
    {
        /// <summary>
        /// Reads the secret stored under <paramref name="key"/>.
        /// </summary>
        /// <param name="prompt">
        /// Authentication text supplied by the engine, shown when what guards the secrets asks the
        /// user for something.
        /// </param>
        /// <exception cref="ProtectedSecretException">
        /// The entry is missing, the user declined, or storage is unavailable. Never returns an
        /// empty array to signal absence.
        /// </exception>
        Task<byte[]> ReadAsync(string key, string prompt);

        /// <summary>
        /// Writes <paramref name="secret"/> under <paramref name="key"/>, replacing any existing
        /// entry.
        /// </summary>
        /// <remarks>
        /// There is no per-entry presence policy to pass: what a later read asks the user for is
        /// what the user chose for the wallet as a whole, and an entry that claimed otherwise could
        /// only ever contradict them.
        ///
        /// Nothing here asks the user anything. Both of these run on the engine's threads, inside
        /// an operation that has already unlocked the vault in the window it was started from -
        /// see <see cref="WalletVault.LeaseAsync"/>.
        /// </remarks>
        Task WriteAsync(string key, byte[] secret);

        /// <summary>
        /// Deletes the entry under <paramref name="key"/>. Succeeds when it is already absent.
        /// </summary>
        Task DeleteAsync(string key);
    }
}
