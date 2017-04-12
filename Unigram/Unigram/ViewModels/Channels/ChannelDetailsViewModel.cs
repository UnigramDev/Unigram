using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Utils;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelDetailsViewModel : UnigramViewModelBase
    {
        public ChannelDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
        }

        private TLChannel _item;
        public TLChannel Item
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

        private TLChannelFull _full;
        public TLChannelFull Full
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
            Item = null;
            Full = null;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;

                var response = await ProtoService.GetFullChannelAsync(channel.ToInputChannel());
                if (response.IsSucceeded)
                {
                    Full = response.Result.FullChat as TLChannelFull;
                    Participants = new ItemsCollection(ProtoService, channel.ToInputChannel(), null);

                    RaisePropertyChanged(() => Participants);
                }
            }
        }

        public ItemsCollection Participants { get; private set; }

        public class ItemsCollection : IncrementalCollection<TLChannelParticipantBase>
        {
            private readonly IMTProtoService _protoService;
            private readonly TLInputChannelBase _inputChannel;
            private readonly TLChannelParticipantsFilterBase _filter;

            public ItemsCollection(IMTProtoService protoService, TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter)
            {
                _protoService = protoService;
                _inputChannel = inputChannel;
                _filter = filter;
            }

            public override async Task<IList<TLChannelParticipantBase>> LoadDataAsync()
            {
                var response = await _protoService.GetParticipantsAsync(_inputChannel, _filter, Items.Count, 200);
                if (response.IsSucceeded)
                {
                    return response.Result.Participants;
                }

                return new TLChannelParticipantBase[0];
            }
        }
    }

    public class TLChannelParticipantBaseComparer : IComparer<TLChannelParticipantBase>
    {
        private bool _epoch;

        public TLChannelParticipantBaseComparer(bool epoch)
        {
            _epoch = epoch;
        }

        public int Compare(TLChannelParticipantBase x, TLChannelParticipantBase y)
        {

            var xUser = x.User;
            var yUser = y.User;
            if (xUser == null || yUser == null)
            {
                return -1;
            }

            if (_epoch)
            {
                var epoch = LastSeenConverter.GetIndex(yUser).CompareTo(LastSeenConverter.GetIndex(xUser));
                if (epoch == 0)
                {
                    var fullName = xUser.FullName.CompareTo(yUser.FullName);
                    if (fullName == 0)
                    {
                        return yUser.Id.CompareTo(xUser.Id);
                    }

                    return fullName;
                }

                return epoch;
            }
            else
            {
                var fullName = xUser.FullName.CompareTo(yUser.FullName);
                if (fullName == 0)
                {
                    return yUser.Id.CompareTo(xUser.Id);
                }

                return fullName;
            }
        }
    }
}
