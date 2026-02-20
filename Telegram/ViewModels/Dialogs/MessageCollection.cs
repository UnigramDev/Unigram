//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Collections;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class MessageCollection : MvxObservableCollection<MessageViewModel>
    {
        private readonly DialogViewModel _viewModel;
        private readonly Dictionary<long, MessageViewModel> _messages = new();

        private bool _suppressOperations = false;
        private bool _suppressPrev = false;
        private bool _suppressNext = false;

        public ICollection<long> Ids => _messages.Keys;

        public long FirstId
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    var item = this[i];
                    if (item.Id != 0)
                    {
                        return item.Id;
                    }
                }

                return long.MaxValue;
            }
        }

        public long LastId
        {
            get
            {
                for (int i = Count - 1; i >= 0; i--)
                {
                    var item = this[i];
                    if (item.Id != 0)
                    {
                        return item.Id;
                    }
                }

                return long.MinValue;
            }
        }

        public Action<IEnumerable<MessageViewModel>> AttachChanged;

        // Used in sub-collection
        public bool IsEndReached { get; }

        public MessageCollection(DialogViewModel viewModel)
        {
            _viewModel = viewModel;
            _messages = new();
        }

        public MessageCollection(DialogViewModel viewModel, ICollection<long> exclude, IEnumerable<Message> source, bool endReached, DialogType type)
        {
            _viewModel = viewModel;

            foreach (var item in source)
            {
                if (item.Id != 0 && exclude != null && exclude.Contains(item.Id))
                {
                    continue;
                }
                else if (item.Content is MessageForumTopicCreated or MessageChatUpgradeFrom && type == DialogType.Thread)
                {
                    continue;
                }

                Insert(0, viewModel.CreateMessage(item, true));
            }

            IsEndReached = endReached || Count == 0;
        }

        //~MessageCollection()
        //{
        //    Debug.WriteLine("Finalizing MessageCollection");
        //    GC.Collect();
        //}

        protected override void ClearItems()
        {
            _messages.Clear();
            base.ClearItems();
        }

        public bool ContainsKey(long id)
        {
            return _messages.ContainsKey(id);
        }

        public bool TryGetValue(long id, out MessageViewModel value)
        {
            return _messages.TryGetValue(id, out value);
        }

        public void UpdateMessageSendSucceeded(long oldMessageId, MessageViewModel message)
        {
            _messages.Remove(oldMessageId);
            _messages[message.Id] = message;
        }

        public void UpdateMessageSendSucceeded(long oldMessageId, long newMessageId, MessageViewModel message)
        {
            _messages.Remove(oldMessageId);
            _messages[newMessageId] = message;
        }

        public void RawAddRange(IList<MessageViewModel> source, bool filter, out bool empty)
        {
            empty = true;

            var lastId = LastId;

            for (int i = 0; i < source.Count; i++)
            {
                var message = source[i];

                if (filter && message.Id != 0)
                {
                    if (message.Id < lastId || _messages.ContainsKey(message.Id))
                    {
                        continue;
                    }
                }

                _suppressOperations = i > 0;
                _suppressNext = !_suppressOperations;

                Add(message);
                empty = false;
            }

            _suppressOperations = false;
            _suppressNext = false;
        }

        public void RawInsertRange(int index, IList<MessageViewModel> source, bool filter, out bool empty)
        {
            empty = true;

            var firstId = FirstId;

            for (int i = source.Count - 1; i >= 0; i--)
            {
                var message = source[i];

                if (filter && message.Id != 0)
                {
                    if (message.Id > firstId || _messages.ContainsKey(message.Id))
                    {
                        continue;
                    }
                }

                _suppressOperations = i < source.Count - 1;
                _suppressPrev = !_suppressOperations;

                Insert(0, message);
                empty = false;
            }

            _suppressOperations = false;
            _suppressPrev = false;
        }

        public void RawReplaceWith(IEnumerable<MessageViewModel> source)
        {
            _messages.Clear();
            _suppressOperations = true;

            ReplaceWith(source);

            _suppressOperations = false;
        }

        protected override void InsertItem(int index, MessageViewModel item)
        {
            if (item.Content is MessageAlbum album)
            {
                foreach (var child in album.Messages)
                {
                    _messages[child.Id] = item;
                }
            }

            if (item.Id != 0)
            {
                _messages[item.Id] = item;
            }

            if (_suppressOperations || item.Content is MessageHeaderNewThread or MessageSponsored)
            {
                base.InsertItem(index, item);
            }
            else if (_suppressNext)
            {
                var prev = index > 0 ? this[index - 1] : null;
                var prevSeparator = UpdateSeparatorOnInsert(prev, item);
                var prevForumTopic = UpdateForumTopicSeparatorOnInsert(prev, item);
                var prevHash = AttachHash(prev);

                if (prevForumTopic != null)
                {
                    UpdateAttach(null, prev);
                    UpdateAttach(prevForumTopic, item);
                }
                else if (prevSeparator != null)
                {
                    UpdateAttach(null, prev);
                    UpdateAttach(prevSeparator, item);
                }
                else
                {
                    UpdateAttach(item, prev);
                }

                if (prevSeparator != null)
                {
                    base.InsertItem(index++, prevSeparator);
                }

                if (prevForumTopic != null)
                {
                    base.InsertItem(index++, prevForumTopic);
                }

                base.InsertItem(index, item);

                var prevUpdate = AttachHash(prev);
                if (prevUpdate != prevHash)
                {
                    AttachChanged?.Invoke(new[] { prev });
                }
            }
            else if (_suppressPrev)
            {
                var next = index < Count ? this[index] : null;
                var nextSeparator = UpdateSeparatorOnInsert(item, next);
                var nextForumTopic = UpdateForumTopicSeparatorOnInsert(item, next);
                var nextHash = AttachHash(next);

                if (nextForumTopic != null)
                {
                    UpdateAttach(next, null);
                    UpdateAttach(item, nextForumTopic);
                }
                else if (nextSeparator != null)
                {
                    UpdateAttach(next, null);
                    UpdateAttach(item, nextSeparator);
                }
                else
                {
                    UpdateAttach(next, item);
                }

                base.InsertItem(index, item);

                if (nextSeparator != null)
                {
                    base.InsertItem(++index, nextSeparator);
                }

                if (nextForumTopic != null)
                {
                    base.InsertItem(++index, nextForumTopic);
                }

                var nextUpdate = AttachHash(next);
                if (nextUpdate != nextHash)
                {
                    AttachChanged?.Invoke(new[] { next });
                }
            }
            else
            {
                var prev = index > 0 ? this[index - 1] : null;
                var next = index < Count ? this[index] : null;

                // Order must be:
                // Separator between previous and item
                // Item
                // Separator between item and next
                // UpdateSeparatorOnInsert must return the new messages
                // This way only two AttachChanged will be needed at most

                var prevSeparator = UpdateSeparatorOnInsert(prev, item);
                var nextSeparator = UpdateSeparatorOnInsert(item, next);

                var prevForumTopic = UpdateForumTopicSeparatorOnInsert(prev, item);
                var nextForumTopic = UpdateForumTopicSeparatorOnInsert(item, next);

                var nextHash = AttachHash(next);
                var prevHash = AttachHash(prev);

                if (prevForumTopic != null)
                {
                    UpdateAttach(null, prev);
                    UpdateAttach(prevForumTopic, item);
                }
                else if (prevSeparator != null)
                {
                    UpdateAttach(null, prev);
                    UpdateAttach(prevSeparator, item);
                }
                else
                {
                    UpdateAttach(item, prev);
                }

                if (nextForumTopic != null)
                {
                    UpdateAttach(next, null);
                    UpdateAttach(item, nextForumTopic);
                }
                else if (nextSeparator != null)
                {
                    UpdateAttach(next, null);
                    UpdateAttach(item, nextSeparator);
                }
                else
                {
                    UpdateAttach(next, item);
                }

                if (prevSeparator != null)
                {
                    base.InsertItem(index++, prevSeparator);
                }

                if (prevForumTopic != null)
                {
                    base.InsertItem(index++, prevForumTopic);
                }

                base.InsertItem(index, item);

                if (nextSeparator != null)
                {
                    base.InsertItem(++index, nextSeparator);
                }

                if (nextForumTopic != null)
                {
                    base.InsertItem(++index, nextForumTopic);
                }

                var nextUpdate = AttachHash(next);
                var prevUpdate = AttachHash(prev);

                if (prevHash != prevUpdate || nextHash != nextUpdate)
                {
                    AttachChanged?.Invoke(new[]
                    {
                        prevHash != prevUpdate ? prev : null,
                        nextHash != nextUpdate ? next : null
                    });
                }
            }
        }

        public void RawRemoveAt(int index)
        {
            _suppressOperations = true;
            RemoveAt(index);
            _suppressOperations = false;
        }

        protected override void RemoveItem(int index)
        {
            var item = this[index];
            if (item.Content is MessageAlbum album)
            {
                foreach (var child in album.Messages)
                {
                    _messages.Remove(child.Id);
                }
            }

            _messages.Remove(item.Id);

            if (_suppressOperations || item.Content is MessageHeaderNewThread or MessageSponsored)
            {
                base.RemoveItem(index);
                return;
            }

            var previous = index > 0 ? this[index - 1] : null;
            var next = index < Count - 1 ? this[index + 1] : null;

            UpdateForumTopicSeparatorOnRemove(ref previous, ref next, ref index);

            var hash2 = AttachHash(previous);
            var hash3 = AttachHash(next);

            UpdateAttach(next, previous);

            var update2 = AttachHash(previous);
            var update3 = AttachHash(next);

            if (hash3 != update3 || hash2 != update2)
            {
                AttachChanged?.Invoke(new[]
                {
                    hash3 != update3 ? next : null,
                    hash2 != update2 ? previous : null
                });
            }

            UpdateSeparatorOnRemove(ref previous, ref next, ref index);

            base.RemoveItem(index);
        }

        // TODO: Support MoveItem to optimize UpdateMessageSendSucceeded

        private MessageViewModel UpdateSeparatorOnInsert(MessageViewModel item, MessageViewModel next)
        {
            if (item != null && next != null && item.Content is not MessageHeaderDate && next.Content is not MessageHeaderDate)
            {
                if (item.Content is MessageHeaderNewThread or MessageSponsored || next.Content is MessageHeaderNewThread or MessageSponsored)
                {
                    return null;
                }

                if (!item.AreOnTheSameDay(next))
                {
                    return new MessageViewModel(next.ClientService, next.Delegate, next.Chat, _viewModel.ForumTopic, _viewModel.DirectMessagesChatTopic, new Message(0, next.SenderId, next.ChatId, null, _viewModel.IsSavedMessagesTab ? item.SchedulingState : next.SchedulingState, next.IsOutgoing, false, false, false, false, next.IsChannelPost, false, false, false, next.Date, 0, null, null, null, null, null, null, null, next.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderDate(_viewModel.IsSavedMessagesTab ? item.Date : next.Date), null));
                }
            }

            return null;
        }

        private MessageViewModel UpdateForumTopicSeparatorOnInsert(MessageViewModel item, MessageViewModel next)
        {
            if (!_viewModel.IsForum && !_viewModel.IsDirectMessagesGroup)
            {
                return null;
            }

            if (item != null && next != null && item.Content is not MessageHeaderMessageTopic && next.Content is not MessageHeaderMessageTopic)
            {
                if (item.Content is MessageHeaderNewThread or MessageSponsored || next.Content is MessageHeaderNewThread or MessageSponsored)
                {
                    return null;
                }

                if (!item.TopicId.AreTheSame(next.TopicId))
                {
                    return new MessageViewModel(next.ClientService, next.Delegate, next.Chat, _viewModel.ForumTopic, _viewModel.DirectMessagesChatTopic, new Message(0, next.SenderId, next.ChatId, null, next.SchedulingState, next.IsOutgoing, false, false, false, false, next.IsChannelPost, false, false, false, next.Date, 0, null, null, null, null, null, null, null, next.TopicId, null, 0, 0, 0, 0, 0, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderMessageTopic(), null));
                }
            }

            return null;
        }

        private void UpdateSeparatorOnRemove(ref MessageViewModel previous, ref MessageViewModel next, ref int index)
        {
            if (previous != null && previous.Content is MessageHeaderDate)
            {
                if (next == null || !next.AreOnTheSameDay(previous))
                {
                    base.RemoveItem(index - 1);

                    index--;
                    previous = index > 0 ? this[index - 1] : null;
                }
            }

            if (next != null && next.Content is MessageHeaderDate)
            {
                if (previous == null || previous.AreOnTheSameDay(next))
                {
                    base.RemoveItem(index + 1);

                    next = index < Count - 1 ? this[index + 1] : null;
                }
            }
        }

        private void UpdateForumTopicSeparatorOnRemove(ref MessageViewModel previous, ref MessageViewModel next, ref int index)
        {
            if (previous != null && previous.Content is MessageHeaderMessageTopic forumTopic)
            {
                if (next == null || !next.TopicId.AreTheSame(previous.TopicId))
                {
                    base.RemoveItem(index - 1);

                    index--;
                    previous = index > 0 ? this[index - 1] : null;
                }
            }

            if (next != null && next.Content is MessageHeaderMessageTopic)
            {
                if (previous == null || previous.TopicId.AreTheSame(next.TopicId))
                {
                    base.RemoveItem(index + 1);

                    next = index < Count - 1 ? this[index + 1] : null;
                }
            }
        }

        private int AttachHash(MessageViewModel item)
        {
            var hash = 0;
            if (item != null && item.IsFirst)
            {
                hash |= 1 << 0;
            }
            if (item != null && item.IsLast)
            {
                hash |= 2 << 0;
            }

            return hash;
        }

        private void UpdateAttach(MessageViewModel item, MessageViewModel previous)
        {
            if (item == null)
            {
                previous?.IsLast = true;

                return;
            }

            if (item.IsChannelPost)
            {
                item.IsFirst = true;
                item.IsLast = true;
                return;
            }

            var attach = false;
            if (previous != null)
            {
                var previousPost = previous.IsChannelPost;

                attach = !previousPost &&
                         //!(previous.IsService()) &&
                         AreTogether(item, previous) &&
                         item.GetDate() - previous.GetDate() < 900;
            }

            item.IsFirst = !attach;

            previous?.IsLast = item.IsFirst /*|| item.IsService()*/;
        }

        private bool AreTogether(MessageViewModel message1, MessageViewModel message2)
        {
            if (message1.IsService || message2.IsService || message1.ChatId == message1.ClientService.Options.VerificationCodesBotChatId)
            {
                return false;
            }

            var saved1 = message1.IsSaved;
            var saved2 = message2.IsSaved;

            if (saved1 && saved2)
            {
                if (message1.ForwardInfo?.Origin is MessageOriginUser fromUser1 && message2.ForwardInfo?.Origin is MessageOriginUser fromUser2)
                {
                    return fromUser1.SenderUserId == fromUser2.SenderUserId && message1.ForwardInfo.Source?.ChatId == message2.ForwardInfo.Source?.ChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageOriginChat fromChat1 && message2.ForwardInfo?.Origin is MessageOriginChat fromChat2)
                {
                    return fromChat1.SenderChatId == fromChat2.SenderChatId && message1.ForwardInfo.Source?.ChatId == message2.ForwardInfo.Source?.ChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageOriginChannel fromChannel1 && message2.ForwardInfo?.Origin is MessageOriginChannel fromChannel2)
                {
                    return fromChannel1.ChatId == fromChannel2.ChatId && message1.ForwardInfo.Source?.ChatId == message2.ForwardInfo.Source?.ChatId;
                }
                else if (message1.ForwardInfo?.Origin is MessageOriginHiddenUser hiddenUser1 && message2.ForwardInfo?.Origin is MessageOriginHiddenUser hiddenUser2)
                {
                    return hiddenUser1.SenderName == hiddenUser2.SenderName;
                }
                else if (message1.ImportInfo != null && message2.ImportInfo != null)
                {
                    return message1.ImportInfo.SenderName == message2.ImportInfo.SenderName;
                }

                return false;
            }
            else if (saved1 || saved2)
            {
                return false;
            }

            if (message1.SenderId is MessageSenderChat chat1 && message2.SenderId is MessageSenderChat chat2)
            {
                if (message1.IsOutgoing || message2.IsOutgoing)
                {
                    return false;
                }

                return chat1.ChatId == chat2.ChatId
                    && message1.AuthorSignature == message2.AuthorSignature;
            }
            else if (message1.SenderId is MessageSenderUser user1 && message2.SenderId is MessageSenderUser user2)
            {
                return user1.UserId == user2.UserId;
            }

            return false;
        }
    }
}
