//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Supergroups.Popups;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupBannedViewModel : SupergroupMembersViewModelBase, IHandle
    {
        public SupergroupBannedViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterBanned(), query => new SupergroupMembersFilterBanned(query))
        {
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateChatMember>(this, Handle);
        }

        private void Handle(UpdateChatMember update)
        {
            if (update.ChatId == _chat.Id)
            {
                var item = Members.Source.FirstOrDefault(x => x.MemberId.AreTheSame(update.NewChatMember.MemberId));
                if (item != null)
                {
                    if (update.NewChatMember.Status is ChatMemberStatusBanned)
                    {
                        item.Status = update.NewChatMember.Status;
                    }
                    else
                    {
                        Members.Source.Remove(item);
                    }
                }
                else if (update.NewChatMember.Status is ChatMemberStatusBanned)
                {
                    Members.Source.Insert(0, update.NewChatMember);
                }
            }
        }

        public void Add()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupChooseMemberPopup(), new SupergroupChooseMemberArgs(chat.Id, SupergroupChooseMemberMode.Block));
        }

        #region Context menu

        public void OpenMember(ChatMember member)
        {
            NavigationService.NavigateToSender(member.MemberId);
        }

        public async void AddMember(ChatMember member)
        {
            await SetMemberStatusAsync(member, new ChatMemberStatusMember());
        }

        public async void UnbanMember(ChatMember member)
        {
            await SetMemberStatusAsync(member, new ChatMemberStatusLeft());
        }

        private async Task SetMemberStatusAsync(ChatMember member, ChatMemberStatus status)
        {
            var chat = _chat;
            if (chat == null || Members == null)
            {
                return;
            }

            var index = Members.Source.IndexOf(member);
            if (index == -1)
            {
                return;
            }

            Members.Source.Remove(member);

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, status));
            if (response is Error && index < Members.Source.Count)
            {
                Members.Source.Insert(index, member);
            }
        }

        #endregion
    }
}
