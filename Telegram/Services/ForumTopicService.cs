using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    internal class ForumTopicService
    {
        public static readonly long GeneralId = 1 << 20;
        public static readonly long PinnedMaxOrder = long.MaxValue - 1;

        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly long _chatId;

        private readonly Dictionary<long, ForumTopic> _topics = new();
        private readonly Dictionary<long, ForumTopic> _messages = new();

        private readonly SortedSet<OrderedItem> _order = new();
        private readonly List<long> _pinnedTopicIds = new();
        private readonly HashSet<long> _unreadTopicIds = new();

        private readonly HashSet<long> _deletedTopicIds = new();

        private readonly HashSet<long> _pendingNewTopics = new();
        private readonly HashSet<long> _pendingLastReadInboxMessageId = new();

        private bool _haveFullList;

        public ForumTopicService(IClientService clientService, IEventAggregator aggregator, long chatId)
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

        private void UpdateTopicOrder(ForumTopic topic, bool publish)
        {
            var order = Order(topic);

            Monitor.Enter(_order);

            _order.Remove(new OrderedItem(topic.Info.MessageThreadId, topic.Order));

            topic.Order = order;

            if (order != 0)
            {
                _order.Add(new OrderedItem(topic.Info.MessageThreadId, order));
            }

            Monitor.Exit(_order);

            if (publish)
            {
                _aggregator.Publish(new UpdateForumTopicLastMessage(_chatId, topic));
            }
        }

        public void ViewMessages(long messageThreadId, IList<long> messageIds)
        {
            if (_topics.TryGetValue(messageThreadId, out ForumTopic topic))
            {
                UpdateLastReadInboxMessageId(topic, messageIds.Max());
            }
        }

        public void SetPinnedForumTopics(IList<long> messageThreadIds)
        {
            if (messageThreadIds.Count > _clientService.Options.PinnedForumTopicCountMax)
            {
                return;
            }

            _clientService.Send(new SetPinnedForumTopics(_chatId, messageThreadIds));

            Monitor.Enter(_order);

            _pinnedTopicIds.Clear();
            _pinnedTopicIds.AddRange(messageThreadIds);

            UpdatePinnedTopics();

            Monitor.Exit(_order);
        }

        private void UpdateLastReadInboxMessageId(ForumTopic topic, long lastReadInboxMessageId)
        {
            _pendingLastReadInboxMessageId.Remove(lastReadInboxMessageId);

            if (lastReadInboxMessageId > topic.LastReadInboxMessageId)
            {
                topic.LastReadInboxMessageId = lastReadInboxMessageId;
                UpdateUnreadCount(topic);
            }
        }

        private void UpdateLastReadOutboxMessageId(ForumTopic topic, long lastReadOutboxMessageId)
        {
            if (topic.LastReadOutboxMessageId < lastReadOutboxMessageId)
            {
                topic.LastReadOutboxMessageId = lastReadOutboxMessageId;
                _aggregator.Publish(new UpdateForumTopicReadOutbox(_chatId, topic.Info.MessageThreadId, lastReadOutboxMessageId));
            }
        }

        private void UpdateUnreadCount(ForumTopic topic)
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

        private void UpdateUnreadTopicCount(ForumTopic topic, bool unread)
        {
            bool update;
            int count;
            lock (_unreadTopicIds)
            {
                update = unread
                    ? _unreadTopicIds.Add(topic.Info.MessageThreadId)
                    : _unreadTopicIds.Remove(topic.Info.MessageThreadId);

                count = _unreadTopicIds.Count;
            }

            if (update)
            {
                // This is done to update unread counts for folders
                if (count == 0 && _clientService.TryGetChat(_chatId, out Chat chat))
                {
                    _clientService.Send(new ViewMessages(_chatId, new[] { chat.LastMessage?.Id ?? 0 }, new MessageSourceChatHistory(), true));
                }

                _aggregator.Publish(new UpdateChatUnreadTopicCount(_chatId, UnreadCount));
                _aggregator.Publish(new UpdateForumTopicReadInbox(_chatId, topic.Info.MessageThreadId, topic.LastReadInboxMessageId, topic.UnreadCount));
            }
        }

        public ForumTopic GetTopic(long id)
        {
            if (_topics.TryGetValue(id, out ForumTopic value))
            {
                return value;
            }

            return null;
        }

        public IEnumerable<ForumTopic> GetTopics(IEnumerable<long> ids)
        {
            foreach (var id in ids)
            {
                if (id == long.MaxValue)
                {
                    // TODO: translate
                    yield return new ForumTopic(new ForumTopicInfo(_chatId, 0, 0, Strings.AllTopicsShort, new ForumTopicIcon(), 0, null, false, false, false, false), null, long.MaxValue, false, 0, 0, 0, 0, 0, new ChatNotificationSettings(), null);
                }

                var topic = GetTopic(id);
                if (topic != null)
                {
                    yield return topic;
                }
            }
        }

        public Task<Topics> GetForumTopicsAsync(int offset, int limit)
        {
            return GetForumTopicsAsyncImpl(offset, limit, false);
        }

        public async Task<Topics> GetForumTopicsAsyncImpl(int offset, int limit, bool reentrancy)
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
                        return new Topics(0, Array.Empty<long>());
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
                        result[pos++] = iter.Current.Id;
                    }
                }
            }

            haveFullList &= count >= sorted.Count;

            Monitor.Exit(_order);
            return new Topics(haveFullList ? -1 : 0, result);
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

                    var topics = new List<ForumTopic>(forumTopics.Topics.Count);

                    foreach (var topic in forumTopics.Topics)
                    {
                        _topics[topic.Info.MessageThreadId] = topic;

                        if (topic.LastMessage != null)
                        {
                            _messages[topic.LastMessage.Id] = topic;
                        }

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

        private long Order(ForumTopic topic)
        {
            if (_deletedTopicIds.Contains(topic.Info.MessageThreadId))
            {
                return 0;
            }

            var index = _pinnedTopicIds.IndexOf(topic.Info.MessageThreadId);
            if (index != -1)
            {
                return PinnedMaxOrder - index;
            }
            else if (topic.LastMessage != null)
            {
                return topic.LastMessage.Id;
            }

            return topic.Info.MessageThreadId;
        }

        public void UpdateForumTopic(UpdateForumTopic update)
        {
            if (_topics.TryGetValue(update.MessageThreadId, out ForumTopic topic))
            {
                UpdateLastReadInboxMessageId(topic, update.LastReadInboxMessageId);
                UpdateLastReadOutboxMessageId(topic, update.LastReadOutboxMessageId);

                if (topic.UnreadMentionCount != update.UnreadMentionCount)
                {
                    _aggregator.Publish(new UpdateForumTopicUnreadMentionCount(_chatId, update.MessageThreadId, topic.UnreadMentionCount = update.UnreadMentionCount));
                }

                if (topic.UnreadReactionCount != update.UnreadReactionCount)
                {
                    _aggregator.Publish(new UpdateForumTopicUnreadReactionCount(_chatId, update.MessageThreadId, topic.UnreadReactionCount = update.UnreadReactionCount));
                }

                if (topic.IsPinned != update.IsPinned)
                {
                    topic.IsPinned = update.IsPinned;

                    if (topic.IsPinned)
                    {
                        _pinnedTopicIds.Insert(0, update.MessageThreadId);
                    }
                    else
                    {
                        _pinnedTopicIds.Remove(update.MessageThreadId);
                        UpdateTopicOrder(topic, true);
                    }

                    UpdatePinnedTopics();
                }
            }
        }

        public void UpdateForumTopicInfo(ForumTopicInfo info)
        {
            if (_topics.TryGetValue(info.MessageThreadId, out ForumTopic topic))
            {
                topic.Info = info;
            }
        }

        private void UpdateNewTopic(BaseObject response)
        {
            ForumTopic topic;
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
                topic.UnreadCount = newTopic.UnreadCount;
                topic.IsPinned = newTopic.IsPinned;
                topic.LastMessage = newTopic.LastMessage;
                topic.Info = newTopic.Info;

                UpdateLastReadInboxMessageId(topic, newTopic.LastReadInboxMessageId);
                UpdateLastReadOutboxMessageId(topic, newTopic.LastReadOutboxMessageId);
                UpdateLastMessage(topic, newTopic.LastMessage);
            }
            else
            {
                topic = newTopic;
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

            if (_topics.TryGetValue(message.TopicId(), out ForumTopic topic))
            {
                UpdateLastMessage(topic, message);
            }
            else
            {
                _clientService.Send(new GetForumTopic(_chatId, message.TopicId()), UpdateNewTopic);
            }

            if (message.SendingState is MessageSendingStatePending)
            {
                _pendingLastReadInboxMessageId.Add(message.Id);
            }
        }

        private void UpdateLastMessage(ForumTopic topic, Message message)
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
                if (_messages.TryGetValue(messageId, out ForumTopic topic))
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
                                _deletedTopicIds.Add(topic.Info.MessageThreadId);

                                if (_pinnedTopicIds.Contains(topic.Info.MessageThreadId))
                                {
                                    _pinnedTopicIds.Remove(topic.Info.MessageThreadId);
                                    updatePinnedTopics = true;
                                }

                                topic.LastMessage = null;
                                topic.IsPinned = false;
                            }

                            if (topic.LastMessage != null)
                            {
                                _messages[topic.LastMessage.Id] = topic;
                            }

                            UpdateTopicOrder(topic, true);

                            if (topic.LastMessage == null && topic.Order != 0)
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
            return new Message(topic.Info.MessageThreadId, topic.Info.CreatorId, _chatId, null, null, topic.Info.IsOutgoing, false, false, false, false, false, false, topic.Info.CreationDate, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, topic.Info.MessageThreadId, new MessageTopicForum(topic.Info.MessageThreadId), null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, false, string.Empty, new MessageForumTopicCreated(topic.Info.Name, topic.Info.Icon), null);
        }

        public void UpdateMessageSendSucceeded(Message message, long oldMessageId)
        {
            // Important
            // Maybe update last message

            if (_messages.TryGetValue(oldMessageId, out ForumTopic topic))
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

            if (_pendingLastReadInboxMessageId.Contains(oldMessageId))
            {
                _pendingLastReadInboxMessageId.Remove(oldMessageId);

                // There is a bug on backend that causes two distinct issues with topics read state:
                // When a message is sent, the backend may accidentally consider it as an incoming message for the current user.
                // When this happens, updateReadChannelDiscussionInbox is not received, and unread counter for the topic is increased by one.
                // On the other end, invoking messages.readDiscussion with the sent message as read_max_id,
                // may cause the same issue to occur with the opposite effect, causing updateReadChannelDiscussionOutbox to never be delivered.
                // _pendingLastReadInboxMessageId tries to workaround this issue by keeping track of currently sent messages and by invoking
                // messages.readDiscussion only when updateReadChannelDiscussionInbox is not received in messages.sendMessage response.
                // At the same time, ChatView.Bubbles.cs makes sure not to include outgoing messages when calling ViewMessages from a topic.
                _clientService.ViewMessages(_chatId, message.TopicId(), new[] { message.Id }, new MessageSourceForumTopicHistory(), false);
            }
        }

        public void UpdateMessageSendFailed(Message message, long oldMessageId, Error error)
        {
            // Important
            // Maybe update last message

            if (_messages.TryGetValue(oldMessageId, out ForumTopic topic))
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

            if (_messages.TryGetValue(messageId, out ForumTopic topic))
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

            if (_messages.TryGetValue(messageId, out ForumTopic topic))
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

            if (_messages.TryGetValue(messageId, out ForumTopic topic))
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

            if (_messages.TryGetValue(messageId, out ForumTopic topic))
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

            if (_messages.TryGetValue(messageId, out ForumTopic topic))
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

            if (_messages.TryGetValue(messageId, out ForumTopic topic))
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

            if (_messages.TryGetValue(messageId, out ForumTopic topic))
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

            if (_messages.TryGetValue(messageId, out ForumTopic topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;
                }
            }
        }

        public void UpdateChatLastMessage(Message message)
        {
            if (message != null)
            {
                UpdateNewMessage(message);
            }
        }
    }
}

namespace Telegram.Td.Api
{
    public sealed partial class UpdateForumTopicLastMessage
    {
        public UpdateForumTopicLastMessage(long chatId, long messageThreadId, long order, Message lastMessage)
        {
            ChatId = chatId;
            MessageThreadId = messageThreadId;
            Order = order;
            LastMessage = lastMessage;
        }

        public UpdateForumTopicLastMessage(long chatId, ForumTopic topic)
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

    public sealed partial class UpdateForumTopicPosition
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

    public sealed partial class UpdateForumTopicReadInbox
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

    public sealed partial class UpdateForumTopicReadOutbox
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

    public sealed partial class UpdateForumTopicUnreadReactionCount
    {
        public UpdateForumTopicUnreadReactionCount(long chatId, long messageThreadId, long unreadReactionCount)
        {
            ChatId = chatId;
            MessageThreadId = messageThreadId;
            UnreadReactionCount = unreadReactionCount;
        }

        public long ChatId { get; set; }

        public long MessageThreadId { get; set; }

        public long UnreadReactionCount { get; set; }
    }

    public sealed partial class UpdateForumTopicUnreadMentionCount
    {
        public UpdateForumTopicUnreadMentionCount(long chatId, long messageThreadId, long unreadMentionCount)
        {
            ChatId = chatId;
            MessageThreadId = messageThreadId;
            UnreadMentionCount = unreadMentionCount;
        }

        public long ChatId { get; set; }

        public long MessageThreadId { get; set; }

        public long UnreadMentionCount { get; set; }
    }

    public sealed partial class UpdateChatUnreadTopicCount
    {
        public UpdateChatUnreadTopicCount(long chatId, int unreadTopicCount)
        {
            ChatId = chatId;
            UnreadTopicCount = unreadTopicCount;
        }

        public long ChatId { get; set; }

        public int UnreadTopicCount { get; set; }
    }
}
