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
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletInfoViewModel : TonViewModelBase
    {
        public WalletInfoViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private WalletInfoState _state;
        public WalletInfoState State
        {
            get => _state;
            set => Set(ref _state, value);
        }

        private long _amount;
        public long Amount
        {
            get => _amount;
            set => Set(ref _amount, value);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is WalletInfoState infoState)
            {
                State = infoState;
            }

            if (state.TryGet("amount", out long amount))
            {
                Amount = amount;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            switch (_state)
            {
                case WalletInfoState.Created:
                    await ContinueCreatedAsync();
                    //NavigationService.Navigate(typeof(WalletExportPage));
                    break;
                case WalletInfoState.Ready:
                case WalletInfoState.Sent:
                    NavigationService.Navigate(typeof(WalletPage));
                    break;
            }
        }

        private async Task ContinueCreatedAsync()
        {
            if (TonService.TryGetCreationState(out WalletCreationState state))
            {
                var encrypt = await TonService.Encryption.EncryptAsync(state.Key.PublicKey, state.Key.Secret, state.LocalPassword);
                if (encrypt)
                {
                    ProtoService.Options.WalletPublicKey = state.Key.PublicKey;
                    NavigationService.Navigate(typeof(WalletExportPage));
                }
            }
        }
    }

    public enum WalletInfoState
    {
        Created,
        Ready,
        Sent
    }
}
