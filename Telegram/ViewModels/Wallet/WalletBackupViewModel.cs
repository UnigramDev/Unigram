//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Wallet;
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

        public void ShowRecoveryPhrase()
        {

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

                _ = _wallet.DeleteAsync();
                NavigationService.GoBackAt(0);

                // TODO: create/import UI
            }
        }
    }
}
