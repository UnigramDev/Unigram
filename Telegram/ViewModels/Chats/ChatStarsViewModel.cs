//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Monetization.Popups;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public class ChatStarsViewModel : ViewModelBase, IIncrementalCollectionOwner, IHandle
    {
        private MessageSender _ownerId;

        private string _nextOffset = string.Empty;

        public ChatStarsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<StarTransaction>(this);
        }

        private double _headerHeight;
        public double HeaderHeight
        {
            get => _headerHeight;
            set => Set(ref _headerHeight, value);
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private ChartViewData _revenue;
        public ChartViewData Revenue
        {
            get => _revenue;
            set => Set(ref _revenue, value);
        }

        private CryptoAmount _availableAmount;
        public CryptoAmount AvailableAmount
        {
            get => _availableAmount;
            set => Set(ref _availableAmount, value);
        }

        private CryptoAmount _previousAmount;
        public CryptoAmount PreviousAmount
        {
            get => _previousAmount;
            set => Set(ref _previousAmount, value);
        }

        private CryptoAmount _totalAmount;
        public CryptoAmount TotalAmount
        {
            get => _totalAmount;
            set => Set(ref _totalAmount, value);
        }

        private bool _isEmpty = true;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

        private bool _isOwner;
        public bool IsOwner
        {
            get => _isOwner;
            set => Set(ref _isOwner, value);
        }

        public double UsdRate { get; private set; }

        private bool _withdrawalEnabled;
        public bool WithdrawalEnabled
        {
            get => _withdrawalEnabled;
            set => Set(ref _withdrawalEnabled, value);
        }

        private int _nextWithdrawalDate;
        public int NextWithdrawalDate
        {
            get => _nextWithdrawalDate;
            set => Set(ref _nextWithdrawalDate, value);
        }

        public IncrementalCollection<StarTransaction> Items { get; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                parameter = new MessageSenderChat(chatId);
            }

            _ownerId = parameter as MessageSender;
            IsLoading = true;

            //Chat = ClientService.GetChat(chatId);

            //if (ClientService.TryGetSupergroup(Chat, out Supergroup supergroup))
            //{
            //    IsOwner = supergroup.Status is ChatMemberStatusCreator;
            //}

            await LoadAsync();

            IsLoading = false;
        }

        private async Task LoadAsync()
        {
            if (_ownerId == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetStarRevenueStatistics(_ownerId, false));
            if (response is StarRevenueStatistics statistics)
            {
                Revenue = ChartViewData.Create(statistics.RevenueByDayGraph, Strings.BotStarsChartRevenue, 8);
                UsdRate = statistics.UsdRate;

                UpdateAmount(statistics.Status);
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateStarRevenueStatus>(this, Handle);
        }

        private void Handle(UpdateStarRevenueStatus update)
        {
            if (update.OwnerId.AreTheSame(_ownerId))
            {
                BeginOnUIThread(() =>
                {
                    HasMoreItems = true;
                    Items.Clear();

                    UpdateAmount(update.Status);
                });
            }
        }

        private void UpdateAmount(StarRevenueStatus status)
        {
            AvailableAmount = new CryptoAmount
            {
                Cryptocurrency = "XTR",
                CryptocurrencyAmount = status.AvailableCount,
                UsdRate = UsdRate,
            };

            PreviousAmount = new CryptoAmount
            {
                Cryptocurrency = "XTR",
                CryptocurrencyAmount = status.CurrentCount,
                UsdRate = UsdRate,
            };

            TotalAmount = new CryptoAmount
            {
                Cryptocurrency = "XTR",
                CryptocurrencyAmount = status.TotalCount,
                UsdRate = UsdRate,
            };

            WithdrawalEnabled = status.WithdrawalEnabled;

            if (status.NextWithdrawalIn > 0)
            {
                NextWithdrawalDate = DateTime.Now.AddSeconds(status.NextWithdrawalIn).ToTimestamp();
            }
            else
            {
                NextWithdrawalDate = 0;
            }
        }

        public async void Transfer()
        {
            if (NextWithdrawalDate != 0)
            {
                ToastPopup.Show(string.Format(Strings.BotStarsWithdrawalToast, Formatter.Duration(NextWithdrawalDate - DateTime.Now.ToTimestamp())), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                return;
            }

            var popup = new InputPopup(InputPopupType.Stars);
            popup.Value = AvailableAmount?.CryptocurrencyAmount ?? 0;
            popup.Maximum = AvailableAmount?.CryptocurrencyAmount ?? 0;

            popup.Title = Strings.BotStarsButtonWithdrawUntil;
            popup.Header = Strings.BotStarsWithdrawPlaceholder;
            popup.PrimaryButtonText = Strings.OK;
            popup.SecondaryButtonText = Strings.Cancel;

            popup.Validating += (s, args) =>
            {
                if (args.Value < ClientService.Options.StarWithdrawalCountMin)
                {
                    ToastPopup.Show(Locale.Declension(Strings.R.BotStarsWithdrawMinLimit, ClientService.Options.StarWithdrawalCountMin), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
                    args.Cancel = true;
                }
            };

            var confirm = await popup.ShowQueuedAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var result = await ShowInputAsync(InputPopupType.Password, Strings.PleaseEnterCurrentPasswordWithdraw, Strings.TwoStepVerification, Strings.LoginPassword, Strings.OK, Strings.Cancel);
            if (result.Result != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetStarWithdrawalUrl(_ownerId, (long)popup.Value, result.Text));
            if (response is HttpUrl httpUrl)
            {
                MessageHelper.OpenUrl(null, null, httpUrl.Url);
            }
        }

        public async void ShowTransaction(StarTransaction transaction)
        {
            await ShowPopupAsync(new ReceiptPopup(ClientService, NavigationService, transaction));
        }

        public async void LearnMore()
        {
            await ShowPopupAsync(new LearnMorePopup());
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.GetStarTransactionsAsync(_ownerId, null, _nextOffset, 20);
            if (response is StarTransactions transactions)
            {
                foreach (var transaction in transactions.Transactions)
                {
                    Items.Add(transaction);
                    totalCount++;
                }

                _nextOffset = transactions.NextOffset;
                HasMoreItems = transactions.NextOffset.Length > 0;
            }
            else
            {
                HasMoreItems = false;
            }

            IsEmpty = Items.Count == 0;

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
