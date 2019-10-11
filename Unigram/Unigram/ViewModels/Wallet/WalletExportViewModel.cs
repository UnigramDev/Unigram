using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Wallet;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletExportViewModel : TonViewModelBase, IDelegable<IWalletExportDelegate>
    {
        private DateTimeOffset _openedAt;

        public IWalletExportDelegate Delegate { get; set; }

        public WalletExportViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private IList<WalletWordViewModel> _items;
        public IList<WalletWordViewModel> Items
        {
            get => _items;
            set => Set(ref _items, value);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (mode == NavigationMode.New)
            {
                _openedAt = DateTimeOffset.Now;
            }

            IList<string> wordList = null;
            if (TonService.TryGetCreationState(out WalletCreationState creationState))
            {
                wordList = creationState.WordList;
            }
            else
            {
                state.TryGet("public_key", out string publicKey);
                state.TryGet("secret", out IList<byte> secret);
                state.TryGet("local_password", out IList<byte> localPassword);

                var privateKey = new InputKey(new Key(publicKey, secret), localPassword);

                var response = await TonService.SendAsync(new ExportKey(privateKey));
                if (response is ExportedKey exportedKey)
                {
                    wordList = exportedKey.WordList;
                }
                else if (response is Error error)
                {
                    await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
                }
            }

            if (wordList == null)
            {
                return;
            }

            var items = new List<WalletWordViewModel>();

            for (int i = 0; i < 12; i++)
            {
                items.Add(new WalletWordViewModel { Index = i + 1, Text = wordList[i] });
                items.Add(new WalletWordViewModel { Index = i + 13, Text = wordList[i + 12] });
            }

            Items = items;
            Delegate?.UpdateWordList(items);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            if (TonService.IsCreating)
            {
                var wait = 60;

#if DEBUG
                wait = 6;
#endif

                var difference = DateTimeOffset.Now - _openedAt;
                if (difference < TimeSpan.FromSeconds(wait))
                {
                    await TLMessageDialog.ShowAsync(Strings.Resources.WalletSecretWordsAlertText, Strings.Resources.WalletSecretWordsAlertTitle, Strings.Resources.WalletSecretWordsAlertButton);
                    return;
                }

                NavigationService.Navigate(typeof(WalletTestPage));
            }
            else
            {
                NavigationService.Navigate(typeof(WalletPage));
            }
        }
    }
}
