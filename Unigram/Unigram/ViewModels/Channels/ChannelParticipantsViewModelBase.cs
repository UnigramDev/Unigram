using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelParticipantsViewModelBase : UnigramViewModelBase
    {
        private readonly TLChannelParticipantsFilterBase _filter;

        public ChannelParticipantsViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, TLChannelParticipantsFilterBase filter)
            : base(protoService, cacheService, aggregator)
        {
            _filter = filter;
        }

        protected TLChannel _item;
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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Item = null;

            var channel = parameter as TLChannel;
            var peer = parameter as TLPeerChannel;
            if (peer != null)
            {
                channel = CacheService.GetChat(peer.ChannelId) as TLChannel;
            }

            if (channel != null)
            {
                Item = channel;

                Participants = new ItemsCollection(ProtoService, channel.ToInputChannel(), _filter);
                RaisePropertyChanged(() => Participants);
            }
        }

        public ItemsCollection Participants { get; protected set; }

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
}
