using System.Collections.Generic;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Messages
{
    public class SenderService : ISenderService
    {
        private Queue<TLMessageBase> _sendingQueue = new Queue<TLMessageBase>();

        private IMTProtoService _mtProtoService;

        public SenderService(IMTProtoService mtProtoService)
        {
            _mtProtoService = mtProtoService;
        }

        public void Send(TLMessageBase message)
        {
            _sendingQueue.Enqueue(message);
        }

        public void ResendAll()
        {
            
        }

        public void Open()
        {
            
        }

        public void Close()
        {
            
        }
    }
}
