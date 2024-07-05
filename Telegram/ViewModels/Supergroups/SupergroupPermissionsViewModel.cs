//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Supergroups;
using Telegram.Views.Supergroups.Popups;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Supergroups
{
    public class SupergroupPermissionsViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupPermissionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator, new SupergroupMembersFilterRestricted(), query => new SupergroupMembersFilterRestricted(query))
        {
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            await base.OnNavigatedToAsync(parameter, mode, state);

            var chat = _chat;
            if (chat == null)
            {
                return;
            }

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
            CanAddLinkPreviews = chat.Permissions.CanAddLinkPreviews;
            CanSendBasicMessages = chat.Permissions.CanSendBasicMessages;

            if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup)
                && ClientService.TryGetSupergroupFull(chat, out SupergroupFullInfo fullInfo))
            {
                if (supergroup.CanRestrictMembers())
                {
                    UnrestrictBoosters = fullInfo.UnrestrictBoostCount > 0;
                    UnrestrictBoostCount = fullInfo.UnrestrictBoostCount;
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
                RaisePropertyChanged(nameof(CanUnrestrictBoosters));

                // Don't allow send media
                if (!value && _CanAddLinkPreviews)
                {
                    CanAddLinkPreviews = false;
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
                RaisePropertyChanged(nameof(CanUnrestrictBoosters));

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
                    CanAddLinkPreviews = value.Value;
                }
            }
        }

        private void UpdateCanSendMediaMessages()
        {
            var count = Count();

            Set(ref _canSendCount, count, nameof(CanSendCount));
            Set(ref _canSendMediaMessages, count == 0 ? false : count == 9 ? true : null, nameof(CanSendMediaMessages));

            RaisePropertyChanged(nameof(CanUnrestrictBoosters));
        }

        private int Count()
        {
            var count = 0;
            if (_CanAddLinkPreviews)
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

        private bool _CanAddLinkPreviews;
        public bool CanAddLinkPreviews
        {
            get => _CanAddLinkPreviews;
            set
            {
                Set(ref _CanAddLinkPreviews, value);
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

        private int _slowModeDelay;
        public int SlowModeDelay
        {
            get => _slowModeDelay;
            set
            {
                Set(ref _slowModeDelay, value);
                RaisePropertyChanged(nameof(CanUnrestrictBoosters));
            }
        }

        private int _unrestrictBoostCount;
        public int UnrestrictBoostCount
        {
            get => _unrestrictBoostCount;
            set => Set(ref _unrestrictBoostCount, value);
        }

        private bool _unrestrictBoosters;
        public bool UnrestrictBoosters
        {
            get => _unrestrictBoosters;
            set => Set(ref _unrestrictBoosters, value);
        }

        public bool CanUnrestrictBoosters
        {
            get
            {
                if (ClientService.TryGetSupergroup(Chat, out Supergroup supergroup) && supergroup.CanRestrictMembers())
                {
                    return !CanSendPhotos
                        || !CanSendVideos
                        || !CanSendOtherMessages
                        || !CanSendAudios
                        || !CanSendDocuments
                        || !CanSendVoiceNotes
                        || !CanSendVideoNotes
                        || !CanSendPolls
                        || !CanAddLinkPreviews
                        || !CanSendBasicMessages
                        || SlowModeDelay > 0;
                }

                return false;
            }
        }

        public async void Continue()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            var permissions = new ChatPermissions
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
                CanAddLinkPreviews = _CanAddLinkPreviews,
                CanSendBasicMessages = _canSendBasicMessages
            };

            var response = await ClientService.SendAsync(new SetChatPermissions(chat.Id, permissions));
            if (response is Error error)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup)
            {
                if (_slowModeDelay != 0)
                {
                    chat = await UpgradeAsync(chat);
                }
                else
                {
                    NavigationService.GoBack();
                    NavigationService.Frame.ForwardStack.Clear();
                    return;
                }
            }

            if (chat == null)
            {
                return;
            }

            var supergroup = ClientService.GetSupergroup(chat);
            if (supergroup == null)
            {
                return;
            }

            var fullInfo = ClientService.GetSupergroupFull(chat);
            if (fullInfo == null)
            {
                return;
            }

            if (fullInfo.SlowModeDelay != _slowModeDelay)
            {
                var slowMode = await ClientService.SendAsync(new SetChatSlowModeDelay(chat.Id, _slowModeDelay));
                if (slowMode is Error)
                {
                    return;
                }
            }

            if (fullInfo.UnrestrictBoostCount != _unrestrictBoostCount && supergroup.CanRestrictMembers())
            {
                var unrestrictBoostCount = await ClientService.SendAsync(new SetSupergroupUnrestrictBoostCount(supergroup.Id, _unrestrictBoostCount));
                if (unrestrictBoostCount is Error)
                {
                    return;
                }
            }

            NavigationService.GoBack();
            NavigationService.Frame.ForwardStack.Clear();
        }

        public void AddRestricted()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(typeof(SupergroupChooseMemberPopup), new SupergroupChooseMemberArgs(chat.Id, SupergroupChooseMemberMode.Restrict));
        }

        public void Banned()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupBannedPage), chat.Id);
        }

        #region Context menu

        public void EditMember(ChatMember member)
        {
            var chat = _chat;
            if (chat == null || member == null)
            {
                return;
            }

            NavigationService.ShowPopupAsync(typeof(SupergroupEditRestrictedPopup), new SupergroupEditMemberArgs(chat.Id, member.MemberId));
        }

        public async void UnbanMember(ChatMember member)
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

            ChatMemberStatus status = member.Status is ChatMemberStatusRestricted { IsMember: true }
                ? new ChatMemberStatusMember()
                : new ChatMemberStatusLeft();

            var response = await ClientService.SendAsync(new SetChatMemberStatus(chat.Id, member.MemberId, status));
            if (response is Error)
            {
                Members.Insert(index, member);
            }
        }

        #endregion
    }
}
