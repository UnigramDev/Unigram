//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography.DataProtection;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// How the wallet's data encryption key is kept, and what the user has to produce to use it.
    /// </summary>
    public enum WalletVaultMethod
    {
        /// <summary>Nothing is asked for. The key is at rest under DPAPI alone.</summary>
        None,

        /// <summary>The app's own passcode, which is also what unlocks the app.</summary>
        Passcode,

        /// <summary>Windows Hello, against a key the TPM holds and only Hello releases.</summary>
        Hello
    }

    /// <summary>
    /// Why the enrollment screen is being shown. All it changes is the wording of the passcode
    /// option, which is the one that reads differently when the passcode is on its way out.
    /// </summary>
    public enum WalletEnrollReason
    {
        /// <summary>The user asked to choose again.</summary>
        Change,

        /// <summary>
        /// The app passcode is being turned off and this wallet is guarded by it, so the passcode
        /// option means "leave it on" rather than "start using it".
        /// </summary>
        PasscodeDisabled
    }

    /// <summary>
    /// Why the vault could not be opened.
    /// </summary>
    public enum WalletVaultFailure
    {
        /// <summary>The user dismissed the prompt.</summary>
        Cancelled,

        /// <summary>What was produced does not open it - a wrong passcode.</summary>
        Incorrect,

        /// <summary>
        /// The method itself is gone: Hello removed, its key destroyed, the passcode disabled
        /// behind our back. The data encryption key cannot be recovered and the wallet has to be
        /// bound again - which costs nothing, because the phrase is in the cloud or on paper.
        /// </summary>
        Unenrolled,

        /// <summary>Nothing has been enrolled yet.</summary>
        Empty,

        Other
    }

    public sealed class WalletVaultException : Exception
    {
        public WalletVaultException(WalletVaultFailure failure, string diagnostic = null)
            : base(failure + (diagnostic != null ? ": " + diagnostic : string.Empty))
        {
            Failure = failure;
        }

        public WalletVaultFailure Failure { get; }
    }

    /// <summary>
    /// What the user picked on the enrollment screen, and the passcode they set there if they had
    /// none before.
    /// </summary>
    public sealed record WalletVaultChoice(WalletVaultMethod Method, string Passcode = null);

    /// <summary>
    /// How the vault asks the user for what it needs.
    /// </summary>
    /// <remarks>
    /// Called from the engine's threads, never the UI one, so an implementation has to marshal
    /// before it shows anything. Both answers are null when the user backed out, which is a refusal
    /// to spend rather than an error.
    ///
    /// Every question names the window it belongs in. It is the one the user started the operation
    /// from, handed down from the caller - never whichever window happens to be focused when the
    /// engine gets round to asking.
    /// </remarks>
    public interface IWalletVaultPrompt
    {
        /// <summary>
        /// The three-option screen: shown the first time there is anything to protect, and again
        /// whenever the user asks to change the choice.
        /// </summary>
        /// <param name="current">
        /// What guards the wallet now, so the screen opens on the answer they already gave. Null
        /// when nothing has been chosen yet.
        /// </param>
        Task<WalletVaultChoice> RequestEnrollmentAsync(INavigationService navigation, WalletVaultMethod? current, WalletEnrollReason reason);

        /// <summary>
        /// The app passcode, for a vault guarded by it.
        /// </summary>
        /// <param name="reason">
        /// What the engine is about to do, as it described it. Worth showing: the prompt appears
        /// at the moment of a spend and the user should see which one.
        /// </param>
        Task<string> RequestPasscodeAsync(INavigationService navigation, string reason);

        /// <summary>
        /// Windows Hello, inside the app's own popup.
        /// </summary>
        /// <remarks>
        /// Windows raises its PIN window over whatever is on screen and says nothing about who
        /// asked or what for, so the app puts a popup behind it that does.
        ///
        /// <paramref name="unlock"/> is what raises that window. It runs as soon as the popup is
        /// up, and again for every retry; it throws <see cref="WalletVaultException"/>, which is
        /// what the popup words for the user. Returning without having run it to completion is how
        /// the popup says the user gave up - except after a failure, which it should rethrow: an
        /// <see cref="WalletVaultFailure.Unenrolled"/> arriving as a plain cancel reads as a
        /// refusal to spend, and the wallet would sit there instead of binding again.
        /// </remarks>
        Task RequestHelloAsync(INavigationService navigation, string reason, Func<Task> unlock);
    }

    /// <summary>
    /// The wallet's data encryption key, and the one thing that guards it.
    /// </summary>
    /// <remarks>
    /// **The key itself never changes.** Everything the wallet stores is encrypted with it, so
    /// changing how it is guarded - enrolling Hello, setting a passcode, choosing to be asked
    /// nothing - rewraps 32 bytes and leaves the data alone. Re-encrypting the data on every
    /// passcode change would be the alternative, and it is both slower and a chance to lose it.
    ///
    /// Exactly one method guards it at a time, which is what the enrollment screen asks. They are
    /// alternatives rather than layers: two of them would mean the weaker one decides.
    ///
    /// DPAPI is underneath all three, always. It costs nothing, it needs nothing from the user,
    /// and it is what stops the file being useful on another machine - which matters most for
    /// <see cref="WalletVaultMethod.Passcode"/>, where the secret above it may be four digits.
    /// </remarks>
    public sealed partial class WalletVault
    {
        // "TWV1", so a file from a future format is refused rather than misread.
        private const uint Magic = 0x31565754u;

        private const int KeyLength = 32;
        private const int SaltLength = 16;
        private const int NonceLength = 12;
        private const int TagLength = 16;

        // High enough to cost an attacker real time per guess on a four-digit code, low enough not
        // to be felt on the machines this runs on.
        private const int Iterations = 210_000;

        // The name the TPM key is filed under. One per account suffix, so two accounts on one
        // machine do not share a key.
        private const string HelloKeyPrefix = "Telegram.Wallet.";

        // One lease at a time. See LeaseAsync.
        private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);

        // Long enough to cover the rest of the action the enrollment happened inside, short enough
        // that it cannot be mistaken for the wallet staying unlocked.
        private static readonly TimeSpan ProvenFor = TimeSpan.FromMinutes(1);

        // The key, kept for a moment after the user enrolled. See ResolveAsync.
        private byte[] _proven;
        private DateTime _provenUntil;

        private readonly string _path;
        private readonly string _suffix;

        public WalletVault(string path, string suffix)
        {
            _path = path;
            _suffix = suffix ?? string.Empty;
        }

        /// <summary>
        /// How the user is asked. Set by the app layer; it holds no window of its own, and is told
        /// which one to ask in on every call.
        /// </summary>
        public IWalletVaultPrompt Prompt { get; set; }

        /// <summary>
        /// The key, while an operation holds a lease on it. Null otherwise.
        /// </summary>
        /// <remarks>
        /// Read by <see cref="WalletSecretStore"/> from the engine's threads, which is the whole
        /// reason it exists: the engine's host interface is generated, carries nothing of ours, and
        /// offers no way to tell which call a callback belongs to - so it cannot be handed a window
        /// to ask in. Unlocking ahead of the engine call removes the question instead of answering
        /// it badly.
        /// </remarks>
        internal byte[] Leased { get; private set; }

        /// <summary>
        /// A lease for one operation, which asks nothing until <see cref="WalletVaultLease.EnsureAsync"/>.
        /// </summary>
        /// <param name="navigation">
        /// The window to ask in - the one the user started this from. Always a parameter, because
        /// the wallet is reachable from every window in the app and there is no such thing as the
        /// current one.
        /// </param>
        /// <param name="reason">
        /// What the operation is, as the prompt should describe it.
        /// </param>
        public WalletVaultLease CreateLease(INavigationService navigation, string reason = null)
        {
            return new WalletVaultLease(() => Task.FromResult(this), navigation, reason);
        }

        /// <summary>
        /// A lease that has already asked, for the operations that must have the key before they
        /// do anything else.
        /// </summary>
        public async Task<WalletVaultLease> LeaseAsync(INavigationService navigation, string reason = null)
        {
            var lease = CreateLease(navigation, reason);

            try
            {
                await lease.EnsureAsync();
                return lease;
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        private async Task AcquireAsync(INavigationService navigation, string reason)
        {
            await _mutex.WaitAsync();

            try
            {
                Leased = await ResolveAsync(navigation, reason);
            }
            catch
            {
                // Nothing was leased, so no lease will come along to release the gate - and a
                // refused prompt would otherwise lock the wallet for the rest of the session.
                _mutex.Release();
                throw;
            }
        }

        private void Release()
        {
            Leased = null;
            _mutex.Release();
        }

        /// <summary>
        /// One operation's hold on the key, taken the first time something in it needs the key.
        /// Disposing it puts the vault back to asking, and lets the next operation through.
        /// </summary>
        /// <remarks>
        /// One lease per operation, and a prompt per lease: the engine reads a secret when it is
        /// about to sign, so an operation is a spend, and "confirm all spending" only means
        /// something if each one asks. Nothing is cached between leases. The operation creates it,
        /// passes it to everything it calls, and disposes it - nothing it calls takes a lease of
        /// its own, which is what makes waiting on itself impossible.
        ///
        /// Inflated before the work is handed to the engine, on the thread that started it, so
        /// that no prompt ever has to be raised from an engine thread with no window in mind.
        ///
        /// **Inflated leases are serialized.** A second operation waits in
        /// <see cref="EnsureAsync"/> for the first to finish and then asks on its own account - it
        /// never rides on a confirmation the user gave for something else. Whatever the operation
        /// checked before inflating may have changed while it waited, so state checks belong after.
        ///
        /// A refusal leaves the lease as it was. What a "no" means is the operation's to decide -
        /// declining an optional extra is not declining the whole - and a later
        /// <see cref="EnsureAsync"/> asks again.
        /// </remarks>
        public sealed class WalletVaultLease : IDisposable
        {
            private readonly Func<Task<WalletVault>> _resolve;
            private readonly INavigationService _navigation;
            private readonly string _reason;

            private WalletVault _vault;
            private Task _inflating;
            private bool _disposed;

            internal WalletVaultLease(Func<Task<WalletVault>> resolve, INavigationService navigation, string reason)
            {
                _resolve = resolve;
                _navigation = navigation;
                _reason = reason;
            }

            public bool IsInflated => _vault != null;

            /// <summary>
            /// The key itself, for the callers that work with it directly rather than through the
            /// engine - changing what guards it, or reading the phrase back.
            /// </summary>
            public byte[] Key => _vault?.Leased ?? throw new InvalidOperationException("The lease has not been inflated.");

            /// <summary>
            /// Asks for the key if this operation has not yet, and returns at once if it has.
            /// </summary>
            /// <remarks>
            /// Throws <see cref="WalletVaultException"/> when the user declines or the vault cannot
            /// be opened.
            /// </remarks>
            public Task EnsureAsync()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(WalletVaultLease));
                }

                if (_vault != null)
                {
                    return Task.CompletedTask;
                }

                // A second inflation in flight would wait on the gate this one is about to take,
                // and nothing would ever release it.
                var inflating = _inflating;
                if (inflating == null || inflating.IsFaulted || inflating.IsCanceled)
                {
                    _inflating = inflating = InflateAsync();
                }

                return inflating;
            }

            private async Task InflateAsync()
            {
                var vault = await _resolve();
                if (vault == null)
                {
                    throw new WalletVaultException(WalletVaultFailure.Empty, "no vault");
                }

                await vault.AcquireAsync(_navigation, _reason);

                if (_disposed)
                {
                    // Disposed while the prompt was up: the operation is gone, so the gate must not
                    // stay with it.
                    vault.Release();
                    throw new ObjectDisposedException(nameof(WalletVaultLease));
                }

                _vault = vault;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                // Releasing twice would hand the gate to two operations at once, and each would
                // believe the user had confirmed it.
                var vault = _vault;
                if (vault != null)
                {
                    _vault = null;
                    vault.Release();
                }
            }
        }

        private string File => Path.Combine(_path, "vault" + _suffix + ".bin");

        private string HelloKeyName => HelloKeyPrefix + _suffix;

        /// <summary>
        /// What the user would be asked for, without asking them.
        /// </summary>
        public WalletVaultMethod? Method
        {
            get
            {
                try
                {
                    return TryRead(out var method, out _, out _, out _) ? method : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public bool IsEnrolled => Method != null;

        /// <summary>
        /// Opens the vault, asking for whatever the enrolled method requires.
        /// </summary>
        /// <param name="passcode">
        /// The passcode, where that is the method. Ignored otherwise, and the caller is expected to
        /// have read <see cref="Method"/> to know whether to collect one.
        /// </param>
        public async Task<byte[]> OpenAsync(string passcode = null)
        {
            if (!TryRead(out var method, out var salt, out var wrapped, out _))
            {
                throw new WalletVaultException(WalletVaultFailure.Empty);
            }

            var unprotected = await UnprotectAsync(wrapped);

            return method switch
            {
                WalletVaultMethod.None => unprotected,
                WalletVaultMethod.Passcode => Unwrap(unprotected, Derive(passcode, salt)),
                WalletVaultMethod.Hello => Unwrap(unprotected, await HelloKeyAsync(salt, false)),
                _ => throw new WalletVaultException(WalletVaultFailure.Other, "unknown method")
            };
        }

        /// <summary>
        /// The data encryption key, asking for whatever the enrolled method requires - and asking
        /// the user to pick one, the first time there is anything to protect.
        /// </summary>
        /// <param name="reason">
        /// What the key is wanted for, to be shown in the passcode prompt.
        /// </param>
        /// <remarks>
        /// Deliberately never cached. The engine reads a secret when it is about to sign, so every
        /// call here is a spend, and "confirm all spending" only means something if each one asks.
        /// <see cref="WalletVaultMethod.None"/> asks nothing, which is the whole of what choosing
        /// it buys.
        /// </remarks>
        /// <remarks>
        /// **Enrolling counts as the proof.** Choosing how the wallet is guarded and producing the
        /// credential right then is a confirmation in itself, and the operation that caused the
        /// enrollment would otherwise ask for the same credential again a second later - which is
        /// what the user has just done. So a key created here is kept for
        /// <see cref="ProvenFor"/> and the next lease reuses it.
        ///
        /// Only enrolling grants it. An ordinary unlock does not, because there the prompt *is*
        /// the confirmation of that particular spend, and the window never extends itself: it is
        /// measured from the enrollment, not from the last time it was used.
        /// </remarks>
        private async Task<byte[]> ResolveAsync(INavigationService navigation, string reason)
        {
            if (_proven != null)
            {
                if (DateTime.UtcNow < _provenUntil)
                {
                    return _proven;
                }

                _proven = null;
            }

            if (!TryRead(out var method, out _, out _, out _))
            {
                // Nobody named a window, so there is nobody to ask - and enrolling anything
                // without asking would be choosing on the user's behalf how their money is
                // guarded.
                if (navigation == null || Prompt == null)
                {
                    throw new WalletVaultException(WalletVaultFailure.Empty, "no window to enroll in");
                }

                var choice = await Prompt.RequestEnrollmentAsync(navigation, null, WalletEnrollReason.Change);
                if (choice == null)
                {
                    throw new WalletVaultException(WalletVaultFailure.Cancelled);
                }

                var created = choice.Method == WalletVaultMethod.Hello
                    ? await ThroughHelloAsync(navigation, reason, () => CreateAsync(choice.Method))
                    : await CreateAsync(choice.Method, choice.Passcode);

                // Windows asks once to create the credential and again to sign with it, so an
                // enrollment has already cost the user two prompts before this returns.
                _proven = created;
                _provenUntil = DateTime.UtcNow + ProvenFor;

                return created;
            }

            if (method == WalletVaultMethod.None)
            {
                // The user chose to be asked nothing, so there is nothing a window would be
                // for. This is the one path that works without one.
                return await OpenAsync();
            }

            if (navigation == null || Prompt == null)
            {
                throw new WalletVaultException(WalletVaultFailure.Cancelled, "no window to ask in");
            }

            if (method == WalletVaultMethod.Hello)
            {
                return await ThroughHelloAsync(navigation, reason, () => OpenAsync());
            }

            var passcode = await Prompt.RequestPasscodeAsync(navigation, reason);
            if (passcode == null)
            {
                throw new WalletVaultException(WalletVaultFailure.Cancelled);
            }

            return await OpenAsync(passcode);
        }

        /// <summary>
        /// Creates the key, the first time there is anything to protect.
        /// </summary>
        public async Task<byte[]> CreateAsync(WalletVaultMethod method, string passcode = null)
        {
            CryptographicBuffer.CopyToByteArray(CryptographicBuffer.GenerateRandom(KeyLength), out var key);

            await StoreAsync(key, method, passcode);
            return key;
        }

        /// <summary>
        /// Asks the user to choose how the wallet is guarded, proving what guards it now first.
        /// </summary>
        /// <remarks>
        /// Opening the vault is the proof, and it is asked for **after** the choice, not before.
        /// Looking at how the wallet is guarded is not itself a privileged act; changing it is. So
        /// the screen opens on the current setting for nothing, and the credential is wanted only
        /// once there is something to write - which is still before anything is written, so
        /// somebody who cannot spend still cannot turn the asking off.
        ///
        /// Answers the method the vault is left under, or null when the user backed out.
        /// </remarks>
        public async Task<WalletVaultMethod?> ReenrollAsync(INavigationService navigation, WalletEnrollReason reason = WalletEnrollReason.Change)
        {
            if (Prompt == null)
            {
                return null;
            }

            if (!IsEnrolled)
            {
                // Nothing to prove, and nothing to change: the first lease is itself the
                // enrollment, and asking twice would be asking the same question twice.
                using var first = await LeaseAsync(navigation);
                return Method;
            }

            var current = Method;

            var choice = await Prompt.RequestEnrollmentAsync(navigation, current, reason);
            if (choice == null)
            {
                return null;
            }

            if (choice.Method == current)
            {
                // Saved without changing anything - including *keep asking for this passcode*,
                // which is a refusal to disable it rather than a change to the vault. Nothing is
                // rewritten, so there is nothing to prove.
                return current;
            }

            using var lease = await LeaseAsync(navigation);

            await ChangeAsync(navigation, lease.Key, choice.Method, choice.Passcode);
            return choice.Method;
        }

        /// <summary>
        /// The same, against a vault the caller has already opened.
        /// </summary>
        /// <remarks>
        /// Holding a lease is the proof, so nothing is asked for twice: a caller that opened the
        /// vault to get here - changing the app passcode, say - would otherwise have the user
        /// produce the same credential again a moment later.
        /// </remarks>
        public async Task<WalletVaultMethod?> ReenrollAsync(INavigationService navigation, WalletVaultLease lease, WalletEnrollReason reason)
        {
            if (Prompt == null)
            {
                return null;
            }

            var current = Method;

            var choice = await Prompt.RequestEnrollmentAsync(navigation, current, reason);
            if (choice == null)
            {
                return null;
            }

            if (choice.Method == current)
            {
                // Nothing to rewrite. This is also the answer that stops the app passcode being
                // disabled, and it arrives carrying no passcode - so writing it would wrap the key
                // under nothing at all.
                return current;
            }

            await lease.EnsureAsync();
            await ChangeAsync(navigation, lease.Key, choice.Method, choice.Passcode);
            return choice.Method;
        }

        /// <summary>
        /// Puts the same key behind a different method. The caller has already opened the vault,
        /// which is how the current credential was proved.
        /// </summary>
        public async Task ChangeAsync(INavigationService navigation, byte[] key, WalletVaultMethod method, string passcode = null)
        {
            if (method == WalletVaultMethod.Hello)
            {
                await ThroughHelloAsync(navigation, null, async () =>
                {
                    await StoreAsync(key, method, passcode);
                    return key;
                });
            }
            else
            {
                await StoreAsync(key, method, passcode);
            }
        }

        /// <summary>
        /// Runs the step that raises the Windows Hello window, with the app's own popup behind it.
        /// </summary>
        /// <remarks>
        /// Safe to retry, which is the point: nothing here writes the vault until after Hello has
        /// answered, so an attempt that fails leaves no trace and the popup can simply run it
        /// again.
        /// </remarks>
        private async Task<byte[]> ThroughHelloAsync(INavigationService navigation, string reason, Func<Task<byte[]>> operation)
        {
            if (navigation == null || Prompt == null)
            {
                // Windows would still raise its own window, but with nothing of ours behind it
                // saying who asked. Better to refuse than to show the user a bare PIN prompt they
                // cannot place.
                throw new WalletVaultException(WalletVaultFailure.Cancelled, "no window to ask in");
            }

            byte[] key = null;
            await Prompt.RequestHelloAsync(navigation, reason, async () => key = await operation());

            if (key == null)
            {
                throw new WalletVaultException(WalletVaultFailure.Cancelled);
            }

            return key;
        }

        public void Delete()
        {
            _proven = null;

            try
            {
                if (System.IO.File.Exists(File))
                {
                    System.IO.File.Delete(File);
                }

                // The TPM key outlives the file otherwise, and the next enrollment would find a key
                // it cannot match to anything.
                _ = KeyCredentialManager.DeleteAsync(HelloKeyName);
            }
            catch
            {
                // Nothing to do about it, and nothing depends on it: a key with no vault beside it
                // opens nothing.
            }
        }

        private async Task StoreAsync(byte[] key, WalletVaultMethod method, string passcode)
        {
            CryptographicBuffer.CopyToByteArray(CryptographicBuffer.GenerateRandom(SaltLength), out var salt);

            var wrapped = method switch
            {
                WalletVaultMethod.None => key,
                WalletVaultMethod.Passcode => Encrypt(key, Derive(passcode, salt)),
                WalletVaultMethod.Hello => Encrypt(key, await HelloKeyAsync(salt, true)),
                _ => throw new WalletVaultException(WalletVaultFailure.Other, "unknown method")
            };

            var body = await ProtectAsync(wrapped);

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write((int)method);
                writer.Write(salt.Length);
                writer.Write(salt);
                writer.Write(body.Length);
                writer.Write(body);
            }

            Directory.CreateDirectory(_path);

            // Written aside and moved into place: a half-written vault is a wallet that has to be
            // bound again, and the move is what makes that impossible.
            var temporary = File + ".tmp";

            System.IO.File.WriteAllBytes(temporary, stream.ToArray());
            Replace(temporary, File);
        }

        private bool TryRead(out WalletVaultMethod method, out byte[] salt, out byte[] wrapped, out int version)
        {
            method = WalletVaultMethod.None;
            salt = null;
            wrapped = null;
            version = 0;

            if (!System.IO.File.Exists(File))
            {
                return false;
            }

            using var stream = System.IO.File.OpenRead(File);
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8);

            if (reader.ReadUInt32() != Magic)
            {
                return false;
            }

            method = (WalletVaultMethod)reader.ReadInt32();
            salt = reader.ReadBytes(reader.ReadInt32());
            wrapped = reader.ReadBytes(reader.ReadInt32());

            return true;
        }

        #region Wrapping

        /// <summary>
        /// AES-GCM, which is what makes a wrong passcode answer "wrong" rather than 32 bytes of
        /// nonsense the engine would then fail to make sense of.
        /// </summary>
        /// <remarks>
        /// Through WinRT rather than <c>System.Security.Cryptography.AesGcm</c>: the legacy UWP
        /// flavour compiles these same sources and has neither that type nor the span-based
        /// helpers beside it.
        ///
        /// Shared with <see cref="WalletSecretStore"/>, which encrypts the secrets themselves with
        /// the key this wraps. One implementation, so the two cannot drift into two formats.
        /// </remarks>
        internal static byte[] Encrypt(byte[] value, byte[] key)
        {
            var algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesGcm);
            var material = algorithm.CreateSymmetricKey(CryptographicBuffer.CreateFromByteArray(key));

            var nonce = CryptographicBuffer.GenerateRandom(NonceLength);
            var encrypted = CryptographicEngine.EncryptAndAuthenticate(
                material, CryptographicBuffer.CreateFromByteArray(value), nonce, null);

            CryptographicBuffer.CopyToByteArray(nonce, out var nonceBytes);
            CryptographicBuffer.CopyToByteArray(encrypted.EncryptedData, out var cipherBytes);
            CryptographicBuffer.CopyToByteArray(encrypted.AuthenticationTag, out var tagBytes);

            var result = new byte[nonceBytes.Length + tagBytes.Length + cipherBytes.Length];

            Buffer.BlockCopy(nonceBytes, 0, result, 0, nonceBytes.Length);
            Buffer.BlockCopy(tagBytes, 0, result, nonceBytes.Length, tagBytes.Length);
            Buffer.BlockCopy(cipherBytes, 0, result, nonceBytes.Length + tagBytes.Length, cipherBytes.Length);

            return result;
        }

        /// <summary>
        /// The other half. False rather than an exception, because the two callers report a failed
        /// tag differently: here it is a wrong passcode, in the secret store it is an entry written
        /// under a key this device no longer has.
        /// </summary>
        internal static bool TryDecrypt(byte[] encrypted, byte[] key, out byte[] value)
        {
            value = null;

            if (encrypted == null || encrypted.Length <= NonceLength + TagLength)
            {
                return false;
            }

            var nonce = new byte[NonceLength];
            var tag = new byte[TagLength];
            var cipher = new byte[encrypted.Length - NonceLength - TagLength];

            Buffer.BlockCopy(encrypted, 0, nonce, 0, nonce.Length);
            Buffer.BlockCopy(encrypted, nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(encrypted, nonce.Length + tag.Length, cipher, 0, cipher.Length);

            try
            {
                var algorithm = SymmetricKeyAlgorithmProvider.OpenAlgorithm(SymmetricAlgorithmNames.AesGcm);
                var material = algorithm.CreateSymmetricKey(CryptographicBuffer.CreateFromByteArray(key));

                var buffer = CryptographicEngine.DecryptAndAuthenticate(material,
                    CryptographicBuffer.CreateFromByteArray(cipher),
                    CryptographicBuffer.CreateFromByteArray(nonce),
                    CryptographicBuffer.CreateFromByteArray(tag), null);

                CryptographicBuffer.CopyToByteArray(buffer, out value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] Unwrap(byte[] wrapped, byte[] kek)
        {
            if (!TryDecrypt(wrapped, kek, out var key))
            {
                // The tag did not check out, which for a passcode means they typed it wrong.
                throw new WalletVaultException(WalletVaultFailure.Incorrect);
            }

            return key;
        }

        /// <summary>
        /// The key a passcode stands for.
        /// </summary>
        /// <remarks>
        /// PBKDF2 with its own salt, and deliberately not the SHA-1 the app compares the passcode
        /// against: that one is a verifier, its cost is meant to be nothing, and a four-digit code
        /// run through it once would fall in no time at all.
        /// </remarks>
        private static byte[] Derive(string passcode, byte[] salt)
        {
            if (string.IsNullOrEmpty(passcode))
            {
                throw new WalletVaultException(WalletVaultFailure.Incorrect, "no passcode supplied");
            }

            var provider = KeyDerivationAlgorithmProvider.OpenAlgorithm(KeyDerivationAlgorithmNames.Pbkdf2Sha256);
            var material = provider.CreateKey(CryptographicBuffer.ConvertStringToBinary(passcode, BinaryStringEncoding.Utf8));

            var parameters = KeyDerivationParameters.BuildForPbkdf2(CryptographicBuffer.CreateFromByteArray(salt), Iterations);
            var derived = CryptographicEngine.DeriveKeyMaterial(material, parameters, KeyLength);

            CryptographicBuffer.CopyToByteArray(derived, out var key);
            return key;
        }

        /// <summary>
        /// The key Windows Hello stands for: a signature the TPM computes, which it will only do
        /// once Hello has been satisfied.
        /// </summary>
        /// <remarks>
        /// The private half never leaves the TPM, so this is a key rather than a prompt - unlike
        /// <c>UserConsentVerifier</c>, which asks and then trusts the caller to have meant it.
        ///
        /// The salt is signed rather than stored: the signature over it is stable for the life of
        /// the key, so it reproduces the same KEK every time, and nothing derived from it is
        /// written down.
        /// </remarks>
        private async Task<byte[]> HelloKeyAsync(byte[] salt, bool create)
        {
            KeyCredentialRetrievalResult result;

            try
            {
                result = await KeyCredentialManager.OpenAsync(HelloKeyName);

                if (result.Status == KeyCredentialStatus.NotFound && create)
                {
                    result = await KeyCredentialManager.RequestCreateAsync(HelloKeyName, KeyCredentialCreationOption.ReplaceExisting);
                }
            }
            catch (Exception ex)
            {
                throw new WalletVaultException(WalletVaultFailure.Other, ex.GetType().Name);
            }

            if (result.Status == KeyCredentialStatus.UserCanceled)
            {
                throw new WalletVaultException(WalletVaultFailure.Cancelled);
            }
            else if (result.Status != KeyCredentialStatus.Success)
            {
                // NotFound once it was there, or the device no longer offers Hello at all: the key
                // is gone and so is the way in.
                throw new WalletVaultException(WalletVaultFailure.Unenrolled, result.Status.ToString());
            }

            var signed = await result.Credential.RequestSignAsync(CryptographicBuffer.CreateFromByteArray(salt));

            if (signed.Status == KeyCredentialStatus.UserCanceled)
            {
                throw new WalletVaultException(WalletVaultFailure.Cancelled);
            }
            else if (signed.Status != KeyCredentialStatus.Success)
            {
                throw new WalletVaultException(WalletVaultFailure.Unenrolled, signed.Status.ToString());
            }

            // The signature is long and not uniform; the KEK has to be 32 bytes and uniform.
            var provider = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);
            var hashed = provider.HashData(signed.Result);

            CryptographicBuffer.CopyToByteArray(hashed, out var key);
            return key;
        }

        #endregion

        #region DPAPI

        // Under everything, always: it needs nothing from the user and it is what keeps the file
        // from being worth copying to another machine.
        private const string Descriptor = "LOCAL=user";

        private static async Task<byte[]> ProtectAsync(byte[] value)
        {
            var provider = new DataProtectionProvider(Descriptor);
            var buffer = await provider.ProtectAsync(CryptographicBuffer.CreateFromByteArray(value));

            CryptographicBuffer.CopyToByteArray(buffer, out var result);
            return result;
        }

        private static async Task<byte[]> UnprotectAsync(byte[] value)
        {
            try
            {
                var provider = new DataProtectionProvider();
                var buffer = await provider.UnprotectAsync(CryptographicBuffer.CreateFromByteArray(value));

                CryptographicBuffer.CopyToByteArray(buffer, out var result);
                return result;
            }
            catch (Exception ex)
            {
                // A vault from another Windows account, or a profile that has been rebuilt.
                throw new WalletVaultException(WalletVaultFailure.Unenrolled, ex.GetType().Name);
            }
        }

        #endregion

        private static void Replace(string temporary, string file)
        {
            if (System.IO.File.Exists(file))
            {
                System.IO.File.Replace(temporary, file, null);
            }
            else
            {
                System.IO.File.Move(temporary, file);
            }
        }
    }
}
