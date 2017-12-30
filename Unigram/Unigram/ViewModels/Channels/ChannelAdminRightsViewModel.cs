using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Api.Aggregator;
using Telegram.Api.Native.TL;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Views.Users;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelAdminRightsViewModel : UnigramViewModelBase
    {
        public ChannelAdminRightsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            ProfileCommand = new RelayCommand(ProfileExecute);
            SendCommand = new RelayCommand(SendExecute);
            DismissCommand = new RelayCommand(DismissExecute);
        }

        private TLChannel _channel;
        public TLChannel Channel
        {
            get
            {
                return _channel;
            }
            set
            {
                Set(ref _channel, value);
            }
        }

        private TLChannelParticipantAdmin _item;
        public TLChannelParticipantAdmin Item
        {
            get
            {
                return _item;
            }
            set
            {
                Set(ref _item, value);
            }
        }

        private TLUserFull _full;
        public TLUserFull Full
        {
            get
            {
                return _full;
            }
            set
            {
                Set(ref _full, value);
            }
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer == null)
            {
                return;
            }

            using (var from = TLObjectSerializer.CreateReader(buffer.AsBuffer()))
            {
                var tuple = new TLTuple<TLPeerChannel, TLChannelParticipantBase>(from);
                if (tuple.Item2 is TLChannelParticipant participant)
                {
                    IsAdminAlready = false;

                    tuple.Item2 = new TLChannelParticipantAdmin
                    {
                        UserId = participant.UserId,
                        Date = participant.Date,
                        IsCanEdit = true,
                        AdminRights = new TLChannelAdminRights
                        {
                            IsChangeInfo = true,
                            IsPinMessages = true,
                            IsInviteLink = true,
                            IsInviteUsers = true,
                            IsBanUsers = true,
                            IsDeleteMessages = true,
                            IsEditMessages = true,
                            IsPostMessages = true,
                            IsAddAdmins = false
                        }
                    };
                }
                else if (tuple.Item2 is TLChannelParticipantBanned banned)
                {
                    IsAdminAlready = false;

                    tuple.Item2 = new TLChannelParticipantAdmin
                    {
                        UserId = banned.UserId,
                        Date = banned.Date,
                        IsCanEdit = true,
                        AdminRights = new TLChannelAdminRights
                        {
                            IsChangeInfo = true,
                            IsPinMessages = true,
                            IsInviteLink = true,
                            IsInviteUsers = true,
                            IsBanUsers = true,
                            IsDeleteMessages = true,
                            IsEditMessages = true,
                            IsPostMessages = true,
                            IsAddAdmins = false
                        }
                    };
                }

                Channel = CacheService.GetChat(tuple.Item1.ChannelId) as TLChannel;
                Item = tuple.Item2 as TLChannelParticipantAdmin;

                IsAddAdmins = _item.AdminRights.IsAddAdmins;
                IsPinMessages = _item.AdminRights.IsPinMessages;
                IsInviteLink = _item.AdminRights.IsInviteLink;
                IsInviteUsers = _item.AdminRights.IsInviteUsers;
                IsBanUsers = _item.AdminRights.IsBanUsers;
                IsDeleteMessages = _item.AdminRights.IsDeleteMessages;
                IsEditMessages = _item.AdminRights.IsEditMessages;
                IsPostMessages = _item.AdminRights.IsPostMessages;
                IsChangeInfo = _item.AdminRights.IsChangeInfo;

                var user = tuple.Item2.User;
                if (user == null)
                {
                    return;
                }

                var full = CacheService.GetFullUser(user.Id);
                if (full == null)
                {
                    var response = await ProtoService.GetFullUserAsync(user.ToInputUser());
                    if (response.IsSucceeded)
                    {
                        full = response.Result;
                    }
                }

                Full = full;
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

        private bool _isChangeInfo;
        public bool IsChangeInfo
        {
            get
            {
                return _isChangeInfo;
            }
            set
            {
                Set(ref _isChangeInfo, value);
            }
        }

        private bool _isPostMessages;
        public bool IsPostMessages
        {
            get
            {
                return _isPostMessages;
            }
            set
            {
                Set(ref _isPostMessages, value);
            }
        }

        private bool _isEditMessages;
        public bool IsEditMessages
        {
            get
            {
                return _isEditMessages;
            }
            set
            {
                Set(ref _isEditMessages, value);
            }
        }

        private bool _isDeleteMessages;
        public bool IsDeleteMessages
        {
            get
            {
                return _isDeleteMessages;
            }
            set
            {
                Set(ref _isDeleteMessages, value);
            }
        }

        private bool _isBanUsers;
        public bool IsBanUsers
        {
            get
            {
                return _isBanUsers;
            }
            set
            {
                Set(ref _isBanUsers, value);
            }
        }

        private bool _isInviteUsers;
        public bool IsInviteUsers
        {
            get
            {
                return _isInviteUsers;
            }
            set
            {
                Set(ref _isInviteUsers, value);

                _isInviteLink = value;
            }
        }

        private bool _isInviteLink;
        public bool IsInviteLink
        {
            get
            {
                return _isInviteLink;
            }
            set
            {
                Set(ref _isInviteLink, value);
            }
        }

        private bool _isPinnedMessages;
        public bool IsPinMessages
        {
            get
            {
                return _isPinnedMessages;
            }
            set
            {
                Set(ref _isPinnedMessages, value);
            }
        }

        private bool _isAddAdmins;
        public bool IsAddAdmins
        {
            get
            {
                return _isAddAdmins;
            }
            set
            {
                Set(ref _isAddAdmins, value);
            }
        }

        #endregion

        public RelayCommand ProfileCommand { get; }
        private void ProfileExecute()
        {
            var user = _item.User;
            if (user == null)
            {
                return;
            }

            NavigationService.Navigate(typeof(UserDetailsPage), user.ToPeer());
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            var rights = new TLChannelAdminRights
            {
                IsChangeInfo = _isChangeInfo,
                IsPostMessages = _isPostMessages,
                IsEditMessages = _isEditMessages,
                IsDeleteMessages = _isDeleteMessages,
                IsBanUsers = _isBanUsers,
                IsInviteUsers = _isInviteUsers,
                IsInviteLink = _isInviteLink,
                IsPinMessages = _isPinnedMessages,
                IsAddAdmins = _isAddAdmins
            };

            var response = await ProtoService.EditAdminAsync(_channel, _item.User.ToInputUser(), rights);
            if (response.IsSucceeded)
            {
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
        }

        public RelayCommand DismissCommand { get; }
        private async void DismissExecute()
        {
            var rights = new TLChannelAdminRights();

            var response = await ProtoService.EditAdminAsync(_channel, _item.User.ToInputUser(), rights);
            if (response.IsSucceeded)
            {
                NavigationService.GoBack();
                NavigationService.Frame.ForwardStack.Clear();
            }
        }
    }
}
