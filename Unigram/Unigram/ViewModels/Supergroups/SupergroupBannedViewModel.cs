using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Supergroups;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupBannedViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupBannedViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new SupergroupMembersFilterBanned(), query => new SupergroupMembersFilterBanned(query))
        {
            AddCommand = new RelayCommand(AddExecute);
            MemberUnbanCommand = new RelayCommand<ChatMember>(MemberUnbanExecute);
        }

        public RelayCommand AddCommand { get; }
        private void AddExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupAddRestrictedPage), chat.Id);
        }

        #region Context menu

        public RelayCommand<ChatMember> MemberUnbanCommand { get; }
        private async void MemberUnbanExecute(ChatMember member)
        {
            var chat = _chat;
            if (chat == null || Members == null)
            {
                return;
            }

            var index = Members.IndexOf(member);
            if (index == -1)
            {
                return;
            }

            Members.Remove(member);

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.UserId, new ChatMemberStatusMember()));
            if (response is Error)
            {
                Members.Insert(index, member);
            }
        }

        #endregion
    }
}
