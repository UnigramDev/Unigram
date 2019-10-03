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
        public WalletCreateViewModel(ITonlibService tonlibService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonlibService, protoService, cacheService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
            ImportCommand = new RelayCommand(ImportExecute);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var local_password = Encoding.UTF8.GetBytes("local_passwordlocal_passwordlocal_passwordlocal_passwordlocal_pa");

            var response = await TonlibService.SendAsync(new CreateNewKey(local_password, new byte[0], new byte[0]));
            if (response is Key key)
            {
                var address = TonlibService.Execute(new WalletGetAccountAddress(new WalletInitialAccountState(key.PublicKey))) as AccountAddress;

                ProtoService.Send(new Telegram.Td.Api.SetOption("x_wallet_public_key", new Telegram.Td.Api.OptionValueString(key.PublicKey)));
                ProtoService.Send(new Telegram.Td.Api.SetOption("x_wallet_secret", new Telegram.Td.Api.OptionValueString(Utils.ByteArrayToString(key.Secret))));
                ProtoService.Send(new Telegram.Td.Api.SetOption("x_wallet_address", new Telegram.Td.Api.OptionValueString(address.AccountAddressValue)));

                await ContinueAsync(key, local_password);
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }

        private async Task ContinueAsync(Key key, byte[] localPassword)
        {
            var response = await TonlibService.SendAsync(new ExportKey(new InputKey(key, localPassword)));
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

                TonlibService.SetCreationState(new WalletCreationState
                {
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
