//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public class SearchChatMessagesCollection : MvxObservableCollection<Message>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;

        private readonly long _chatId;
        private readonly long _threadId;
        private readonly long _savedMessagesTopicId;
        private readonly string _query;
        private readonly MessageSender _sender;
        private readonly ReactionType _savedMessagesTag;
        private readonly bool _secretChat;

        private long _fromMessageId;
        private string _fromOffset = string.Empty;

        private bool _hasMoreItems = true;

        private readonly SearchMessagesFilter _filter;

        public SearchChatMessagesCollection(IClientService clientService, long chatId, long threadId, long savedMessagesTopicId, string query, MessageSender sender, long fromMessageId, SearchMessagesFilter filter, ReactionType savedMessagesTag)
        {
            _clientService = clientService;

            _chatId = chatId;
            _threadId = threadId;
            _savedMessagesTopicId = savedMessagesTopicId;
            _query = query;
            _sender = sender;
            _fromMessageId = fromMessageId;
            _filter = filter;
            _savedMessagesTag = savedMessagesTag;

            if (clientService.TryGetChat(chatId, out Chat chat))
            {
                _secretChat = chat.Type is ChatTypeSecret;
            }
        }

        public IClientService ClientService => _clientService;

        public int TotalCount { get; private set; }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                Function function;
                if (_secretChat)
                {
                    function = new SearchSecretMessages(_chatId, _query, _fromOffset, 50, _filter);
                }
                else
                {
                    var fromMessageId = _fromMessageId;
                    var offset = -49;

                    var last = this.LastOrDefault();
                    if (last != null)
                    {
                        fromMessageId = last.Id;
                        offset = 0;
                    }

                    if (_savedMessagesTag != null)
                    {
                        function = new SearchSavedMessages(_savedMessagesTopicId, _savedMessagesTag, _query, fromMessageId, offset, (int)count);
                    }
                    else
                    {
                        function = new SearchChatMessages(_chatId, _query, _sender, fromMessageId, offset, (int)count, _filter, _threadId, _savedMessagesTopicId);
                    }
                }

                var response = await _clientService.SendAsync(function);
                if (response is FoundChatMessages chatMessages)
                {
                    TotalCount = chatMessages.TotalCount;
                    AddRange(chatMessages.Messages);

                    _fromMessageId = chatMessages.NextFromMessageId;
                    _hasMoreItems = chatMessages.NextFromMessageId != 0;

                    return new LoadMoreItemsResult
                    {
                        Count = (uint)chatMessages.Messages.Count
                    };
                }
                else if (response is FoundMessages messages)
                {
                    TotalCount = messages.TotalCount;
                    AddRange(messages.Messages);

                    _fromOffset = messages.NextOffset;
                    _hasMoreItems = messages.NextOffset.Length > 0;

                    return new LoadMoreItemsResult
                    {
                        Count = (uint)messages.Messages.Count
                    };
                }

                return new LoadMoreItemsResult
                {
                    Count = 0
                };
            });
        }

        public bool HasMoreItems => _hasMoreItems;
    }
}
