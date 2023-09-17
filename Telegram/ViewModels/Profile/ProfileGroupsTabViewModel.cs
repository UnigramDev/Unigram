//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Profile
{
    public class ProfileGroupsTabViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private long _userId;
        private long _nextOffsetId;

        public ProfileGroupsTabViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<Chat>(this);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is long chatId)
            {
                var chat = ClientService.GetChat(chatId);
                if (chat == null)
                {
                    return Task.CompletedTask;
                }

                var user = ClientService.GetUser(chat);
                if (user == null)
                {
                    return Task.CompletedTask;
                }

                _userId = user.Id;
            }

            return Task.CompletedTask;
        }

        public IncrementalCollection<Chat> Items { get; private set; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var total = 0u;

            var response = await ClientService.SendAsync(new GetGroupsInCommon(_userId, _nextOffsetId, 20));
            if (response is Telegram.Td.Api.Chats chats)
            {
                foreach (var chat in ClientService.GetChats(chats.ChatIds))
                {
                    _nextOffsetId = chat.Id;
                    Items.Add(chat);

                    total++;
                }
            }

            HasMoreItems = total > 0;

            return new LoadMoreItemsResult
            {
                Count = total
            };
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
