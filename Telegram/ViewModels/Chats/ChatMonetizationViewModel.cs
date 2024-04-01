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
using Telegram.Controls.Cells;
using Telegram.Controls.Cells.Monetization;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Monetization.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public class ChatMonetizationViewModel : ViewModelBase
    {
        public ChatMonetizationViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<TransactionInfo>();
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

        private ChatStatistics _statistics;
        public ChatStatistics Statistics
        {
            get => _statistics;
            set => Set(ref _statistics, value);
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

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

        public MvxObservableCollection<TransactionInfo> Items { get; }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var chatId = (long)parameter;

            IsLoading = true;

            Chat = ClientService.GetChat(chatId);

            AvailableAmount = new CryptoAmount
            {
                Cryptocurrency = "TON",
                CryptocurrencyAmount = 5412,
                Currency = "USD",
                Amount = 11910
            };

            PreviousAmount = new CryptoAmount
            {
                Cryptocurrency = "TON",
                CryptocurrencyAmount = 8452,
                Currency = "USD",
                Amount = 18590
            };

            TotalAmount = new CryptoAmount
            {
                Cryptocurrency = "TON",
                CryptocurrencyAmount = 20689,
                Currency = "USD",
                Amount = 45520
            };

            Items.Add(new TransactionInfo
            {
                Cryptocurrency = "TON",
                CryptocurrencyAmount = 8452,
                Date = DateTime.Now.ToTimestamp(),
                EndDate = DateTime.Now.ToTimestamp()
            });

            Items.Add(new TransactionInfo
            {
                DestinationAddress = "UQBaNWR3X8kXidbrGca5oGdDXzePSILQWujfKubn0Dqhdi-a",
                Cryptocurrency = "TON",
                CryptocurrencyAmount = -5412,
                Date = DateTime.Now.ToTimestamp()
            });

            Items.Add(new TransactionInfo
            {
                Cryptocurrency = "TON",
                CryptocurrencyAmount = 19025,
                Date = DateTime.Now.ToTimestamp(),
                EndDate = DateTime.Now.ToTimestamp()
            });

            Items.Add(new TransactionInfo
            {
                DestinationAddress = "UQBaNWR3X8kXidbrGca5oGdDXzePSILQWujfKubn0Dqhdi-a",
                Cryptocurrency = "TON",
                CryptocurrencyAmount = -28034,
                Date = DateTime.Now.ToTimestamp()
            });

            var response = await ClientService.SendAsync(new GetChatStatistics(chatId, false));
            if (response is ChatStatistics statistics)
            {
                Statistics = statistics;

                if (statistics is ChatStatisticsChannel channelStats)
                {
                    Impressions = ChartViewData.Create(channelStats.ViewCountByHourGraph, Strings.MonetizationGraphImpressions, 2);
                    Revenue = ChartViewData.Create(channelStats.MemberCountGraph, Strings.MonetizationGraphRevenue, 0);

                }
                else if (statistics is ChatStatisticsSupergroup groupStats)
                {
                }
            }

            IsLoading = false;
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

        public async void ShowTransaction(TransactionInfo info)
        {
            await ShowPopupAsync(new TransactionPopup(ClientService, Chat, info));
        }

        public async void LearnMore()
        {
            await ShowPopupAsync(new LearnMorePopup());
        }
    }
}
