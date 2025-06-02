using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public partial interface ICacheService
    {
        void SetPinnedForumTopics(long chatId, IList<long> messageThreadIds);

        Task<Topics> GetForumTopicsAsync(long chatId, int offset, int limit);

        bool TryGetForumTopic(long chatId, long id, out ForumTopic topic);
        bool TryGetForumTopic(long chatId, MessageTopic messageTopic, out ForumTopic topic);

        IEnumerable<ForumTopic> GetForumTopics(long chatId, IEnumerable<long> ids);
        ForumTopic GetForumTopic(long chatId, long id);

        int UnreadTopicCount(long chatId);
    }

    public partial class ClientService
    {
        private readonly ConcurrentDictionary<long, ForumTopicService> _forums = new();

        public void SetPinnedForumTopics(long chatId, IList<long> messageThreadIds)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                manager.SetPinnedForumTopics(messageThreadIds);
            }
        }

        public Task<Topics> GetForumTopicsAsync(long chatId, int offset, int limit)
        {
            _forums.TryGetValue(chatId, out ForumTopicService manager);

            if (manager == null)
            {
                manager = new ForumTopicService(this, _aggregator, chatId);
                _forums[chatId] = manager;
            }

            return manager.GetForumTopicsAsync(offset, limit);
        }

        public ForumTopic GetForumTopic(long chatId, long id)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                return manager.GetTopic(id);
            }

            return null;
        }

        public bool TryGetForumTopic(long chatId, long id, out ForumTopic topic)
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

        public IEnumerable<ForumTopic> GetForumTopics(long chatId, IEnumerable<long> ids)
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

        private void UpdateForumTopic(long chatId, Action<ForumTopicService> update)
        {
            if (_forums.TryGetValue(chatId, out ForumTopicService manager))
            {
                update(manager);
            }
            else
            {
                manager = new ForumTopicService(this, _aggregator, chatId);
                _forums[chatId] = manager;

                manager.GetForumTopicsAsync(0, 20);

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

                    manager.GetForumTopicsAsync(0, 20);
                }
                else if (supergroup.IsFeedbackGroup && !_feedbackChats.ContainsKey(chat.Id))
                {
                    var manager = new FeedbackChatTopicService(this, _aggregator, chat.Id);
                    _feedbackChats[chat.Id] = manager;
                }
            }
        }
    }
}
