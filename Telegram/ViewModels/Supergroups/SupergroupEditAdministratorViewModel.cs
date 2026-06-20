//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Telegram.Views.Supergroups.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public partial class SupergroupEditAdministratorViewModel : ViewModelBase, IDelegable<IMemberPopupDelegate>
    {
        public IMemberPopupDelegate Delegate { get; set; }

        public SupergroupEditAdministratorViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private long _userId;

        private ChatMember _member;
        public ChatMember Member
        {
            get => _member;
            set => Set(ref _member, value);
        }

        private bool _isForum;
        public bool IsForum
        {
            get => _isForum;
            set => Set(ref _isForum, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            // Currently, we only support editing admin rights for users
            if (parameter is not SupergroupEditMemberArgs args || args.MemberId is not MessageSenderUser user)
            {
                return;
            }

            _userId = user.UserId;
            Chat = ClientService.GetChat(args.ChatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            IsForum = ClientService.IsForum(chat);

            var response = await ClientService.SendAsync(new GetChatMember(chat.Id, args.MemberId));
            if (response is ChatMember member)
            {
                var item = ClientService.GetUser(user.UserId);
                var cache = ClientService.GetUserFull(user.UserId);

                Delegate?.UpdateMember(chat, item, member);
                Delegate?.UpdateUser(chat, item, cache, false, false);

                if (cache == null)
                {
                    ClientService.Send(new GetUserFullInfo(user.UserId));
                }

                Member = member;
                CustomTitle = member.Tag;

                if (member.Status is ChatMemberStatusAdministrator administrator)
                {
                    CanChangeInfo = administrator.Rights.CanChangeInfo;
                    CanDeleteMessages = administrator.Rights.CanDeleteMessages;
                    CanEditMessages = administrator.Rights.CanEditMessages;
                    CanInviteUsers = administrator.Rights.CanInviteUsers;
                    CanManageDirectMessages = administrator.Rights.CanManageDirectMessages;
                    CanPinMessages = administrator.Rights.CanPinMessages;
                    CanManageTags = administrator.Rights.CanManageTags;
                    CanPostMessages = administrator.Rights.CanPostMessages;
                    CanPostStories = administrator.Rights.CanPostStories;
                    CanEditStories = administrator.Rights.CanEditStories;
                    CanDeleteStories = administrator.Rights.CanDeleteStories;
                    CanPromoteMembers = administrator.Rights.CanPromoteMembers;
                    CanRestrictMembers = administrator.Rights.CanRestrictMembers;
                    CanManageTopics = administrator.Rights.CanManageTopics;
                    CanManageVideoChats = administrator.Rights.CanManageVideoChats;
                    IsAnonymous = administrator.Rights.IsAnonymous;
                }
                else
                {
                    CanChangeInfo = true;
                    CanDeleteMessages = true;
                    CanEditMessages = true;
                    CanInviteUsers = true;
                    CanManageDirectMessages = true;
                    CanPinMessages = true;
                    CanManageTags = true;
                    CanPostMessages = true;
                    CanPostStories = true;
                    CanEditStories = true;
                    CanDeleteStories = true;
                    CanPromoteMembers = member.Status is ChatMemberStatusCreator;
                    CanRestrictMembers = true;
                    CanManageTopics = true;
                    CanManageVideoChats = true;

                    if (member.Status is ChatMemberStatusCreator creator)
                    {
                        IsAnonymous = creator.IsAnonymous;
                    }
                    else
                    {
                        IsAnonymous = false;
                    }
                }

                if (member.MemberId is MessageSenderUser senderUser && ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
                {
                    ProcessJoinRequests = fullInfo.GuardBotUserId == senderUser.UserId;
                }

                UpdateCanManageMessages();
                UpdateCanManageStories();
            }
        }

        private bool _isAdminAlready = true;
        public bool IsAdminAlready
        {
            get => _isAdminAlready;
            set => Set(ref _isAdminAlready, value);
        }

        public bool CanTransferOwnership
        {
            get
            {
                var chat = _chat;
                if (chat == null || _member?.Status is ChatMemberStatusCreator)
                {
                    return false;
                }

                var supergroup = ClientService.GetSupergroup(chat);
                if (supergroup == null || supergroup.Status is not ChatMemberStatusCreator)
                {
                    return false;
                }

                if (ClientService.TryGetUser(_userId, out var user))
                {
                    if (user.Type is not UserTypeRegular)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }

                return _canChangeInfo &&
                    _canDeleteMessages &&
                    _canInviteUsers &&
                    _canPromoteMembers &&
                    (!supergroup.IsChannel || _canEditMessages) &&
                    (supergroup.IsChannel || _canPinMessages) &&
                    (supergroup.IsChannel || _canManageTags) &&
                    (!supergroup.IsChannel || _canPostMessages) &&
                    (!supergroup.IsChannel || _canManageDirectMessages) &&
                    _canPostStories &&
                    _canEditStories &&
                    _canDeleteStories &&
                    _canRestrictMembers &&
                    (supergroup.IsChannel || _canManageVideoChats) &&
                    (!_isForum || _canManageTopics);
            }
        }

        #region Manage messages

        private bool? _canManageMessages;
        public bool? CanManageMessages
        {
            get => _canManageMessages;
            set
            {
                Set(ref _canManageMessages, value);

                if (value.HasValue)
                {
                    Set(ref _canPostMessages, value.Value, nameof(CanPostMessages));
                    Set(ref _canEditMessages, value.Value, nameof(CanEditMessages));
                    Set(ref _canDeleteMessages, value.Value, nameof(CanDeleteMessages));
                    Set(ref _canManageMessagesCount, value.Value ? 3 : 0, nameof(CanManageMessagesCount));
                }
            }
        }

        private void UpdateCanManageMessages()
        {
            var count = CountMessages();

            Set(ref _canManageMessagesCount, count, nameof(CanManageMessagesCount));
            Set(ref _canManageMessages, count == 0 ? false : count == 3 ? true : null, nameof(CanManageMessages));
        }

        private int CountMessages()
        {
            var count = 0;
            if (_canPostMessages)
            {
                count++;
            }
            if (_canEditMessages)
            {
                count++;
            }
            if (_canDeleteMessages)
            {
                count++;
            }

            return count;
        }

        private int _canManageMessagesCount;
        public int CanManageMessagesCount
        {
            get => _canManageMessagesCount;
            set => Set(ref _canManageMessagesCount, value);
        }

        #endregion

        #region Manage stories

        private bool? _canManageStories;
        public bool? CanManageStories
        {
            get => _canManageStories;
            set
            {
                Set(ref _canManageStories, value);

                if (value.HasValue)
                {
                    Set(ref _canPostStories, value.Value, nameof(CanPostStories));
                    Set(ref _canEditStories, value.Value, nameof(CanEditStories));
                    Set(ref _canDeleteStories, value.Value, nameof(CanDeleteStories));
                    Set(ref _canManageStoriesCount, value.Value ? 3 : 0, nameof(CanManageStoriesCount));
                }
            }
        }

        private void UpdateCanManageStories()
        {
            var count = CountStories();

            Set(ref _canManageStoriesCount, count, nameof(CanManageStoriesCount));
            Set(ref _canManageStories, count == 0 ? false : count == 3 ? true : null, nameof(CanManageStories));
        }

        private int CountStories()
        {
            var count = 0;
            if (_canPostStories)
            {
                count++;
            }
            if (_canEditStories)
            {
                count++;
            }
            if (_canDeleteStories)
            {
                count++;
            }

            return count;
        }

        private int _canManageStoriesCount;
        public int CanManageStoriesCount
        {
            get => _canManageStoriesCount;
            set => Set(ref _canManageStoriesCount, value);
        }

        #endregion

        #region Flags

        private bool _canChangeInfo;
        public bool CanChangeInfo
        {
            get => _canChangeInfo;
            set
            {
                if (Set(ref _canChangeInfo, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _canPostMessages;
        public bool CanPostMessages
        {
            get => _canPostMessages;
            set
            {
                if (Set(ref _canPostMessages, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                    UpdateCanManageMessages();
                }
            }
        }

        private bool _canEditMessages;
        public bool CanEditMessages
        {
            get => _canEditMessages;
            set
            {
                if (Set(ref _canEditMessages, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                    UpdateCanManageMessages();
                }
            }
        }

        private bool _canDeleteMessages;
        public bool CanDeleteMessages
        {
            get => _canDeleteMessages;
            set
            {
                if (Set(ref _canDeleteMessages, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                    UpdateCanManageMessages();
                }
            }
        }

        private bool _canPostStories;
        public bool CanPostStories
        {
            get => _canPostStories;
            set
            {
                if (Set(ref _canPostStories, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                    UpdateCanManageStories();
                }
            }
        }

        private bool _canEditStories;
        public bool CanEditStories
        {
            get => _canEditStories;
            set
            {
                if (Set(ref _canEditStories, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                    UpdateCanManageStories();
                }
            }
        }

        private bool _canDeleteStories;
        public bool CanDeleteStories
        {
            get => _canDeleteStories;
            set
            {
                if (Set(ref _canDeleteStories, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                    UpdateCanManageStories();
                }
            }
        }


        private bool _canRestrictMembers;
        public bool CanRestrictMembers
        {
            get => _canRestrictMembers;
            set
            {
                if (Set(ref _canRestrictMembers, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _canManageDirectMessages;
        public bool CanManageDirectMessages
        {
            get => _canManageDirectMessages;
            set
            {
                if (Set(ref _canManageDirectMessages, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _canInviteUsers;
        public bool CanInviteUsers
        {
            get => _canInviteUsers;
            set
            {
                if (Set(ref _canInviteUsers, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _canPinMessages;
        public bool CanPinMessages
        {
            get => _canPinMessages;
            set
            {
                if (Set(ref _canPinMessages, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _canManageTags;
        public bool CanManageTags
        {
            get => _canManageTags;
            set
            {
                if (Set(ref _canManageTags, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _canManageVideoChats;
        public bool CanManageVideoChats
        {
            get => _canManageVideoChats;
            set
            {
                if (Set(ref _canManageVideoChats, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _canManageTopics;
        public bool CanManageTopics
        {
            get => _canManageTopics;
            set
            {
                if (Set(ref _canManageTopics, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _isAnonymous;
        public bool IsAnonymous
        {
            get => _isAnonymous;
            set => Set(ref _isAnonymous, value);// Is Anonymous isn't needed for transfer ownership.
        }

        private bool _canPromoteMembers;
        public bool CanPromoteMembers
        {
            get => _canPromoteMembers;
            set
            {
                if (Set(ref _canPromoteMembers, value))
                {
                    RaisePropertyChanged(nameof(CanTransferOwnership));
                }
            }
        }

        private bool _processJoinRequests;
        public bool ProcessJoinRequests
        {
            get => _processJoinRequests;
            set => Set(ref _processJoinRequests, value);
        }

        #endregion

        private string _customTitle;
        public string CustomTitle
        {
            get => _customTitle;
            set => Set(ref _customTitle, value);
        }

        public void OpenProfile()
        {
            var member = _member;
            if (member == null)
            {
                return;
            }

            Delegate?.Hide();
            NavigationService.NavigateToSender(member.MemberId);
        }

        public async void Continue()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var member = _member;
            if (member == null)
            {
                return;
            }

            var channel = chat.Type is ChatTypeSupergroup { IsChannel: true };

            ChatMemberStatus status;
            if (member.Status is ChatMemberStatusCreator creator)
            {
                status = new ChatMemberStatusCreator(!channel && _isAnonymous, creator.IsMember);
            }
            else
            {
                status = new ChatMemberStatusAdministrator
                {
                    Rights = new ChatAdministratorRights
                    {
                        IsAnonymous = !channel && _isAnonymous,
                        CanChangeInfo = _canChangeInfo,
                        CanDeleteMessages = _canDeleteMessages,
                        CanEditMessages = channel && _canEditMessages,
                        CanInviteUsers = _canInviteUsers,
                        CanManageDirectMessages = channel && _canManageDirectMessages,
                        CanPinMessages = !channel && _canPinMessages,
                        CanManageTags = !channel && _canManageTags,
                        CanPostMessages = channel && _canPostMessages,
                        CanPostStories = _canPostStories,
                        CanEditStories = _canEditStories,
                        CanDeleteStories = _canDeleteStories,
                        CanPromoteMembers = _canPromoteMembers,
                        CanRestrictMembers = _canRestrictMembers,
                        CanManageVideoChats = !channel && _canManageVideoChats,
                        CanManageTopics = _isForum && _canManageTopics,
                    },
                    CanBeEdited = true
                };
            }

            if (status is ChatMemberStatusAdministrator administrator)
            {
                bool hasNoRights;
                if (channel)
                {
                    hasNoRights = !administrator.Rights.CanChangeInfo
                        && !administrator.Rights.CanDeleteMessages
                        && !administrator.Rights.CanEditMessages
                        && !administrator.Rights.CanInviteUsers
                        && !administrator.Rights.CanManageDirectMessages
                        && !administrator.Rights.CanPostMessages
                        && !administrator.Rights.CanPostStories
                        && !administrator.Rights.CanEditStories
                        && !administrator.Rights.CanDeleteStories
                        && !administrator.Rights.CanPromoteMembers
                        && !administrator.Rights.CanRestrictMembers;
                }
                else
                {
                    hasNoRights = !administrator.Rights.IsAnonymous
                        && !administrator.Rights.CanChangeInfo
                        && !administrator.Rights.CanDeleteMessages
                        && !administrator.Rights.CanInviteUsers
                        && !administrator.Rights.CanPinMessages
                        && !administrator.Rights.CanManageTags
                        && !administrator.Rights.CanPostStories
                        && !administrator.Rights.CanEditStories
                        && !administrator.Rights.CanDeleteStories
                        && !administrator.Rights.CanPromoteMembers
                        && !administrator.Rights.CanRestrictMembers
                        && !administrator.Rights.CanManageVideoChats
                        && (_isForum && !administrator.Rights.CanManageTopics);
                }

                if (hasNoRights)
                {
                    status = new ChatMemberStatusMember(0);
                }
            }

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, status));
            if (response is Ok)
            {
                if (member.MemberId is MessageSenderUser user)
                {
                    if (!string.Equals(_customTitle, member.Tag))
                    {
                        var tag = await ClientService.SendAsync(new SetChatMemberTag(chat.Id, user.UserId, _customTitle ?? string.Empty));
                        if (tag is Error error)
                        {
                            ShowToast(error);
                            return;
                        }
                    }

                    if (ClientService.TryGetSupergroup(Chat, out Supergroup supergroup)
                        && ClientService.TryGetSupergroupFull(Chat, out SupergroupFullInfo fullInfo))
                    {
                        var processJoinRequests = fullInfo.GuardBotUserId == user.UserId;
                        if (processJoinRequests != ProcessJoinRequests)
                        {
                            var joinByRequest = await ClientService.SendAsync(new ToggleSupergroupJoinByRequest(supergroup.Id, supergroup.JoinByRequest, user.UserId, false));
                            if (joinByRequest is Error error)
                            {
                                ShowToast(error);
                                return;
                            }
                        }
                    }
                }

                Aggregator.Publish(new UpdateChatMember(chat.Id, 0, 0, null, false, false, Member, new ChatMember(member.MemberId, _customTitle ?? string.Empty, ClientService.Options.MyId, member.JoinedChatDate, status)));
                Delegate?.Hide();
            }
            else if (response is Error error)
            {
                ShowToast(error);
            }
        }

        public async void Transfer()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var supergroup = ClientService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var member = _member;
            if (member == null)
            {
                return;
            }

            var user = ClientService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            var canTransfer = await ClientService.SendAsync(new CanTransferOwnership());
            if (canTransfer is CanTransferOwnershipResultPasswordNeeded or CanTransferOwnershipResultPasswordTooFresh or CanTransferOwnershipResultSessionTooFresh)
            {
                var primary = Strings.OK;

                var builder = new StringBuilder();
                builder.AppendFormat(supergroup.IsChannel ? Strings.EditChannelAdminTransferAlertText : Strings.EditAdminTransferAlertText, user.FirstName);
                builder.AppendLine();
                builder.AppendLine($"\u2022 {Strings.EditAdminTransferAlertText1}");
                builder.AppendLine($"\u2022 {Strings.EditAdminTransferAlertText2}");

                if (canTransfer is CanTransferOwnershipResultPasswordNeeded)
                {
                    primary = Strings.EditAdminTransferSetPassword;
                }
                else
                {
                    builder.AppendLine();
                    builder.AppendLine(Strings.EditAdminTransferAlertText3);
                }

                var confirm = await ShowPopupAsync(builder.ToString(), Strings.EditAdminTransferAlertTitle, primary, Strings.Cancel);
                if (confirm == ContentDialogResult.Primary && canTransfer is CanTransferOwnershipResultPasswordNeeded)
                {
                    NavigationService.NavigateToPasswordSetup();
                }
            }
            else if (canTransfer is CanTransferOwnershipResultOk)
            {
                var confirm = await ShowPopupAsync(string.Format(Strings.EditAdminTransferReadyAlertText, chat.Title, user.FullName()), supergroup.IsChannel ? Strings.EditAdminChannelTransfer : Strings.EditAdminGroupTransfer, Strings.EditAdminTransferChangeOwner, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var result = await ShowInputAsync(null, InputPopupType.Password, Strings.PleaseEnterCurrentPasswordTransfer, Strings.TwoStepVerification, Strings.LoginPassword, Strings.OK, Strings.Cancel);
                if (result.Result != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new TransferChatOwnership(chat.Id, user.Id, result.Text));
                if (response is Ok)
                {

                }
                else if (response is Error error)
                {
                    ShowToast(error);
                }
            }
            else if (canTransfer is Error error)
            {
                ShowToast(error);
            }
        }

        public async void Dismiss()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var member = _member;
            if (member == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusMember()));
            if (response is Ok)
            {
                Aggregator.Publish(new UpdateChatMember(chat.Id, 0, 0, null, false, false, Member, new ChatMember(member.MemberId, member.Tag, ClientService.Options.MyId, member.JoinedChatDate, new ChatMemberStatusMember())));
                Delegate?.Hide();
            }
            else if (response is Error error)
            {
                ShowToast(error);
            }
        }
    }
}
