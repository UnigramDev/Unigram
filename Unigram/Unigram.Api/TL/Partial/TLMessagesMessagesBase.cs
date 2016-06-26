using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLMessagesMessagesBase
    {
        public abstract TLMessagesMessagesBase GetEmptyObject();
    }

    public partial class TLMessagesMessages
    {
        public override TLMessagesMessagesBase GetEmptyObject()
        {
            return new TLMessagesMessages
            {
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public partial class TLMessagesMessagesSlice
    {
        public override TLMessagesMessagesBase GetEmptyObject()
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

    public partial class TLMessagesChannelMessages
    {
        public override TLMessagesMessagesBase GetEmptyObject()
        {
            // TODO: Verify
            return new TLMessagesChannelMessages
            {
                Count = Count,
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }
}
