using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Template10.Mvvm;
using Ton.Tonlib.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Wallet;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletImportViewModel : TonViewModelBase
    {
        public WalletImportViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<WalletWordViewModel>();

            for (int i = 0; i < 12; i++)
            {
                Items.Add(new WalletWordViewModel { Index = i + 1 });
                Items.Add(new WalletWordViewModel { Index = i + 13 });
            }

            TooBadCommand = new RelayCommand(TooBadExecute);
            SendCommand = new RelayCommand(SendExecute);
        }

        public MvxObservableCollection<WalletWordViewModel> Items { get; private set; }

        private IList<string> _hints;
        public IList<string> Hints
        {
            get => _hints;
            set => Set(ref _hints, value);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
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

        public RelayCommand TooBadCommand { get; }
        private void TooBadExecute()
        {
            NavigationService.Navigate(typeof(WalletInfoPage), WalletInfoState.TooBad);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response = await TonService.Encryption.GenerateLocalPasswordAsync();
            if (response is ByteTuple localPassword)
            {
                await ContinueAsync(localPassword.Item1);
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }

        private async Task ContinueAsync(IList<byte> localPassword)
        {
            var words = Items.OrderBy(x => x.Index).Select(x => x.Text).ToArray();

            var response = await TonService.SendAsync(new ImportKey(localPassword, new byte[0], new ExportedKey(words)));
            if (response is Key key)
            {
                var encrypt = await TonService.Encryption.EncryptAsync(key.PublicKey, key.Secret, localPassword);
                if (encrypt)
                {
                    ProtoService.Options.WalletPublicKey = key.PublicKey;
                    NavigationService.Navigate(typeof(WalletPage));
                }
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(Strings.Resources.WalletImportAlertText, Strings.Resources.WalletImportAlertTitle, Strings.Resources.OK);
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }
    }

    public class WalletWordViewModel : BindableBase
    {
        private int _index;
        public int Index
        {
            get => _index;
            set => Set(ref _index, value);
        }

        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set => Set(ref _text, value);
        }
    }
}
