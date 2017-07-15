using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL.Messages
{
    public abstract partial class TLMessagesDialogsBase
    {
        public abstract TLMessagesDialogsBase GetEmptyObject();
    }

    public partial class TLMessagesDialogs
    {
        public override TLMessagesDialogsBase GetEmptyObject()
        {
            return new TLMessagesDialogs
            {
                Dialogs = new TLVector<TLDialog>(Dialogs.Count),
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public partial class TLMessagesDialogsSlice
    {
        public override TLMessagesDialogsBase GetEmptyObject()
        {
            return new TLMessagesDialogsSlice
            {
                Count = Count,
                Dialogs = new TLVector<TLDialog>(Dialogs.Count),
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }
}
