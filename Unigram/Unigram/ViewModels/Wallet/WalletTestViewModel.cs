using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Wallet;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletTestViewModel : TonViewModelBase
    {
        public WalletTestViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private IList<string> _hints;
        public IList<string> Hints
        {
            get => _hints;
            set => Set(ref _hints, value);
        }

        private IList<int> _indices;
        public IList<int> Indices
        {
            get => _indices;
            set => Set(ref _indices, value);
        }

        private string[] _words = new string[3];
        public string Word0
        {
            get => _words[0];
            set => Set(ref _words[0], value);
        }

        public string Word1
        {
            get => _words[1];
            set => Set(ref _words[1], value);
        }

        public string Word2
        {
            get => _words[2];
            set => Set(ref _words[2], value);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (TonService.TryGetCreationState(out WalletCreationState creationState))
            {
                Indices = creationState.Indices;
            }

            var response = await TonService.SendAsync(new GetBip39Hints());
            if (response is Bip39Hints hints)
            {
                Hints = hints.Words;
            }
            else
            {
                Hints = null;
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (TonService.TryGetCreationState(out WalletCreationState creationState))
            {
                for (int i = 0; i < creationState.Indices.Count; i++)
                {
                    if (!string.Equals(creationState.WordList[creationState.Indices[i]], _words[i]))
                    {
                        var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.WalletTestTimeAlertText, Strings.Resources.WalletTestTimeAlertTitle, Strings.Resources.WalletTestTimeAlertButtonTry, Strings.Resources.WalletTestTimeAlertButtonSee);
                        if (confirm == ContentDialogResult.Secondary)
                        {
                            NavigationService.GoBack();
                        }

                        return;
                    }
                }

                TonService.SetCreationState(null);
                NavigationService.Navigate(typeof(WalletInfoPage), WalletInfoState.Ready);
            }
        }
    }
}
