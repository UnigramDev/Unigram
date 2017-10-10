using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache.EventArgs
{
    public class MessageExpiredEventArgs
    {
        public TLMessage Message { get; protected set; }

        public MessageExpiredEventArgs(TLMessage message)
        {
            Message = message;
        }
    }
}