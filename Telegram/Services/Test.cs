using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    internal class Test2
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly Dictionary<long, Test> _forums = new();

        public Test2(IClientService clientService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _aggregator = aggregator;
        }

        public void ViewMessages(long chatId, long messageThreadId, IList<long> messageIds)
        {
            if (_forums.TryGetValue(chatId, out Test manager))
            {
                manager.ViewMessages(messageThreadId, messageIds);
            }
        }

        public Task<ForuminoTopicinos> GetForumTopicsAsync(long chatId, int offset, int limit)
        {
            _forums.TryGetValue(chatId, out Test manager);

            if (manager == null)
            {
                manager = new Test(_clientService, _aggregator, chatId);
                _forums[chatId] = manager;
            }

            return manager.GetForumTopicsAsync(offset, limit);
        }

        public ForuminoTopicino GetTopic(long chatId, long id)
        {
            if (_forums.TryGetValue(chatId, out Test manager))
            {
                return manager.GetTopic(id);
            }

            return null;
        }

        public IEnumerable<ForuminoTopicino> GetTopics(long chatId, IEnumerable<long> ids)
        {
            if (_forums.TryGetValue(chatId, out Test manager))
            {
                return manager.GetTopics(ids);
            }

            return Array.Empty<ForuminoTopicino>();
        }

        public int UnreadCount(long chatId)
        {
            if (_forums.TryGetValue(chatId, out Test manager))
            {
                return manager.UnreadCount;
            }

            return 0;
        }

        private void UpdateForumTopic(long chatId, Action<Test> update)
        {
            if (_forums.TryGetValue(chatId, out Test manager))
            {
                update(manager);
            }
            else
            {

            }
        }

        public void UpdateForumTopicInfo(long chatId, ForumTopicInfo info)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateForumTopicInfo(info));
        }

        public void UpdateNewChat(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup && _clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.IsForum && !_forums.ContainsKey(chat.Id))
                {
                    var manager = new Test(_clientService, _aggregator, chat.Id);
                    _forums[chat.Id] = manager;

                    manager.GetForumTopicsAsync(0, 20);
                }
            }
        }

        public void UpdateNewMessage(Message message)
        {
            UpdateForumTopic(message.ChatId, manager => manager.UpdateNewMessage(message));
        }

        public void UpdateDeleteMessages(long chatId, IList<long> messageIds, bool isPermanent, bool fromCache)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateDeleteMessages(messageIds, isPermanent, fromCache));
        }

        public void UpdateMessageSendSucceeded(Message message, long oldMessageId)
        {
            UpdateForumTopic(message.ChatId, manager => manager.UpdateMessageSendSucceeded(message, oldMessageId));
        }

        public void UpdateMessageSendFailed(Message message, long oldMessageId, Error error)
        {
            UpdateForumTopic(message.ChatId, manager => manager.UpdateMessageSendFailed(message, oldMessageId, error));
        }

        public void UpdateMessageContent(long chatId, long messageId, MessageContent newContent)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateMessageContent(messageId, newContent));
        }

        public void UpdateMessageEdited(long chatId, long messageId, int editDate, ReplyMarkup replyMarkup)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateMessageEdited(messageId, editDate, replyMarkup));
        }

        public void UpdateMessageIsPinned(long chatId, long messageId, bool isPinned)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateMessageIsPinned(messageId, isPinned));
        }

        public void UpdateMessageInteractionInfo(long chatId, long messageId, MessageInteractionInfo interactionInfo)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateMessageInteractionInfo(messageId, interactionInfo));
        }

        public void UpdateMessageContentOpened(long chatId, long messageId)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateMessageContentOpened(messageId));
        }

        public void UpdateMessageMentionRead(long chatId, long messageId, int unreadMentionCount)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateMessageMentionRead(messageId, unreadMentionCount));
        }

        public void UpdateMessageUnreadReactions(long chatId, long messageId, IList<UnreadReaction> unreadReactions, int unreadReactionCount)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateMessageUnreadReactions(messageId, unreadReactions, unreadReactionCount));
        }

        public void UpdateMessageFactCheck(long chatId, long messageId, FactCheck factCheck)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateMessageFactCheck(messageId, factCheck));
        }

        public void UpdateChatDraftMessage(long chatId, DraftMessage draftMessage)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateChatDraftMessage(draftMessage));
        }

        public void UpdateChatNotificationSettings(long chatId, ChatNotificationSettings notificationSettings)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateChatNotificationSettings(notificationSettings));
        }

        public void UpdateChatLastMessage(long chatId, Message lastMessage)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateChatLastMessage(lastMessage));
        }

        public void UpdateChatReadInbox(long chatId, long lastReadInboxMessageId, int unreadCount)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateChatReadInbox(lastReadInboxMessageId, unreadCount));
        }

        public void UpdateChatReadOutbox(long chatId, long lastReadOutboxMessageId)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateChatReadOutbox(lastReadOutboxMessageId));
        }

        public void UpdateChatUnreadMentionCount(long chatId, long unreadMentionCount)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateChatUnreadMentionCount(unreadMentionCount));
        }

        public void UpdateChatUnreadReactionCount(long chatId, long unreadReactionCount)
        {
            UpdateForumTopic(chatId, manager => manager.UpdateChatUnreadReactionCount(unreadReactionCount));
        }
    }

    internal class Test
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly long _chatId;

        private readonly Dictionary<long, ForuminoTopicino> _topics = new();
        private readonly Dictionary<long, ForuminoTopicino> _messages = new();

        private readonly SortedSet<OrderedForumTopic> _order = new();
        private readonly List<long> _pinnedTopicIds = new();
        private readonly HashSet<long> _unreadTopicIds = new();

        private readonly HashSet<long> _pendingNewTopics = new();

        private bool _haveFullList;

        public Test(IClientService clientService, IEventAggregator aggregator, long chatId)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _chatId = chatId;
        }

        public int UnreadCount
        {
            get
            {
                lock (_unreadTopicIds)
                {
                    return _unreadTopicIds.Count;
                }
            }
        }

        private void UpdateTopicOrder(ForuminoTopicino topic, bool publish)
        {
            var order = Order(topic);

            Monitor.Enter(_order);

            _order.Remove(new OrderedForumTopic(topic.Info.MessageThreadId, topic.Order));

            topic.Order = order;

            if (order != 0)
            {
                _order.Add(new OrderedForumTopic(topic.Info.MessageThreadId, order));
            }

            Monitor.Exit(_order);

            if (publish)
            {
                _aggregator.Publish(new UpdateForumTopicLastMessage(_chatId, topic));
            }
        }

        public void ViewMessages(long messageThreadId, IList<long> messageIds)
        {
            if (_topics.TryGetValue(messageThreadId, out ForuminoTopicino topic))
            {
                UpdateReadInbox(topic, messageIds.Max());
            }
        }

        private void UpdateReadInbox(ForuminoTopicino topic, long lastReadInboxMessageId)
        {
            if (lastReadInboxMessageId > topic.LastReadInboxMessageId)
            {
                topic.LastReadInboxMessageId = lastReadInboxMessageId;
                UpdateUnreadCount(topic);
            }
        }

        private void UpdateUnreadCount(ForuminoTopicino topic)
        {
            if (topic.LastMessage?.Id <= topic.LastReadInboxMessageId && topic.UnreadCount > 0)
            {
                topic.UnreadCount = 0;
                UpdateUnreadTopicCount(topic, false);
            }
            else if (topic.LastMessage?.Id > topic.LastReadInboxMessageId && topic.UnreadCount == 0 && !topic.LastMessage.IsOutgoing)
            {
                topic.UnreadCount = 1;
                UpdateUnreadTopicCount(topic, true);
            }
        }

        private void UpdateUnreadTopicCount(ForuminoTopicino topic, bool unread)
        {
            bool update;
            lock (_unreadTopicIds)
            {
                update = unread
                    ? _unreadTopicIds.Add(topic.Info.MessageThreadId)
                    : _unreadTopicIds.Remove(topic.Info.MessageThreadId);
            }

            if (update)
            {
                _aggregator.Publish(new UpdateChatUnreadTopicCount(_chatId, UnreadCount));
                _aggregator.Publish(new UpdateForumTopicReadInbox(_chatId, topic.Info.MessageThreadId, topic.LastReadInboxMessageId, topic.UnreadCount));
            }
        }

        public ForuminoTopicino GetTopic(long id)
        {
            if (_topics.TryGetValue(id, out ForuminoTopicino value))
            {
                return value;
            }

            return null;
        }

        public IEnumerable<ForuminoTopicino> GetTopics(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                var chat = GetTopic(id);
                if (chat != null)
                {
                    yield return chat;
                }
            }
        }

        public Task<ForuminoTopicinos> GetForumTopicsAsync(int offset, int limit)
        {
            return GetForumTopicsAsyncImpl(offset, limit, false);
        }

        public async Task<ForuminoTopicinos> GetForumTopicsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            Monitor.Enter(_order);

            var count = offset + limit;
            var sorted = _order;

            var haveFullList = _haveFullList;

#if MOCKUP
            _haveFullChatList[index] = true;
#else
            if (count > sorted.Count && !haveFullList && !reentrancy)
            {
                Monitor.Exit(_order);

                var response = await LoadForumTopicsAsync(count - sorted.Count);
                if (response is Error error)
                {
                    if (error.Code == 404)
                    {
                        _haveFullList = true;
                    }
                    else
                    {
                        return new ForuminoTopicinos(0, Array.Empty<long>());
                    }
                }

                // Chats have already been received through updates, let's retry request
                return await GetForumTopicsAsyncImpl(offset, limit, true);
            }
#endif

            // Have enough chats in the chat list to answer request
            var result = new long[Math.Max(0, Math.Min(limit, sorted.Count - offset))];
            var pos = 0;

            using (var iter = sorted.GetEnumerator())
            {
                int max = Math.Min(count, sorted.Count);

                for (int i = 0; i < max; i++)
                {
                    iter.MoveNext();

                    if (i >= offset)
                    {
                        result[pos++] = iter.Current.TopicId;
                    }
                }
            }

            haveFullList &= count >= sorted.Count;

            Monitor.Exit(_order);
            return new ForuminoTopicinos(haveFullList ? -1 : 0, result);
        }

        private int _nextOffsetDate;
        private long _nextOffsetMessageId;
        private long _nextOffsetMessageThreadId;

        private Task<BaseObject> LoadForumTopicsAsync(int count)
        {
            var tsc = new TaskCompletionSource<BaseObject>();
            var request = new GetForumTopics(_chatId, string.Empty, _nextOffsetDate, _nextOffsetMessageId, _nextOffsetMessageThreadId, count);

            _clientService.Send(request, response =>
            {
                Monitor.Enter(_order);

                if (response is ForumTopics forumTopics)
                {
                    _nextOffsetDate = forumTopics.NextOffsetDate;
                    _nextOffsetMessageId = forumTopics.NextOffsetMessageId;
                    _nextOffsetMessageThreadId = forumTopics.NextOffsetMessageThreadId;

                    var topics = new List<ForuminoTopicino>(forumTopics.Topics.Count);

                    foreach (var item in forumTopics.Topics)
                    {
                        var topic = new ForuminoTopicino(item);

                        _topics[topic.Info.MessageThreadId] = topic;
                        _messages[topic.LastMessage.Id] = topic;

                        if (topic.IsPinned)
                        {
                            _pinnedTopicIds.Add(topic.Info.MessageThreadId);
                        }

                        if (topic.UnreadCount > 0)
                        {
                            _unreadTopicIds.Add(topic.Info.MessageThreadId);
                        }

                        topics.Add(topic);
                    }

                    foreach (var topic in topics)
                    {
                        UpdateTopicOrder(topic, false);
                    }

                    _aggregator.Publish(new UpdateChatUnreadTopicCount(_chatId, UnreadCount));

                    if (forumTopics.Topics.Count > 0 && _order.Count < forumTopics.TotalCount + 1)
                    {
                        tsc.SetResult(new Ok());
                    }
                    else
                    {
                        tsc.SetResult(new Error(404, string.Empty));
                    }
                }
                else
                {
                    tsc.SetResult(new Error(500, string.Empty));
                }

                Monitor.Exit(_order);
            });

            return tsc.Task;
        }

        private long Order(ForuminoTopicino topic)
        {
            if (topic.IsDeleted)
            {
                return 0;
            }

            var index = _pinnedTopicIds.IndexOf(topic.Info.MessageThreadId);
            if (index != -1)
            {
                return long.MaxValue - index;
            }
            else if (topic.LastMessage != null)
            {
                return topic.LastMessage.Id;
            }

            return topic.Info.MessageThreadId;
        }

        public void UpdateForumTopicInfo(ForumTopicInfo info)
        {
            if (_topics.TryGetValue(info.MessageThreadId, out ForuminoTopicino topic))
            {
                topic.Info = info;
            }
        }

        private void UpdateNewTopic(BaseObject response)
        {
            ForuminoTopicino topic;
            ForumTopic newTopic = response as ForumTopic;

            if (newTopic == null)
            {
                return;
            }

            if (_topics.TryGetValue(newTopic.Info.MessageThreadId, out topic))
            {
                topic.DraftMessage = newTopic.DraftMessage;
                topic.NotificationSettings = newTopic.NotificationSettings;
                topic.UnreadReactionCount = newTopic.UnreadReactionCount;
                topic.UnreadMentionCount = newTopic.UnreadMentionCount;
                topic.LastReadInboxMessageId = newTopic.LastReadInboxMessageId;
                topic.LastReadOutboxMessageId = newTopic.LastReadOutboxMessageId;
                topic.UnreadCount = newTopic.UnreadCount;
                topic.IsPinned = newTopic.IsPinned;
                topic.LastMessage = newTopic.LastMessage;
                topic.Info = newTopic.Info;

                UpdateReadInbox(topic, newTopic.LastReadInboxMessageId);
                UpdateLastMessage(topic, newTopic.LastMessage);
            }
            else
            {
                topic = new ForuminoTopicino(newTopic);
            }

            _topics[topic.Info.MessageThreadId] = topic;

            if (topic.LastMessage != null)
            {
                _messages[topic.LastMessage.Id] = topic;
            }

            UpdateTopicOrder(topic, true);
        }

        private long _lastProcessedMessageId;

        public void UpdateNewMessage(Message message)
        {
            // Important
            // Maybe update last message

            if (_lastProcessedMessageId == message.Id)
            {
                return;
            }

            _lastProcessedMessageId = message.Id;

            var messageThreadId = message.IsTopicMessage
                ? message.MessageThreadId
                : ForuminoTopicino.GeneralId;

            if (_topics.TryGetValue(messageThreadId, out ForuminoTopicino topic))
            {
                UpdateLastMessage(topic, message);
            }
            else
            {
                _clientService.Send(new GetForumTopic(_chatId, message.MessageThreadId), UpdateNewTopic);
            }

            //else if (!_pendingNewTopics.Contains(message.MessageThreadId))
            //{
            //    _pendingNewTopics.Add(message.MessageThreadId);
            //    _clientService.Send(new GetForumTopic(_chatId, message.MessageThreadId), result =>
            //    {
            //        _pendingNewTopics.Remove(message.MessageThreadId);
            //        UpdateNewTopic(result);
            //    });
            //}
        }

        private void UpdateLastMessage(ForuminoTopicino topic, Message message)
        {
            if (topic.LastMessage == null || topic.LastMessage?.Id < message.Id)
            {
                // Update last message
                // Deliver update UpdateForumTopicLastMessage;
                if (topic.LastMessage != null)
                {
                    _messages.Remove(topic.LastMessage.Id);
                }

                _messages[message.Id] = topic;

                topic.LastMessage = message;

                UpdateTopicOrder(topic, true);
                UpdateUnreadCount(topic);
            }
        }

        public void UpdateDeleteMessages(IList<long> messageIds, bool isPermanent, bool fromCache)
        {
            if (fromCache)
            {
                return;
            }

            // Important
            // Maybe update last message

            foreach (long messageId in messageIds)
            {
                if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
                {
                    if (topic.LastMessage?.Id == messageId)
                    {
                        // Update last message
                        // Deliver update UpdateForumTopicLastMessage;

                        _clientService.Send(new GetForumTopic(_chatId, topic.Info.MessageThreadId), response =>
                        {
                            if (topic.LastMessage != null)
                            {
                                _messages.Remove(topic.LastMessage.Id);
                            }

                            var updatePinnedTopics = false;
                            var updateCurrentTopic = false;

                            if (response is ForumTopic newTopic)
                            {
                                topic.LastMessage = newTopic.LastMessage ?? MessageForumTopicCreated(newTopic);
                            }
                            else if (response is Error { Code: 404 })
                            {
                                if (_pinnedTopicIds.Contains(topic.Info.MessageThreadId))
                                {
                                    _pinnedTopicIds.Remove(topic.Info.MessageThreadId);
                                    updatePinnedTopics = true;
                                }

                                topic.LastMessage = null;
                                topic.IsPinned = false;
                                topic.IsDeleted = true;
                            }

                            if (topic.LastMessage != null)
                            {
                                _messages[topic.LastMessage.Id] = topic;
                            }

                            UpdateTopicOrder(topic, true);

                            if (topic.LastMessage == null && !topic.IsDeleted)
                            {
                                _clientService.Send(new GetForumTopic(_chatId, topic.Info.MessageThreadId), UpdateNewTopic);
                            }

                            if (updatePinnedTopics)
                            {
                                UpdatePinnedTopics();
                            }
                        });

                        break;
                    }
                }
            }
        }

        private void UpdatePinnedTopics()
        {
            foreach (var topicId in _pinnedTopicIds)
            {
                if (_topics.TryGetValue(topicId, out var topic))
                {
                    UpdateTopicOrder(topic, true);
                }
            }
        }

        private Message MessageForumTopicCreated(ForumTopic topic)
        {
            return new Message(topic.Info.MessageThreadId, topic.Info.CreatorId, _chatId, null, null, topic.Info.IsOutgoing, false, false, false, false, false, true, false, topic.Info.CreationDate, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, topic.Info.MessageThreadId, 0, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, false, string.Empty, new MessageForumTopicCreated(topic.Info.Name, topic.Info.Icon), null);
        }

        public void UpdateMessageSendSucceeded(Message message, long oldMessageId)
        {
            // Important
            // Maybe update last message

            if (_messages.TryGetValue(oldMessageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == oldMessageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;

                    _messages.Remove(oldMessageId);
                    _messages[message.Id] = topic;

                    topic.LastMessage = message;

                    UpdateTopicOrder(topic, true);
                }
            }
        }

        public void UpdateMessageSendFailed(Message message, long oldMessageId, Error error)
        {
            // Important
            // Maybe update last message

            if (_messages.TryGetValue(oldMessageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == oldMessageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;
                }
            }
        }

        public void UpdateMessageContent(long messageId, MessageContent newContent)
        {
            // Important
            // Maybe update last message

            if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;

                    topic.LastMessage.Content = newContent;

                    _aggregator.Publish(new UpdateForumTopicLastMessage(_chatId, topic));
                }
            }
        }

        public void UpdateMessageEdited(long messageId, int editDate, ReplyMarkup replyMarkup)
        {
            // Maybe update last message

            if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;
                }
            }
        }

        public void UpdateMessageIsPinned(long messageId, bool isPinned)
        {
            // Maybe update last message

            if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;
                }
            }
        }

        public void UpdateMessageInteractionInfo(long messageId, MessageInteractionInfo interactionInfo)
        {
            // Maybe update last message

            if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;
                }
            }
        }

        public void UpdateMessageContentOpened(long messageId)
        {
            // Maybe update last message

            if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;
                }
            }
        }

        public void UpdateMessageMentionRead(long messageId, int unreadMentionCount)
        {
            // Important
            // Update UnreadMentionCount

            if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
            {
                // Update topic unreadMentionCount
                // Deliver update UpdateForumTopicMentionRead;
            }
        }

        public void UpdateMessageUnreadReactions(long messageId, IList<UnreadReaction> unreadReactions, int unreadReactionCount)
        {
            // Important
            // Update UnreadMentionReactions

            // Maybe update last message

            if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;
                }

                // Update topic unreadReactionCount
                // Deliver update UpdateForumTopicUnreadReactions;
            }
        }

        public void UpdateMessageFactCheck(long messageId, FactCheck factCheck)
        {
            // Maybe update last message

            if (_messages.TryGetValue(messageId, out ForuminoTopicino topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;
                }
            }
        }

        public void UpdateChatDraftMessage(DraftMessage draftMessage)
        {
            // Not supported
            // Update draft message
            // Deliver UpdateForumTopicDraftMessage, UpdateForumTopicPosition
        }

        public void UpdateChatNotificationSettings(ChatNotificationSettings notificationSettings)
        {
            // Not supported
        }

        public void UpdateChatLastMessage(Message message)
        {
            if (message != null)
            {
                UpdateNewMessage(message);
            }
        }

        public void UpdateChatReadInbox(long lastReadInboxMessageId, int unreadCount)
        {
            // Not supported
        }

        public void UpdateChatReadOutbox(long lastReadOutboxMessageId)
        {
            // Not supported
        }

        public void UpdateChatUnreadMentionCount(long unreadMentionCount)
        {
            // Not supported
        }

        public void UpdateChatUnreadReactionCount(long unreadReactionCount)
        {
            // Not supported
        }

        private readonly struct OrderedForumTopic : IComparable<OrderedForumTopic>
        {
            public readonly long TopicId;
            public readonly long Order;

            public OrderedForumTopic(long topicId, long order)
            {
                TopicId = topicId;
                Order = order;
            }

            public int CompareTo(OrderedForumTopic o)
            {
                if (Order != o.Order)
                {
                    return o.Order < Order ? -1 : 1;
                }

                if (TopicId != o.TopicId)
                {
                    return o.TopicId < TopicId ? -1 : 1;
                }

                return 0;
            }

            public override bool Equals(object obj)
            {
                OrderedForumTopic o = (OrderedForumTopic)obj;
                return TopicId == o.TopicId && Order == o.Order;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(TopicId, Order);
            }
        }
    }
}

namespace Telegram.Td.Api
{
    public sealed class UpdateForumTopicLastMessage
    {
        public UpdateForumTopicLastMessage(long chatId, long messageThreadId, long order, Message lastMessage)
        {
            ChatId = chatId;
            MessageThreadId = messageThreadId;
            Order = order;
            LastMessage = lastMessage;
        }

        public UpdateForumTopicLastMessage(long chatId, ForuminoTopicino topic)
        {
            ChatId = chatId;
            MessageThreadId = topic.Info.MessageThreadId;
            Order = topic.Order;
            LastMessage = topic.LastMessage;
        }

        public long ChatId { get; set; }

        public long MessageThreadId { get; set; }

        public long Order { get; set; }

        public Message LastMessage { get; set; }
    }

    public sealed class UpdateForumTopicPosition
    {
        public UpdateForumTopicPosition(long chatId, long messageThreadId, long order)
        {
            ChatId = chatId;
            MessageThreadId = messageThreadId;
            Order = order;
        }

        public long ChatId { get; set; }

        public long MessageThreadId { get; set; }

        public long Order { get; set; }
    }

    public sealed class UpdateForumTopicReadInbox
    {
        public UpdateForumTopicReadInbox(long chatId, long messageThreadId, long lastReadInboxMessageId, int unreadCount)
        {
            ChatId = chatId;
            MessageThreadId = messageThreadId;
            LastReadInboxMessageId = lastReadInboxMessageId;
        }

        public long ChatId { get; set; }

        public long MessageThreadId { get; set; }

        public long LastReadInboxMessageId { get; set; }

        public int UnreadCount { get; set; }
    }

    public sealed class UpdateForumTopicReadOutbox
    {
        public UpdateForumTopicReadOutbox(long chatId, long messageThreadId, long lastReadOutboxMessageId)
        {
            ChatId = chatId;
            MessageThreadId = messageThreadId;
            LastReadOutboxMessageId = lastReadOutboxMessageId;
        }

        public long ChatId { get; set; }

        public long MessageThreadId { get; set; }

        public long LastReadOutboxMessageId { get; set; }
    }

    public sealed class UpdateChatUnreadTopicCount
    {
        public UpdateChatUnreadTopicCount(long chatId, int unreadTopicCount)
        {
            ChatId = chatId;
            UnreadTopicCount = unreadTopicCount;
        }

        public long ChatId { get; set; }

        public int UnreadTopicCount { get; set; }
    }

    public sealed class ForuminoTopicinos
    {
        public ForuminoTopicinos(int totalCount, IList<long> topics)
        {
            TotalCount = totalCount;
            TopicIds = topics;
        }

        public int TotalCount { get; set; }

        public IList<long> TopicIds { get; set; }
    }

    public sealed class ForuminoTopicino
    {
        public static readonly long GeneralId = 1 << 20;

        public ForuminoTopicino(ForumTopic topic)
        {
            Id = topic.Info.MessageThreadId;
            Order = -1;

            DraftMessage = topic.DraftMessage;
            NotificationSettings = topic.NotificationSettings;
            UnreadReactionCount = topic.UnreadReactionCount;
            UnreadMentionCount = topic.UnreadMentionCount;
            LastReadInboxMessageId = topic.LastReadInboxMessageId;
            LastReadOutboxMessageId = topic.LastReadOutboxMessageId;
            UnreadCount = topic.UnreadCount;
            IsPinned = topic.IsPinned;
            LastMessage = topic.LastMessage;
            Info = topic.Info;
        }

        public long Id { get; set; }

        public long Order { get; set; }

        public bool IsDeleted { get; set; }

        /// <summary>
        /// A draft of a message in the topic; may be null if none.
        /// </summary>
        public DraftMessage DraftMessage { get; set; }

        /// <summary>
        /// Notification settings for the topic.
        /// </summary>
        public ChatNotificationSettings NotificationSettings { get; set; }

        /// <summary>
        /// Number of messages with unread reactions in the topic.
        /// </summary>
        public int UnreadReactionCount { get; set; }

        /// <summary>
        /// Number of unread messages with a mention/reply in the topic.
        /// </summary>
        public int UnreadMentionCount { get; set; }

        /// <summary>
        /// Identifier of the last read outgoing message.
        /// </summary>
        public long LastReadOutboxMessageId { get; set; }

        /// <summary>
        /// Identifier of the last read incoming message.
        /// </summary>
        public long LastReadInboxMessageId { get; set; }

        /// <summary>
        /// Number of unread messages in the topic.
        /// </summary>
        public int UnreadCount { get; set; }

        /// <summary>
        /// True, if the topic is pinned in the topic list.
        /// </summary>
        public bool IsPinned { get; set; }

        /// <summary>
        /// Last message in the topic; may be null if unknown.
        /// </summary>
        public Message LastMessage { get; set; }

        /// <summary>
        /// Basic information about the topic.
        /// </summary>
        public ForumTopicInfo Info { get; set; }
    }
}
