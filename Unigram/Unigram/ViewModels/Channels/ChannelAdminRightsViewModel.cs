using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelAdminRightsViewModel : UnigramViewModelBase
    {
        public ChannelAdminRightsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var buffer = parameter as byte[];
            if (buffer != null)
            {
                using (var from = new TLBinaryReader(buffer))
                {
                    var tuple = new TLTuple<TLPeerChannel, TLChannelParticipantAdmin>(from);

                    Channel = CacheService.GetChat(tuple.Item1.ChannelId) as TLChannel;
                    Item = tuple.Item2;

                    IsAddAdmins = _item.AdminRights.IsAddAdmins;
                    IsPinMessages = _item.AdminRights.IsPinMessages;
                    //IsInviteLink = _item.AdminRights.IsInviteLink;
                    //IsInviteUsers = _item.AdminRights.IsInviteUsers;
                    IsBanUsers = _item.AdminRights.IsBanUsers;
                    IsDeleteMessages = _item.AdminRights.IsDeleteMessages;
                    //IsEditMessages = _item.AdminRights.IsEditMessages;
                    //IsPostMessages = _item.AdminRights.IsPostMessages;
                    IsChangeInfo = _item.AdminRights.IsChangeInfo;
                }
            }

            return Task.CompletedTask;
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
    }
}
