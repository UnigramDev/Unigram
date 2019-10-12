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

namespace Unigram.ViewModels.Wallet
{
    public class WalletCreateViewModel : TonViewModelBase
    {
        public WalletCreateViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
            ImportCommand = new RelayCommand(ImportExecute);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var response = await TonService.Encryption.GenerateLocalPasswordAsync();
            if (response is ByteTuple localPassword)
            {
                await ContinueAsync(localPassword.Item1, localPassword.Item2);
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }

        private async Task ContinueAsync(IList<byte> localPassword, IList<byte> salt)
        {
            var response = await TonService.SendAsync(new CreateNewKey(localPassword, new byte[0], salt));
            if (response is Key key)
            {
                await ContinueAsync(key, localPassword);
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }

        private async Task ContinueAsync(Key key, IList<byte> localPassword)
        {
            var response = await TonService.SendAsync(new ExportKey(new InputKey(key, localPassword)));
            if (response is ExportedKey exportedKey)
            {
                var indices = new List<int>();
                var random = new Random();

                while (indices.Count < 3)
                {
                    var next = random.Next(0, 24);
                    if (!indices.Contains(next))
                    {
                        indices.Add(next);
                    }
                }

                indices.Sort();

                TonService.SetCreationState(new WalletCreationState
                {
                    LocalPassword = localPassword,
                    Key = key,
                    WordList = exportedKey.WordList,
                    Indices = indices
                });

                NavigationService.Navigate(typeof(WalletInfoPage), WalletInfoState.Created);
            }
        }

        public RelayCommand ImportCommand { get; }
        private void ImportExecute()
        {
            NavigationService.Navigate(typeof(WalletImportPage));
        }
    }
}
