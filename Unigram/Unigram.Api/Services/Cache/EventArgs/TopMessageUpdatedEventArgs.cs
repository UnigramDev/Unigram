using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache.EventArgs
{
    public class TopMessageUpdatedEventArgs : System.EventArgs
    {
        public TLPeerBase Peer { get; protected set; }

        public TLDialog Dialog { get; protected set; }

        public TLMessageBase Message { get; protected set; }

        public TLDecryptedMessageBase DecryptedMessage { get; protected set; }

        public TopMessageUpdatedEventArgs(TLPeerBase peer)
        {
            Peer = peer;
        }

        public TopMessageUpdatedEventArgs(TLDialog dialog, TLMessageBase message)
        {
            Dialog = dialog;
            Message = message;
        }

        public TopMessageUpdatedEventArgs(TLDialog dialog, TLDecryptedMessageBase message)
        {
            Dialog = dialog;
            DecryptedMessage = message;
        }
    }
}