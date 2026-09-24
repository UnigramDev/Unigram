//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Telegram.Views.Supergroups.Popups;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupMembersViewModel : SupergroupMembersViewModelBase, IDelegable<ISupergroupMembersDelegate>, IHandle
    {
        public SupergroupMembersViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterRecent(), query => new SupergroupMembersFilterSearch(query))
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
                    if (update.NewChatMember.Status is ChatMemberStatusMember or ChatMemberStatusAdministrator or ChatMemberStatusCreator or ChatMemberStatusRestricted)
                    {
                        item.Tag = update.NewChatMember.Tag;
                        item.Status = update.NewChatMember.Status;

                        Delegate?.UpdateMember(item);
                    }
                    else
                    {
                        Members.Source.Remove(item);
                    }
                }
                else if (update.NewChatMember.Status is ChatMemberStatusMember or ChatMemberStatusAdministrator or ChatMemberStatusCreator or ChatMemberStatusRestricted)
                {
                    Members.Source.Insert(0, update.NewChatMember);
                }
            }
        }

        public bool IsEmbedded { get; set; }

        private bool _hasHiddenMembers;
        public bool HasHiddenMembers
        {
            get => _hasHiddenMembers;
            set => SetHiddenMembers(value);
        }

        public void UpdateHiddenMembers(bool value)
        {
            Set(ref _hasHiddenMembers, value, nameof(HasHiddenMembers));
        }

        private void SetHiddenMembers(bool value)
        {
            if (Chat.Type is ChatTypeSupergroup supergroupType && ClientService.TryGetSupergroupFull(Chat, out SupergroupFullInfo supergroup))
            {
                if (supergroup.CanHideMembers)
                {
                    Set(ref _hasHiddenMembers, value, nameof(HasHiddenMembers));
                    ClientService.Send(new ToggleSupergroupHasHiddenMembers(supergroupType.SupergroupId, value));
                }
                else
                {
                    Set(ref _hasHiddenMembers, false, nameof(HasHiddenMembers));
                }
            }
        }

        private bool _canEditTags;
        public bool CanEditTags
        {
            get => _canEditTags;
            set => SetEditTags(value);
        }

        public void UpdateEditTags(bool value)
        {
            Set(ref _canEditTags, value, nameof(CanEditTags));
        }

        private void SetEditTags(bool value)
        {
            if (_canEditTags == value)
            {
                return;
            }

            var permissions = new ChatPermissions
            {
                CanChangeInfo = Chat.Permissions.CanChangeInfo,
                CanPinMessages = Chat.Permissions.CanPinMessages,
                CanInviteUsers = Chat.Permissions.CanInviteUsers,
                CanSendPhotos = Chat.Permissions.CanSendPhotos,
                CanSendVideos = Chat.Permissions.CanSendVideos,
                CanSendOtherMessages = Chat.Permissions.CanSendOtherMessages,
                CanSendAudios = Chat.Permissions.CanSendAudios,
                CanSendDocuments = Chat.Permissions.CanSendDocuments,
                CanSendVoiceNotes = Chat.Permissions.CanSendVoiceNotes,
                CanSendVideoNotes = Chat.Permissions.CanSendVideoNotes,
                CanSendPolls = Chat.Permissions.CanSendPolls,
                CanAddLinkPreviews = Chat.Permissions.CanAddLinkPreviews,
                CanSendBasicMessages = Chat.Permissions.CanSendBasicMessages,
                CanEditTag = value,
            };

            Set(ref _canEditTags, value, nameof(CanEditTags));
            ClientService.Send(new SetChatPermissions(Chat.Id, permissions));
        }

        public void Add()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup or ChatTypeBasicGroup)
            {
                ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationInviteToChat(chat.Id));
            }
        }

        #region Context menu

        public void PromoteMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditAdministratorPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public void RestrictMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(new SupergroupEditRestrictedPopup(), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public async void RemoveMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            // The member may already be gone from the source (removed by an update, or a
            // search swapping it) while still on screen; the ban is sent regardless.
            var source = Members.Source;
            var index = source.IndexOf(member);
            if (index != -1)
            {
                source.RemoveAt(index);
            }

            // The list can shrink or be replaced by a search while the request is in
            // flight; a replacement was loaded after the failure and already has the member.
            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusBanned()));
            if (response is Error && index != -1 && source == Members.Source)
            {
                source.Insert(Math.Min(index, source.Count), member);
            }
        }

        public void EditTag(ChatMember member)
        {
            ShowPopup(new MemberTagEditPopup(ClientService, Aggregator, Chat, member));
        }

        #endregion
    }
}
