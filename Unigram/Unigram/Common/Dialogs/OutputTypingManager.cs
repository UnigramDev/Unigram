using System;
using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.Common.Dialogs
{
    public class OutputTypingManager
    {
        private readonly IProtoService _protoService;
        private readonly Chat _chat;
        private readonly double _delay;

        private DateTime? _lastTypingTime;

        public OutputTypingManager(IProtoService protoService, Chat chat, double delay = 5.0)
        {
            _chat = chat;
            _delay = delay;
            _protoService = protoService;
        }

        public void SetTyping(ChatAction action)
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

            if (_lastTypingTime.HasValue && _lastTypingTime.Value.AddSeconds(_delay) > DateTime.Now)
            {
                return;
            }

            _lastTypingTime = DateTime.Now;
            _protoService.Send(new SendChatAction(chat.Id, action));
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
            _protoService.Send(new SendChatAction(chat.Id, new ChatActionCancel()));
        }
    }
}
