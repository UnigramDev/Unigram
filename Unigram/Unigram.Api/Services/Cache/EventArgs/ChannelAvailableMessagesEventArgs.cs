using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;

namespace Telegram.Api.Services.Cache.EventArgs
{
    public class ChannelAvailableMessagesEventArgs
    {
        public TLDialog Dialog { get; protected set; }

        public int AvailableMinId { get; protected set; }

        public ChannelAvailableMessagesEventArgs(TLDialog dialog, int availableMinId)
        {
            Dialog = dialog;
            AvailableMinId = availableMinId;
        }
    }
}
