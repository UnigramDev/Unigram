//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Supergroups;
using Telegram.Views;
using Telegram.Views.Chats.Popups;
using Telegram.Views.Monetization.Popups;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public class ChatRevenueViewModel : MultiViewModelBase, IIncrementalCollectionOwner, IHandle
    {
        private ChatBoostStatus _status;
        private ChatBoostFeatures _features;

        public ChatRevenueViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Stars = TypeResolver.Current.Resolve<ChatStarsViewModel>(clientService.SessionId);
            Items = new IncrementalCollection<ChatRevenueTransaction>(this);

            Children.Add(Stars);
        }

        private double _headerHeight;
        public double HeaderHeight
        {
            get => _headerHeight;
            set => Set(ref _headerHeight, value);
        }

        public ChatStarsViewModel Stars { get; }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private ChartViewData _impressions;
        public ChartViewData Impressions
        {
            get => _impressions;
            set => Set(ref _impressions, value);
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

        private bool _disableSponsoredMessages;
        public bool DisableSponsoredMessages
        {
            get => _disableSponsoredMessages;
            set => Set(ref _disableSponsoredMessages, value);
        }

        private int _minSponsoredMessageDisableBoostLevel;
        public int MinSponsoredMessageDisableBoostLevel
        {
            get => _minSponsoredMessageDisableBoostLevel;
            set => Set(ref _minSponsoredMessageDisableBoostLevel, value);
        }

        public double UsdRate { get; private set; }

        public bool WithdrawalEnabled => AvailableAmount?.CryptocurrencyAmount > 0 && ClientService.Options.CanWithdrawChatRevenue;

        public IncrementalCollection<ChatRevenueTransaction> Items { get; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            IsLoading = true;

            Chat = ClientService.GetChat(chatId);

            if (ClientService.TryGetSupergroup(Chat, out Supergroup supergroup))
            {
                IsOwner = supergroup.Status is ChatMemberStatusCreator;
            }

            if (ClientService.TryGetSupergroupFull(Chat, out SupergroupFullInfo fullInfo))
            {
                DisableSponsoredMessages = !fullInfo.CanHaveSponsoredMessages;
            }

            await LoadAsync();

            IsLoading = false;
        }

        private async Task LoadAsync()
        {
            if (Chat == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetChatRevenueStatistics(Chat.Id, false));
            if (response is ChatRevenueStatistics statistics)
            {
                Impressions = ChartViewData.Create(statistics.RevenueByHourGraph, Strings.MonetizationGraphImpressions, 5);
                Revenue = ChartViewData.Create(statistics.RevenueGraph, Strings.MonetizationGraphRevenue, 7);
                UsdRate = statistics.UsdRate;

                UpdateAmount(statistics.RevenueAmount);
            }

            var response1 = await ClientService.SendAsync(new GetChatBoostFeatures(Chat.Type is ChatTypeSupergroup { IsChannel: true }));
            var response2 = await ClientService.SendAsync(new GetChatBoostStatus(Chat.Id));

            if (response1 is ChatBoostFeatures features && response2 is ChatBoostStatus status)
            {
                _features = features;
                _status = status;

                int MinLevelOrZero(int level)
                {
                    return level < status.Level ? 0 : level;
                }

                MinSponsoredMessageDisableBoostLevel = MinLevelOrZero(features.MinSponsoredMessageDisableBoostLevel);
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateChatRevenueAmount>(this, Handle);
        }

        private void Handle(UpdateChatRevenueAmount update)
        {
            BeginOnUIThread(() =>
            {
                HasMoreItems = true;
                Items.Clear();

                UpdateAmount(update.RevenueAmount);
            });
        }

        private void UpdateAmount(ChatRevenueAmount amount)
        {
            AvailableAmount = new CryptoAmount
            {
                Cryptocurrency = amount.Cryptocurrency,
                CryptocurrencyAmount = amount.AvailableAmount,
                UsdRate = UsdRate,
            };

            PreviousAmount = new CryptoAmount
            {
                Cryptocurrency = amount.Cryptocurrency,
                CryptocurrencyAmount = amount.BalanceAmount,
                UsdRate = UsdRate,
            };

            TotalAmount = new CryptoAmount
            {
                Cryptocurrency = amount.Cryptocurrency,
                CryptocurrencyAmount = amount.TotalAmount,
                UsdRate = UsdRate,
            };

            RaisePropertyChanged(nameof(WithdrawalEnabled));
        }

        public async void Transfer()
        {
            //if (!IsAddressValid)
            //{
            //    RaisePropertyChanged("ADDRESS_INVALID");
            //    return;
            //}

            //var confirm = await ShowPopupAsync(new TransferPopup(Address, AvailableAmount));
            //if (confirm == ContentDialogResult.Primary)
            //{

            //}
        }

        public async void ShowTransaction(ChatRevenueTransaction info)
        {
            await ShowPopupAsync(new TransactionPopup(ClientService, Chat, info));
        }

        public async void LearnMore()
        {
            await ShowPopupAsync(new LearnMorePopup());
        }

        public async void ToggleSponsoredMessages()
        {
            if (Chat is not Chat chat)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetChatBoostStatus(chat.Id));
            if (response is not ChatBoostStatus status || _features == null || !ClientService.TryGetSupergroup(Chat, out Supergroup supergroup))
            {
                return;
            }

            if (_features.MinSponsoredMessageDisableBoostLevel > status.Level)
            {
                await ShowPopupAsync(new ChatBoostFeaturesPopup(ClientService, NavigationService, chat, status, null, _features, ChatBoostFeature.DisableSponsoredMessages, _features.MinSponsoredMessageDisableBoostLevel));
                return;
            }

            ClientService.Send(new ToggleSupergroupCanHaveSponsoredMessages(supergroup.Id, DisableSponsoredMessages));
            DisableSponsoredMessages = !DisableSponsoredMessages;
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new GetChatRevenueTransactions(Chat.Id, Items.Count, 10));
            if (response is ChatRevenueTransactions transactions)
            {
                foreach (var transaction in transactions.Transactions)
                {
                    Items.Add(transaction);
                    totalCount++;
                }
            }

            HasMoreItems = totalCount > 0;
            IsEmpty = Items.Count == 0;

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
