using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache.EventArgs
{
    public class TopMessageUpdatedEventArgs : System.EventArgs
    {
        public TLDialog Dialog { get; protected set; }

        public TLMessageBase Message { get; protected set; }

        // TODO: Secrets
        //public TLDecryptedMessageBase DecryptedMessage { get; protected set; }

        public TopMessageUpdatedEventArgs(TLDialog dialog, TLMessageBase message)
        {
            Dialog = dialog;
            Message = message;
        }

        // TODO: Secrets
        //public TopMessageUpdatedEventArgs(TLDialogBase dialog, TLDecryptedMessageBase message)
        //{
        //    Dialog = dialog;
        //    DecryptedMessage = message;
        //}
    }
}