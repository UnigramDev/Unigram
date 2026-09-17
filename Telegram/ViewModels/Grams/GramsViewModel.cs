//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels.Grams
{
    public partial class GramsViewModel : ViewModelBase, IHandle
    {
        private readonly TransactionTabs<TonTransaction> _transactions;

        public GramsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _transactions = new TransactionTabs<TonTransaction>(LoadPageAsync, GetId, IsIncoming, OnTransactionsChanged);
        }

        public IncrementalCollectionView<TonTransaction, IncrementalCollection<TonTransaction>> Items => _transactions.Items;

        public bool HasTransactions => _transactions.HasTransactions;

        public long OwnedGramCount => ClientService.OwnedGramCount;

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateOwnedGramCount>(this, Handle);
        }

        private void Handle(UpdateOwnedGramCount update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(OwnedGramCount)));
        }

        private static string GetId(TonTransaction transaction)
        {
            return transaction.Id;
        }

        private static bool IsIncoming(TonTransaction transaction)
        {
            return transaction.GramAmount >= 0;
        }

        private void OnTransactionsChanged()
        {
            RaisePropertyChanged(nameof(HasTransactions));
        }

        private async Task<TransactionPage<TonTransaction>> LoadPageAsync(TransactionDirection direction, string offset, int limit)
        {
            Logger.Info();

            var response = await ClientService.GetTonTransactionsAsync(direction, offset, limit);
            if (response is TonTransactions transactions)
            {
                return new TransactionPage<TonTransaction>(transactions.Transactions, transactions.NextOffset);
            }

            return default;
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
