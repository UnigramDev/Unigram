using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Wallet;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletReceiveViewModel : TonViewModelBase
    {
        public WalletReceiveViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            CopyCommand = new RelayCommand(CopyExecute);
            InvoiceCommand = new RelayCommand(InvoiceExecute);
            ShareCommand = new RelayCommand<string>(ShareExecute);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var address = TonService.GetAccountAddress(ProtoService.Options.WalletPublicKey);
            if (address != null)
            {
                Address = address.AccountAddressValue;
            }

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public RelayCommand CopyCommand { get; }
        private void CopyExecute()
        {
            var address = _address;
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(address);
            ClipboardEx.TrySetContent(dataPackage);
        }

        public RelayCommand InvoiceCommand { get; }
        private void InvoiceExecute()
        {
            NavigationService.Navigate(typeof(WalletInvoicePage));
        }

        public RelayCommand<string> ShareCommand { get; }
        private async void ShareExecute(string url)
        {
            await ShareView.GetForCurrentView().ShowAsync(new Uri(url), null);
        }
    }
}
