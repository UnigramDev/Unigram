using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;

namespace Unigram.Collections
{
    public class MediaCollection : IncrementalCollection<Message>
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

        public override async Task<IList<Message>> LoadDataAsync()
        {
            try
            {
                IsLoading = true;

                var response = await _protoService.SendAsync(new SearchChatMessages(_chatId, _query, null, _lastMaxId, 0, 50, _filter, 0));
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

                    IsLoading = false;

                    return messages.MessagesValue;
                }
            }
            catch { }

            IsLoading = false;

            return new Message[0];
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }

        protected override void InsertItem(int index, Message item)
        {
            base.InsertItem(index, item);

            var previous = index > 0 ? this[index - 1] : null;
            var next = index < Count - 1 ? this[index + 1] : null;

            UpdateSeparatorOnInsert(item, next, index);
            UpdateSeparatorOnInsert(previous, item, index - 1);
        }

        private static Message ShouldUpdateSeparatorOnInsert(Message item, Message previous, int index)
        {
            if (item != null && previous != null)
            {
                var itemDate = Utils.UnixTimestampToDateTime(item.Date);
                var previousDate = Utils.UnixTimestampToDateTime(previous.Date);
                if (previousDate.Year != itemDate.Year || previousDate.Month != itemDate.Month)
                {
                    var service = new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, previous.IsChannelPost, false, previous.Date, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null);
                    return service;
                }
            }
            else if (item == null && previous != null)
            {
                var service = new Message(0, previous.SenderId, previous.ChatId, null, null, previous.IsOutgoing, false, false, false, false, true, false, false, false, false, false, false, previous.IsChannelPost, false, previous.Date, 0, null, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, string.Empty, new MessageHeaderDate(), null);
                return service;
            }

            return null;
        }

        private void UpdateSeparatorOnInsert(Message item, Message previous, int index)
        {
            var service = ShouldUpdateSeparatorOnInsert(item, previous, index);
            if (service != null)
            {
                base.InsertItem(index + 1, service);
            }
        }
    }

    public class GroupedMediaCollection : IncrementalCollection<DateMessageKeyedList>
    {
        private readonly IProtoService _protoService;
        private readonly SearchMessagesFilter _filter;
        private readonly long _chatId;
        private readonly string _query;

        private long _lastMaxId;
        private bool _hasMore;

        public GroupedMediaCollection(IProtoService protoService, long chatId, SearchMessagesFilter filter, string query = null)
        {
            _protoService = protoService;
            _chatId = chatId;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        public override async Task<IList<DateMessageKeyedList>> LoadDataAsync()
        {
            try
            {
                var response = await _protoService.SendAsync(new SearchChatMessages(_chatId, _query, null, _lastMaxId, 0, 50, _filter, 0));
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

                    }).Select(x => new DateMessageKeyedList(x)).ToList();
                }
            }
            catch { }

            return new DateMessageKeyedList[0];
        }

        protected override bool GetHasMoreItems()
        {
            return _hasMore;
        }

        protected override void Merge(IList<DateMessageKeyedList> result)
        {
            base.Merge(result);
            return;

            var last = this.LastOrDefault();
            if (last == null)
            {
                Add(new DateMessageKeyedList(DateTime.Now));
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

    public class DateMessageKeyedList : KeyedList<DateTime, Message> /*, IKeyIndexMapping*/
    {
        public DateMessageKeyedList(DateTime key)
            : base(key)
        {
        }

        public DateMessageKeyedList(DateTime key, IEnumerable<Message> source)
            : base(key, source)
        {
        }

        public DateMessageKeyedList(IGrouping<DateTime, Message> source)
            : base(source)
        {
        }

        public string KeyFromIndex(int index)
        {
            return this[index].Id.ToString();
        }

        public int IndexFromKey(string key)
        {
            if (int.TryParse(key, out int id))
            {
                return IndexOf(this.FirstOrDefault(x => x.Id == id));
            }

            return -1;
        }
    }
}
