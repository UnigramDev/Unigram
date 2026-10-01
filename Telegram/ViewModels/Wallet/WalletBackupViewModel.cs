//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Wallet;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Wallet.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Wallet
{
    public class WalletBackupViewModel : ViewModelBase
    {
        private readonly IWalletService _wallet;

        public WalletBackupViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IWalletService wallet)
            : base(clientService, settingsService, aggregator)
        {
            _wallet = wallet;
        }

        //protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        //{
        //    var wallet = await ClientService.Wallet.WalletAsync();
        //    if (wallet == null)
        //    {
        //        // ???
        //        return;
        //    }

        //    Address = wallet.Address;
        //    Balance = await wallet.BalanceAsync();
        //}

        //private string _address;
        //public string Address
        //{
        //    get => _address;
        //    set => Set(ref _address, value);
        //}

        //private TonAmount _balance;
        //public TonAmount Balance
        //{
        //    get => _balance;
        //    set => Set(ref _balance, value);
        //}

        public async void ShowRecoveryPhrase()
        {
            var confirm = await ShowPopupAsync(new WalletRecoveryInfoPopup());
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var words = await RequestRecoveryPhraseAsync();
            if (words != null)
            {
                await ShowPopupAsync(new WalletPhrasePopup(words));
            }
        }

        /// <summary>
        /// The recovery phrase, once this device is allowed to have it.
        /// </summary>
        /// <remarks>
        /// Binding is what the account password is for, and it happens once; reading the phrase
        /// back afterwards is the device's own prompt. Null when either was refused, or when there
        /// is no way to get the key onto this device - all of which say so for themselves.
        /// </remarks>
        private async Task<IReadOnlyList<string>> RequestRecoveryPhraseAsync()
        {
            if (!await WalletHelper.EnsureBoundAsync(ClientService, _wallet, NavigationService))
            {
                return null;
            }

            try
            {
                return await _wallet.RevealRecoveryPhraseAsync(NavigationService);
            }
            catch (WalletAccessDeniedException)
            {
                // They were asked and declined.
                return null;
            }
        }

        /// <summary>
        /// Puts the recovery phrase back in the Telegram cloud.
        /// </summary>
        /// <remarks>
        /// The phrase has to come from this device, because the server is being asked to store one
        /// it does not have. One lease covers the bind, the read and the proof - and the account
        /// password is no longer part of it, since the account now takes a signature over its own
        /// challenge instead.
        /// </remarks>
        public async void EnableBackup()
        {
            using var lease = await RequestLeaseAsync();
            if (lease == null)
            {
                return;
            }

            var bound = await WalletHelper.BindAsync(ClientService, _wallet, NavigationService, lease);
            if (!bound.IsBound)
            {
                return;
            }

            IReadOnlyList<string> words;

            try
            {
                words = await _wallet.RevealRecoveryPhraseAsync(NavigationService, lease);
            }
            catch (WalletAccessDeniedException)
            {
                return;
            }

            if (words == null)
            {
                // Bound a moment ago, so this is storage refusing rather than a device that never
                // had the phrase. Nothing to upload and nothing useful to say about why.
                NavigationService.ShowToast("[The backup could not be enabled.]", ToastPopupIcon.Error);
                return;
            }

            try
            {
                await _wallet.EnableBackupAsync(NavigationService, words, lease);
                NavigationService.ShowToast(Toast(Strings.WalletBackupEnabled, Strings.WalletBackupEnabledInfo), ToastPopupIcon.Success);
            }
            catch (Exception ex)
            {
                Logger.Error("wallet backup could not be enabled: " + ex.Message);
                NavigationService.ShowToast("[The backup could not be enabled.]", ToastPopupIcon.Error);
            }
        }

        /// <summary>
        /// The two-line toast the wallet uses: a bold heading and a sentence under it.
        /// </summary>
        private static string Toast(string title, string message)
        {
            return string.Format("**{0}**\n{1}", title, message);
        }

        public async void DisableBackup()
        {
            // One confirmation for the whole operation, collected before the popup. Binding this
            // device, pricing a phrase update and performing it all read the phrase, so the lease
            // is taken once and carried through. Disabling itself asks for nothing further: the
            // account takes a signature over its own challenge rather than the password.
            //
            // The vault is opened first, deliberately: binding writes the phrase into it, so a
            // lease taken afterwards would be a second prompt rather than the only one.
            using var lease = await RequestLeaseAsync();
            if (lease == null)
            {
                // Asked to confirm and said no - or the vault would not open. Either way, saying
                // no has to stop the thing that was said no to: carrying on to the popup reads as
                // the cancel having done nothing.
                return;
            }

            // With the vault already open, so the bind stores the phrase into it rather than
            // opening a second one and asking again.
            var bound = await WalletHelper.BindAsync(ClientService, _wallet, NavigationService, lease);
            if (!bound.IsBound)
            {
                // The account password was dismissed, or there is no wallet to bind to. Same
                // reasoning: nothing further should appear.
                return;
            }

            // Unlike the two above, a fee that cannot be worked out is not a refusal. The backup
            // can still be turned off; only the offer to replace the phrase goes away.
            var fee = await RequestRotationFeeAsync(lease);

            var popup = new WalletDisableBackupPopup(ClientService, _wallet, NavigationService, fee);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                await _wallet.DisableBackupAsync(NavigationService, lease);
            }
            catch (Exception ex)
            {
                Logger.Error("wallet backup could not be disabled: " + ex.Message);
                NavigationService.ShowToast("[The backup could not be disabled.]", ToastPopupIcon.Error);
                return;
            }

            if (!popup.IsPhraseUpdateRequested)
            {
                NavigationService.ShowToast(Toast(Strings.WalletBackupDisabled, Strings.WalletBackupDisabledInfo), ToastPopupIcon.Success);
                return;
            }

            await UpdateRecoveryPhraseAsync(lease);
        }

        /// <summary>
        /// Opens the vault for the whole of this operation, or answers null if it cannot be.
        /// </summary>
        /// <remarks>
        /// Null is not a failure: the phrase update is what needs a key, and everything else here -
        /// turning the backup off - works without one. It just will not be offered.
        /// </remarks>
        private async Task<WalletVault.WalletVaultLease> RequestLeaseAsync()
        {
            try
            {
                // The stores are built on the first restore, and the vault with them, so there is
                // nothing to open until this has run.
                await _wallet.RestoreAsync();

                var vault = _wallet.Vault;
                if (vault == null)
                {
                    return null;
                }

                return await vault.LeaseAsync(NavigationService);
            }
            catch (WalletVaultException)
            {
                // Declined, or nothing on this device satisfies what guards it any more.
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("wallet could not be opened: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Replaces the phrase, and puts the new one in front of the user.
        /// </summary>
        /// <remarks>
        /// After the backup is off, never before. Rotating first would leave the server holding a
        /// phrase that no longer signs - a backup that looks like one and restores a wallet nobody
        /// can spend from - for as long as the disable took to go through, or forever if it failed.
        ///
        /// The words are shown whatever happens next, because at this point they are the only copy
        /// in existence: the server has none and the chain is about to stop accepting the old key.
        /// </remarks>
        private async Task UpdateRecoveryPhraseAsync(WalletVault.WalletVaultLease lease)
        {
            try
            {
                var words = await _wallet.UpdateRecoveryPhraseAsync(NavigationService, lease);
                if (words != null)
                {
                    await ShowPopupAsync(new WalletPhrasePopup(words));
                }
            }
            catch (WalletRotationPendingException)
            {
                NavigationService.ShowToast("[Your recovery phrase is already being updated.]", ToastPopupIcon.Info);
            }
            catch (WalletAccessDeniedException)
            {
                // Asked to confirm the change and declined. The backup is off either way, and the
                // phrase they already have is still the wallet's.
                NavigationService.ShowToast(Toast(Strings.WalletBackupDisabled, Strings.WalletBackupDisabledInfo), ToastPopupIcon.Success);
            }
            catch (Exception ex)
            {
                // The backup is off and the phrase did not change, which is a state they can be
                // told plainly - and the phrase they wrote down before is still the right one.
                Logger.Error("wallet recovery phrase could not be updated: " + ex.Message);
                NavigationService.ShowToast("[The backup was disabled, but your recovery phrase could not be updated.]", ToastPopupIcon.Error);
            }
        }

        /// <summary>
        /// What a phrase update would cost, or null if it cannot be offered at all.
        /// </summary>
        /// <remarks>
        /// Null covers every reason the offer cannot stand: no key on this device and the user
        /// unwilling to put one there, a refused confirmation, or an estimate that did not come
        /// back. None of them is a reason to stop them disabling the backup, which is what they
        /// actually asked for.
        /// </remarks>
        private async Task<BigInteger?> RequestRotationFeeAsync(WalletVault.WalletVaultLease lease)
        {
            if (lease == null)
            {
                return null;
            }

            try
            {
                return await _wallet.EstimateKeyRotationFeeAsync(NavigationService, lease);
            }
            catch (WalletNotBoundException)
            {
                return null;
            }
            catch (WalletAccessDeniedException)
            {
                // They were asked to confirm, to sign a message that is never sent, and declined.
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error("wallet key rotation fee could not be estimated: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// The account password, or an empty string where the account has none.
        /// </summary>
        /// <remarks>
        /// Asked of TDLib rather than assumed: 2-step verification can be turned on and off at any
        /// time, and asking an account that has none for a password is a demand it cannot satisfy.
        /// Null when the user dismissed the prompt.
        /// </remarks>
        private async Task<string> RequestPasswordAsync()
        {
            var state = await ClientService.SendAsync(new GetPasswordState());
            if (state is PasswordState { HasPassword: false })
            {
                return string.Empty;
            }

            var result = await ShowInputAsync(InputPopupType.Password, Strings.PleaseEnterCurrentPasswordWithdraw, Strings.TwoStepVerification, Strings.LoginPassword, Strings.OK, Strings.Cancel);
            return result.Result == ContentDialogResult.Primary ? result.Text : null;
        }

        /// <summary>
        /// Forgets the key on this device, so the binding half of the flow can be walked again.
        /// </summary>
        /// <remarks>
        /// **Debug only, and it has to go before this ships.** It leaves the account's wallet
        /// alone and only drops what this device holds, which is otherwise reachable only by
        /// clearing app data.
        /// </remarks>
        public async void UnbindWallet()
        {
            await _wallet.ForgetAsync();
            NavigationService.ShowToast("[This device no longer holds the key.]", ToastPopupIcon.Info);
        }

        public async void DeleteWallet()
        {
            var confirm = await ShowPopupAsync(Strings.WalletDeleteWalletInfo, Strings.WalletDeleteWalletTitle, Strings.WalletDeleteAnyway, Strings.Cancel, destructive: true);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            // Vertical, because neither of these is the safe default the horizontal layout implies
            // by putting one on the right: they are two different wallets to end up with.
            var replace = new MessagePopup
            {
                Title = Strings.WalletReplaceWalletTitle,
                PrimaryButtonText = Strings.WalletCreateNew,
                SecondaryButtonText = Strings.WalletImportExisting,
                ButtonsLayout = ContentPopupButtonsLayout.Vertical
            };

            var action = await ShowPopupAsync(replace);
            if (action == ContentDialogResult.None)
            {
                return;
            }

            var password = await RequestPasswordAsync();
            if (password == null)
            {
                return;
            }

            if (action == ContentDialogResult.Secondary)
            {
                // Replacing rather than deleting: the account keeps a wallet throughout, and which
                // one it is changes when the imported phrase proves itself. Nothing is deleted
                // first, so a refused replacement leaves the user where they were.
                HidePopup(typeof(WalletBackupPopup));

                await ShowPopupAsync(WalletImportPopup.ForReplacement(_wallet, NavigationService, password));
                return;
            }

            try
            {
                await _wallet.DeleteWalletAsync(password);
                HidePopup(typeof(WalletBackupPopup));
            }
            catch (Exception ex)
            {
                Logger.Error("wallet could not be deleted: " + ex.Message);
                NavigationService.ShowToast("[The wallet could not be deleted.]", ToastPopupIcon.Error);
            }
        }
    }
}
