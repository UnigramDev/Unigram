using System;
using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.Common.Chats
{
    public class OutputChatActionManager
    {
        private readonly IProtoService _protoService;
        private readonly Chat _chat;
        private readonly double _delay;

        private long _threadId;

        private DateTime? _lastTypingTime;

        public OutputChatActionManager(IProtoService protoService, Chat chat, long threadId  = 0, double delay = 5.0)
        {
            _chat = chat;
            _threadId = threadId;
            _delay = delay;
            _protoService = protoService;
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

            if (chat.Type is ChatTypeSupergroup super && super.IsChannel || chat.Type is ChatTypePrivate privata && privata.UserId == _protoService.Options.MyId)
            {
                return;
            }

            if (_lastTypingTime.HasValue && _lastTypingTime.Value.AddSeconds(_delay) > DateTime.Now)
            {
                return;
            }

            _lastTypingTime = DateTime.Now;
            _protoService.Send(new SendChatAction(chat.Id, _threadId, action));
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
            _protoService.Send(new SendChatAction(chat.Id, _threadId, new ChatActionCancel()));
        }
    }
}
