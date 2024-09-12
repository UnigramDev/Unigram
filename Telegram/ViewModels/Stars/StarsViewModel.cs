//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;

namespace Telegram.ViewModels.Stars
{
    public partial class StarsViewModel : ViewModelBase, IIncrementalCollectionOwner, IHandle
    {
        private readonly SubscriptionCollection _subscriptions;

        private string _nextOffset = string.Empty;
        private StarTransactionDirection _direction;

        public StarsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _subscriptions = new SubscriptionCollection(clientService, settingsService, aggregator);

            Items = new IncrementalCollection<StarTransaction>(this);
            OwnedStarCount = clientService.OwnedStarCount;
        }

        public IncrementalCollection<StarTransaction> Items { get; private set; }

        public IncrementalCollection<StarSubscription> Subscriptions => _subscriptions.Items;

        private long _ownedStarCount;
        public long OwnedStarCount
        {
            get => _ownedStarCount;
            set => Set(ref _ownedStarCount, value);
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateOwnedStarCount>(this, Handle);
        }

        private void Handle(UpdateOwnedStarCount update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(OwnedStarCount)));
        }

        public Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            if (_subscriptions.HasMoreItems)
            {
                return _subscriptions.LoadMoreItemsAsync(count);
            }

            return LoadMoreItemsAsync2(count);
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync2(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.GetStarTransactionsAsync(ClientService.MyId, string.Empty, _direction, _nextOffset, 20);
            if (response is StarTransactions transactions)
            {
                foreach (var item in transactions.Transactions)
                {
                    Items.Add(item);
                    totalCount++;
                }

                _nextOffset = transactions.NextOffset;
                HasMoreItems = transactions.NextOffset.Length > 0;

                OwnedStarCount = transactions.StarCount;
            }
            else
            {
                HasMoreItems = false;
            }

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        class SubscriptionCollection : ViewModelBase, IIncrementalCollectionOwner
        {
            private string _nextOffset = string.Empty;

            public SubscriptionCollection(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
                : base(clientService, settingsService, aggregator)
            {
                Items = new IncrementalCollection<StarSubscription>(this);
            }

            public IncrementalCollection<StarSubscription> Items { get; private set; }

            public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                var totalCount = 0u;

                var response = await ClientService.SendAsync(new GetStarSubscriptions(false, _nextOffset));
                if (response is StarSubscriptions subscriptions)
                {
                    foreach (var item in subscriptions.Subscriptions)
                    {
                        Items.Add(item);
                        totalCount++;
                    }

                    _nextOffset = subscriptions.NextOffset;
                    HasMoreItems = subscriptions.NextOffset.Length > 0;
                }
                else
                {
                    HasMoreItems = false;
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            public bool HasMoreItems { get; private set; } = true;
        }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetSelectedIndex(value);
        }

        private void SetSelectedIndex(int value)
        {
            if (Set(ref _selectedIndex, value, nameof(SelectedIndex)))
            {
                _nextOffset = string.Empty;
                _direction = _selectedIndex switch
                {
                    1 => new StarTransactionDirectionIncoming(),
                    2 => new StarTransactionDirectionOutgoing(),
                    _ => null
                };

                HasMoreItems = true;
                Items.Clear();
            }
        }
    }
}
