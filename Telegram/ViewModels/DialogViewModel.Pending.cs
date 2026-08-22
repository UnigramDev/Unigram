//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls.Messages;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class DialogViewModel
    {
        protected Dictionary<long, DialogPendingMessage> _pendingMessages = new();

        // td_api gives a yet-unsent message an identifier low in the band above the newest server
        // message; the layout is spelled out in MessageSelector. Pending bubbles take the last
        // slots of that band, so they sort after everything loaded and after a message the user
        // sends while the bot is still generating, but before the identifier the bot's own
        // message will get. draft_id can't be used: the bot chooses it, so it can be anything.
        private const long MessageTypeMask = (1L << 20) - 1;
        private const long FirstServerMessageId = 1L << 20;
        private const long PendingMessageIdCount = 64;

        private int _pendingMessageIndex;

        private long NextPendingMessageId()
        {
            var last = Items.LastId;
            var band = last > 0
                ? (last | MessageTypeMask) + 1
                : FirstServerMessageId;

            var index = Math.Min(_pendingMessageIndex++, PendingMessageIdCount - 1);
            return band - PendingMessageIdCount + index;
        }

        private bool _canStopPendingMessage;

        public bool CanStopPendingMessage => _canStopPendingMessage;

        private void UpdateCanStopPendingMessage()
        {
            var canStop = false;

            foreach (var pending in _pendingMessages.Values)
            {
                if (pending.CanStop)
                {
                    canStop = true;
                    break;
                }
            }

            if (canStop != _canStopPendingMessage)
            {
                _canStopPendingMessage = canStop;
                Delegate?.UpdatePendingMessage(_chat);
            }
        }

        public void StopPendingMessage()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            foreach (var pending in _pendingMessages.Values.ToArray())
            {
                if (!pending.CanStop)
                {
                    continue;
                }

                var topicId = pending.ForumTopicId != 0
                    ? new MessageTopicForum(pending.ForumTopicId)
                    : null;

                ClientService.Send(new StopPendingMessage(chat.Id, topicId, pending.DraftId));

                if (pending.KeepOnStop)
                {
                    pending.Freeze();
                }
                else
                {
                    RemovePendingMessage(pending);
                }
            }

            UpdateCanStopPendingMessage();
        }

        private void RemovePendingMessage(DialogPendingMessage pending)
        {
            pending.Stop();
            pending.Updated -= PendingMessage_Updated;
            pending.Completed -= PendingMessage_Completed;

            _pendingMessages.Remove(pending.DraftId);

            if (_pendingMessages.Count == 0)
            {
                _pendingMessageIndex = 0;
            }

            if (Items.TryGetValue(pending.MessageId, out MessageViewModel message))
            {
                Items.Remove(message);
            }
        }

        private void ClearPendingMessages()
        {
            foreach (var pending in _pendingMessages.Values)
            {
                pending.Stop();
                pending.Updated -= PendingMessage_Updated;
                pending.Completed -= PendingMessage_Completed;
            }

            _pendingMessages.Clear();
            _pendingMessageIndex = 0;
            _canStopPendingMessage = false;
        }

        private void PendingMessage_Updated(DialogPendingMessage sender, MessageViewModel message)
        {
            if (Items.TryGetValue(sender.MessageId, out MessageViewModel already))
            {
                already.Replace(message);
                Delegate?.UpdateBubbleWithMessageId(sender.MessageId, bubble => bubble.UpdateMessageContent(already));
            }
        }

        private void PendingMessage_Completed(DialogPendingMessage sender, Message completed)
        {
            _pendingMessages.Remove(sender.DraftId);

            sender.Updated -= PendingMessage_Updated;
            sender.Completed -= PendingMessage_Completed;

            if (_pendingMessages.Count == 0)
            {
                _pendingMessageIndex = 0;
            }

            UpdateCanStopPendingMessage();

            if (completed != null)
            {
                Handle(sender.MessageId, message =>
                {
                    message.Replace(completed);
                    message.AnimationState = MessageAnimationState.None;
                    message.GeneratedContentUnread = true;
                    message.IsSynthetic = false;

                    if (message.Content is MessagePaidMedia paidMedia)
                    {
                        message.Content = new MessagePaidAlbum(paidMedia);
                    }

                    InsertMessage(message, sender.MessageId);

                    return true;
                },
                (bubble, message) =>
                {
                    if (bubble.Parent is MessageSelector selector)
                    {
                        selector.PrepareForItemOverride(message, true);
                    }

                    bubble.UpdateMessage(message);
                    Delegate?.ViewVisibleMessages();
                }, newMessageId: completed.Id);
            }
            else if (Items.TryGetValue(sender.MessageId, out MessageViewModel already))
            {
                Items.Remove(already);
            }
        }
    }
}
