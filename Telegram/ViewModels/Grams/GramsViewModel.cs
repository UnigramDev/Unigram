//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Text;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

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

        private bool _canWithdraw;
        public bool CanWithdraw
        {
            get => _canWithdraw;
            set => Set(ref _canWithdraw, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var response = await ClientService.SendAsync(new GetGramRevenueStatistics(false));
            if (response is GramRevenueStatistics statistics)
            {
                UpdateStatus(statistics.Status);
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateOwnedGramCount>(this, Handle)
                .Subscribe<UpdateGramRevenueStatus>(Handle);
        }

        private void Handle(UpdateOwnedGramCount update)
        {
            BeginOnUIThread(() => RaisePropertyChanged(nameof(OwnedGramCount)));
        }

        private void Handle(UpdateGramRevenueStatus update)
        {
            BeginOnUIThread(() => UpdateStatus(update.Status));
        }

        private void UpdateStatus(GramRevenueStatus status)
        {
            CanWithdraw = status.WithdrawalEnabled && status.AvailableAmount > 0;
        }

        public async void Withdraw()
        {
            var state = await ClientService.SendAsync(new GetPasswordState());
            if (state is not PasswordState passwordState)
            {
                return;
            }
            else if (!passwordState.HasPassword)
            {
                ShowSecurityCheck(true);
                return;
            }

            var placeholder = string.IsNullOrEmpty(passwordState.PasswordHint)
                ? Strings.LoginPassword
                : passwordState.PasswordHint;

            var result = await ShowInputAsync(InputPopupType.Password, Strings.PleaseEnterCurrentPasswordWithdraw, Strings.TwoStepVerification, placeholder, Strings.OK, Strings.Cancel);
            if (result.Result != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetGramWithdrawalUrl(result.Text));
            if (response is HttpUrl httpUrl)
            {
                MessageHelper.OpenUrl(null, null, httpUrl.Url);
            }
            else if (response is Error error)
            {
                if (error.Message.Equals("PASSWORD_MISSING") || error.Message.StartsWith("PASSWORD_TOO_FRESH_") || error.Message.StartsWith("SESSION_TOO_FRESH_"))
                {
                    ShowSecurityCheck(error.Message.Equals("PASSWORD_MISSING"));
                }
                else
                {
                    ShowToast(error);
                }
            }
        }

        private async void ShowSecurityCheck(bool passwordMissing)
        {
            var builder = new StringBuilder();
            builder.AppendLine(Strings.WithdrawChannelAlertText);
            builder.AppendLine($"\u2022 {Strings.EditAdminTransferAlertText1}");
            builder.AppendLine($"\u2022 {Strings.EditAdminTransferAlertText2}");

            if (passwordMissing)
            {
                var confirm = await ShowPopupAsync(builder.ToString(), Strings.EditAdminTransferAlertTitle, Strings.EditAdminTransferSetPassword, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    NavigationService.NavigateToPasswordSetup();
                }
            }
            else
            {
                builder.AppendLine();
                builder.AppendLine(Strings.EditAdminTransferAlertText3);

                await ShowPopupAsync(builder.ToString(), Strings.EditAdminTransferAlertTitle, Strings.OK);
            }
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
