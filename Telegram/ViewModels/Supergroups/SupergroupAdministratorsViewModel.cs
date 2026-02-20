//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views;
using Telegram.Views.Supergroups.Popups;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupAdministratorsViewModel : SupergroupMembersViewModelBase, IHandle
    {
        public SupergroupAdministratorsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterAdministrators(), query => new SupergroupMembersFilterAdministrators())
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
                    if (update.NewChatMember.Status is ChatMemberStatusAdministrator or ChatMemberStatusCreator)
                    {
                        item.Status = update.NewChatMember.Status;
                    }
                    else
                    {
                        Members.Source.Remove(item);
                    }
                }
                else if (update.NewChatMember.Status is ChatMemberStatusAdministrator or ChatMemberStatusCreator)
                {
                    Members.Source.Insert(0, update.NewChatMember);
                }
            }
        }

        private bool _isAggressiveAntiSpamEnabled;
        public bool IsAggressiveAntiSpamEnabled
        {
            get => _isAggressiveAntiSpamEnabled;
            set => SetIsAggressiveAntiSpamEnabled(value);
        }

        public void UpdateIsAggressiveAntiSpamEnabled(bool value)
        {
            Set(ref _isAggressiveAntiSpamEnabled, value, nameof(IsAggressiveAntiSpamEnabled));
        }

        private void SetIsAggressiveAntiSpamEnabled(bool value)
        {
            if (Chat.Type is ChatTypeSupergroup supergroupType && ClientService.TryGetSupergroupFull(Chat, out SupergroupFullInfo supergroup))
            {
                if (supergroup.CanToggleAggressiveAntiSpam)
                {
                    Set(ref _isAggressiveAntiSpamEnabled, value, nameof(IsAggressiveAntiSpamEnabled));
                    ClientService.Send(new ToggleSupergroupHasAggressiveAntiSpamEnabled(supergroupType.SupergroupId, value));
                }
                else
                {
                    Set(ref _isAggressiveAntiSpamEnabled, false, nameof(IsAggressiveAntiSpamEnabled));
                }
            }
        }

        private bool _signMessages;
        public bool SignMessages
        {
            get => _signMessages;
            set => SetSignMessages(value);
        }

        public void UpdateSignMessages(bool value)
        {
            Set(ref _signMessages, value, nameof(SignMessages));
        }

        private void SetSignMessages(bool signMessages)
        {
            if (Set(ref _signMessages, signMessages, nameof(SignMessages)) && ClientService.TryGetSupergroup(Chat, out Supergroup supergroup))
            {
                if (signMessages is false)
                {
                    Set(ref _showMessageSender, false, nameof(ShowMessageSender));
                }

                ClientService.Send(new ToggleSupergroupSignMessages(supergroup.Id, signMessages, _showMessageSender));
            }
        }

        private bool _showMessageSender;
        public bool ShowMessageSender
        {
            get => _showMessageSender;
            set => SetShowMessageSender(value);
        }

        public void UpdateShowMessageSender(bool value)
        {
            Set(ref _showMessageSender, value, nameof(ShowMessageSender));
        }

        private void SetShowMessageSender(bool showMessageSender)
        {
            if (Set(ref _showMessageSender, showMessageSender, nameof(ShowMessageSender)) && ClientService.TryGetSupergroup(Chat, out Supergroup supergroup))
            {
                if (_showMessageSender)
                {
                    Set(ref _signMessages, true, nameof(SignMessages));
                }

                ClientService.Send(new ToggleSupergroupSignMessages(supergroup.Id, _signMessages, showMessageSender));
            }
        }

        public void EventLog()
        {
            if (_chat is Chat chat)
            {
                NavigationService.Navigate(typeof(ChatEventLogPage), chat.Id);
            }
        }

        public void Add()
        {
            if (_chat is Chat chat)
            {
                NavigationService.ShowPopupAsync(new SupergroupChooseMemberPopup(), new SupergroupChooseMemberArgs(chat.Id, SupergroupChooseMemberMode.Promote));
            }
        }

        #region Context menu

        public void EditMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null || member == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public async void DismissMember(ChatMember member)
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

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusMember()));
            if (response is Error)
            {
                Members.Source.Insert(index, member);
            }
        }

        #endregion
    }
}
