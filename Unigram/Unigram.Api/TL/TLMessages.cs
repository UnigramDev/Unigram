using System;

namespace Telegram.Api.TL
{
    [Flags]
    public enum MessagesFlags
    {
        Collapsed = 0x1
    }

    public abstract class TLMessagesBase : TLObject
    {
        public TLVector<TLMessageBase> Messages { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public abstract TLMessagesBase GetEmptyObject();
    }

    public class TLMessages : TLMessagesBase
    {
        public const uint Signature = TLConstructors.TLMessages;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLMessagesBase GetEmptyObject()
        {
            return new TLMessages
            {
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public class TLMessagesSlice : TLMessages
    {
        public new const uint Signature = TLConstructors.TLMessagesSlice;

        public TLInt Count { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Count = GetObject<TLInt>(bytes, ref position);

            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLMessagesBase GetEmptyObject()
        {
            return new TLMessagesSlice
            {
                Count = Count,
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public class TLChannelMessages : TLMessagesSlice
    {
        public new const uint Signature = TLConstructors.TLChannelMessages;

        public TLInt Flags { get; set; }

        public TLInt Pts { get; set; }

        public TLVector<TLMessageGroup> Collapsed { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Count = GetObject<TLInt>(bytes, ref position);

            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            if (IsSet(Flags, (int) MessagesFlags.Collapsed))
            {
                Collapsed = GetObject<TLVector<TLMessageGroup>>(bytes, ref position);
            }
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLMessagesBase GetEmptyObject()
        {
            return new TLMessagesSlice
            {
                Count = Count,
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }
}
