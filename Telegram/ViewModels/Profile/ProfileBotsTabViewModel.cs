//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Profile
{
    public partial class ProfileBotsTabViewModel : ViewModelBase, IHandle, IIncrementalCollectionOwner
    {
        private long _chatId;
        private long _botUserId;

        public ProfileBotsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<User>(this);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId && ClientService.TryGetChat(chatId, out Chat chat) && ClientService.TryGetUser(chat, out User user))
            {
                _chatId = chatId;
                _botUserId = user.Id;
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdatePremiumState>(this, Handle);
        }

        private void Handle(UpdatePremiumState update)
        {
            if (update.IsPremium && _canUnlockMore)
            {
                Dispatcher.Dispatch(Reload);
            }
        }

        private void Reload()
        {
            CanUnlockMore = false;

            HasMoreItems = true;
            Items.Clear();
        }

        public IncrementalCollection<User> Items { get; private set; }

        private bool _canUnlockMore;
        public bool CanUnlockMore
        {
            get => _canUnlockMore && !IsPremium && IsPremiumAvailable;
            set => Set(ref _canUnlockMore, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => Set(ref _totalCount, value);
        }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var total = 0u;

            var response = await ClientService.SendAsync(new GetBotSimilarBots(_botUserId));
            if (response is Telegram.Td.Api.Users users)
            {
                CanUnlockMore = users.TotalCount > users.UserIds.Count;
                TotalCount = users.TotalCount;

                foreach (var chat in ClientService.GetUsers(users.UserIds))
                {
                    Items.Add(chat);

                    total++;
                }
            }

            HasMoreItems = false;

            return new LoadMoreItemsResult
            {
                Count = total
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public void UnlockMore()
        {
            NavigationService.ShowPromo(new PremiumSourceLimitExceeded(new PremiumLimitTypeSimilarChatCount()));
        }
    }
}
