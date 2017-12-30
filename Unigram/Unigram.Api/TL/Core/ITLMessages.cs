using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL.Messages;

namespace Telegram.Api.TL
{
    public interface ITLMessages
    {
        ITLMessages GetEmptyObject();

        TLVector<TLMessageBase> Messages { get; set; }
        TLVector<TLChatBase> Chats { get; set; }
        TLVector<TLUserBase> Users { get; set; }
    }
}
