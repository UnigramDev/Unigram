using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
    public class WalletViewModel : TonViewModelBase
    {
        public WalletViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            Transactions = new MvxObservableCollection<RawTransaction>();

            RefreshCommand = new RelayCommand(RefreshExecute);
            SettingsCommand = new RelayCommand(SettingsExecute);
            ReceiveCommand = new RelayCommand(ReceiveExecute);
            SendCommand = new RelayCommand(SendExecute);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => Set(ref _address, value);
        }

        private long _balance;
        public long Balance
        {
            get => _balance;
            set => Set(ref _balance, value);
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

        public MvxObservableCollection<RawTransaction> Transactions { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var address = TonService.Execute(new WalletGetAccountAddress(new WalletInitialAccountState(ProtoService.Options.WalletPublicKey))) as AccountAddress;
            if (address == null)
            {
                return;
            }

            Address = address.AccountAddressValue;

            IsLoading = true;

            var response = await TonService.SendAsync(new GenericGetAccountState(address));
            if (response is GenericAccountState accountState)
            {
                IsLoading = false;
                Balance = accountState.GetBalance();

                var lastTransactionId = accountState.GetLastTransactionId();
                if (lastTransactionId == null)
                {
                    Transactions.Clear();
                    return;
                }

                var transactions = await TonService.SendAsync(new RawGetTransactions(address, lastTransactionId)) as RawTransactions;
                if (transactions == null)
                {
                    Transactions.Clear();
                    return;
                }

                IsEmpty = transactions.Transactions.Count == 0;
                Transactions.Clear();

                foreach (var transaction in transactions.Transactions)
                {
                    Transactions.Add(transaction);
                }
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }
        }

        public RelayCommand RefreshCommand { get; }
        private async void RefreshExecute()
        {
            await OnNavigatedToAsync(null, NavigationMode.Refresh, null);
        }

        public RelayCommand SettingsCommand { get; }
        private void SettingsExecute()
        {
            NavigationService.Navigate(typeof(WalletSettingsPage));
        }

        public RelayCommand ReceiveCommand { get; }
        private void ReceiveExecute()
        {
            NavigationService.Navigate(typeof(WalletReceivePage));
        }

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            NavigationService.Navigate(typeof(WalletSendPage));
        }
    }
}
