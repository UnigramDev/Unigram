using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Ton.Tonlib.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Wallet;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Wallet
{
    public class WalletViewModel : TonViewModelBase, ISupportIncrementalLoading
    {
        private TaskCompletionSource<bool> _navigationTask;

        private InternalTransactionId _lastTransactionId;
        private bool _hasMoreItems;

        public WalletViewModel(ITonService tonService, IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(tonService, protoService, cacheService, settingsService, aggregator)
        {
            _navigationTask = new TaskCompletionSource<bool>();

            Transactions = new IncrementalCollectionWithDelegate<object>(this);

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

        public IncrementalCollectionWithDelegate<object> Transactions { get; private set; }

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

                    _navigationTask.SetResult(false);
                    return;
                }

                var transactions = await TonService.SendAsync(new RawGetTransactions(address, lastTransactionId)) as RawTransactions;
                if (transactions == null)
                {
                    Transactions.Clear();

                    _navigationTask.SetResult(false);
                    return;
                }

                IsEmpty = transactions.Transactions.Count == 0;
                Transactions.Clear();

                InsertTransactions(transactions);
            }
            else if (response is Error error)
            {
                await TLMessageDialog.ShowAsync(error.Message, error.Code.ToString(), Strings.Resources.OK);
            }

            _navigationTask.TrySetResult(true);
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                await _navigationTask.Task;

                if (_address == null || _lastTransactionId == null)
                {
                    return new LoadMoreItemsResult { Count = 0 };
                }

                count = 0;

                var response = await TonService.SendAsync(new RawGetTransactions(new AccountAddress(_address), _lastTransactionId));
                if (response is RawTransactions transactions)
                {
                    count = InsertTransactions(transactions);
                }

                return new LoadMoreItemsResult { Count = count };
            });
        }

        private uint InsertTransactions(RawTransactions transactions)
        {
            var count = 0u;

            _lastTransactionId = transactions.PreviousTransactionId;
            _hasMoreItems = transactions.PreviousTransactionId.Lt != 0;

            Debug.WriteLine(transactions.PreviousTransactionId.ToString());

            DateTime? previous = null;
            foreach (var item in transactions.Transactions)
            {
                var itemDate = Utils.UnixTimestampToDateTime(item.Utime).Date;
                if (itemDate != previous)
                {
                    Transactions.Add(itemDate);
                }

                Transactions.Add(item);
                count++;

                previous = itemDate;
            }

            return count;
        }

        public bool HasMoreItems => _hasMoreItems;

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
            var parameters = new Dictionary<string, object>
            {
                { "balance", _balance }
            };

            NavigationService.Navigate(typeof(WalletSendPage), state: parameters);
        }
    }
}
