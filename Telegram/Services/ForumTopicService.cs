//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    /// <summary>
    /// Every topic of one forum, in order.
    /// </summary>
    public partial class ForumTopicService : OrderedSourceService<ForumTopic>
    {
        public static readonly long GeneralId = 1 << 20;
        public static readonly long PinnedMaxOrder = long.MaxValue - 1;

        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly long _chatId;

        // Every collection below is written from the TDLib thread, through the Update*
        // methods, and read from the UI thread through GetTopic/GetTopics/UnreadCount —
        // which also write, since GetTopic records a pending request. They share SyncRoot
        // with the order the base keeps, so there is no ordering to get wrong between them:
        // an order is decided from state the same lock guards.
        //
        // It guards the containers, not the ForumTopic objects inside: those are handed out
        // to the UI and mutated by the update methods, exactly as ClientService does with
        // Chat and User. Publishing happens outside the lock.
        private readonly Dictionary<int, ForumTopic> _topics = new();
        private readonly Dictionary<long, ForumTopic> _messages = new();

        private readonly List<int> _pinnedTopicIds = new();
        private readonly HashSet<int> _unreadTopicIds = new();

        private readonly HashSet<int> _deletedTopicIds = new();

        private readonly HashSet<int> _pendingNewTopics = new();
        private readonly HashSet<long> _pendingLastReadInboxMessageId = new();

        private bool TryGetTopic(int forumTopicId, out ForumTopic topic)
        {
            lock (SyncRoot)
            {
                return _topics.TryGetValue(forumTopicId, out topic);
            }
        }

        private bool TryGetTopicByMessage(long messageId, out ForumTopic topic)
        {
            lock (SyncRoot)
            {
                return _messages.TryGetValue(messageId, out topic);
            }
        }

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
                lock (SyncRoot)
                {
                    return _unreadTopicIds.Count;
                }
            }
        }

        /// <summary>
        /// Topics held, whatever any view has paged in.
        /// </summary>
        public int TopicCount
        {
            get
            {
                lock (SyncRoot)
                {
                    return _topics.Count;
                }
            }
        }

        private void UpdateTopicOrder(ForumTopic topic, bool publish)
        {
            long order;

            lock (SyncRoot)
            {
                // Inside the lock: Order reads _deletedTopicIds and _pinnedTopicIds.
                order = Order(topic);

                topic.Order = order;
                SetOrder(topic.Info.ForumTopicId, order);
            }

            // A page reorders every topic it brought in, and the list paging it in is about
            // to place them itself: reporting those would be work for an arrangement nobody
            // has seen yet.
            if (publish)
            {
                RaiseChanged(topic, order, true);
            }
        }

        public void ViewMessages(int forumTopicId, Vector<long> messageIds)
        {
            if (TryGetTopic(forumTopicId, out ForumTopic topic))
            {
                UpdateLastReadInboxMessageId(topic, messageIds.Max());
            }
        }

        public void SetPinnedForumTopics(Vector<int> forumTopicIds)
        {
            if (forumTopicIds.Count > _clientService.Options.PinnedForumTopicCountMax)
            {
                return;
            }

            _clientService.Send(new SetPinnedForumTopics(_chatId, forumTopicIds));

            lock (SyncRoot)
            {
                _pinnedTopicIds.Clear();
                _pinnedTopicIds.AddRange(forumTopicIds);
            }

            UpdatePinnedTopics();
        }

        private void UpdateLastReadInboxMessageId(ForumTopic topic, long lastReadInboxMessageId)
        {
            lock (SyncRoot)
            {
                _pendingLastReadInboxMessageId.Remove(lastReadInboxMessageId);
            }

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
                _aggregator.Publish(new UpdateForumTopicReadOutbox(_chatId, topic.Info.ForumTopicId, lastReadOutboxMessageId));
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
            lock (SyncRoot)
            {
                update = unread
                    ? _unreadTopicIds.Add(topic.Info.ForumTopicId)
                    : _unreadTopicIds.Remove(topic.Info.ForumTopicId);

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
                _aggregator.Publish(new UpdateForumTopicReadInbox(_chatId, topic.Info.ForumTopicId, topic.LastReadInboxMessageId, topic.UnreadCount));
            }
        }

        public ForumTopic GetTopic(int id)
        {
            // Called from the UI thread, and it writes: recording the pending request is
            // what keeps a miss from sending one getForumTopic per enumeration.
            bool request;

            lock (SyncRoot)
            {
                if (_topics.TryGetValue(id, out ForumTopic value))
                {
                    return value;
                }

                request = _pendingNewTopics.Add(id);
            }

            if (request)
            {
                _clientService.Send(new GetForumTopic(_chatId, id), response => UpdateNewTopic(id, response));
            }

            return null;
        }

        public IEnumerable<ForumTopic> GetTopics(IEnumerable<int> ids)
        {
            foreach (var id in ids)
            {
                if (id == int.MaxValue)
                {
                    if (_clientService.TryGetChat(_chatId, out Chat chat) && chat.Type is ChatTypePrivate)
                    {
                        yield return new ForumTopic(new ForumTopicInfo(_chatId, 0, Strings.BotForumNewTopic, new ForumTopicIcon(), 0, null, false, false, false, false, false), null, long.MaxValue, false, 0, 0, 0, 0, 0, 0, new ChatNotificationSettings(), null);
                    }
                    else
                    {
                        yield return new ForumTopic(new ForumTopicInfo(_chatId, 0, Strings.AllTopicsShort, new ForumTopicIcon(), 0, null, false, false, false, false, false), null, long.MaxValue, false, 0, 0, 0, 0, 0, 0, new ChatNotificationSettings(), null);
                    }

                    // This id is the synthetic row above, not a topic. Falling through asked
                    // the server for topic 2147483647, and the miss then sat in
                    // _pendingNewTopics for the life of the service.
                    continue;
                }

                var topic = GetTopic(id);
                if (topic != null)
                {
                    yield return topic;
                }
            }
        }

        public async Task<ForumTopics2> GetForumTopicsAsync(int offset, int limit)
        {
            var page = await GetItemsAsync(offset, limit);

            // Topic ids are ints, and the base pages in the ids every other list uses.
            var result = new int[page.Ids.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (int)page.Ids[i];
            }

            return new ForumTopics2(page.HaveFullList ? -1 : 0, result);
        }

        private int _nextOffsetDate;
        private long _nextOffsetMessageId;
        private int _nextOffsetForumTopicId;

        protected override Task<Object> LoadMoreItemsAsync(int count)
        {
            var tsc = new TaskCompletionSource<Object>();
            var request = new GetForumTopics(_chatId, string.Empty, _nextOffsetDate, _nextOffsetMessageId, _nextOffsetForumTopicId, count);

            _clientService.Send(request, response =>
            {
                Object result;

                lock (SyncRoot)
                {
                    if (response is ForumTopics forumTopics)
                    {
                        _nextOffsetDate = forumTopics.NextOffsetDate;
                        _nextOffsetMessageId = forumTopics.NextOffsetMessageId;
                        _nextOffsetForumTopicId = forumTopics.NextOffsetForumTopicId;

                        var topics = new List<ForumTopic>(forumTopics.Topics.Count);

                        foreach (var topic in forumTopics.Topics)
                        {
                            _topics[topic.Info.ForumTopicId] = topic;

                            if (topic.LastMessage != null)
                            {
                                _messages[topic.LastMessage.Id] = topic;
                            }

                            if (topic.IsPinned)
                            {
                                _pinnedTopicIds.Add(topic.Info.ForumTopicId);
                            }

                            if (topic.UnreadCount > 0)
                            {
                                _unreadTopicIds.Add(topic.Info.ForumTopicId);
                            }

                            topics.Add(topic);
                        }

                        foreach (var topic in topics)
                        {
                            UpdateTopicOrder(topic, false);
                        }

                        _aggregator.Publish(new UpdateChatUnreadTopicCount(_chatId, UnreadCount));

                        result = forumTopics.Topics.Count > 0 && ItemCount < forumTopics.TotalCount + 1
                            ? new Ok()
                            : new Error(404, string.Empty);
                    }
                    else
                    {
                        result = new Error(500, string.Empty);
                    }
                }

                // Completed outside the lock on purpose: the continuation waiting on this is
                // the base's pager, which takes SyncRoot itself, and SetResult runs it inline.
                // Recursion made that safe rather than deadlocked, but a throw in there would
                // have skipped the Exit and wedged the topic list for good.
                tsc.SetResult(result);
            });

            return tsc.Task;
        }

        // Caller must hold SyncRoot: reads _deletedTopicIds and _pinnedTopicIds.
        private long Order(ForumTopic topic)
        {
            if (_deletedTopicIds.Contains(topic.Info.ForumTopicId))
            {
                return 0;
            }

            // TODO: DraftMessage

            var index = _pinnedTopicIds.IndexOf(topic.Info.ForumTopicId);
            if (index != -1)
            {
                return PinnedMaxOrder - index;
            }
            else if (topic.LastMessage != null)
            {
                return topic.LastMessage.Id;
            }

            return topic.Info.ForumTopicId;
        }

        public void UpdateForumTopic(UpdateForumTopic update)
        {
            if (TryGetTopic(update.ForumTopicId, out ForumTopic topic))
            {
                if (!topic.NotificationSettings.AreTheSame(update.NotificationSettings))
                {
                    _aggregator.Publish(new UpdateForumTopicNotificationSettings(_chatId, topic.Info.ForumTopicId, topic.NotificationSettings = update.NotificationSettings));
                }

                UpdateLastReadInboxMessageId(topic, update.LastReadInboxMessageId);
                UpdateLastReadOutboxMessageId(topic, update.LastReadOutboxMessageId);

                if (topic.UnreadMentionCount != update.UnreadMentionCount)
                {
                    _aggregator.Publish(new UpdateForumTopicUnreadMentionCount(_chatId, update.ForumTopicId, topic.UnreadMentionCount = update.UnreadMentionCount));
                }

                if (topic.UnreadReactionCount != update.UnreadReactionCount)
                {
                    _aggregator.Publish(new UpdateForumTopicUnreadReactionCount(_chatId, update.ForumTopicId, topic.UnreadReactionCount = update.UnreadReactionCount));
                }

                if (topic.DraftMessage?.Date != update.DraftMessage?.Date)
                {
                    _aggregator.Publish(new UpdateForumTopicDraftMessage(_chatId, update.ForumTopicId, topic.DraftMessage = update.DraftMessage));
                }

                if (topic.IsPinned != update.IsPinned)
                {
                    topic.IsPinned = update.IsPinned;

                    if (topic.IsPinned)
                    {
                        lock (SyncRoot)
                        {
                            _pinnedTopicIds.Insert(0, update.ForumTopicId);
                        }
                    }
                    else
                    {
                        lock (SyncRoot)
                        {
                            _pinnedTopicIds.Remove(update.ForumTopicId);
                        }

                        UpdateTopicOrder(topic, true);
                    }

                    UpdatePinnedTopics();
                }
            }
        }

        public void UpdateForumTopicInfo(ForumTopicInfo info)
        {
            if (TryGetTopic(info.ForumTopicId, out ForumTopic topic))
            {
                topic.Info = info;
            }
            else if (_clientService.TryGetChat(_chatId, out Chat chat))
            {
                // Preload empty topic to have info readily available
                var preloaded = new ForumTopic
                {
                    DraftMessage = null,
                    NotificationSettings = chat.NotificationSettings,
                    UnreadReactionCount = 0,
                    UnreadMentionCount = 0,
                    LastReadOutboxMessageId = 0,
                    LastReadInboxMessageId = 0,
                    UnreadCount = 0,
                    IsPinned = false,
                    Order = 0,
                    LastMessage = null,
                    Info = info
                };

                lock (SyncRoot)
                {
                    _topics[info.ForumTopicId] = preloaded;
                }
            }
        }

        /// <param name="forumTopicId">
        /// The topic that was asked for. A failure carries no id of its own, and it is the
        /// failure case that has to clear the pending entry.
        /// </param>
        private void UpdateNewTopic(int forumTopicId, Object response)
        {
            ForumTopic topic;
            ForumTopic newTopic = response as ForumTopic;

            if (newTopic == null)
            {
                // Only a server or transport failure is retried. Leaving the pending entry
                // set is what made one failed load hide a topic for the rest of the session,
                // but clearing it for every failure is the worse bug: a topic that genuinely
                // does not exist would then be asked for again on every enumeration. A 4xx
                // says the request itself is wrong or the topic is gone, and repeating it
                // cannot change that — TDLib reports a missing topic as 400 at least as often
                // as 404, so keying on 404 alone would leave that storm open.
                if (response is Error { Code: >= 500 or < 0 })
                {
                    lock (SyncRoot)
                    {
                        _pendingNewTopics.Remove(forumTopicId);
                    }
                }

                return;
            }

            lock (SyncRoot)
            {
                _pendingNewTopics.Remove(newTopic.Info.ForumTopicId);
            }

            if (TryGetTopic(newTopic.Info.ForumTopicId, out topic))
            {
                topic.DraftMessage = newTopic.DraftMessage;
                topic.NotificationSettings = newTopic.NotificationSettings;
                topic.UnreadReactionCount = newTopic.UnreadReactionCount;
                topic.UnreadMentionCount = newTopic.UnreadMentionCount;
                topic.UnreadCount = newTopic.UnreadCount;
                topic.IsPinned = newTopic.IsPinned;
                topic.Info = newTopic.Info;

                UpdateLastReadInboxMessageId(topic, newTopic.LastReadInboxMessageId);
                UpdateLastReadOutboxMessageId(topic, newTopic.LastReadOutboxMessageId);

                // TODO: Not sure this is right
                if (newTopic.LastMessage != null)
                {
                    UpdateLastMessage(topic, newTopic.LastMessage);
                }
            }
            else
            {
                topic = newTopic;
            }

            lock (SyncRoot)
            {
                _topics[topic.Info.ForumTopicId] = topic;

                if (topic.LastMessage != null)
                {
                    _messages[topic.LastMessage.Id] = topic;
                }
            }

            UpdateTopicOrder(topic, true);
        }

        private long _lastProcessedMessageId;

        public void UpdateNewMessage(Message message)
        {
            // Important
            // Maybe update last message

            if (_lastProcessedMessageId == message.Id || message.TopicId is not MessageTopicForum topicForum)
            {
                return;
            }

            _lastProcessedMessageId = message.Id;

            if (TryGetTopic(topicForum.ForumTopicId, out ForumTopic topic))
            {
                UpdateLastMessage(topic, message);
            }
            else
            {
                _clientService.Send(new GetForumTopic(_chatId, topicForum.ForumTopicId), response => UpdateNewTopic(topicForum.ForumTopicId, response));
            }

            if (message.SendingState is MessageSendingStatePending)
            {
                lock (SyncRoot)
                {
                    _pendingLastReadInboxMessageId.Add(message.Id);
                }
            }
        }

        private void UpdateLastMessage(ForumTopic topic, Message message)
        {
            if (topic.LastMessage == null || topic.LastMessage?.Id < message.Id)
            {
                // Update last message
                // Deliver update UpdateForumTopicLastMessage;
                lock (SyncRoot)
                {
                    if (topic.LastMessage != null)
                    {
                        _messages.Remove(topic.LastMessage.Id);
                    }

                    if (message != null)
                    {
                        _messages[message.Id] = topic;
                    }
                }

                topic.LastMessage = message;

                UpdateTopicOrder(topic, true);
                UpdateUnreadCount(topic);
            }
        }

        public void UpdateDeleteMessages(Vector<long> messageIds, bool isPermanent, bool fromCache)
        {
            if (fromCache)
            {
                return;
            }

            // One delete can span several topics — clearing a chat's history, or deleting
            // everything one member ever sent — and each of them needs its own last message
            // back. Refreshing only the first left the rest showing a preview of a message
            // that no longer exists, sorted by it too.
            //
            // Each _messages entry is handled at most once, because the entry is removed as
            // it is handled and a later id in the batch resolves to a different one. Note
            // that is per entry, not per topic: LoadForumTopicsAsync can leave a stale entry
            // behind for a topic it reloads, which costs a redundant refresh here, never a
            // wrong one.
            foreach (long messageId in messageIds)
            {
                if (TryGetTopicByMessage(messageId, out ForumTopic topic))
                {
                    if (topic.LastMessage?.Id == messageId)
                    {
                        if (topic.LastMessage != null)
                        {
                            lock (SyncRoot)
                            {
                                _messages.Remove(topic.LastMessage.Id);
                            }
                        }

                        // Update last message
                        // Deliver update UpdateForumTopicLastMessage;

                        _clientService.Send(new GetForumTopic(_chatId, topic.Info.ForumTopicId), response =>
                        {
                            var updatePinnedTopics = false;

                            if (response is ForumTopic newTopic)
                            {
                                topic.LastMessage = newTopic.LastMessage ?? MessageForumTopicCreated(newTopic);
                            }
                            else if (response is Error { Code: 404 })
                            {
                                lock (SyncRoot)
                                {
                                    _deletedTopicIds.Add(topic.Info.ForumTopicId);

                                    updatePinnedTopics = _pinnedTopicIds.Remove(topic.Info.ForumTopicId);
                                }

                                topic.LastMessage = null;
                                topic.IsPinned = false;
                            }

                            if (topic.LastMessage != null)
                            {
                                lock (SyncRoot)
                                {
                                    _messages[topic.LastMessage.Id] = topic;
                                }
                            }

                            UpdateTopicOrder(topic, true);

                            if (topic.LastMessage == null && topic.Order != 0)
                            {
                                _clientService.Send(new GetForumTopic(_chatId, topic.Info.ForumTopicId), inner => UpdateNewTopic(topic.Info.ForumTopicId, inner));
                            }

                            if (updatePinnedTopics)
                            {
                                UpdatePinnedTopics();
                            }
                        });
                    }
                }
            }
        }

        private void UpdatePinnedTopics()
        {
            // Collected under the lock, reordered outside it: UpdateTopicOrder publishes.
            List<ForumTopic> pinned = null;

            lock (SyncRoot)
            {
                foreach (var topicId in _pinnedTopicIds)
                {
                    if (_topics.TryGetValue(topicId, out var topic))
                    {
                        pinned ??= new List<ForumTopic>(_pinnedTopicIds.Count);
                        pinned.Add(topic);
                    }
                }
            }

            if (pinned != null)
            {
                foreach (var topic in pinned)
                {
                    UpdateTopicOrder(topic, true);
                }
            }
        }

        private Message MessageForumTopicCreated(ForumTopic topic)
        {
            return new Message(topic.Info.ForumTopicId, topic.Info.CreatorId, null, _chatId, null, null, topic.Info.IsOutgoing, false, false, false, false, false, false, false, false, false, topic.Info.CreationDate, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, null, new MessageTopicForum(topic.Info.ForumTopicId), null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, new MessageForumTopicCreated(topic.Info.Name, false, topic.Info.Icon), null, null);
        }

        public void UpdateMessageSendSucceeded(Message message, long oldMessageId)
        {
            // Important
            // Maybe update last message

            if (TryGetTopicByMessage(oldMessageId, out ForumTopic topic))
            {
                if (topic.LastMessage?.Id == oldMessageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;

                    lock (SyncRoot)
                    {
                        _messages.Remove(oldMessageId);
                        _messages[message.Id] = topic;
                    }

                    topic.LastMessage = message;

                    UpdateTopicOrder(topic, true);
                }
            }

            if (message.TopicId is not MessageTopicForum topicForum)
            {
                return;
            }

            bool pending;

            lock (SyncRoot)
            {
                pending = _pendingLastReadInboxMessageId.Remove(oldMessageId);
            }

            if (pending)
            {
                // There is a bug on backend that causes two distinct issues with topics read state:
                // When a message is sent, the backend may accidentally consider it as an incoming message for the current user.
                // When this happens, updateReadChannelDiscussionInbox is not received, and unread counter for the topic is increased by one.
                // On the other end, invoking messages.readDiscussion with the sent message as read_max_id,
                // may cause the same issue to occur with the opposite effect, causing updateReadChannelDiscussionOutbox to never be delivered.
                // _pendingLastReadInboxMessageId tries to workaround this issue by keeping track of currently sent messages and by invoking
                // messages.readDiscussion only when updateReadChannelDiscussionInbox is not received in messages.sendMessage response.
                // At the same time, ChatView.Bubbles.cs makes sure not to include outgoing messages when calling ViewMessages from a topic.
                _clientService.ViewMessages(_chatId, topicForum, new[] { message.Id }, new MessageSourceForumTopicHistory(), false);
            }
        }

        public void UpdateMessageSendFailed(Message message, long oldMessageId, Error error)
        {
            // Important
            // Maybe update last message

            if (TryGetTopicByMessage(oldMessageId, out ForumTopic topic))
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

            if (TryGetTopicByMessage(messageId, out ForumTopic topic))
            {
                if (topic.LastMessage?.Id == messageId)
                {
                    // Update last message
                    // Deliver update UpdateForumTopicLastMessage;

                    topic.LastMessage.Content = newContent;

                    // The row has something new to show without having moved.
                    RaiseChanged(topic, topic.Order, true);
                }
            }
        }

        public void UpdateMessageEdited(long messageId, int editDate, ReplyMarkup replyMarkup)
        {
            // Maybe update last message

            if (TryGetTopicByMessage(messageId, out ForumTopic topic))
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

            if (TryGetTopicByMessage(messageId, out ForumTopic topic))
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

            if (TryGetTopicByMessage(messageId, out ForumTopic topic))
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

            if (TryGetTopicByMessage(messageId, out ForumTopic topic))
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

            if (TryGetTopicByMessage(messageId, out ForumTopic topic))
            {
                // Update topic unreadMentionCount
                // Deliver update UpdateForumTopicMentionRead;
            }
        }

        public void UpdateMessageUnreadReactions(long messageId, Vector<UnreadReaction> unreadReactions, int unreadReactionCount)
        {
            // Important
            // Update UnreadMentionReactions

            // Maybe update last message

            if (TryGetTopicByMessage(messageId, out ForumTopic topic))
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

            if (TryGetTopicByMessage(messageId, out ForumTopic topic))
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
    public sealed partial class UpdateForumTopicReadInbox
    {
        public UpdateForumTopicReadInbox(long chatId, int forumTopicId, long lastReadInboxMessageId, int unreadCount)
        {
            ChatId = chatId;
            ForumTopicId = forumTopicId;
            LastReadInboxMessageId = lastReadInboxMessageId;
        }

        public long ChatId { get; set; }

        public int ForumTopicId { get; set; }

        public long LastReadInboxMessageId { get; set; }

        public int UnreadCount { get; set; }
    }

    public sealed partial class UpdateForumTopicReadOutbox
    {
        public UpdateForumTopicReadOutbox(long chatId, int forumTopicId, long lastReadOutboxMessageId)
        {
            ChatId = chatId;
            ForumTopicId = forumTopicId;
            LastReadOutboxMessageId = lastReadOutboxMessageId;
        }

        public long ChatId { get; set; }

        public int ForumTopicId { get; set; }

        public long LastReadOutboxMessageId { get; set; }
    }

    public sealed partial class UpdateForumTopicNotificationSettings
    {
        public UpdateForumTopicNotificationSettings(long chatId, int forumTopicId, ChatNotificationSettings notificationSettings)
        {
            ChatId = chatId;
            ForumTopicId = forumTopicId;
            NotificationSettings = notificationSettings;
        }

        public long ChatId { get; set; }

        public int ForumTopicId { get; set; }

        public ChatNotificationSettings NotificationSettings { get; set; }
    }

    public sealed partial class UpdateForumTopicUnreadReactionCount
    {
        public UpdateForumTopicUnreadReactionCount(long chatId, int forumTopicId, long unreadReactionCount)
        {
            ChatId = chatId;
            ForumTopicId = forumTopicId;
            UnreadReactionCount = unreadReactionCount;
        }

        public long ChatId { get; set; }

        public int ForumTopicId { get; set; }

        public long UnreadReactionCount { get; set; }
    }

    public sealed partial class UpdateForumTopicUnreadMentionCount
    {
        public UpdateForumTopicUnreadMentionCount(long chatId, int forumTopicId, long unreadMentionCount)
        {
            ChatId = chatId;
            ForumTopicId = forumTopicId;
            UnreadMentionCount = unreadMentionCount;
        }

        public long ChatId { get; set; }

        public int ForumTopicId { get; set; }

        public long UnreadMentionCount { get; set; }
    }

    public sealed partial class UpdateForumTopicDraftMessage
    {
        public UpdateForumTopicDraftMessage(long chatId, int forumTopicId, DraftMessage draftMessage)
        {
            ChatId = chatId;
            ForumTopicId = forumTopicId;
            DraftMessage = draftMessage;
        }

        public long ChatId { get; set; }

        public int ForumTopicId { get; set; }

        public DraftMessage DraftMessage { get; set; }
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
