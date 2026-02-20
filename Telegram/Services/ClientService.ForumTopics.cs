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
        void SetPinnedForumTopics(long chatId, IList<int> forumTopicIds);

        Task<ForumTopics2> GetForumTopicsAsync(long chatId, int offset, int limit);

        bool TryGetForumTopic(long chatId, int id, out ForumTopic topic);
        bool TryGetForumTopic(long chatId, MessageTopic messageTopic, out ForumTopic topic);

        IEnumerable<ForumTopic> GetForumTopics(long chatId, IEnumerable<int> ids);
        ForumTopic GetForumTopic(long chatId, int id);

        int UnreadTopicCount(long chatId);
    }

    public partial class ClientService
    {
        private readonly ReaderWriterDictionary<long, ForumTopicService> _forums = new(100);

        public void SetPinnedForumTopics(long chatId, IList<int> forumTopicIds)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                manager.SetPinnedForumTopics(forumTopicIds);
            }
        }

        public Task<ForumTopics2> GetForumTopicsAsync(long chatId, int offset, int limit)
        {
            _forums.TryGetValue(chatId, out ForumTopicService manager);

            if (manager == null)
            {
                manager = new ForumTopicService(this, _aggregator, chatId);
                _forums[chatId] = manager;
            }

            return manager.GetForumTopicsAsync(offset, limit);
        }

        public ForumTopic GetForumTopic(long chatId, int id)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                return manager.GetTopic(id);
            }

            return null;
        }

        public bool TryGetForumTopic(long chatId, int id, out ForumTopic topic)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                topic = manager.GetTopic(id);
                return topic != null;
            }

            topic = null;
            return false;
        }

        public bool TryGetForumTopic(long chatId, MessageTopic messageTopic, out ForumTopic topic)
        {
            if (messageTopic is MessageTopicForum topicForum)
            {
                return TryGetForumTopic(chatId, topicForum.ForumTopicId, out topic);
            }

            topic = null;
            return false;
        }

        public IEnumerable<ForumTopic> GetForumTopics(long chatId, IEnumerable<int> ids)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                return manager.GetTopics(ids);
            }

            return Array.Empty<ForumTopic>();
        }

        public int UnreadTopicCount(long chatId)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                return manager.UnreadCount;
            }

            return 0;
        }

        private void UpdateForumTopic(long chatId, bool createNew, Action<ForumTopicService> update)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                update(manager);
            }
            else if (createNew)
            {
                manager = new ForumTopicService(this, _aggregator, chatId);
                _forums[chatId] = manager;

                //manager.GetForumTopicsAsync(0, 20);

                update(manager);
            }
        }

        private void UpdateMessageTopicNewChat(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup && TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.IsForum && !_forums.ContainsKey(chat.Id))
                {
                    var manager = new ForumTopicService(this, _aggregator, chat.Id);
                    _forums[chat.Id] = manager;

                    //manager.GetForumTopicsAsync(0, 20);
                }
                else if (supergroup.IsDirectMessagesGroup && !_directMessagesChats.ContainsKey(chat.Id))
                {
                    var manager = new DirectMessagesChatTopicService(this, _aggregator, chat.Id);
                    _directMessagesChats[chat.Id] = manager;
                }
            }
        }
    }
}
