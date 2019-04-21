using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Supergroups;

namespace Unigram.ViewModels.Supergroups
{
    public class SupergroupPermissionsViewModel : SupergroupMembersViewModelBase
    {
        public SupergroupPermissionsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator) 
            : base(protoService, cacheService, settingsService, aggregator, null, query => new SupergroupMembersFilterRestricted(query))
        {
            AddCommand = new RelayCommand(AddExecute);
            BannedCommand = new RelayCommand(BannedExecute);

            ParticipantDismissCommand = new RelayCommand<ChatMember>(ParticipantDismissExecute);
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

        private bool _canSendPollMessages;
        public bool CanSendPollMessages
        {
            get
            {
                return _canSendPollMessages;
            }
            set
            {
                Set(ref _canSendPollMessages, value);

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
