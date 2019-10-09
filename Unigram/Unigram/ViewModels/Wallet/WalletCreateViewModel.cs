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
            var localPassword = await TonService.Encryption.GenerateLocalPasswordAsync();
            if (localPassword == null)
            {
                // TODO: ???
                return;
            }

            var response = await TonService.SendAsync(new CreateNewKey(localPassword.Item1, new byte[0], localPassword.Item2));
            if (response is Key key)
            {
                await ContinueAsync(key, localPassword.Item1);
                //var encrypt = await TonService.Encryption.EncryptAsync(key.PublicKey, key.Secret);
                //if (encrypt)
                //{
                //    ProtoService.Options.WalletPublicKey = key.PublicKey;
                //}
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
