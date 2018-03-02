using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TdWindows;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views;
using Unigram.Views.Supergroups;
using Unigram.Views.Users;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditAdministratorViewModel : UnigramViewModelBase, IDelegable<IMemberDelegate>
    {
        public IMemberDelegate Delegate { get; set; }

        public SupergroupEditAdministratorViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            ProfileCommand = new RelayCommand(ProfileExecute);
            SendCommand = new RelayCommand(SendExecute);
            DismissCommand = new RelayCommand(DismissExecute);
        }

        private Chat _chat;
        public Chat Chat
        {
            get
            {
                return _chat;
            }
            set
            {
                Set(ref _chat, value);
            }
        }

        private ChatMember _member;
        public ChatMember Member
        {
            get
            {
                return _member;
            }
            set
            {
                Set(ref _member, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var bundle = parameter as ChatMemberNavigation;
            if (bundle == null)
            {
                return;
            }

            Chat = ProtoService.GetChat(bundle.ChatId);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new GetChatMember(chat.Id, bundle.UserId));
            if (response is ChatMember member && chat.Type is ChatTypeSupergroup super)
            {
                var item = ProtoService.GetUser(member.UserId);
                var cache = ProtoService.GetUserFull(member.UserId);

                var group = ProtoService.GetSupergroup(super.SupergroupId);

                Delegate?.UpdateMember(chat, group, item, member);
                Delegate?.UpdateUser(chat, item, false);

                if (cache == null)
                {
                    ProtoService.Send(new GetUserFullInfo(member.UserId));
                }
                else
                {
                    Delegate?.UpdateUserFullInfo(chat, item, cache, false);
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
                }
                else
                {
                    CanChangeInfo = true;
                    CanDeleteMessages = true;
                    CanEditMessages = true;
                    CanInviteUsers = true;
                    CanPinMessages = true;
                    CanPostMessages = true;
                    CanPromoteMembers = false;
                    CanRestrictMembers = true;
                }
            }
        }

        private bool _isAdminAlready = true;
        public bool IsAdminAlready
        {
            get
            {
                return _isAdminAlready;
            }
            set
            {
                Set(ref _isAdminAlready, value);
            }
        }

        #region Flags

        private bool _canChangeInfo;
        public bool CanChangeInfo
        {
            get
            {
                return _canChangeInfo;
            }
            set
            {
                Set(ref _canChangeInfo, value);
            }
        }

        private bool _canPostMessages;
        public bool CanPostMessages
        {
            get
            {
                return _canPostMessages;
            }
            set
            {
                Set(ref _canPostMessages, value);
            }
        }

        private bool _canEditMessages;
        public bool CanEditMessages
        {
            get
            {
                return _canEditMessages;
            }
            set
            {
                Set(ref _canEditMessages, value);
            }
        }

        private bool _canDeleteMessages;
        public bool CanDeleteMessages
        {
            get
            {
                return _canDeleteMessages;
            }
            set
            {
                Set(ref _canDeleteMessages, value);
            }
        }

        private bool _canRestrictMembers;
        public bool CanRestrictMembers
        {
            get
            {
                return _canRestrictMembers;
            }
            set
            {
                Set(ref _canRestrictMembers, value);
            }
        }

        private bool _canInviteUsers;
        public bool CanInviteUsers
        {
            get
            {
                return _canInviteUsers;
            }
            set
            {
                Set(ref _canInviteUsers, value);
            }
        }

        private bool _canPinMessages;
        public bool CanPinMessages
        {
            get
            {
                return _canPinMessages;
            }
            set
            {
                Set(ref _canPinMessages, value);
            }
        }

        private bool _canPromoteMembers;
        public bool CanPromoteMembers
        {
            get
            {
                return _canPromoteMembers;
            }
            set
            {
                Set(ref _canPromoteMembers, value);
            }
        }

        #endregion

        public RelayCommand ProfileCommand { get; }
        private async void ProfileExecute()
        {
            var member = _member;
            if (member == null)
            {
                return;
            }

            var response = await ProtoService.SendAsync(new CreatePrivateChat(member.UserId, false));
            if (response is Chat chat)
            {
                NavigationService.Navigate(typeof(ProfilePage), chat.Id);
            }
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

            var supergroup = chat.Type as ChatTypeSupergroup;
            if (supergroup == null)
            {
                return;
            }

            var status = new ChatMemberStatusAdministrator
            {
                CanChangeInfo = _canChangeInfo,
                CanDeleteMessages = _canDeleteMessages,
                CanEditMessages = supergroup.IsChannel ? _canEditMessages : false,
                CanInviteUsers = _canInviteUsers,
                CanPinMessages = supergroup.IsChannel ? false : _canPinMessages,
                CanPostMessages = supergroup.IsChannel ? _canPostMessages : false,
                CanPromoteMembers = _canPromoteMembers,
                CanRestrictMembers = supergroup.IsChannel ? false : _canRestrictMembers,
            };

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.UserId, status));
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

            var response = await ProtoService.SendAsync(new SetChatMemberStatus(chat.Id, member.UserId, new ChatMemberStatusMember()));
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
