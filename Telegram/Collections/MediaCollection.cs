//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public partial class MediaCollection : ObservableCollection<MessageWithOwner>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly SearchMessagesFilter _filter;
        private readonly long _chatId;
        private readonly MessageTopic _topic;
        private readonly string _query;

        private string _nextOffset;
        private long _nextFromMessageId;
        private bool _hasMore = true;

        public SearchMessagesFilter Filter => _filter;

        public MediaCollection(IClientService clientService, long chatId, MessageTopic topic, SearchMessagesFilter filter, string query = null)
        {
            _clientService = clientService;
            _chatId = chatId;
            _topic = topic;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        public MediaCollection(IClientService clientService, SearchMessagesFilter filter, string query = null)
        {
            _clientService = clientService;
            _filter = filter;
            _query = query ?? string.Empty;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                var count = 0u;

                Function func;
                if (_chatId != 0)
                {
                    func = new SearchChatMessages(_chatId, _topic, _query, null, _nextFromMessageId, 0, 50, _filter);
                }
                else
                {
                    func = new SearchMessages(new ChatListMain(), _query, _nextOffset ?? string.Empty, 50, _filter, null, 0, 0);
                }

                var response = await _clientService.SendAsync(func);
                if (response is FoundChatMessages foundChatMessages)
                {
                    if (foundChatMessages.NextFromMessageId != 0)
                    {
                        _nextFromMessageId = foundChatMessages.NextFromMessageId;
                        _hasMore = true;
                    }
                    else
                    {
                        _hasMore = false;
                    }

                    foreach (var message in foundChatMessages.Messages)
                    {
                        Add(new MessageWithOwner(_clientService, message));
                        count++;
                    }
                }
                else if (response is FoundMessages foundMessages)
                {
                    if (foundMessages.NextOffset.Length > 0)
                    {
                        _nextOffset = foundMessages.NextOffset;
                        _hasMore = true;
                    }
                    else
                    {
                        _hasMore = false;
                    }

                    foreach (var message in foundMessages.Messages)
                    {
                        Add(new MessageWithOwner(_clientService, message));
                        count++;
                    }
                }

                return new LoadMoreItemsResult
                {
                    Count = count
                };
            });
        }

        public bool HasMoreItems => _hasMore;
    }
}
