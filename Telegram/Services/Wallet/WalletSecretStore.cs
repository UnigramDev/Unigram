//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// Stores wallet secrets under the vault's key, one file per entry.
    /// </summary>
    /// <remarks>
    /// The whole of the policy lives in <see cref="WalletVault"/>: what the user has to produce to
    /// read a secret is what they chose on the enrollment screen, and there is nothing recorded
    /// here to disagree with it. An earlier version wrote a presence byte per entry and verified it
    /// with <c>UserConsentVerifier</c>, which was both weaker - a consent prompt guards nothing a
    /// caller cannot skip - and a way to end up with entries a device could no longer satisfy.
    ///
    /// One entry per file rather than one shared blob, so writing a secret never rewrites - and so
    /// never risks losing - another.
    /// </remarks>
    public sealed class WalletSecretStore : IProtectedSecretStore
    {
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        private readonly WalletVault _vault;
        private readonly string _path;
        private readonly string _suffix;

        /// <param name="suffix">
        /// Distinguishes test-server entries from production ones, which share a folder because
        /// they share a session id. A wallet must never read the wrong network's key.
        /// </param>
        public WalletSecretStore(WalletVault vault, string path, string suffix)
        {
            _vault = vault ?? throw new ArgumentNullException(nameof(vault));
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
                var vaultKey = Leased();

                if (!WalletVault.TryDecrypt(stored, vaultKey, out var secret))
                {
                    // The vault opened but does not open this: the entry belongs to a key that is
                    // gone. Nothing here recovers it, and returning anything at all would hand the
                    // engine bytes that are not the mnemonic - which it would derive a wallet from.
                    throw new ProtectedSecretException(ProtectedSecretFailure.PolicyViolation, "the stored secret does not belong to this vault");
                }

                return secret;
            }
            catch (WalletVaultException ex)
            {
                throw Translate(ex);
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

        public async Task WriteAsync(string key, byte[] secret)
        {
            await _lock.WaitAsync();
            try
            {
                var stored = WalletVault.Encrypt(secret, Leased());

                Directory.CreateDirectory(_path);

                // Written aside and moved into place: a half-written secret file is unrecoverable,
                // and the wallet it belongs to would be unrecoverable with it.
                var file = FileFor(key);
                var temporary = file + ".tmp";

                File.WriteAllBytes(temporary, stored);
                Replace(temporary, file);
            }
            catch (WalletVaultException ex)
            {
                throw Translate(ex);
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

        /// <summary>
        /// The key of the operation this callback belongs to.
        /// </summary>
        /// <remarks>
        /// Nothing here can ask the user for anything: the engine calls this from its own threads,
        /// and it is the operation - started in a window, by someone holding that window's
        /// navigation service - that unlocked the vault before handing the work over. An engine
        /// callback arriving with no lease open is a wallet operation that forgot to take one, and
        /// it fails rather than guessing which window to interrupt.
        /// </remarks>
        private byte[] Leased()
        {
            return _vault.Leased
                ?? throw new ProtectedSecretException(ProtectedSecretFailure.PolicyViolation, "the vault was not unlocked for this operation");
        }

        /// <summary>
        /// Every way the vault can fail, said in the engine's terms.
        /// </summary>
        /// <remarks>
        /// Everything that is not the user saying no becomes <see cref="ProtectedSecretFailure.PolicyViolation"/>,
        /// which is what the app treats as "this device is no longer bound" and re-acquires from.
        /// The alternative - reporting it as unavailable - leaves the wallet stuck on a device it
        /// could recover on in a couple of seconds.
        /// </remarks>
        private static ProtectedSecretException Translate(WalletVaultException ex)
        {
            var failure = ex.Failure switch
            {
                WalletVaultFailure.Cancelled => ProtectedSecretFailure.Cancelled,
                WalletVaultFailure.Incorrect => ProtectedSecretFailure.AuthenticationFailed,
                _ => ProtectedSecretFailure.PolicyViolation
            };

            return new ProtectedSecretException(failure, ex.Message);
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
            var provider = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
            var hashed = provider.HashData(CryptographicBuffer.ConvertStringToBinary(key, BinaryStringEncoding.Utf8));

            CryptographicBuffer.CopyToByteArray(hashed, out var hash);

            var builder = new StringBuilder(hash.Length * 2);
            foreach (var value in hash)
            {
                builder.Append(value.ToString("x2"));
            }

            return Path.Combine(_path, builder.ToString() + _suffix + ".secret");
        }
    }
}
