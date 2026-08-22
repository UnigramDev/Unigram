//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using Telegram.Controls.Messages;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public partial class DialogViewModel
    {
        protected Dictionary<long, DialogPendingMessage> _pendingMessages = new();

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

            if (Items.TryGetValue(long.MaxValue, out MessageViewModel message))
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
            _canStopPendingMessage = false;
        }

        private void PendingMessage_Updated(DialogPendingMessage sender, MessageViewModel message)
        {
            if (Items.TryGetValue(long.MaxValue, out MessageViewModel already))
            {
                already.Replace(message);
                Delegate?.UpdateBubbleWithMessageId(long.MaxValue, bubble => bubble.UpdateMessageContent(already));
            }
        }

        private void PendingMessage_Completed(DialogPendingMessage sender, Message completed)
        {
            _pendingMessages.Remove(sender.DraftId);

            sender.Updated -= PendingMessage_Updated;
            sender.Completed -= PendingMessage_Completed;

            UpdateCanStopPendingMessage();

            if (completed != null)
            {
                Handle(long.MaxValue, message =>
                {
                    message.Replace(completed);
                    message.AnimationState = MessageAnimationState.None;
                    message.GeneratedContentUnread = true;

                    if (message.Content is MessagePaidMedia paidMedia)
                    {
                        message.Content = new MessagePaidAlbum(paidMedia);
                    }

                    InsertMessage(message, long.MaxValue);

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
            else
            {
                if (Items.TryGetValue(sender.DraftId, out MessageViewModel already))
                {
                    Items.Remove(already);
                }
            }
        }
    }
}
