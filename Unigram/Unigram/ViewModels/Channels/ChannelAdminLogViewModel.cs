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
    public class ChannelAdminLogViewModel : UnigramViewModelBase
    {
        public ChannelAdminLogViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
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

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
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

                Items = new ItemsCollection(ProtoService, channel);
                RaisePropertyChanged(() => Items);
            }

            return Task.CompletedTask;
        }

        public ItemsCollection Items { get; protected set; }

        public class ItemsCollection : IncrementalCollection<AdminLogEvent>
        {
            private readonly IMTProtoService _protoService;
            private readonly TLInputChannelBase _inputChannel;
            private readonly TLChannel _channel;
            //private readonly TLChannelParticipantsFilterBase _filter;

            private long _minEventId = long.MaxValue;
            private bool _hasMore;

            public ItemsCollection(IMTProtoService protoService, TLChannel channel)
            {
                _protoService = protoService;
                _inputChannel = channel.ToInputChannel();
                _channel = channel;
                //_filter = filter;
                _hasMore = true;
            }

            public override async Task<IList<AdminLogEvent>> LoadDataAsync()
            {
                var maxId = Count > 0 ? _minEventId : 0;

                var response = await _protoService.GetAdminLogAsync(_inputChannel, null, null, null, maxId, 0, 50);
                if (response.IsSucceeded)
                {
                    foreach (var item in response.Result.Events)
                    {
                        _minEventId = Math.Min(_minEventId, item.Id);
                    }

                    if (response.Result.Events.Count < 50)
                    {
                        _hasMore = false;
                    }

                    return response.Result.Events.Select(x => new AdminLogEvent { Channel = _channel, Event = x }).ToArray();
                }

                return new AdminLogEvent[0];
            }

            protected override bool GetHasMoreItems()
            {
                return _hasMore;
            }
        }

    }
}
