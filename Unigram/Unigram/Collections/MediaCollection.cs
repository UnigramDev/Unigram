using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TdWindows;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.Collections
{
    public class MediaCollection : IncrementalCollection<KeyedList<DateTime, Message>>
    {
        private readonly IProtoService _protoService;
        private readonly SearchMessagesFilter _filter;
        private readonly long _chatId;

        private long _lastMaxId;
        private bool _hasMore;

        public MediaCollection(IProtoService protoService, long chatId, SearchMessagesFilter filter)
        {
            _protoService = protoService;
            _chatId = chatId;
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

        public override async Task<IList<KeyedList<DateTime, Message>>> LoadDataAsync()
        {
            try
            {
                var response = await _protoService.SendAsync(new SearchChatMessages(_chatId, _query, 0, _lastMaxId, 0, 50, _filter));
                if (response is TdWindows.Messages messages)
                {
                    if (messages.MessagesData.Count > 0)
                    {
                        _lastMaxId = messages.MessagesData.Min(x => x.Id);
                        _hasMore = true;
                    }
                    else
                    {
                        _hasMore = false;
                    }

                    return messages.MessagesData.GroupBy(x =>
                    {
                        var dateTime = Utils.UnixTimestampToDateTime(x.Date);
                        return new DateTime(dateTime.Year, dateTime.Month, 1);

                    }).Select(x => new KeyedList<DateTime, Message>(x)).ToList();
                }
            }
            catch { }

            return new KeyedList<DateTime, Message>[0];
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }

        protected override void Merge(IList<KeyedList<DateTime, Message>> result)
        {
            base.Merge(result);
            return;

            var last = this.LastOrDefault();
            if (last == null)
            {
                Add(new KeyedList<DateTime, Message>(DateTime.Now));
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
