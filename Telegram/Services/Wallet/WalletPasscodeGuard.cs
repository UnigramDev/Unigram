//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Navigation.Services;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// The wallets guarded by the app passcode, while it is being changed or turned off.
    /// </summary>
    /// <remarks>
    /// The passcode is one setting for the whole app, but every account has a vault of its own, so
    /// a change has to reach all of them: a vault left wrapped under the old passcode cannot be
    /// opened by the new one, and that wallet would have to be bound again.
    ///
    /// The user is asked for the passcode once per account that needs it, which on one account -
    /// the normal case - is once.
    /// </remarks>
    public sealed class WalletPasscodeGuard : IDisposable
    {
        private readonly List<Held> _held = new List<Held>();

        private WalletPasscodeGuard()
        {
        }

        private readonly struct Held
        {
            public Held(WalletVault vault, WalletVault.WalletVaultLease lease)
            {
                Vault = vault;
                Lease = lease;
            }

            public WalletVault Vault { get; }

            public WalletVault.WalletVaultLease Lease { get; }
        }

        /// <summary>
        /// Opens every vault the passcode guards, and holds them open.
        /// </summary>
        /// <remarks>
        /// Null when a vault could not be opened - the user declined, or got the passcode wrong -
        /// which is a reason not to change the passcode at all rather than one to carry on and
        /// leave that wallet behind. Anything already opened is released first.
        /// </remarks>
        public static async Task<WalletPasscodeGuard> OpenAsync(INavigationService navigation, ILifetimeService lifetime)
        {
            var guard = new WalletPasscodeGuard();

            try
            {
                foreach (var wallet in lifetime.ResolveAll<IWalletService>())
                {
                    // The vault is built on the first restore, so a session whose wallet has never
                    // been opened has to be asked before its method can be read at all.
                    await wallet.RestoreAsync();

                    var vault = wallet.Vault;
                    if (vault?.Method != WalletVaultMethod.Passcode)
                    {
                        continue;
                    }

                    guard._held.Add(new Held(vault, await vault.LeaseAsync(navigation)));
                }

                return guard;
            }
            catch (Exception)
            {
                guard.Dispose();
                return null;
            }
        }

        /// <summary>
        /// Rewraps every held vault under <paramref name="passcode"/>.
        /// </summary>
        /// <remarks>
        /// Before the app passcode itself is changed, deliberately: a vault that cannot be
        /// rewrapped stops the change while everything still agrees, where the other order would
        /// leave the wallet asking for a passcode that no longer exists.
        /// </remarks>
        public async Task RewrapAsync(INavigationService navigation, string passcode)
        {
            foreach (var held in _held)
            {
                await held.Vault.ChangeAsync(navigation, held.Lease.Key, WalletVaultMethod.Passcode, passcode);
            }
        }

        /// <summary>
        /// Asks what should guard each held wallet now that the passcode is going away, and
        /// answers whether it may.
        /// </summary>
        /// <remarks>
        /// False when any of them came back still on the passcode - the user choosing *keep asking
        /// for this passcode*, or backing out. One wallet is enough: the passcode cannot be half
        /// turned off.
        /// </remarks>
        public async Task<bool> ReleaseAsync(INavigationService navigation)
        {
            foreach (var held in _held)
            {
                // Against the lease this guard already holds, so the user is not asked for the
                // passcode a second time to answer a question about it going away.
                var method = await held.Vault.ReenrollAsync(navigation, held.Lease, WalletEnrollReason.PasscodeDisabled);
                if (method is null or WalletVaultMethod.Passcode)
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            foreach (var held in _held)
            {
                held.Lease.Dispose();
            }

            _held.Clear();
        }
    }
}
