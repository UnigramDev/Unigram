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
            ChangePasscodeCommand = new RelayCommand(ChangePasscodeExecute);
            DeleteCommand = new RelayCommand(DeleteExecute);
        }

        public RelayCommand ExportCommand { get; }
        private async void ExportExecute()
        {
            var publicKey = ProtoService.Options.WalletPublicKey;

            var secret = await TonService.Encryption.DecryptAsync(publicKey);
            if (secret == null)
            {
                // TODO:
                return;
            }

            var parameters = new Dictionary<string, object>
            {
                { "public_key", publicKey },
                { "secret", secret.Item1 },
                { "local_password", secret.Item2 },
            };

            NavigationService.Navigate(typeof(WalletExportPage), state: parameters);
        }

        public RelayCommand ChangePasscodeCommand { get; }
        private async void ChangePasscodeExecute()
        {

        }

        public RelayCommand DeleteCommand { get; }
        private async void DeleteExecute()
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.WalletDeleteInfo, Strings.Resources.WalletDeleteTitle, Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            TonService.CleanUp();
            NavigationService.Navigate(typeof(WalletCreatePage));
        }
    }
}
