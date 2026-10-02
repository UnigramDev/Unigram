//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
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

            using var lease = _wallet.CreateLease(NavigationService);

            var words = await RequestRecoveryPhraseAsync(lease);
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
        private async Task<IReadOnlyList<string>> RequestRecoveryPhraseAsync(WalletVault.WalletVaultLease lease)
        {
            if (!await WalletHelper.EnsureBoundAsync(ClientService, _wallet, NavigationService, lease))
            {
                return null;
            }

            try
            {
                return await _wallet.RevealRecoveryPhraseAsync(lease);
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
            using var lease = _wallet.CreateLease(NavigationService);

            if (!await WalletHelper.EnsureBoundAsync(ClientService, _wallet, NavigationService, lease))
            {
                return;
            }

            IReadOnlyList<string> words;

            try
            {
                words = await _wallet.RevealRecoveryPhraseAsync(lease);
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
                await _wallet.EnableBackupAsync(words, lease);
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
            // One confirmation for the whole operation, given at whichever step first needs the
            // key: binding this device, pricing the phrase update in the popup, or what follows it.
            using var lease = _wallet.CreateLease(NavigationService);

            if (!await WalletHelper.EnsureBoundAsync(ClientService, _wallet, NavigationService, lease))
            {
                // The account password was dismissed, the vault prompt declined, or there is no
                // wallet to bind to. Saying no has to stop the thing that was said no to.
                return;
            }

            var popup = new WalletDisableBackupPopup(ClientService, _wallet, NavigationService, lease);

            var confirm = await ShowPopupAsync(popup);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            // Either way the phrase the user ends up with is the only copy once the backup is gone,
            // so nothing changes until they have shown they wrote it down.
            if (popup.IsPhraseUpdateRequested)
            {
                var update = await PrepareRecoveryPhraseUpdateAsync(lease);
                if (update == null || !await ConfirmRecoveryPhraseAsync(update.Words, true))
                {
                    return;
                }

                // The backup is what would let them recover with the old phrase, so it stays on
                // unless the new phrase can still replace it.
                if (update.IsExpired)
                {
                    NavigationService.ShowToast("[The new recovery phrase expired before it was confirmed. Nothing was changed.]", ToastPopupIcon.Error);
                    return;
                }

                if (await TryDisableBackupAsync(lease))
                {
                    await CommitRecoveryPhraseUpdateAsync(update, lease);
                }

                return;
            }

            var words = await RevealRecoveryPhraseAsync(lease);
            if (words != null && await ConfirmRecoveryPhraseAsync(words, false) && await TryDisableBackupAsync(lease))
            {
                NavigationService.ShowToast(Toast(Strings.WalletBackupDisabled, Strings.WalletBackupDisabledInfo), ToastPopupIcon.Success);
            }
        }

        private async Task<bool> TryDisableBackupAsync(WalletVault.WalletVaultLease lease)
        {
            try
            {
                await _wallet.DisableBackupAsync(lease);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("wallet backup could not be disabled: " + ex.Message);
                NavigationService.ShowToast("[The backup could not be disabled.]", ToastPopupIcon.Error);
                return false;
            }
        }

        private async Task<IReadOnlyList<string>> RevealRecoveryPhraseAsync(WalletVault.WalletVaultLease lease)
        {
            IReadOnlyList<string> words;

            try
            {
                words = await _wallet.RevealRecoveryPhraseAsync(lease);
            }
            catch (WalletAccessDeniedException)
            {
                return null;
            }

            if (words == null)
            {
                NavigationService.ShowToast("[The backup could not be disabled.]", ToastPopupIcon.Error);
            }

            return words;
        }

        /// <summary>
        /// Shows the phrase and tests it, until the user passes the test or abandons the operation.
        /// </summary>
        /// <remarks>
        /// Backing out of the test goes back to the phrase rather than out of the flow: someone
        /// who could not answer needs to look at the words again, not to start over.
        /// </remarks>
        private async Task<bool> ConfirmRecoveryPhraseAsync(IReadOnlyList<string> words, bool isNew)
        {
            while (true)
            {
                var phrase = isNew
                    ? WalletPhrasePopup.ForNewPhrase(words)
                    : WalletPhrasePopup.ForDisableBackup(words);

                if (await ShowPopupAsync(phrase) != ContentDialogResult.Primary)
                {
                    return false;
                }

                if (await ShowPopupAsync(new WalletTestPopup(words)) == ContentDialogResult.Primary)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// The phrase that would replace the current one, or null when it cannot be offered -
        /// which has already been said.
        /// </summary>
        private async Task<WalletPhraseUpdate> PrepareRecoveryPhraseUpdateAsync(WalletVault.WalletVaultLease lease)
        {
            try
            {
                return await _wallet.PrepareRecoveryPhraseUpdateAsync(lease);
            }
            catch (WalletRotationPendingException)
            {
                NavigationService.ShowToast("[Your recovery phrase is already being updated.]", ToastPopupIcon.Info);
            }
            catch (WalletAccessDeniedException)
            {
                // Asked to confirm and declined.
            }
            catch (Exception ex)
            {
                Logger.Error("wallet recovery phrase update could not be prepared: " + ex.Message);
                NavigationService.ShowToast("[Your recovery phrase could not be updated.]", ToastPopupIcon.Error);
            }

            return null;
        }

        /// <summary>
        /// Makes the new phrase the wallet's.
        /// </summary>
        /// <remarks>
        /// After the backup is off, never before. Rotating first would leave the server holding a
        /// phrase that no longer signs - a backup that looks like one and restores a wallet nobody
        /// can spend from - for as long as the disable took to go through, or forever if it failed.
        /// </remarks>
        private async Task CommitRecoveryPhraseUpdateAsync(WalletPhraseUpdate update, WalletVault.WalletVaultLease lease)
        {
            try
            {
                await _wallet.CommitRecoveryPhraseUpdateAsync(update, lease);
                NavigationService.ShowToast(Toast(Strings.WalletBackupDisabled, Strings.WalletBackupDisabledInfo), ToastPopupIcon.Success);
            }
            catch (Exception ex)
            {
                // The words they just wrote down open nothing, and they have to be told so: the
                // phrase they had before is still the wallet's.
                Logger.Error("wallet recovery phrase could not be updated: " + ex.Message);
                NavigationService.ShowToast("[The backup was disabled, but your recovery phrase could not be updated. Your previous phrase still opens your wallet.]", ToastPopupIcon.Error);
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

                using var lease = _wallet.CreateLease(NavigationService);

                await ShowPopupAsync(WalletImportPopup.ForReplacement(_wallet, NavigationService, lease, password));
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
