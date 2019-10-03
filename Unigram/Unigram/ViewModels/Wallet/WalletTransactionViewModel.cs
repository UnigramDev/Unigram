using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Wallet;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletTransactionViewModel : TonViewModelBase, IDelegable<IWalletTransactionDelegate>
    {
        public IWalletTransactionDelegate Delegate { get; set; }

        public WalletTransactionViewModel(ITonlibService tonlibService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonlibService, protoService, cacheService, settingsService, aggregator)
        {
            CopyCommand = new RelayCommand(CopyExecute);
            SendCommand = new RelayCommand(SendExecute);
        }

        private RawTransaction _item;
        public RawTransaction Item
        {
            get => _item;
            set => Set(ref _item, value);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is RawTransaction item)
            {
                Item = item;
            }
            else if (state.TryGet("transaction", out RawTransaction raw))
            {
                Item = raw;
            }

            Delegate?.UpdateTransaction(_item);

            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        public RelayCommand CopyCommand { get; }
        private void CopyExecute()
        {
            var address = GetTargetAddress();
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(address);
            ClipboardEx.TrySetContent(dataPackage);
        }

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            var address = GetTargetAddress();
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            var state = new Dictionary<string, object>
            {
                { "address", address }
            };

            NavigationService.Navigate(typeof(WalletSendPage), state);
        }

        private string GetTargetAddress()
        {
            var item = _item;
            if (item == null)
            {
                return null;
            }

            long amount;
            if (item.InMsg != null)
            {
                amount = item.InMsg.Value;
            }
            else
            {
                amount = 0;
            }

            foreach (var msg in item.OutMsgs)
            {
                amount -= msg.Value;
            }

            amount -= item.Fee;

            if (amount > 0)
            {
                return item.InMsg.Source;
            }
            else
            {
                if (item.OutMsgs.IsEmpty())
                {
                    return null;
                }

                return item.OutMsgs[0].Destination;
            }
        }
    }
}
