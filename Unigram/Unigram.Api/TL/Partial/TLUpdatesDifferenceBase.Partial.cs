using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public abstract partial class TLUpdatesDifferenceBase
    {
        public abstract TLUpdatesDifferenceBase GetEmptyObject();
    }

    public partial class TLUpdatesDifferenceEmpty
    {
        public override TLUpdatesDifferenceBase GetEmptyObject()
        {
            return new TLUpdatesDifferenceEmpty
            {
                Date = Date,
                Seq = Seq
            };
        }
    }

    public partial class TLUpdatesDifferenceTooLong
    {
        public override TLUpdatesDifferenceBase GetEmptyObject()
        {
            return new TLUpdatesDifferenceTooLong
            {
                Pts = Pts
            };
        }
    }

    public partial class TLUpdatesDifference
    {
        public override TLUpdatesDifferenceBase GetEmptyObject()
        {
            return new TLUpdatesDifference
            {
                NewMessages = new TLVector<TLMessageBase>(NewMessages.Count),
                NewEncryptedMessages = new TLVector<TLEncryptedMessageBase>(NewEncryptedMessages.Count),
                OtherUpdates = new TLVector<TLUpdateBase>(OtherUpdates.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                State = State
            };
        }
    }

    public partial class TLUpdatesDifferenceSlice
    {
        public override TLUpdatesDifferenceBase GetEmptyObject()
        {
            return new TLUpdatesDifferenceSlice
            {
                NewMessages = new TLVector<TLMessageBase>(NewMessages.Count),
                NewEncryptedMessages = new TLVector<TLEncryptedMessageBase>(NewEncryptedMessages.Count),
                OtherUpdates = new TLVector<TLUpdateBase>(OtherUpdates.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                IntermediateState = IntermediateState
            };
        }
    }
}
