//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Chats;
using Telegram.Views.Monetization.Popups;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public partial class ChatStarsViewModel : ViewModelBase, IIncrementalCollectionOwner, IHandle
    {
        private MessageSender _ownerId;

        private string _nextOffset = string.Empty;

        public ChatStarsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<object>(this);
        }

        private double _headerHeight;
        public double HeaderHeight
        {
            get => _headerHeight;
            set => Set(ref _headerHeight, value);
        }

        private ChartViewData _revenue;
        public ChartViewData Revenue
        {
            get => _revenue;
            set => Set(ref _revenue, value);
        }

        private StarAmount _availableAmount;
        public StarAmount AvailableAmount
        {
            get => _availableAmount;
            set => Set(ref _availableAmount, value);
        }

        private StarAmount _previousAmount;
        public StarAmount PreviousAmount
        {
            get => _previousAmount;
            set => Set(ref _previousAmount, value);
        }

        private StarAmount _totalAmount;
        public StarAmount TotalAmount
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

        private double _usdRate;
        public double UsdRate
        {
            get => _usdRate;
            set => Set(ref _usdRate, value);
        }

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

        public IncrementalCollection<object> Items { get; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                if (ClientService.TryGetChat(chatId, out Chat chat))
                {
                    if (chat.Type is ChatTypePrivate privata)
                    {
                        parameter = new MessageSenderUser(privata.UserId);
                    }
                    else
                    {
                        parameter = new MessageSenderChat(chatId);
                    }
                }
            }

            _ownerId = parameter as MessageSender;
            IsLoading = true;

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
            AvailableAmount = status.AvailableAmount;
            PreviousAmount = status.CurrentAmount;
            TotalAmount = status.TotalAmount;

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
                ShowToast(string.Format(Strings.BotStarsWithdrawalToast, Formatter.Duration(NextWithdrawalDate - DateTime.Now.ToTimestamp())), ToastPopupIcon.Info);
                return;
            }

            var popup = new InputPopup(InputPopupType.Stars);
            popup.Value = AvailableAmount?.StarCount ?? 0;
            popup.Maximum = AvailableAmount?.StarCount ?? 0;

            popup.Title = Strings.BotStarsButtonWithdrawUntil;
            popup.Header = Strings.BotStarsWithdrawPlaceholder;
            popup.PrimaryButtonText = Strings.OK;
            popup.SecondaryButtonText = Strings.Cancel;

            popup.Validating += (s, args) =>
            {
                if (args.Value < ClientService.Options.StarWithdrawalCountMin)
                {
                    ShowToast(Locale.Declension(Strings.R.BotStarsWithdrawMinLimit, ClientService.Options.StarWithdrawalCountMin), ToastPopupIcon.Info);
                    args.Cancel = true;
                }
            };

            var confirm = await ShowPopupAsync(popup);
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

        public void OpenAffiliate()
        {
            if (_ownerId is MessageSenderChat senderChat)
            {
                NavigationService.Navigate(typeof(ChatAffiliatePage), new AffiliateTypeChannel(senderChat.ChatId));
            }
            else if (_ownerId is MessageSenderUser senderUser)
            {
                if (senderUser.UserId == ClientService.Options.MyId)
                {
                    NavigationService.Navigate(typeof(ChatAffiliatePage), new AffiliateTypeCurrentUser());
                }
                else
                {
                    NavigationService.Navigate(typeof(ChatAffiliatePage), new AffiliateTypeBot(senderUser.UserId));
                }
            }
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.GetStarTransactionsAsync(_ownerId, string.Empty, null, _nextOffset, 20);
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
