using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache.EventArgs
{
    public class TopMessageUpdatedEventArgs : System.EventArgs
    {
        public TLPeerBase Peer { get; protected set; }

        public TLDialogBase Dialog { get; protected set; }

        public TLMessageBase Message { get; protected set; }

        public TLDecryptedMessageBase DecryptedMessage { get; protected set; }

        public TopMessageUpdatedEventArgs(TLPeerBase peer)
        {
            Peer = peer;
        }

        public TopMessageUpdatedEventArgs(TLDialogBase dialog, TLMessageBase message)
        {
            Dialog = dialog;
            Message = message;
        }

        public TopMessageUpdatedEventArgs(TLDialogBase dialog, TLDecryptedMessageBase message)
        {
            Dialog = dialog;
            DecryptedMessage = message;
        }
    }
}