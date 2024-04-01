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
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public class ChatBoostsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private long _chatId;
        private string _nextOffset = string.Empty;

        public ChatBoostsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<ChatBoost>(this);
        }

        private double _headerHeight;
        public double HeaderHeight
        {
            get => _headerHeight;
            set => Set(ref _headerHeight, value);
        }

        public IncrementalCollection<ChatBoost> Items { get; }

        private int _level;
        public int Level
        {
            get => _level;
            set => Set(ref _level, value);
        }

        private int _boostCount;
        public int BoostCount
        {
            get => _boostCount;
            set => Set(ref _boostCount, value);
        }

        private int _currentLevelBoostCount;
        public int CurrentLevelBoostCount
        {
            get => _currentLevelBoostCount;
            set => Set(ref _currentLevelBoostCount, value);
        }

        private int _nextLevelBoostCount;
        public int NextLevelBoostCount
        {
            get => _nextLevelBoostCount;
            set => Set(ref _nextLevelBoostCount, value);
        }

        private int _premiumMemberCount;
        public int PremiumMemberCount
        {
            get => _premiumMemberCount;
            set => Set(ref _premiumMemberCount, value);
        }

        private double _premiumMemberPercentage;
        public double PremiumMemberPercentage
        {
            get => _premiumMemberPercentage;
            set => Set(ref _premiumMemberPercentage, value);
        }

        private string _link;
        public string Link
        {
            get => _link;
            set => Set(ref _link, value);
        }

        private bool _isEmpty;
        public bool IsEmpty
        {
            get => _isEmpty;
            set => Set(ref _isEmpty, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                _chatId = chatId;

                var response = await ClientService.SendAsync(new GetChatBoostStatus(chatId));
                if (response is ChatBoostStatus status)
                {
                    Level = status.Level;
                    CurrentLevelBoostCount = status.CurrentLevelBoostCount;
                    NextLevelBoostCount = Math.Max(status.BoostCount, status.NextLevelBoostCount);
                    BoostCount = status.BoostCount;
                    PremiumMemberCount = status.PremiumMemberCount;
                    PremiumMemberPercentage = status.PremiumMemberPercentage;
                    Link = status.BoostUrl.Replace("https://", string.Empty);
                }
            }
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var total = 0u;

            var response = await ClientService.SendAsync(new GetChatBoosts(_chatId, false, _nextOffset, Items.Count > 5 ? 50 : Items.Count > 0 ? 45 : 5));
            if (response is FoundChatBoosts boosts)
            {
                foreach (var item in boosts.Boosts)
                {
                    total++;
                    Items.Add(item);
                }

                _nextOffset = boosts.NextOffset;
                HasMoreItems = false;

                RemainingItems = boosts.NextOffset.Length > 0 ? boosts.TotalCount - Items.Count : 0;
                RaisePropertyChanged(nameof(HasRemainingItems));
            }

            IsEmpty = Items.Count == 0;

            return new LoadMoreItemsResult
            {
                Count = total
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        private int _remainingItems;
        public int RemainingItems
        {
            get => _remainingItems;
            set => Set(ref _remainingItems, value);
        }

        public bool HasRemainingItems => RemainingItems > 0;

        public void CopyLink()
        {
            MessageHelper.CopyLink("https://" + Link);
        }

        public async void ShareLink()
        {
            await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationPostLink(new HttpUrl("https://" + Link)));
        }

        public void OpenProfile(ChatBoost chatBoost)
        {
            var userId = chatBoost.Source switch
            {
                ChatBoostSourceGiftCode giftCode => giftCode.UserId,
                ChatBoostSourceGiveaway giveaway => giveaway.UserId,
                ChatBoostSourcePremium premium => premium.UserId,
                _ => 0
            };

            if (userId != 0 && userId != ClientService.Options.MyId)
            {
                NavigationService.NavigateToUser(userId);
            }
            else if (userId == 0)
            {

            }
        }
    }
}
