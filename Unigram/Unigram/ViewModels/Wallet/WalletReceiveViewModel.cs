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
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletReceiveViewModel : TonViewModelBase, IDelegable<IWalletReceiveDelegate>
    {
        public IWalletReceiveDelegate Delegate { get; set; }

        public WalletReceiveViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            CopyCommand = new RelayCommand(CopyExecute);
            ShareCommand = new RelayCommand(ShareExecute);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var address = TonService.Execute(new WalletGetAccountAddress(new WalletInitialAccountState(ProtoService.Options.WalletPublicKey))) as AccountAddress;
            if (address != null)
            {
                Address = address.AccountAddressValue;
                Delegate?.UpdateAddress(address.AccountAddressValue);
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

        public RelayCommand ShareCommand { get; }
        private async void ShareExecute()
        {
            var address = _address;
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            await ShareView.GetForCurrentView().ShowAsync(new Uri($"ton://{address}"), null);
        }
    }
}
