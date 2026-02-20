//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial interface ICacheService
    {
        Task<Topics> GetDirectMessagesChatTopicsAsync(long chatId, int offset, int limit);

        bool TryGetDirectMessagesChatTopic(long chatId, long id, out DirectMessagesChatTopic topic);
        bool TryGetDirectMessagesChatTopic(long chatId, MessageTopic messageTopic, out DirectMessagesChatTopic topic);

        IEnumerable<DirectMessagesChatTopic> GetDirectMessagesChatTopics(long chatId, IEnumerable<long> ids);
        DirectMessagesChatTopic GetDirectMessagesChatTopic(long chatId, long id);
    }

    public partial class ClientService
    {
        private readonly ReaderWriterDictionary<long, DirectMessagesChatTopicService> _directMessagesChats = new(100);

        public Task<Topics> GetDirectMessagesChatTopicsAsync(long chatId, int offset, int limit)
        {
            _directMessagesChats.TryGetValue(chatId, out DirectMessagesChatTopicService manager);

            if (manager == null)
            {
                manager = new DirectMessagesChatTopicService(this, _aggregator, chatId);
                _directMessagesChats[chatId] = manager;
            }

            return manager.GetDirectMessagesChatTopicsAsync(offset, limit);
        }

        public DirectMessagesChatTopic GetDirectMessagesChatTopic(long chatId, long id)
        {
            if (_directMessagesChats.TryGetValue(chatId, out DirectMessagesChatTopicService manager))
            {
                return manager.GetTopic(id);
            }

            return null;
        }

        public bool TryGetDirectMessagesChatTopic(long chatId, long id, out DirectMessagesChatTopic topic)
        {
            if (_directMessagesChats.TryGetValue(chatId, out DirectMessagesChatTopicService manager))
            {
                topic = manager.GetTopic(id);
                return topic != null;
            }

            topic = null;
            return false;
        }

        public bool TryGetDirectMessagesChatTopic(long chatId, MessageTopic messageTopic, out DirectMessagesChatTopic topic)
        {
            if (messageTopic is MessageTopicDirectMessages topicDirectMessagesChat)
            {
                return TryGetDirectMessagesChatTopic(chatId, topicDirectMessagesChat.DirectMessagesChatTopicId, out topic);
            }

            topic = null;
            return false;
        }

        public IEnumerable<DirectMessagesChatTopic> GetDirectMessagesChatTopics(long chatId, IEnumerable<long> ids)
        {
            if (_directMessagesChats.TryGetValue(chatId, out DirectMessagesChatTopicService manager))
            {
                return manager.GetTopics(ids);
            }

            return Array.Empty<DirectMessagesChatTopic>();
        }

        private void UpdateDirectMessagesChatTopic(long chatId, Action<DirectMessagesChatTopicService> update)
        {
            if (_directMessagesChats.TryGetValue(chatId, out DirectMessagesChatTopicService manager))
            {
                update(manager);
            }
            else
            {
                manager = new DirectMessagesChatTopicService(this, _aggregator, chatId);
                _directMessagesChats[chatId] = manager;

                update(manager);
            }
        }
    }
}
