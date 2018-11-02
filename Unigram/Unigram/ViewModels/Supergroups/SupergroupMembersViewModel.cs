using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Channels;
using Unigram.Views.Chats;
using Unigram.Views.Supergroups;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupMembersViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupMembersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
            AddCommand = new RelayCommand(AddExecute);

            MemberPromoteCommand = new RelayCommand<ChatMember>(MemberPromoteExecute);
            MemberRemoveCommand = new RelayCommand<ChatMember>(MemberRemoveExecute);
        }

        public RelayCommand AddCommand { get; }
        private void AddExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(ChatInvitePage), chat.Id);
        }

        #region Context menu

        public RelayCommand<ChatMember> MemberPromoteCommand { get; }
        private void MemberPromoteExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupEditAdministratorPage), new ChatMemberNavigation(chat.Id, member.UserId));
        }

        public RelayCommand<ChatMember> MemberRemoveCommand { get; }
        private async void MemberRemoveExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null || _members == null)
            {
                return;
            }

            var index = _members.IndexOf(member);

            _members.Remove(member);

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.UserId, new ChatMemberStatusBanned()));
            if (response is Error)
            {
                _members.Insert(index, member);
            }
        }

        #endregion
    }
}
