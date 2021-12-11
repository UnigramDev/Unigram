using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
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
    }
}
