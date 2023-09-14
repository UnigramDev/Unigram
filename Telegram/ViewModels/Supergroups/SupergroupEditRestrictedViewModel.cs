//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Popups;
using Telegram.Views.Supergroups;
using Telegram.Views.Supergroups.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupEditRestrictedViewModel : ViewModelBase, IDelegable<IMemberPopupDelegate>
    {
        public IMemberPopupDelegate Delegate { get; set; }

        public SupergroupEditRestrictedViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
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

            var response = await ClientService.SendAsync(new GetChatMember(chat.Id, args.MemberId));
            if (response is ChatMember member)
            {
                var item = ClientService.GetUser(user.UserId);
                var cache = ClientService.GetUserFull(user.UserId);

                Delegate?.UpdateMember(chat, item, member);
                Delegate?.UpdateUser(chat, item, false);

                if (cache == null)
                {
                    ClientService.Send(new GetUserFullInfo(user.UserId));
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
                    CanSendPhotos = restricted.Permissions.CanSendPhotos;
                    CanSendVideos = restricted.Permissions.CanSendVideos;
                    CanSendOtherMessages = restricted.Permissions.CanSendOtherMessages;
                    CanSendAudios = restricted.Permissions.CanSendAudios;
                    CanSendDocuments = restricted.Permissions.CanSendDocuments;
                    CanSendVoiceNotes = restricted.Permissions.CanSendVoiceNotes;
                    CanSendVideoNotes = restricted.Permissions.CanSendVideoNotes;
                    CanSendPolls = restricted.Permissions.CanSendPolls;
                    CanAddWebPagePreviews = restricted.Permissions.CanAddWebPagePreviews;
                    CanSendBasicMessages = restricted.Permissions.CanSendBasicMessages;
                    UntilDate = restricted.RestrictedUntilDate;
                }
                else if (member.Status is ChatMemberStatusBanned banned)
                {
                    CanChangeInfo = false;
                    CanPinMessages = false;
                    CanInviteUsers = false;
                    CanSendMediaMessages = false;
                    CanSendBasicMessages = false;
                    UntilDate = banned.BannedUntilDate;
                }
                else
                {
                    CanChangeInfo = chat.Permissions.CanChangeInfo;
                    CanPinMessages = chat.Permissions.CanPinMessages;
                    CanInviteUsers = chat.Permissions.CanInviteUsers;
                    CanSendPhotos = chat.Permissions.CanSendPhotos;
                    CanSendVideos = chat.Permissions.CanSendVideos;
                    CanSendOtherMessages = chat.Permissions.CanSendOtherMessages;
                    CanSendAudios = chat.Permissions.CanSendAudios;
                    CanSendDocuments = chat.Permissions.CanSendDocuments;
                    CanSendVoiceNotes = chat.Permissions.CanSendVoiceNotes;
                    CanSendVideoNotes = chat.Permissions.CanSendVideoNotes;
                    CanSendPolls = chat.Permissions.CanSendPolls;
                    CanAddWebPagePreviews = chat.Permissions.CanAddWebPagePreviews;
                    CanSendBasicMessages = chat.Permissions.CanSendBasicMessages;
                    UntilDate = 0;
                }
            }
        }

        #region Flags

        private bool _canSendBasicMessages;
        public bool CanSendBasicMessages
        {
            get => _canSendBasicMessages;
            set
            {
                Set(ref _canSendBasicMessages, value);

                // Don't allow send media
                if (!value && _canAddWebPagePreviews)
                {
                    CanAddWebPagePreviews = false;
                }
            }
        }

        private bool? _canSendMediaMessages;
        public bool? CanSendMediaMessages
        {
            get => _canSendMediaMessages;
            set
            {
                Set(ref _canSendMediaMessages, value);

                if (value.HasValue)
                {
                    CanSendPhotos = value.Value;
                    CanSendVideos = value.Value;
                    CanSendOtherMessages = value.Value;
                    CanSendAudios = value.Value;
                    CanSendDocuments = value.Value;
                    CanSendVoiceNotes = value.Value;
                    CanSendVideoNotes = value.Value;
                    CanSendPolls = value.Value;
                    CanAddWebPagePreviews = value.Value;
                }
            }
        }

        private void UpdateCanSendMediaMessages()
        {
            var count = Count();

            Set(ref _canSendCount, count, nameof(CanSendCount));
            Set(ref _canSendMediaMessages, count == 0 ? false : count == 9 ? true : null, nameof(CanSendMediaMessages));
        }

        private int Count()
        {
            var count = 0;
            if (_canAddWebPagePreviews)
            {
                count++;
            }
            if (_canSendVoiceNotes)
            {
                count++;
            }
            if (_canSendVideoNotes)
            {
                count++;
            }
            if (_canSendVideos)
            {
                count++;
            }
            if (_canSendPhotos)
            {
                count++;
            }
            if (_canSendDocuments)
            {
                count++;
            }
            if (_canSendAudios)
            {
                count++;
            }
            if (_canSendOtherMessages)
            {
                count++;
            }
            if (_canSendPolls)
            {
                count++;
            }

            return count;
        }

        private int _canSendCount;
        public int CanSendCount
        {
            get => _canSendCount;
            set => Set(ref _canSendCount, value);
        }


        private bool _canSendPhotos;
        public bool CanSendPhotos
        {
            get => _canSendPhotos;
            set
            {
                Set(ref _canSendPhotos, value);
                UpdateCanSendMediaMessages();
            }
        }

        private bool _canSendVideos;
        public bool CanSendVideos
        {
            get => _canSendVideos;
            set
            {
                Set(ref _canSendVideos, value);
                UpdateCanSendMediaMessages();
            }
        }

        private bool _canSendOtherMessages;
        public bool CanSendOtherMessages
        {
            get => _canSendOtherMessages;
            set
            {
                Set(ref _canSendOtherMessages, value);
                UpdateCanSendMediaMessages();
            }
        }

        private bool _canSendAudios;
        public bool CanSendAudios
        {
            get => _canSendAudios;
            set
            {
                Set(ref _canSendAudios, value);
                UpdateCanSendMediaMessages();
            }
        }

        private bool _canSendDocuments;
        public bool CanSendDocuments
        {
            get => _canSendDocuments;
            set
            {
                Set(ref _canSendDocuments, value);
                UpdateCanSendMediaMessages();
            }
        }

        private bool _canSendVoiceNotes;
        public bool CanSendVoiceNotes
        {
            get => _canSendVoiceNotes;
            set
            {
                Set(ref _canSendVoiceNotes, value);
                UpdateCanSendMediaMessages();
            }
        }

        private bool _canSendVideoNotes;
        public bool CanSendVideoNotes
        {
            get => _canSendVideoNotes;
            set
            {
                Set(ref _canSendVideoNotes, value);
                UpdateCanSendMediaMessages();
            }
        }

        private bool _canSendPolls;
        public bool CanSendPolls
        {
            get => _canSendPolls;
            set
            {
                Set(ref _canSendPolls, value);
                UpdateCanSendMediaMessages();
            }
        }

        private bool _canAddWebPagePreviews;
        public bool CanAddWebPagePreviews
        {
            get => _canAddWebPagePreviews;
            set
            {
                Set(ref _canAddWebPagePreviews, value);
                UpdateCanSendMediaMessages();
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
                    CanSendPhotos = _canSendPhotos,
                    CanSendVideos = _canSendVideos,
                    CanSendOtherMessages = _canSendOtherMessages,
                    CanSendAudios = _canSendAudios,
                    CanSendDocuments = _canSendDocuments,
                    CanSendVoiceNotes = _canSendVoiceNotes,
                    CanSendVideoNotes = _canSendVideoNotes,
                    CanSendPolls = _canSendPolls,
                    CanAddWebPagePreviews = _canAddWebPagePreviews,
                    CanSendBasicMessages = _canSendBasicMessages,
                }
            };

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, status));
            if (response is Ok)
            {
                Delegate?.Hide();

                if (NavigationService.CurrentPageType == typeof(SupergroupAddRestrictedPage))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();
                }
            }
            else
            {
                // TODO: ...
            }
        }

        public async void EditUntil()
        {
            // TODO: this is currently unsupported

            var dialog = new SupergroupEditRestrictedUntilPopup(_untilDate);
            var confirm = await ShowPopupAsync(dialog);
            if (confirm == ContentDialogResult.Primary)
            {
                UntilDate = dialog.Value <= DateTime.Now ? 0 : dialog.Value.ToTimestamp();
            }
            else if (confirm == ContentDialogResult.Secondary)
            {
                UntilDate = 0;
            }
        }

        public async void Dismiss()
        {
            if (_chat is not Chat chat || _member is not ChatMember member)
            {
                return;
            }

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, new ChatMemberStatusBanned()));
            if (response is Ok)
            {
                Delegate?.Hide();

                if (NavigationService.CurrentPageType == typeof(SupergroupAddRestrictedPage))
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();
                }
            }
            else
            {
                // TODO: ...
            }
        }
    }
}
