//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Views.Supergroups;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupAddRestrictedViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupAddRestrictedViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
            AddCommand = new RelayCommand<MessageSender>(AddExecute);
        }

        private SearchMembersAndUsersCollection _search;
        public new SearchMembersAndUsersCollection Search
        {
            get => _search;
            set => Set(ref _search, value);
        }

        public RelayCommand<MessageSender> AddCommand { get; }
        private async void AddExecute(MessageSender memberId)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
            {
                var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, memberId, new ChatMemberStatusBanned()));
                if (response is Ok)
                {
                    NavigationService.GoBack();
                }
                else
                {

                }
            }
            else
            {
                NavigationService.Navigate(typeof(SupergroupEditRestrictedPage), state: NavigationState.GetChatMember(chat.Id, memberId));
            }
        }
    }
}
