using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Channels;
using Unigram.Common;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Channels
{
    public class ChannelParticipantsViewModelBase : UnigramViewModelBase
    {
        private readonly TLChannelParticipantsFilterBase _filter;
        private readonly Func<string, TLChannelParticipantsFilterBase> _search;

        public ChannelParticipantsViewModelBase(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, TLChannelParticipantsFilterBase filter, Func<string, TLChannelParticipantsFilterBase> search)
            : base(protoService, cacheService, aggregator)
        {
            _filter = filter;
            _search = search;
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

                Participants = new ItemsCollection(ProtoService, channel.ToInputChannel(), _filter ?? _search(null), null);
                RaisePropertyChanged(() => Participants);
            }

            return Task.CompletedTask;
        }

        public void Find(string query)
        {
            Search = new ItemsCollection(ProtoService, _item.ToInputChannel(), _search(query), null);
            RaisePropertyChanged(() => Search);
        }

        public ItemsCollection Participants { get; protected set; }

        public ItemsCollection Search { get; protected set; }

        public class ItemsCollection : IncrementalCollection<TLChannelParticipantBase>
        {
            private readonly IMTProtoService _protoService;
            private readonly TLInputChannelBase _inputChannel;
            private readonly TLChannelParticipantsFilterBase _filter;
            private readonly int? _count;

            private bool _hasMore;

            public ItemsCollection(IMTProtoService protoService, TLInputChannelBase inputChannel, TLChannelParticipantsFilterBase filter, int? count)
            {
                _protoService = protoService;
                _inputChannel = inputChannel;
                _filter = filter;
                _count = count;
                _hasMore = true;
            }

            public override async Task<IList<TLChannelParticipantBase>> LoadDataAsync()
            {
                //var hash = CalculateHash(this);
                var hash = 0;

                var response = await _protoService.GetParticipantsAsync(_inputChannel, _filter, Items.Count, 200, hash);
                if (response.IsSucceeded && response.Result is TLChannelsChannelParticipants participants)
                {
                    if (participants.Participants.Count < 200)
                    {
                        _hasMore = false;
                    }

                    if (_filter == null && _count.HasValue && _count <= 200)
                    {
                        return participants.Participants.OrderBy(x => x, new TLChannelParticipantBaseComparer(true)).ToList();
                    }

                    return participants.Participants;
                }

                return new TLChannelParticipantBase[0];
            }

            private int CalculateHash(IList<TLChannelParticipantBase> participants)
            {
                var acc = 0L;

                foreach (var item in participants)
                {
                    acc = ((acc * 20261) + 0x80000000L + item.UserId) % 0x80000000L;
                }

                return (int)acc;
            }

            protected override bool GetHasMoreItems()
            {
                return _hasMore;
            }
        }
    }
}
