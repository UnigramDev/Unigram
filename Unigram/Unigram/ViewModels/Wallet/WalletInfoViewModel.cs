using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.Views.Wallet;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletInfoViewModel : TonViewModelBase
    {
        public WalletInfoViewModel(ITonlibService tonlibService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonlibService, protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private WalletInfoState _state;
        public WalletInfoState State
        {
            get => _state;
            set => Set(ref _state, value);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is WalletInfoState infoState)
            {
                State = infoState;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            switch (_state)
            {
                case WalletInfoState.Created:
                    //await ContinueCreatedAsync();
                    NavigationService.Navigate(typeof(WalletExportPage));
                    break;
                case WalletInfoState.Ready:
                    NavigationService.Navigate(typeof(WalletPage));
                    break;
            }
        }

        //private async Task ContinueCreatedAsync()
        //{
        //    IBuffer keyMaterial;

        //    if (await KeyCredentialManager.IsSupportedAsync())
        //    {

        //    }
        //    else
        //    {
        //        var dialog = new SettingsPasscodeInputView();

        //        var confirm = await dialog.ShowQueuedAsync();
        //        if (confirm != ContentDialogResult.Primary)
        //        {
        //            return;
        //        }

        //        var salt = CryptographicBuffer.GenerateRandom(32);
        //        var test = Utils.PBKDF2(dialog.Passcode, salt);
        //    }

        //    NavigationService.Navigate(typeof(WalletExportPage));
        //}
    }

    public enum WalletInfoState
    {
        Created,
        Ready
    }
}
