using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL.Messages
{
    public abstract partial class TLMessagesMessagesBase
    {
        public abstract ITLMessages GetEmptyObject();
    }

    public partial class TLMessagesMessages : ITLMessages
    {
        public override ITLMessages GetEmptyObject()
        {
            return new TLMessagesMessages
            {
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public partial class TLMessagesMessagesSlice : ITLMessages
    {
        public override ITLMessages GetEmptyObject()
        {
            return new TLMessagesMessagesSlice
            {
                Count = Count,
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public partial class TLMessagesChannelMessages : ITLMessages
    {
        public override ITLMessages GetEmptyObject()
        {
            return new TLMessagesChannelMessages
            {
                Count = Count,
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public partial class TLMessagesMessagesNotModified
    {
        public override ITLMessages GetEmptyObject()
        {
            throw new NotImplementedException();
        }
    }
}
