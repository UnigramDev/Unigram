using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache.EventArgs
{
    public class DialogRemovedEventArgs
    {
        public TLDialog Dialog { get; protected set; }

        public DialogRemovedEventArgs(TLDialog dialog)
        {
            Dialog = dialog;
        }
    }
}