//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.Collections
{
    public partial class SearchChatMessagesCollection : IncrementalCollection<Message>
    {
        private readonly IClientService _clientService;

        private readonly long _chatId;
        private readonly MessageTopic _topic;
        private readonly long _savedMessagesTopicId;
        private readonly string _query;
        private readonly MessageSender _sender;
        private readonly ReactionType _savedMessagesTag;
        private readonly bool _secretChat;

        private long _fromMessageId;
        private string _fromOffset = string.Empty;

        private readonly SearchMessagesFilter _filter;

        public SearchChatMessagesCollection(IClientService clientService, long chatId, MessageTopic topic, string query, MessageSender sender, long fromMessageId, SearchMessagesFilter filter, ReactionType savedMessagesTag)
        {
            _clientService = clientService;

            _chatId = chatId;
            _topic = topic;
            _query = query;
            _sender = sender;
            _fromMessageId = fromMessageId;
            _filter = filter;
            _savedMessagesTag = savedMessagesTag;

            if (topic is MessageTopicSavedMessages savedMessages)
            {
                _savedMessagesTopicId = savedMessages.SavedMessagesTopicId;
            }

            if (clientService.TryGetChat(chatId, out Chat chat))
            {
                _secretChat = chat.Type is ChatTypeSecret;
            }
        }

        public IClientService ClientService => _clientService;

        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
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
                    function = new SearchChatMessages(_chatId, _topic, _query, _sender, fromMessageId, offset, (int)count, _filter);
                }
            }

            var response = await _clientService.SendAsync(function);
            if (response is FoundChatMessages chatMessages)
            {
                TotalCount = chatMessages.TotalCount;
                AddRange(chatMessages.Messages);

                _fromMessageId = chatMessages.NextFromMessageId;

                return new IncrementalLoadResult((uint)chatMessages.Messages.Count, chatMessages.NextFromMessageId != 0);
            }
            else if (response is FoundMessages messages)
            {
                TotalCount = messages.TotalCount;
                AddRange(messages.Messages);

                _fromOffset = messages.NextOffset;

                return new IncrementalLoadResult((uint)messages.Messages.Count, messages.NextOffset.Length > 0);
            }

            return default;
        }
    }
}
