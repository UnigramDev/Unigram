//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Stars
{
    public partial class StarsViewModel : ViewModelBase, IIncrementalCollectionOwner, IHandle
    {
        private readonly SubscriptionCollection _subscriptions;

        private string _nextOffset = string.Empty;
        private TransactionDirection _direction;

        public StarsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _subscriptions = new SubscriptionCollection(clientService, settingsService, aggregator);

            Items = new IncrementalCollection<StarTransaction>(this);
        }

        public IncrementalCollection<StarTransaction> Items { get; private set; }

        public IncrementalCollection<StarSubscription> Subscriptions => _subscriptions.Items;

        public string OwnedStarCount => ClientService.OwnedStarCount.ToValue();

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateOwnedStarCount>(this, Handle);
        }

        private void Handle(UpdateOwnedStarCount update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(OwnedStarCount)));
        }

        public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
        {
            Logger.Info();

            // Subscriptions are pumped through this list too, and running out of them is not the
            // end of it: the transactions follow. Routed through the collection rather than the
            // owner, so that the collection is the one applying the result to its own flag.
            if (Subscriptions.HasMoreItems)
            {
                var subscriptions = await Subscriptions.LoadMoreItemsAsync(count);
                if (subscriptions.Count > 0 || Subscriptions.HasMoreItems)
                {
                    return new IncrementalLoadResult(subscriptions.Count, true);
                }
            }

            return await LoadMoreItemsAsync2(count);
        }

        public async Task<IncrementalLoadResult> LoadMoreItemsAsync2(uint count)
        {
            var totalCount = 0u;
            var hasMoreItems = false;

            var response = await ClientService.GetStarTransactionsAsync(ClientService.MyId, string.Empty, _direction, _nextOffset, 20);
            if (response is StarTransactions transactions)
            {
                foreach (var item in transactions.Transactions)
                {
                    Items.Add(item);
                    totalCount++;
                }

                _nextOffset = transactions.NextOffset;
                hasMoreItems = transactions.NextOffset.Length > 0;
            }

            return new IncrementalLoadResult(totalCount, hasMoreItems);
        }

        partial class SubscriptionCollection : ViewModelBase, IIncrementalCollectionOwner
        {
            private string _nextOffset = string.Empty;

            public SubscriptionCollection(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
                : base(clientService, settingsService, aggregator)
            {
                Items = new IncrementalCollection<StarSubscription>(this);
            }

            public IncrementalCollection<StarSubscription> Items { get; private set; }

            public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
            {
                Logger.Info();

                var totalCount = 0u;
                var hasMoreItems = false;

                var response = await ClientService.SendAsync(new GetStarSubscriptions(false, _nextOffset));
                if (response is StarSubscriptions subscriptions)
                {
                    foreach (var item in subscriptions.Subscriptions)
                    {
                        Items.Add(item);
                        totalCount++;
                    }

                    _nextOffset = subscriptions.NextOffset;
                    hasMoreItems = subscriptions.NextOffset.Length > 0;
                }

                return new IncrementalLoadResult(totalCount, hasMoreItems);
            }
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
                    1 => new TransactionDirectionIncoming(),
                    2 => new TransactionDirectionOutgoing(),
                    _ => null
                };

                Items.Restart();
            }
        }
    }
}
