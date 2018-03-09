using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Channels;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupMembersViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupMembersViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
        {
            ParticipantPromoteCommand = new RelayCommand<ChatMember>(ParticipantPromoteExecute);
            MemberRemoveCommand = new RelayCommand<ChatMember>(MemberRemoveExecute);
        }

        #region Context menu

        public RelayCommand<ChatMember> ParticipantPromoteCommand { get; }
        private void ParticipantPromoteExecute(ChatMember participant)
        {
            //if (_item == null)
            //{
            //    return;
            //}

            //NavigationService.Navigate(typeof(ChannelAdminRightsPage), TLTuple.Create(_item.ToPeer(), participant));
        }

        public RelayCommand<ChatMember> MemberRemoveCommand { get; }
        private async void MemberRemoveExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var index = Members.IndexOf(member);

            Members.Remove(member);

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.UserId, new ChatMemberStatusBanned()));
            if (response is Error)
            {
                Members.Insert(index, member);
            }
        }

        #endregion
    }
}
