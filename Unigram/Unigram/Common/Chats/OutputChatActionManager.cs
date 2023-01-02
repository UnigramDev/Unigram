//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.Common.Chats
{
    public class OutputChatActionManager
    {
        private readonly IClientService _clientService;
        private readonly Chat _chat;
        private readonly double _delay;

        private long _threadId;

        private DateTime? _lastTypingTime;

        public OutputChatActionManager(IClientService clientService, Chat chat, long threadId = 0, double delay = 5.0)
        {
            _chat = chat;
            _threadId = threadId;
            _delay = delay;
            _clientService = clientService;
        }

        public void SetThreadId(long threadId)
        {
            _threadId = threadId;
        }

        public void SetTyping(ChatAction action)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.IsChannel || chat.Type is ChatTypePrivate privata && privata.UserId == _clientService.Options.MyId)
            {
                return;
            }

            if (_lastTypingTime.HasValue && _lastTypingTime.Value.AddSeconds(_delay) > DateTime.Now)
            {
                return;
            }

            _lastTypingTime = DateTime.Now;
            _clientService.Send(new SendChatAction(chat.Id, _threadId, action));
        }

        public void CancelTyping()
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
            {
                return;
            }

            _lastTypingTime = null;
            _clientService.Send(new SendChatAction(chat.Id, _threadId, new ChatActionCancel()));
        }
    }
}
