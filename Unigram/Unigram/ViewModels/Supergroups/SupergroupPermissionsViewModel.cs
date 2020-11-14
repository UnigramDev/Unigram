using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Supergroups;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupPermissionsViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupPermissionsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator, new SupergroupMembersFilterRestricted(), query => new SupergroupMembersFilterRestricted(query))
        {
            SendCommand = new RelayCommand(SendExecute);
            AddCommand = new RelayCommand(AddExecute);
            BannedCommand = new RelayCommand(BannedExecute);

            ParticipantDismissCommand = new RelayCommand<ChatMember>(ParticipantDismissExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
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
            CanSendPolls = chat.Permissions.CanSendPolls;
            CanAddWebPagePreviews = chat.Permissions.CanAddWebPagePreviews;
            CanSendOtherMessages = chat.Permissions.CanSendOtherMessages;
            CanSendMediaMessages = chat.Permissions.CanSendMediaMessages;
            CanSendMessages = chat.Permissions.CanSendMessages;
        }

        #region Flags

        private bool _canSendMessages;
        public bool CanSendMessages
        {
            get
            {
                return _canSendMessages;
            }
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
            get
            {
                return _canSendMediaMessages;
            }
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
            }
        }

        private bool _canSendOtherMessages;
        public bool CanSendOtherMessages
        {
            get
            {
                return _canSendOtherMessages;
            }
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
            get
            {
                return _canSendPolls;
            }
            set
            {
                Set(ref _canSendPolls, value);

                // Allow send media
                if (value && !_canSendMediaMessages)
                {
                    CanSendMediaMessages = true;
                }
            }
        }

        private bool _canAddWebPagePreviews;
        public bool CanAddWebPagePreviews
        {
            get
            {
                return _canAddWebPagePreviews;
            }
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

        #endregion

        private int _slowModeDelay;
        public int SlowModeDelay
        {
            get => _slowModeDelay;
            set => Set(ref _slowModeDelay, value);
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
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
                CanAddWebPagePreviews = _canAddWebPagePreviews,
                CanSendPolls = _canSendPolls,
                CanSendOtherMessages = _canSendOtherMessages,
                CanSendMediaMessages = _canSendMediaMessages,
                CanSendMessages = _canSendMessages
            };

            var response = await ProtoService.SendAsync(new SetChatPermissions(chat.Id, permissions));
            if (response is Error error)
            {
                return;
            }

            if (chat.Type is ChatTypeBasicGroup)
            {
                if (_slowModeDelay != 0)
                {
                    chat = await ProtoService.SendAsync(new UpgradeBasicGroupChatToSupergroupChat(chat.Id)) as Chat;
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

            var fullInfo = CacheService.GetSupergroupFull(chat);
            if (fullInfo == null)
            {
                return;
            }

            if (fullInfo.SlowModeDelay != _slowModeDelay)
            {
                var slowMode = await ProtoService.SendAsync(new SetChatSlowModeDelay(chat.Id, _slowModeDelay));
                if (slowMode is Error)
                {
                    return;
                }
            }

            NavigationService.GoBack();
            NavigationService.Frame.ForwardStack.Clear();
        }

        public RelayCommand AddCommand { get; }
        private void AddExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupAddRestrictedPage), chat.Id);
        }

        public RelayCommand BannedCommand { get; }
        private void BannedExecute()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(SupergroupBannedPage), chat.Id);
        }

        #region Context menu

        public RelayCommand<ChatMember> ParticipantDismissCommand { get; }
        private async void ParticipantDismissExecute(ChatMember participant)
        {
            //if (_item == null)
            //{
            //    return;
            //}

            //if (participant.User == null)
            //{
            //    return;
            //}

            //var rights = new TLChannelBannedRights();

            //var response = await LegacyService.EditBannedAsync(_item, participant.User.ToInputUser(), rights);
            //if (response.IsSucceeded)
            //{
            //    Participants.Remove(participant);
            //}
        }

        #endregion
    }
}
