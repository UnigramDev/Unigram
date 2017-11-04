using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache.EventArgs
{
    public class DialogAddedEventArgs
    {
        public TLDialog Dialog { get; protected set; }

        public DialogAddedEventArgs(TLDialog dialog)
        {
            Dialog = dialog;
        }
    }
}