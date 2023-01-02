//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupEditRestrictedViewModel : TLViewModelBase, IDelegable<IMemberDelegate>
    {
        public IMemberDelegate Delegate { get; set; }

        public SupergroupEditRestrictedViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
            EditUntilCommand = new RelayCommand(EditUntilExecute);
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

                if (member.Status is ChatMemberStatusRestricted restricted)
                {
                    CanChangeInfo = restricted.Permissions.CanChangeInfo;
                    CanPinMessages = restricted.Permissions.CanPinMessages;
                    CanInviteUsers = restricted.Permissions.CanInviteUsers;
                    CanSendPolls = restricted.Permissions.CanSendPolls;
                    CanAddWebPagePreviews = restricted.Permissions.CanAddWebPagePreviews;
                    CanSendOtherMessages = restricted.Permissions.CanSendOtherMessages;
                    CanSendMediaMessages = restricted.Permissions.CanSendMediaMessages;
                    CanSendMessages = restricted.Permissions.CanSendMessages;
                    UntilDate = restricted.RestrictedUntilDate;
                }
                else if (member.Status is ChatMemberStatusBanned banned)
                {
                    CanChangeInfo = false;
                    CanPinMessages = false;
                    CanInviteUsers = false;
                    CanSendPolls = false;
                    CanAddWebPagePreviews = false;
                    CanSendOtherMessages = false;
                    CanSendMediaMessages = false;
                    CanSendMessages = false;
                    UntilDate = banned.BannedUntilDate;
                }
                else
                {
                    CanChangeInfo = chat.Permissions.CanChangeInfo;
                    CanPinMessages = chat.Permissions.CanPinMessages;
                    CanInviteUsers = chat.Permissions.CanInviteUsers;
                    CanSendPolls = chat.Permissions.CanSendPolls;
                    CanAddWebPagePreviews = chat.Permissions.CanAddWebPagePreviews;
                    CanSendOtherMessages = chat.Permissions.CanSendOtherMessages;
                    CanSendMediaMessages = chat.Permissions.CanSendMediaMessages;
                    CanSendMessages = chat.Permissions.CanSendMessages;
                    UntilDate = 0;
                }
            }
        }

        #region Flags

        private bool _canSendMessages;
        public bool CanSendMessages
        {
            get => _canSendMessages;
            set
            {
                Set(ref _canSendMessages, value);

                // Don't allow send media
                if (!value && _canSendMediaMessages)
                {
                    CanSendMediaMessages = false;
                }
            }
        }

        private bool _canSendMediaMessages;
        public bool CanSendMediaMessages
        {
            get => _canSendMediaMessages;
            set
            {
                Set(ref _canSendMediaMessages, value);

                // Allow send messages
                if (value && !_canSendMessages)
                {
                    CanSendMessages = true;
                }

                // Don't allow send stickers
                if (!value && _canSendOtherMessages)
                {
                    CanSendOtherMessages = false;
                }

                // Don't allow embed links
                if (!value && _canAddWebPagePreviews)
                {
                    CanAddWebPagePreviews = false;
                }

                // Don't allow polls
                if (!value && _canSendPolls)
                {
                    CanSendPolls = false;
                }
            }
        }

        private bool _canSendOtherMessages;
        public bool CanSendOtherMessages
        {
            get => _canSendOtherMessages;
            set
            {
                Set(ref _canSendOtherMessages, value);

                // Allow send media
                if (value && !_canSendMediaMessages)
                {
                    CanSendMediaMessages = true;
                }
            }
        }

        private bool _canSendPolls;
        public bool CanSendPolls
        {
            get => _canSendPolls;
            set
            {
                Set(ref _canSendPolls, value);

                if (value && !_canSendMediaMessages)
                {
                    CanSendMediaMessages = true;
                }
            }
        }

        private bool _canAddWebPagePreviews;
        public bool CanAddWebPagePreviews
        {
            get => _canAddWebPagePreviews;
            set
            {
                Set(ref _canAddWebPagePreviews, value);

                if (value && !_canSendMediaMessages)
                {
                    CanSendMediaMessages = true;
                }
            }
        }



        private bool _canInviteUsers;
        public bool CanInviteUsers
        {
            get => _canInviteUsers;
            set => Set(ref _canInviteUsers, value);
        }

        private bool _canPinMessages;
        public bool CanPinMessages
        {
            get => _canPinMessages;
            set => Set(ref _canPinMessages, value);
        }

        private bool _canChangeInfo;
        public bool CanChangeInfo
        {
            get => _canChangeInfo;
            set => Set(ref _canChangeInfo, value);
        }

        #endregion

        private int _untilDate;
        public int UntilDate
        {
            get => _untilDate;
            set => Set(ref _untilDate, value);
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

            var supergroup = chat.Type as ChatTypeSupergroup;
            if (supergroup == null)
            {
                return;
            }

            var status = new ChatMemberStatusRestricted
            {
                IsMember = true,
                RestrictedUntilDate = _untilDate,
                Permissions = new ChatPermissions
                {
                    CanChangeInfo = _canChangeInfo,
                    CanPinMessages = _canPinMessages,
                    CanInviteUsers = _canInviteUsers,
                    CanAddWebPagePreviews = _canAddWebPagePreviews,
                    CanSendPolls = _canSendPolls,
                    CanSendOtherMessages = _canSendOtherMessages,
                    CanSendMediaMessages = _canSendMediaMessages,
                    CanSendMessages = _canSendMessages,
                }
            };

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, status));
            if (response is Ok)
            {
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
            else
            {
                // TODO: ...
            }
        }

        public RelayCommand EditUntilCommand { get; }
        private async void EditUntilExecute()
        {
            var dialog = new SupergroupEditRestrictedUntilPopup(_untilDate);
            var confirm = await dialog.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                UntilDate = dialog.Value <= DateTime.Now ? 0 : dialog.Value.ToTimestamp();
            }
            else if (confirm == ContentDialogResult.Secondary)
            {
                UntilDate = 0;
            }
        }

        public RelayCommand DismissCommand { get; }
        private async void DismissExecute()
        {
            if (_chat is not Chat chat || _member is not ChatMember member)
            {
                return;
            }

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusBanned()));
            if (response is Ok)
            {
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
