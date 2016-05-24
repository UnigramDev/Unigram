namespace Telegram.Api.TL
{
    public abstract class TLStatedMessagesBase : TLObject
    {
        public TLVector<TLMessageBase> Messages { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public TLInt Pts { get; set; }

        public abstract TLStatedMessagesBase GetEmptyObject();

        public virtual TLInt GetSeq()
        {
            return null;
        }
    }

    public class TLStatedMessages : TLStatedMessagesBase
    {
        public const uint Signature = TLConstructors.TLStatedMessages;

        public TLInt Seq { get; set; }

        public override TLInt GetSeq()
        {
            return Seq;
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLStatedMessagesBase GetEmptyObject()
        {
            return new TLStatedMessages
            {
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Pts = Pts,
                Seq = Seq
            };
        }
    }

    public class TLStatedMessages24 : TLStatedMessagesBase, IMultiPts
    {
        public const uint Signature = TLConstructors.TLStatedMessages24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLStatedMessagesBase GetEmptyObject()
        {
            return new TLStatedMessages24
            {
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Pts = Pts,
                PtsCount = PtsCount
            };
        }
    }

    public class TLStatedMessagesLinks : TLStatedMessages, ILinks
    {
        public new const uint Signature = TLConstructors.TLStatedMessagesLinks;

        public TLVector<TLLinkBase> Links { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Links = GetObject<TLVector<TLLinkBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLStatedMessagesBase GetEmptyObject()
        {
            return new TLStatedMessagesLinks
            {
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Links = new TLVector<TLLinkBase>(Links.Count),
                Pts = Pts,
                Seq = Seq
            };
        }
    }

    public class TLStatedMessagesLinks24 : TLStatedMessages24, ILinks
    {
        public new const uint Signature = TLConstructors.TLStatedMessagesLinks24;

        public TLVector<TLLinkBase> Links { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Links = GetObject<TLVector<TLLinkBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLStatedMessagesBase GetEmptyObject()
        {
            return new TLStatedMessagesLinks24
            {
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Links = new TLVector<TLLinkBase>(Links.Count),
                Pts = Pts,
                PtsCount = PtsCount
            };
        }
    }
}
