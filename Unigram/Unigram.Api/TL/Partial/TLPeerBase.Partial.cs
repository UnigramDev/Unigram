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

        public override bool Equals(object obj)
        {
            var peer = obj as TLPeerBase;
            if ((this is TLPeerUser && obj is TLPeerUser) ||
                (this is TLPeerChat && obj is TLPeerChat) ||
                (this is TLPeerChannel && obj is TLPeerChannel))
            {
                return Id.Equals(peer.Id);
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}