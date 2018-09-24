using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.Collections
{
    public class MediaCollection : IncrementalCollection<KeyedList<DateTime, Message>>
    {
        private readonly IProtoService _protoService;
        private readonly SearchMessagesFilter _filter;
        private readonly long _chatId;
        private readonly string _query;

        private long _lastMaxId;
        private bool _hasMore;

        public MediaCollection(IProtoService protoService, long chatId, SearchMessagesFilter filter, string query = null)
        {
            _protoService = protoService;
            _chatId = chatId;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        public override async Task<IList<KeyedList<DateTime, Message>>> LoadDataAsync()
        {
            try
            {
                var response = await _protoService.SendAsync(new SearchChatMessages(_chatId, _query, 0, _lastMaxId, 0, 50, _filter));
                if (response is Messages messages)
                {
                    if (messages.MessagesValue.Count > 0)
                    {
                        _lastMaxId = messages.MessagesValue.Min(x => x.Id);
                        _hasMore = true;
                    }
                    else
                    {
                        _hasMore = false;
                    }

                    return messages.MessagesValue.GroupBy(x =>
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
