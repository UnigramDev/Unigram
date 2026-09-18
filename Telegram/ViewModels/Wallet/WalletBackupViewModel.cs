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
using Telegram.Views.Wallet.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels.Wallet
{
    public class WalletBackupViewModel : ViewModelBase
    {
        private readonly IWalletService _wallet;

        /// <summary>
        /// Wallets this device still holds the key for, which the account no longer points at.
        /// </summary>
        public IReadOnlyList<WalletArchivedWallet> Archive => _wallet.State.Archive;

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
            if (!await WalletHelper.EnsureBoundAsync(_wallet, NavigationService))
            {
                return null;
            }

            try
            {
                return await _wallet.RevealRecoveryPhraseAsync();
            }
            catch (WalletAccessDeniedException)
            {
                // They were asked and declined.
                return null;
            }
        }

        public async void DisableBackup()
        {
            var confirm = await ShowPopupAsync("[If you disable backup, you may lose access to your funds. The only way to recover your wallet will be to manually enter your recovery phrase.]", "[Disable Backup?]", Strings.Disable, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                NavigationService.ShowToast("[**Backup Disabled**\nYour recovery phrase is now the only way to restore your wallet.]", ToastPopupIcon.Success);
            }
        }

        public async void DeleteWallet()
        {
            var confirm = await ShowPopupAsync("[You'll lose access to your funds unless you've saved your 24-word recovery phrase.]", "[Delete Wallet?]", "[Delete Anyway]", "[Cancel]", destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var popup = new MessagePopup
                {
                    Title = Strings.AppName,
                    Message = "[How do you want to replace the old wallet?]",
                    PrimaryButtonText = "[Create a New Wallet]",
                    SecondaryButtonText = "[Import an Existing Wallet]",
                    ButtonsLayout = ContentPopupButtonsLayout.Horizontal
                };

                var action = await ShowPopupAsync(popup);
                if (action == ContentDialogResult.None)
                {
                    return;
                }

                // Forgets the key on this device only. Deleting the account's wallet is
                // deleteTonWallet, which replaces it with a new one and needs the account password.
                _ = _wallet.ForgetAsync();
                HidePopup(typeof(WalletBackupPopup));

                // TODO: create/import UI
            }
        }
    }
}
