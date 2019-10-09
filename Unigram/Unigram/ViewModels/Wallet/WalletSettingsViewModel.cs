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

namespace Unigram.ViewModels.Wallet
{
    public class WalletSettingsViewModel : TonViewModelBase
    {
        public WalletSettingsViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            ExportCommand = new RelayCommand(ExportExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
        }

        public RelayCommand ExportCommand { get; }
        private void ExportExecute()
        {
            // TODO: make this secure
            NavigationService.Navigate(typeof(WalletExportPage));
        }

        public RelayCommand DeleteCommand { get; }
        private async void DeleteExecute()
        {
            // TODO: make this secure
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.WalletDeleteText, Strings.Resources.WalletDeleteTitle, Strings.Resources.OK, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var publicKey = ProtoService.Options.WalletPublicKey;
            
            var secret = await TonService.Encryption.DecryptAsync(publicKey);
            if (secret == null)
            {
                // TODO:
                return;
            }

            var response = await TonService.SendAsync(new DeleteKey(new Key(publicKey, secret.Item1)));
            if (response is Ok)
            {
                TonService.Encryption.Delete(publicKey);
                ProtoService.Options.WalletPublicKey = null;

                NavigationService.Navigate(typeof(WalletCreatePage));
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }
    }
}
