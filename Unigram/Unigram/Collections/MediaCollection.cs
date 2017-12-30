using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Unigram.Common;

namespace Unigram.Collections
{
    public class MediaCollection : IncrementalCollection<KeyedList<DateTime, TLMessage>>
    {
        private readonly IMTProtoService _protoService;
        private readonly TLMessagesFilterBase _filter;
        private readonly TLInputPeerBase _peer;

        private int _lastMaxId;
        private bool _hasMore;

        public MediaCollection(IMTProtoService protoService, TLInputPeerBase peer, TLMessagesFilterBase filter)
        {
            _protoService = protoService;
            _peer = peer;
            _filter = filter;
        }

        private string _query = string.Empty;
        public string Query
        {
            get
            {
                return _query;
            }
            set
            {
                if (_query != value)
                {
                    _query = value;
                    _lastMaxId = 0;
                    RaisePropertyChanged("Query");

                    Clear();

                    HasMoreItems = true;
                }
            }
        }

        public override async Task<IList<KeyedList<DateTime, TLMessage>>> LoadDataAsync()
        {
            try
            {
                var response = await _protoService.SearchAsync(_peer, _query, null, _filter, 0, 0, _lastMaxId, 0, 50);
                if (response.IsSucceeded && response.Result is ITLMessages result)
                {
                    if (result.Messages.Count > 0)
                    {
                        //_lastMaxId = response.Result.Messages.Min(x => x.Id);
                        _lastMaxId += result.Messages.Count;
                        _hasMore = result.Messages.Count == 50;
                    }
                    else
                    {
                        _hasMore = false;
                    }

                    return result.Messages.OfType<TLMessage>().GroupBy(x =>
                    {
                        var clientDelta = MTProtoService.Current.ClientTicksDelta;
                        var utc0SecsInt = x.Date - clientDelta / 4294967296.0;
                        var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

                        return new DateTime(dateTime.Year, dateTime.Month, 1);

                    }).Select(x => new KeyedList<DateTime, TLMessage>(x)).ToList();
                }
            }
            catch { }

            return new KeyedList<DateTime, TLMessage>[0];
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }

        protected override void Merge(IList<KeyedList<DateTime, TLMessage>> result)
        {
            base.Merge(result);
            return;

            var last = this.LastOrDefault();
            if (last == null)
            {
                Add(new KeyedList<DateTime, TLMessage>(DateTime.Now));
            }

            foreach (var group in result)
            {
                if (last != null && last.Key.Date == group.Key.Date)
                {
                    //last.AddRange(group);

                    foreach (var item in group)
                    {
                        last.Add(item);
                    }

                    last.Update();
                }
                else
                {
                    //if (Count < 1)
                    {
                        Add(group);
                    }
                }
            }
        }
    }
}
