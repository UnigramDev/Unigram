using System;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Unigram.Views.Settings;
using Unigram.Views.Supergroups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditAdministratorViewModel : TLViewModelBase, IDelegable<IMemberDelegate>
    {
        public IMemberDelegate Delegate { get; set; }

        public SupergroupEditAdministratorViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            ProfileCommand = new RelayCommand(ProfileExecute);
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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            state.TryGet("chatId", out long chatId);
            state.TryGet("senderUserId", out long userId);

            Chat = ProtoService.GetChat(chatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetChatMember(chat.Id, new MessageSenderUser(userId)));
            if (response is ChatMember member)
            {
                var item = ProtoService.GetUser(userId);
                var cache = ProtoService.GetUserFull(userId);

                Delegate?.UpdateMember(chat, item, member);
                Delegate?.UpdateUser(chat, item, false);

                if (cache == null)
                {
                    ProtoService.Send(new GetUserFullInfo(userId));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false, false);
                }

                Member = member;

                if (member.Status is ChatMemberStatusAdministrator administrator)
                {
                    CanChangeInfo = administrator.CanChangeInfo;
                    CanDeleteMessages = administrator.CanDeleteMessages;
                    CanEditMessages = administrator.CanEditMessages;
                    CanInviteUsers = administrator.CanInviteUsers;
                    CanPinMessages = administrator.CanPinMessages;
                    CanPostMessages = administrator.CanPostMessages;
                    CanPromoteMembers = administrator.CanPromoteMembers;
                    CanRestrictMembers = administrator.CanRestrictMembers;
                    CanManageVideoChats = administrator.CanManageVideoChats;
                    IsAnonymous = administrator.IsAnonymous;

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
                if (chat == null)
                {
                    return false;
                }

                var supergroup = CacheService.GetSupergroup(chat);
                if (supergroup == null)
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

        public RelayCommand ProfileCommand { get; }
        private void ProfileExecute()
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
                status = new ChatMemberStatusCreator(_customTitle ?? string.Empty, channel ? false : _isAnonymous, creator.IsMember);
            }
            else
            {
                status = new ChatMemberStatusAdministrator
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
                    CanManageVideoChats = channel ? false : _canManageVideoChats,
                    CustomTitle = _customTitle ?? string.Empty
                };
            }

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, status));
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

            var supergroup = CacheService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var member = _member;
            if (member == null)
            {
                return;
            }

            var user = CacheService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            var canTransfer = await ProtoService.SendAsync(new CanTransferOwnership());
            if (canTransfer is CanTransferOwnershipResultPasswordNeeded or CanTransferOwnershipResultPasswordTooFresh or CanTransferOwnershipResultSessionTooFresh)
            {
                var primary = Strings.Resources.OK;

                var builder = new StringBuilder();
                builder.AppendFormat(supergroup.IsChannel ? Strings.Resources.EditChannelAdminTransferAlertText : Strings.Resources.EditAdminTransferAlertText, user.FirstName);
                builder.AppendLine();
                builder.AppendLine($"\u2022 {Strings.Resources.EditAdminTransferAlertText1}");
                builder.AppendLine($"\u2022 {Strings.Resources.EditAdminTransferAlertText2}");

                if (canTransfer is CanTransferOwnershipResultPasswordNeeded)
                {
                    primary = Strings.Resources.EditAdminTransferSetPassword;
                }
                else
                {
                    builder.AppendLine();
                    builder.AppendLine(Strings.Resources.EditAdminTransferAlertText3);
                }

                var confirm = await MessagePopup.ShowAsync(builder.ToString(), Strings.Resources.EditAdminTransferAlertTitle, primary, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary && canTransfer is CanTransferOwnershipResultPasswordNeeded)
                {
                    NavigationService.Navigate(typeof(SettingsPasswordPage));
                }
            }
            else if (canTransfer is CanTransferOwnershipResultOk)
            {
                var confirm = await MessagePopup.ShowAsync(string.Format(Strings.Resources.EditAdminTransferReadyAlertText, chat.Title, user.GetFullName()), supergroup.IsChannel ? Strings.Resources.EditAdminChannelTransfer : Strings.Resources.EditAdminGroupTransfer, Strings.Resources.EditAdminTransferChangeOwner, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                var popup = new InputPopup(InputPopupType.Password)
                {
                    Title = Strings.Resources.TwoStepVerification,
                    Header = Strings.Resources.PleaseEnterCurrentPasswordTransfer,
                    PrimaryButtonText = Strings.Resources.OK,
                    SecondaryButtonText = Strings.Resources.Cancel
                };

                var result = await popup.ShowQueuedAsync();
                if (result != ContentDialogResult.Primary)
                {
                    return;
                }

                var response = await ProtoService.SendAsync(new TransferChatOwnership(chat.Id, user.Id, popup.Text));
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

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusMember()));
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
