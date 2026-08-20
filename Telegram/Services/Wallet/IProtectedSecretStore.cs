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
        /// Authentication text supplied by the engine, shown only when the entry was written with
        /// a user-presence requirement.
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
        /// <param name="requireUserPresence">
        /// Whether later reads must verify the user before returning the bytes. The policy is
        /// recorded with the entry, because only the writer knows it.
        /// </param>
        Task WriteAsync(string key, byte[] secret, bool requireUserPresence);

        /// <summary>
        /// Deletes the entry under <paramref name="key"/>. Succeeds when it is already absent.
        /// </summary>
        Task DeleteAsync(string key);
    }
}
