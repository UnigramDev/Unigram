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

        public override async Task<IEnumerable<KeyedList<DateTime, TLMessage>>> LoadDataAsync()
        {
            try
            {
                var result = await _protoService.SearchAsync(_peer, _query, _filter, 0, 0, 0, _lastMaxId, 50);
                if (result.IsSucceeded)
                {
                    if (result.Result.Messages.Count > 0)
                    {
                        _lastMaxId = result.Result.Messages.Min(x => x.Id);
                    }

                    return result.Result.Messages.OfType<TLMessage>().GroupBy(x =>
                    {
                        var clientDelta = MTProtoService.Current.ClientTicksDelta;
                        var utc0SecsLong = x.Date * 4294967296 - clientDelta;
                        var utc0SecsInt = utc0SecsLong / 4294967296.0;
                        var dateTime = Utils.UnixTimestampToDateTime(utc0SecsInt);

                        return new DateTime(dateTime.Year, dateTime.Month, 1);

                    }).Select(x => new KeyedList<DateTime, TLMessage>(x));
                }
            }
            catch { }

            return new KeyedList<DateTime, TLMessage>[0];
        }

        protected override void Merge(IEnumerable<KeyedList<DateTime, TLMessage>> result)
        {
            var last = this.LastOrDefault();

            foreach (var group in result)
            {
                if (last != null && last.Key.Date == group.Key.Date)
                {
                    foreach (var item in group)
                    {
                        last.Add(item);
                    }
                }
                else
                {
                    Add(group);
                }
            }
        }
    }
}
