//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Services
{
    /// <summary>
    /// Every topic of one forum, in order.
    /// </summary>
    public partial class ForumTopicService : OrderedSourceService<ForumTopic>
    {
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
            if (!TryGetTopic(forumTopicId, out ForumTopic topic))
            {
                return;
            }

            // One pass over the batch: the read position is the newest of them, and the count
            // drops by the ones that had not been read. The caller leaves outgoing messages out,
            // so every one of them counts.
            long lastReadInboxMessageId = 0;
            var read = 0;

            foreach (var messageId in messageIds)
            {
                if (messageId > lastReadInboxMessageId)
                {
                    lastReadInboxMessageId = messageId;
                }

                if (messageId > topic.LastReadInboxMessageId)
                {
                    read++;
                }
            }

            UpdateLastReadInboxMessageId(topic, lastReadInboxMessageId, read);
        }

        public void SetPinnedForumTopics(Vector<int> forumTopicIds)
        {
            if (forumTopicIds.Count > _clientService.Options.PinnedForumTopicCountMax)
            {
                return;
            }

            _clientService.Send(new SetPinnedForumTopics(_chatId, forumTopicIds));

            // UpdatePinnedTopics reaches the topics in the list, so a topic dropped from it would
            // keep its pinned order until something else moved it.
            List<ForumTopic> unpinned = null;

            lock (SyncRoot)
            {
                foreach (var topicId in _pinnedTopicIds)
                {
                    if (!forumTopicIds.Contains(topicId) && _topics.TryGetValue(topicId, out ForumTopic topic))
                    {
                        unpinned ??= new List<ForumTopic>();
                        unpinned.Add(topic);
                    }
                }

                _pinnedTopicIds.Clear();
                _pinnedTopicIds.AddRange(forumTopicIds);
            }

            if (unpinned != null)
            {
                foreach (var topic in unpinned)
                {
                    topic.IsPinned = false;
                    UpdateTopicOrder(topic, true);
                }
            }

            UpdatePinnedTopics();
        }

        /// <param name="read">
        /// How many messages the read position was moved over that had not been read, for a caller
        /// that knows which ones they were.
        /// </param>
        private void UpdateLastReadInboxMessageId(ForumTopic topic, long lastReadInboxMessageId, int read = 0)
        {
            lock (SyncRoot)
            {
                _pendingLastReadInboxMessageId.Remove(lastReadInboxMessageId);
            }

            if (lastReadInboxMessageId > topic.LastReadInboxMessageId)
            {
                topic.LastReadInboxMessageId = lastReadInboxMessageId;
                UpdateUnreadCount(topic, -read);
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

        /// <param name="delta">
        /// How far the count moved on its own: one up for a message that arrived, one down for
        /// each message read. Zero when only the read position moved.
        /// </param>
        /// <remarks>
        /// TDLib never counts the unread messages of a topic itself: the number arrives with the
        /// topic and moves only when the server sends another one, so counting between two of
        /// those is ours to do. Reaching the last message is the one exact answer either way.
        /// </remarks>
        private void UpdateUnreadCount(ForumTopic topic, int delta)
        {
            var unreadCount = topic.UnreadCount;

            if (topic.LastMessage?.Id <= topic.LastReadInboxMessageId)
            {
                topic.UnreadCount = 0;
            }
            else if (delta != 0)
            {
                topic.UnreadCount = Math.Max(0, topic.UnreadCount + delta);
            }

            PublishUnreadCount(topic, topic.UnreadCount != unreadCount);
        }

        /// <summary>
        /// Reports the unread state of a topic, whether or not that took it into or out of the
        /// unread set.
        /// </summary>
        /// <param name="changed">
        /// The count itself moved. Leaving and joining the set carries the count with it, so this
        /// is the change nothing else reports.
        /// </param>
        private void PublishUnreadCount(ForumTopic topic, bool changed)
        {
            if (!UpdateUnreadTopicCount(topic, topic.UnreadCount > 0) && changed)
            {
                _aggregator.Publish(new UpdateForumTopicReadInbox(_chatId, topic.Info.ForumTopicId, topic.LastReadInboxMessageId, topic.UnreadCount));
            }
        }

        /// <returns>Whether the topic joined or left the unread set, which is what it reports.</returns>
        private bool UpdateUnreadTopicCount(ForumTopic topic, bool unread)
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

            return update;
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

                // A row the page does not move is redrawn only if it is reported, so what the
                // merge changed is collected here and published once the lock is released.
                List<(ForumTopic Topic, TopicChange Change)> changed = null;

                lock (SyncRoot)
                {
                    if (response is ForumTopics forumTopics)
                    {
                        _nextOffsetDate = forumTopics.NextOffsetDate;
                        _nextOffsetMessageId = forumTopics.NextOffsetMessageId;
                        _nextOffsetForumTopicId = forumTopics.NextOffsetForumTopicId;

                        var topics = new List<ForumTopic>(forumTopics.Topics.Count);

                        foreach (var newTopic in forumTopics.Topics)
                        {
                            var forumTopicId = newTopic.Info.ForumTopicId;

                            // A topic can come back on a later page, and the copy TDLib sends is
                            // the one the server sent it: its last message never advances. Taking
                            // it whole would undo the updates applied since, moving the topic back
                            // down, and would hand the list a second object for a row it already
                            // shows.
                            if (_topics.TryGetValue(forumTopicId, out ForumTopic topic))
                            {
                                var change = MergeTopic(topic, newTopic);
                                if (change != TopicChange.None)
                                {
                                    changed ??= new List<(ForumTopic, TopicChange)>();
                                    changed.Add((topic, change));
                                }
                            }
                            else
                            {
                                topic = newTopic;
                                _topics[forumTopicId] = topic;
                            }

                            if (topic.LastMessage != null)
                            {
                                _messages[topic.LastMessage.Id] = topic;
                            }

                            if (topic.IsPinned)
                            {
                                if (!_pinnedTopicIds.Contains(forumTopicId))
                                {
                                    _pinnedTopicIds.Add(forumTopicId);
                                }
                            }
                            else
                            {
                                _pinnedTopicIds.Remove(forumTopicId);
                            }

                            if (topic.UnreadCount > 0)
                            {
                                _unreadTopicIds.Add(forumTopicId);
                            }
                            else
                            {
                                _unreadTopicIds.Remove(forumTopicId);
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

                if (changed != null)
                {
                    foreach (var (topic, change) in changed)
                    {
                        PublishTopicChange(topic, change);
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

        /// <summary>
        /// What a merge took over, so that a row already on screen is redrawn for it. The info is
        /// not among them: TDLib reports a change to it as updateForumTopicInfo of its own.
        /// </summary>
        [Flags]
        private enum TopicChange
        {
            None = 0,
            NotificationSettings = 1,
            DraftMessage = 2,
            UnreadMentionCount = 4,
            UnreadReactionCount = 8,
            ReadInbox = 16,
            ReadOutbox = 32,
            LastMessage = 64
        }

        /// <summary>
        /// Takes over what a page of the list says about a topic already held, keeping the object
        /// the list is showing and the newer of the two last messages.
        /// </summary>
        /// <remarks>
        /// Caller must hold SyncRoot. Nothing is published from here: what changed is returned, so
        /// that the caller can report it once it lets the lock go.
        /// </remarks>
        private TopicChange MergeTopic(ForumTopic topic, ForumTopic newTopic)
        {
            var change = TopicChange.None;

            topic.Info = newTopic.Info;
            topic.IsPinned = newTopic.IsPinned;

            if (!topic.NotificationSettings.AreTheSame(newTopic.NotificationSettings))
            {
                topic.NotificationSettings = newTopic.NotificationSettings;
                change |= TopicChange.NotificationSettings;
            }

            if (topic.DraftMessage?.Date != newTopic.DraftMessage?.Date)
            {
                topic.DraftMessage = newTopic.DraftMessage;
                change |= TopicChange.DraftMessage;
            }

            if (topic.UnreadMentionCount != newTopic.UnreadMentionCount)
            {
                topic.UnreadMentionCount = newTopic.UnreadMentionCount;
                change |= TopicChange.UnreadMentionCount;
            }

            if (topic.UnreadReactionCount != newTopic.UnreadReactionCount)
            {
                topic.UnreadReactionCount = newTopic.UnreadReactionCount;
                change |= TopicChange.UnreadReactionCount;
            }

            // Ordered: the merge compares against the read position the line below replaces.
            if (MergeUnreadCount(topic, newTopic) || newTopic.LastReadInboxMessageId > topic.LastReadInboxMessageId)
            {
                topic.LastReadInboxMessageId = Math.Max(topic.LastReadInboxMessageId, newTopic.LastReadInboxMessageId);
                change |= TopicChange.ReadInbox;
            }

            if (newTopic.LastReadOutboxMessageId > topic.LastReadOutboxMessageId)
            {
                topic.LastReadOutboxMessageId = newTopic.LastReadOutboxMessageId;
                change |= TopicChange.ReadOutbox;
            }

            if (newTopic.LastMessage != null && newTopic.LastMessage.Id > (topic.LastMessage?.Id ?? 0))
            {
                if (topic.LastMessage != null)
                {
                    _messages.Remove(topic.LastMessage.Id);
                }

                topic.LastMessage = newTopic.LastMessage;
                change |= TopicChange.LastMessage;
            }

            return change;
        }

        /// <summary>
        /// Reports a merge to the list and to whatever else is showing the topic.
        /// </summary>
        /// <remarks>
        /// Called with SyncRoot released. The order is read off the topic rather than decided
        /// again: the page settled it before publishing, and the row only has to be redrawn where
        /// it already is.
        /// </remarks>
        private void PublishTopicChange(ForumTopic topic, TopicChange change)
        {
            var forumTopicId = topic.Info.ForumTopicId;

            if ((change & TopicChange.NotificationSettings) != 0)
            {
                _aggregator.Publish(new UpdateForumTopicNotificationSettings(_chatId, forumTopicId, topic.NotificationSettings));
            }

            if ((change & TopicChange.DraftMessage) != 0)
            {
                _aggregator.Publish(new UpdateForumTopicDraftMessage(_chatId, forumTopicId, topic.DraftMessage));
            }

            if ((change & TopicChange.UnreadMentionCount) != 0)
            {
                _aggregator.Publish(new UpdateForumTopicUnreadMentionCount(_chatId, forumTopicId, topic.UnreadMentionCount));
            }

            if ((change & TopicChange.UnreadReactionCount) != 0)
            {
                _aggregator.Publish(new UpdateForumTopicUnreadReactionCount(_chatId, forumTopicId, topic.UnreadReactionCount));
            }

            if ((change & TopicChange.ReadInbox) != 0)
            {
                _aggregator.Publish(new UpdateForumTopicReadInbox(_chatId, forumTopicId, topic.LastReadInboxMessageId, topic.UnreadCount));
            }

            if ((change & TopicChange.ReadOutbox) != 0)
            {
                _aggregator.Publish(new UpdateForumTopicReadOutbox(_chatId, forumTopicId, topic.LastReadOutboxMessageId));
            }

            if ((change & TopicChange.LastMessage) != 0)
            {
                RaiseChanged(topic, topic.Order, true);
            }
        }

        /// <summary>
        /// Takes the unread count the server sent, which is the only one either side counts from,
        /// and says whether that moved it: a count can change without the topic joining or leaving
        /// the unread set, and that is the one change UpdateUnreadTopicCount does not report.
        /// </summary>
        /// <remarks>
        /// The server counts on every request, so its number replaces ours - which is the same
        /// number plus what we have counted since, and drifts where a read skipped over messages.
        /// The one response to distrust is one whose read position is behind ours: it was built
        /// before a read we have already applied, and counts messages that are read by now.
        /// </remarks>
        private static bool MergeUnreadCount(ForumTopic topic, ForumTopic newTopic)
        {
            if (newTopic.LastReadInboxMessageId >= topic.LastReadInboxMessageId)
            {
                var unreadCount = topic.UnreadCount;

                topic.UnreadCount = newTopic.UnreadCount;
                return topic.UnreadCount != unreadCount;
            }

            return false;
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

            return GetCreationMessageId(topic.Info.ForumTopicId);
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
                            if (!_pinnedTopicIds.Contains(update.ForumTopicId))
                            {
                                _pinnedTopicIds.Insert(0, update.ForumTopicId);
                            }
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

            var unreadCountChanged = false;

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
                topic.IsPinned = newTopic.IsPinned;
                topic.Info = newTopic.Info;

                unreadCountChanged = MergeUnreadCount(topic, newTopic);

                UpdateLastReadInboxMessageId(topic, newTopic.LastReadInboxMessageId);
                UpdateLastReadOutboxMessageId(topic, newTopic.LastReadOutboxMessageId);

                // TODO: Not sure this is right
                if (newTopic.LastMessage != null)
                {
                    UpdateLastMessage(topic, newTopic.LastMessage, false);
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

                // Order reads the pinned list rather than the flag, so a topic that arrives pinned
                // on its own would have sorted by its last message. Where it belongs among the
                // pinned ones is only known from a page of the list, so it goes last until one
                // arrives.
                if (topic.IsPinned)
                {
                    if (!_pinnedTopicIds.Contains(topic.Info.ForumTopicId))
                    {
                        _pinnedTopicIds.Add(topic.Info.ForumTopicId);
                    }
                }
                else
                {
                    _pinnedTopicIds.Remove(topic.Info.ForumTopicId);
                }
            }

            PublishUnreadCount(topic, unreadCountChanged);
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
                UpdateLastMessage(topic, message, true);
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

        private void UpdateLastMessage(ForumTopic topic, Message message, bool newMessage)
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
                UpdateUnreadCount(topic, newMessage && !message.IsOutgoing ? 1 : 0);
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

        /// <summary>
        /// The identifier of the message that created the topic: a forum topic identifier is the
        /// server identifier of that message, and needs the same shift as any other.
        /// </summary>
        public static long GetCreationMessageId(int forumTopicId)
        {
            return (long)forumTopicId << 20;
        }

        private Message MessageForumTopicCreated(ForumTopic topic)
        {
            return new Message(GetCreationMessageId(topic.Info.ForumTopicId), topic.Info.CreatorId, null, _chatId, null, null, topic.Info.IsOutgoing, false, false, false, false, false, false, false, false, false, topic.Info.CreationDate, 0, null, null, null, Array.Empty<UnreadReaction>(), null, null, null, new MessageTopicForum(topic.Info.ForumTopicId), null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, new MessageForumTopicCreated(topic.Info.Name, false, topic.Info.Icon), null, null);
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
            UnreadCount = unreadCount;
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
