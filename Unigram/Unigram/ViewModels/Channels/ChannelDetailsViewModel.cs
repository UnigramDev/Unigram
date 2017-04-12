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
using Unigram.Converters;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelDetailsViewModel : UnigramViewModelBase
    {
        public ICacheService _CacheService;
        public int offset = 0;
        public ChannelDetailsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {
            _CacheService = cacheService;
        }

        private TLChat _item;
        public TLChat Item
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

        private TLChatFull _full;
        public TLChatFull Full
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

        public SortedObservableCollection<TLChannelParticipantBase> Participants { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var inputPeerChannel = parameter as TLInputPeerChannel;
            var inputChannel = new TLInputChannel { AccessHash = inputPeerChannel.AccessHash, ChannelId = inputPeerChannel.ChannelId };
            var peerChannel = inputPeerChannel.ToPeer();
            var channel = await ProtoService.GetFullChannelAsync(inputChannel);
            if (channel.IsSucceeded)
            {
               Item = CacheService.GetChat(channel.Result.FullChat.Id) as TLChat;
               var participantsX = await ProtoService.GetParticipantsAsync(inputChannel, null, offset, 20);
               offset += 20;
               var collection = new SortedObservableCollection<TLChannelParticipantBase>(new TLChannelParticipantBaseComparer(true));

               Participants = collection;
               RaisePropertyChanged(() => Participants);

               collection.AddRange(participantsX.Result.Participants, true);
            }
        }

        public void getMoreParticipants()
        {

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
}
