using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services;
using Telegram.Api.TL;

namespace Unigram.Common.Dialogs
{
    public class OutputTypingManager
    {
        private readonly IMTProtoService _protoService;
        private readonly TLInputPeerBase _peer;
        private readonly double _delay;

        private DateTime? _lastTypingTime;

        public OutputTypingManager(IMTProtoService protoService, TLInputPeerBase peer, double delay = 5.0)
        {
            _peer = peer;
            _delay = delay;
            _protoService = protoService;
        }

        public void SetTyping(TLSendMessageActionBase action)
        {
            if (_peer is TLInputPeerChannel)
            {
                return;
            }

            if (_lastTypingTime.HasValue && _lastTypingTime.Value.AddSeconds(_delay) > DateTime.Now)
            {
                return;
            }

            _lastTypingTime = DateTime.Now;
            _protoService.SetTypingAsync(_peer, action, null);
        }

        public void CancelTyping()
        {
            if (_peer is TLInputPeerChannel)
            {
                return;
            }

            _lastTypingTime = null;
            _protoService.SetTypingAsync(_peer, new TLSendMessageCancelAction(), null);
        }
    }
}
