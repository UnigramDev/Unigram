using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLPeerBase : TLObject
    {
        public Int32 Id
        {
            get
            {
                if (this is TLPeerUser)
                    return ((TLPeerUser)this).UserId;
                else if (this is TLPeerChat)
                    return ((TLPeerChat)this).ChatId;
                else
                    return ((TLPeerChannel)this).ChannelId;
            }
            set
            {
                if (this is TLPeerUser)
                    ((TLPeerUser)this).UserId = value;
                else if (this is TLPeerChat)
                    ((TLPeerChat)this).ChatId = value;
                else
                    ((TLPeerChannel)this).ChannelId = value;
            }
        }
    }
}