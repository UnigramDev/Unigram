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
    public partial class StarsViewModel : ViewModelBase, IHandle
    {
        private readonly SubscriptionCollection _subscriptions;
        private readonly TransactionTabs<StarTransaction> _transactions;

        public StarsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _subscriptions = new SubscriptionCollection(clientService, settingsService, aggregator);
            _transactions = new TransactionTabs<StarTransaction>(LoadPageAsync, GetId, IsIncoming, OnTransactionsChanged, LoadSubscriptionsAsync);
        }

        public IncrementalCollectionView<StarTransaction, IncrementalCollection<StarTransaction>> Items => _transactions.Items;

        public bool HasTransactions => _transactions.HasTransactions;

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

        private static string GetId(StarTransaction transaction)
        {
            return transaction.Id;
        }

        private static bool IsIncoming(StarTransaction transaction)
        {
            return !transaction.StarAmount.IsNegative();
        }

        private void OnTransactionsChanged()
        {
            RaisePropertyChanged(nameof(HasTransactions));
        }

        private async Task<TransactionPage<StarTransaction>> LoadPageAsync(TransactionDirection direction, string offset, int limit)
        {
            Logger.Info();

            var response = await ClientService.GetStarTransactionsAsync(ClientService.MyId, string.Empty, direction, offset, limit);
            if (response is StarTransactions transactions)
            {
                return new TransactionPage<StarTransaction>(transactions.Transactions, transactions.NextOffset);
            }

            return default;
        }

        // Subscriptions have no list of their own to page them: they render inside the transaction
        // list's header, where nothing would ever ask for more. Running out of them is not the end
        // of it, so the All tab drains them first and only then starts on its own pages.
        private async Task<IncrementalLoadResult?> LoadSubscriptionsAsync(uint count)
        {
            if (Subscriptions.HasMoreItems)
            {
                var subscriptions = await Subscriptions.LoadMoreItemsAsync(count);
                if (subscriptions.Count > 0 || Subscriptions.HasMoreItems)
                {
                    return new IncrementalLoadResult(subscriptions.Count, true);
                }
            }

            return null;
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

        public int SelectedIndex
        {
            get => _transactions.SelectedIndex;
            set
            {
                if (_transactions.SelectedIndex != value)
                {
                    _transactions.SelectedIndex = value;

                    RaisePropertyChanged(nameof(SelectedIndex));
                }
            }
        }
    }
}
