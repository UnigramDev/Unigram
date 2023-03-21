//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Telegram.Views.Supergroups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupEditAdministratorViewModel : TLViewModelBase, IDelegable<IMemberDelegate>
    {
        public IMemberDelegate Delegate { get; set; }

        public SupergroupEditAdministratorViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
            TransferCommand = new RelayCommand(TransferExecute);
            DismissCommand = new RelayCommand(DismissExecute);
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => Set(ref _chat, value);
        }

        private ChatMember _member;
        public ChatMember Member
        {
            get => _member;
            set => Set(ref _member, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            state.TryGet("chatId", out long chatId);
            state.TryGet("senderUserId", out long userId);

            Chat = ClientService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ClientService.SendAsync(new GetChatMember(chat.Id, new MessageSenderUser(userId)));
            if (response is ChatMember member)
            {
                var item = ClientService.GetUser(userId);
                var cache = ClientService.GetUserFull(userId);

                Delegate?.UpdateMember(chat, item, member);
                Delegate?.UpdateUser(chat, item, false);

                if (cache == null)
                {
                    ClientService.Send(new GetUserFullInfo(userId));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false, false);
                }

                Member = member;

                if (member.Status is ChatMemberStatusAdministrator administrator)
                {
                    CanChangeInfo = administrator.Rights.CanChangeInfo;
                    CanDeleteMessages = administrator.Rights.CanDeleteMessages;
                    CanEditMessages = administrator.Rights.CanEditMessages;
                    CanInviteUsers = administrator.Rights.CanInviteUsers;
                    CanPinMessages = administrator.Rights.CanPinMessages;
                    CanPostMessages = administrator.Rights.CanPostMessages;
                    CanPromoteMembers = administrator.Rights.CanPromoteMembers;
                    CanRestrictMembers = administrator.Rights.CanRestrictMembers;
                    CanManageVideoChats = administrator.Rights.CanManageVideoChats;
                    IsAnonymous = administrator.Rights.IsAnonymous;

                    CustomTitle = administrator.CustomTitle;
                }
                else
                {
                    CanChangeInfo = true;
                    CanDeleteMessages = true;
                    CanEditMessages = true;
                    CanInviteUsers = true;
                    CanPinMessages = true;
                    CanPostMessages = true;
                    CanManageVideoChats = true;
                    CanPromoteMembers = member.Status is ChatMemberStatusCreator;
                    CanRestrictMembers = true;

                    if (member.Status is ChatMemberStatusCreator creator)
                    {
                        IsAnonymous = creator.IsAnonymous;

                        CustomTitle = creator.CustomTitle;
                    }
                    else
                    {
                        IsAnonymous = false;

                        CustomTitle = string.Empty;
                    }
                }
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

                return _canChangeInfo &&
                    _canDeleteMessages &&
                    _canInviteUsers &&
                    _canPromoteMembers &&
                    (supergroup.IsChannel ? _canEditMessages : true) &&
                    (supergroup.IsChannel ? true : _canPinMessages) &&
                    (supergroup.IsChannel ? _canPostMessages : true) &&
                    (supergroup.IsChannel ? true : _canRestrictMembers) &&
                    (supergroup.IsChannel ? true : _canManageVideoChats);
            }
        }

        #region Flags

        private bool _canChangeInfo;
        public bool CanChangeInfo
        {
            get => _canChangeInfo;
            set
            {
                Set(ref _canChangeInfo, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
            }
        }

        private bool _canPostMessages;
        public bool CanPostMessages
        {
            get => _canPostMessages;
            set
            {
                Set(ref _canPostMessages, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
            }
        }

        private bool _canEditMessages;
        public bool CanEditMessages
        {
            get => _canEditMessages;
            set
            {
                Set(ref _canEditMessages, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
            }
        }

        private bool _canDeleteMessages;
        public bool CanDeleteMessages
        {
            get => _canDeleteMessages;
            set
            {
                Set(ref _canDeleteMessages, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
            }
        }

        private bool _canRestrictMembers;
        public bool CanRestrictMembers
        {
            get => _canRestrictMembers;
            set
            {
                Set(ref _canRestrictMembers, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
            }
        }

        private bool _canInviteUsers;
        public bool CanInviteUsers
        {
            get => _canInviteUsers;
            set
            {
                Set(ref _canInviteUsers, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
            }
        }

        private bool _canPinMessages;
        public bool CanPinMessages
        {
            get => _canPinMessages;
            set
            {
                Set(ref _canPinMessages, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
            }
        }

        private bool _canManageVideoChats;
        public bool CanManageVideoChats
        {
            get => _canManageVideoChats;
            set
            {
                Set(ref _canManageVideoChats, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
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
                Set(ref _canPromoteMembers, value);
                RaisePropertyChanged(nameof(CanTransferOwnership));
            }
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

            NavigationService.NavigateToSender(member.MemberId);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
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

            var channel = chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel;

            ChatMemberStatus status;
            if (member.Status is ChatMemberStatusCreator creator)
            {
                status = new ChatMemberStatusCreator(_customTitle ?? string.Empty, !channel && _isAnonymous, creator.IsMember);
            }
            else
            {
                status = new ChatMemberStatusAdministrator
                {
                    Rights = new ChatAdministratorRights
                    {
                        IsAnonymous = channel ? false : _isAnonymous,
                        CanChangeInfo = _canChangeInfo,
                        CanDeleteMessages = _canDeleteMessages,
                        CanEditMessages = channel ? _canEditMessages : false,
                        CanInviteUsers = _canInviteUsers,
                        CanPinMessages = channel ? false : _canPinMessages,
                        CanPostMessages = channel ? _canPostMessages : false,
                        CanPromoteMembers = _canPromoteMembers,
                        CanRestrictMembers = channel ? false : _canRestrictMembers,
                        CanManageVideoChats = channel ? false : _canManageVideoChats
                    },
                    CustomTitle = _customTitle ?? string.Empty
                };
            }

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, status));
            if (response is Ok)
            {
                NavigationService.RemoveLastIf(typeof(SupergroupAddAdministratorPage));
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
            else
            {
                // TODO: ...
            }
        }

        public RelayCommand TransferCommand { get; }
        private async void TransferExecute()
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
                    NavigationService.Navigate(typeof(SettingsPasswordPage));
                }
            }
            else if (canTransfer is CanTransferOwnershipResultOk)
            {
                var confirm = await ShowPopupAsync(string.Format(Strings.EditAdminTransferReadyAlertText, chat.Title, user.FullName()), supergroup.IsChannel ? Strings.EditAdminChannelTransfer : Strings.EditAdminGroupTransfer, Strings.EditAdminTransferChangeOwner, Strings.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var popup = new InputPopup(InputPopupType.Password)
                {
                    Title = Strings.TwoStepVerification,
                    Header = Strings.PleaseEnterCurrentPasswordTransfer,
                    PlaceholderText = Strings.LoginPassword,
                    PrimaryButtonText = Strings.OK,
                    SecondaryButtonText = Strings.Cancel
                };

                var result = await ShowPopupAsync(popup);
                if (result != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await ClientService.SendAsync(new TransferChatOwnership(chat.Id, user.Id, popup.Text));
                if (response is Ok)
                {

                }
            }
        }

        public RelayCommand DismissCommand { get; }
        private async void DismissExecute()
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
                NavigationService.RemoveLastIf(typeof(SupergroupAddAdministratorPage));
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
            else
            {
                // TODO: ...
            }
        }
    }
}
