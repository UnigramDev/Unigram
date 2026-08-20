//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Credentials.UI;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Stores wallet secrets with DPAPI, one file per entry.
    /// </summary>
    /// <remarks>
    /// Each file is a single presence byte followed by the protected blob. The byte is deliberately
    /// outside the protected region: a read that requires user presence has to verify *before*
    /// unprotecting, so the policy cannot live behind the thing it guards.
    ///
    /// One entry per file rather than one shared blob, so writing a secret never rewrites - and so
    /// never risks losing - another.
    /// </remarks>
    public sealed class WalletSecretStore : IProtectedSecretStore
    {
        // "LOCAL=user" scopes the key to this Windows user on this machine. The descriptor is only
        // needed to protect; Unprotect reads it back out of the blob.
        private const string Descriptor = "LOCAL=user";

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly string _path;
        private readonly string _suffix;

        /// <param name="suffix">
        /// Distinguishes test-server entries from production ones, which share a folder because
        /// they share a session id. A wallet must never read the wrong network's key.
        /// </param>
        public WalletSecretStore(string path, string suffix)
        {
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _suffix = suffix ?? string.Empty;
        }

        public async Task<byte[]> ReadAsync(string key, string prompt)
        {
            var file = FileFor(key);

            await _lock.WaitAsync();
            try
            {
                if (!File.Exists(file))
                {
                    throw new ProtectedSecretException(ProtectedSecretFailure.NotFound, "no secret stored for this reference");
                }

                var stored = File.ReadAllBytes(file);
                if (stored.Length < 1)
                {
                    throw new ProtectedSecretException(ProtectedSecretFailure.Other, "stored secret is truncated");
                }

                if (stored[0] != 0)
                {
                    await VerifyUserAsync(prompt);
                }

                var protectedBytes = new byte[stored.Length - 1];
                Buffer.BlockCopy(stored, 1, protectedBytes, 0, protectedBytes.Length);

                var provider = new DataProtectionProvider();
                var buffer = await provider.UnprotectAsync(CryptographicBuffer.CreateFromByteArray(protectedBytes));

                CryptographicBuffer.CopyToByteArray(buffer, out var plain);
                return plain;
            }
            catch (ProtectedSecretException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ProtectedSecretException(ProtectedSecretFailure.Unavailable, ex.GetType().Name);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task WriteAsync(string key, byte[] secret, bool requireUserPresence)
        {
            await _lock.WaitAsync();
            try
            {
                var provider = new DataProtectionProvider(Descriptor);
                var buffer = await provider.ProtectAsync(CryptographicBuffer.CreateFromByteArray(secret));

                CryptographicBuffer.CopyToByteArray(buffer, out var protectedBytes);

                var stored = new byte[protectedBytes.Length + 1];
                stored[0] = requireUserPresence ? (byte)1 : (byte)0;
                Buffer.BlockCopy(protectedBytes, 0, stored, 1, protectedBytes.Length);

                Directory.CreateDirectory(_path);

                // Written aside and moved into place: a half-written secret file is unrecoverable,
                // and the wallet it belongs to would be unrecoverable with it.
                var file = FileFor(key);
                var temporary = file + ".tmp";

                File.WriteAllBytes(temporary, stored);
                Replace(temporary, file);
            }
            catch (Exception ex)
            {
                throw new ProtectedSecretException(ProtectedSecretFailure.Unavailable, ex.GetType().Name);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task DeleteAsync(string key)
        {
            await _lock.WaitAsync();
            try
            {
                var file = FileFor(key);
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                throw new ProtectedSecretException(ProtectedSecretFailure.Unavailable, ex.GetType().Name);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task VerifyUserAsync(string prompt)
        {
            UserConsentVerificationResult result;

            try
            {
                result = await UserConsentVerifier.RequestVerificationAsync(prompt);
            }
            catch (Exception ex)
            {
                throw new ProtectedSecretException(ProtectedSecretFailure.Unavailable, ex.GetType().Name);
            }

            switch (result)
            {
                case UserConsentVerificationResult.Verified:
                    return;
                case UserConsentVerificationResult.Canceled:
                    throw new ProtectedSecretException(ProtectedSecretFailure.Cancelled, "the user dismissed the prompt");
                case UserConsentVerificationResult.DeviceNotPresent:
                case UserConsentVerificationResult.NotConfiguredForUser:
                case UserConsentVerificationResult.DisabledByPolicy:
                case UserConsentVerificationResult.DeviceBusy:
                    // The entry was written under a policy this device can no longer satisfy.
                    // Handing the secret over anyway would silently drop the guarantee.
                    throw new ProtectedSecretException(ProtectedSecretFailure.PolicyViolation, result.ToString());
                default:
                    throw new ProtectedSecretException(ProtectedSecretFailure.AuthenticationFailed, result.ToString());
            }
        }

        private static void Replace(string temporary, string file)
        {
            if (File.Exists(file))
            {
                File.Replace(temporary, file, null);
            }
            else
            {
                File.Move(temporary, file);
            }
        }

        // The engine's references look like "wallet:{recordId}:mnemonic", which is not a file name.
        // Hashing keeps the mapping stable and total without having to escape anything.
        private string FileFor(string key)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));

            var builder = new StringBuilder(hash.Length * 2);
            foreach (var value in hash)
            {
                builder.Append(value.ToString("x2"));
            }

            return Path.Combine(_path, builder.ToString() + _suffix + ".secret");
        }
    }
}
