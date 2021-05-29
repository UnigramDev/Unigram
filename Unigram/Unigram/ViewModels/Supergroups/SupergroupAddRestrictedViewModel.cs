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
        public SupergroupAddRestrictedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
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
                var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, memberId, new ChatMemberStatusBanned()));
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
