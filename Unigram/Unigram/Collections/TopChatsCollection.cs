using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class TopChatsCollection : MvxObservableCollection<Chat>, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly TopChatCategory _category;
        private readonly int _limit;

        private bool _hasMore = false;

        public TopChatsCollection(IProtoService protoService, TopChatCategory category, int limit)
        {
            _protoService = protoService;
            _category = category;
            _limit = limit;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                count = 0;

                var response = await _protoService.SendAsync(new GetTopChats(_category, _limit));
                if (response is Telegram.Td.Api.Chats chats)
                {
                    foreach (var id in chats.ChatIds)
                    {
                        var chat = _protoService.GetChat(id);
                        if (chat != null)
                        {
                            Add(chat);
                            count++;
                        }
                    }
                }

                return new LoadMoreItemsResult { Count = count };
            });
        }

        public bool HasMoreItems => _hasMore;
    }
}
