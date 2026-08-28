//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Telegram.Collections;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class MessageCollection : SuppressObservableCollection<MessageViewModel>
    {
        private readonly DialogViewModel _viewModel;
        private readonly Dictionary<long, MessageViewModel> _messages = new();

        /// <summary>
        /// Which neighbours of an inserted item still need their attach state and separators
        /// worked out. A slice only needs it at the seam, because it already settled
        /// everything within itself as it was built.
        /// </summary>
        private enum AttachMode
        {
            Both,
            Previous,
            Next,
            None
        }

        private AttachMode _attachMode = AttachMode.Both;

        public ICollection<long> Ids => _messages.Keys;

        public long FirstId
        {
            get
            {
                for (int i = 0; i < Count; i++)
                {
                    var item = this[i];
                    if (item.Id != 0 && !item.IsSynthetic)
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
                    if (item.Id != 0 && !item.IsSynthetic)
                    {
                        return item.Id;
                    }
                }

                return long.MinValue;
            }
        }

        public Action<MessageViewModel, MessageViewModel> AttachChanged;

        // Only ever set on a slice
        public bool IsEndReached { get; }

        public MessageCollection(DialogViewModel viewModel)
        {
            _viewModel = viewModel;
            _messages = new();
        }

        public MessageCollection(DialogViewModel viewModel, ICollection<long> exclude, IEnumerable<Message> source, bool endReached, DialogType type)
        {
            _viewModel = viewModel;

            using (SuppressEvents())
            {
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

        /// <summary>
        /// Puts the message at its place in the list, or moves it there if it is already in.
        /// </summary>
        /// <param name="oldMessageId">
        /// The identifier the message was listed under, when it has just been given a new one.
        /// </param>
        public void InsertInOrder(MessageViewModel message, long oldMessageId = 0)
        {
            var newIndex = NextIndexOf(message, oldMessageId, out int oldIndex);
            if (oldIndex == -1)
            {
                Insert(newIndex, message);
            }
            else if (newIndex != oldIndex)
            {
                Reinsert(oldIndex, newIndex, message);
            }
        }

        /// <summary>
        /// Takes the message out and puts it back where it already was, so that the list builds a
        /// fresh container for it: a content template cannot be swapped in place.
        /// </summary>
        public void Reinsert(MessageViewModel message)
        {
            var newIndex = NextIndexOf(message, 0, out int oldIndex);
            if (oldIndex != -1)
            {
                Reinsert(oldIndex, newIndex, message);
            }
        }

        // Not Move: the list mishandles a Move notification.
        private void Reinsert(int oldIndex, int newIndex, MessageViewModel message)
        {
            RemoveAt(oldIndex);
            Insert(NextIndexOf(message, newIndex), message);
        }

        // Both indices in one backward walk, because it is the same walk: the ordering position
        // is normally settled on the first iteration and only the search for the message itself
        // carries on, so a message that is not listed costs a single comparison.
        private int NextIndexOf(MessageViewModel message, long oldMessageId, out int oldIndex)
        {
            oldIndex = -1;
            var newIndex = Count;

            // The two identifiers differ while a message is being renumbered: the item has
            // already taken its new id and only the map still knows it under the old one, so the
            // map answers whether it is listed and the walk answers where.
            //
            // An album is listed under its first child's id alone, so a later child of one
            // answers the lookup and is then matched nowhere below. Deliberate: it leaves the
            // album where it is rather than dragging it around by one of its children.
            var oldIndexNeeded = _messages.ContainsKey(oldMessageId != 0 ? oldMessageId : message.Id);
            var newIndexNeeded = true;

            for (int i = Count - 1; i >= 0; i--)
            {
                var item = this[i];
                if (item.Id == 0)
                {
                    // A separator has no identifier to compare, so it is placed by date, and
                    // skipped outright when it belongs after the message.
                    if (item.Date <= message.Date)
                    {
                        newIndex = i + 1;
                        newIndexNeeded = false;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (item.Id < message.Id && newIndexNeeded)
                {
                    newIndex = i + 1;
                    newIndexNeeded = false;
                }

                if (item.Id == message.Id && oldIndexNeeded)
                {
                    oldIndex = i;
                    oldIndexNeeded = false;
                }

                if (!newIndexNeeded && !oldIndexNeeded)
                {
                    break;
                }
            }

            if (oldIndex != -1 && oldIndex < newIndex)
            {
                newIndex--;
            }

            // Left as the count when nothing sorts below the message: that is what puts a
            // sponsored message at the end, its identifier being smaller than every real one.
            return newIndex;
        }

        // Re-derives an index across a removal. RemoveItem also drops the separator the removal
        // orphans, so it can take up to four rows rather than the one the caller's index was
        // adjusted for, leaving that index too high -- never too low, since a removal only ever
        // shifts rows down. Walking back from it therefore settles within those few rows instead
        // of costing another pass over the list.
        private int NextIndexOf(MessageViewModel message, int from)
        {
            for (int i = Math.Min(from, Count) - 1; i >= 0; i--)
            {
                var item = this[i];
                if (item.Id == 0)
                {
                    if (item.Date <= message.Date)
                    {
                        return i + 1;
                    }

                    continue;
                }

                if (item.Id < message.Id)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        public void AppendSlice(MessageCollection source, bool filter, out bool empty)
        {
            empty = true;

            var lastId = LastId;

            try
            {
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

                    // Only the first item that actually lands has a neighbour above it whose
                    // attach state can still change: the rest brought theirs from the slice.
                    // Keyed off empty rather than the loop index, which counts candidates.
                    _attachMode = empty ? AttachMode.Previous : AttachMode.None;

                    Add(message);
                    empty = false;
                }
            }
            finally
            {
                _attachMode = AttachMode.Both;
            }
        }

        public void PrependSlice(MessageCollection source, bool filter, out bool empty)
        {
            empty = true;

            var firstId = FirstId;

            try
            {
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

                    _attachMode = empty ? AttachMode.Next : AttachMode.None;

                    Insert(0, message);
                    empty = false;
                }
            }
            finally
            {
                _attachMode = AttachMode.Both;
            }
        }

        /// <summary>
        /// Replaces the whole list in one Reset. Nothing is recomputed here, so the source must
        /// already carry its own attach state and separators — every slice does, having been
        /// built through the same insert path.
        /// </summary>
        public void ReplaceSlice(MessageCollection source)
        {
            _messages.Clear();
            _attachMode = AttachMode.None;

            try
            {
                using (SuppressEvents())
                {
                    Clear();

                    foreach (var item in source)
                    {
                        Add(item);
                    }
                }
            }
            finally
            {
                _attachMode = AttachMode.Both;
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        // ObservableCollection builds the NotifyCollectionChangedEventArgs before it reaches
        // the virtual that drops it, so a suppressed mutation still allocates one per item.
        // Filling a slice or replacing the whole list does that hundreds of times over.
        private void InsertCore(int index, MessageViewModel item)
        {
            if (EventsAreSuppressed)
            {
                Items.Insert(index, item);
            }
            else
            {
                base.InsertItem(index, item);
            }
        }

        private void RemoveCore(int index)
        {
            if (EventsAreSuppressed)
            {
                Items.RemoveAt(index);
            }
            else
            {
                base.RemoveItem(index);
            }
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

            var mode = _attachMode;
            if (mode == AttachMode.None || item.Content is MessageHeaderNewThread or MessageSponsored)
            {
                InsertCore(index, item);
                return;
            }

            var joinPrev = mode is AttachMode.Both or AttachMode.Previous;
            var joinNext = mode is AttachMode.Both or AttachMode.Next;

            var prev = joinPrev && index > 0 ? this[index - 1] : null;
            var next = joinNext && index < Count ? this[index] : null;

            var prevSeparator = joinPrev ? UpdateSeparatorOnInsert(prev, item) : null;
            var nextSeparator = joinNext ? UpdateSeparatorOnInsert(item, next) : null;

            var prevForumTopic = joinPrev ? UpdateForumTopicSeparatorOnInsert(prev, item) : null;
            var nextForumTopic = joinNext ? UpdateForumTopicSeparatorOnInsert(item, next) : null;

            // The separators are returned rather than inserted so that the attach state of both
            // neighbours settles before anything moves, leaving at most two of them to report.
            var prevHash = AttachHash(prev);
            var nextHash = AttachHash(next);

            if (joinPrev)
            {
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
            }

            if (joinNext)
            {
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
            }

            // Order must be: the separators between prev and item, item, then the separators
            // between item and next.
            if (prevSeparator != null)
            {
                InsertCore(index++, prevSeparator);
            }

            if (prevForumTopic != null)
            {
                InsertCore(index++, prevForumTopic);
            }

            InsertCore(index, item);

            if (nextSeparator != null)
            {
                InsertCore(++index, nextSeparator);
            }

            if (nextForumTopic != null)
            {
                InsertCore(++index, nextForumTopic);
            }

            var prevChanged = prevHash != AttachHash(prev);
            var nextChanged = nextHash != AttachHash(next);

            if (prevChanged || nextChanged)
            {
                AttachChanged?.Invoke(prevChanged ? prev : null, nextChanged ? next : null);
            }
        }

        // The one mutation that used to leave _messages stale. It maintains the id map only:
        // the sole caller swaps an album root in over the child that seeded it, so neither
        // the day nor the neighbours change and there is no attach state to recompute.
        protected override void SetItem(int index, MessageViewModel item)
        {
            var previous = this[index];
            if (previous.Content is MessageAlbum previousAlbum)
            {
                foreach (var child in previousAlbum.Messages)
                {
                    _messages.Remove(child.Id);
                }
            }

            _messages.Remove(previous.Id);

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

            base.SetItem(index, item);
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

            if (_attachMode == AttachMode.None || item.Content is MessageHeaderNewThread or MessageSponsored)
            {
                RemoveCore(index);
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

            if (hash2 != update2 || hash3 != update3)
            {
                AttachChanged?.Invoke(hash2 != update2 ? previous : null, hash3 != update3 ? next : null);
            }

            UpdateSeparatorOnRemove(ref previous, ref next, ref index);

            RemoveCore(index);
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
                    return new MessageViewModel(next.ClientService, next.Delegate, next.Chat, _viewModel.ForumTopic, _viewModel.DirectMessagesChatTopic, new Message(0, next.SenderId, null, next.ChatId, null, _viewModel.IsSavedMessagesTab ? item.SchedulingState : next.SchedulingState, next.IsOutgoing, false, false, false, false, next.IsChannelPost, false, false, false, false, next.Date, 0, null, null, null, null, null, null, null, next.TopicId, null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderDate(_viewModel.IsSavedMessagesTab ? item.Date : next.Date), null, null));
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
                    return new MessageViewModel(next.ClientService, next.Delegate, next.Chat, _viewModel.ForumTopic, _viewModel.DirectMessagesChatTopic, new Message(0, next.SenderId, null, next.ChatId, null, next.SchedulingState, next.IsOutgoing, false, false, false, false, next.IsChannelPost, false, false, false, false, next.Date, 0, null, null, null, null, null, null, null, next.TopicId, null, 0, 0, 0, null, 0, 0, string.Empty, 0, string.Empty, 0, 0, null, string.Empty, new MessageHeaderMessageTopic(), null, null));
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
                    RemoveCore(index - 1);

                    index--;
                    previous = index > 0 ? this[index - 1] : null;
                }
            }

            if (next != null && next.Content is MessageHeaderDate)
            {
                if (previous == null || previous.AreOnTheSameDay(next))
                {
                    RemoveCore(index + 1);

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
                    RemoveCore(index - 1);

                    index--;
                    previous = index > 0 ? this[index - 1] : null;
                }
            }

            if (next != null && next.Content is MessageHeaderMessageTopic)
            {
                if (previous == null || previous.TopicId.AreTheSame(next.TopicId))
                {
                    RemoveCore(index + 1);

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
