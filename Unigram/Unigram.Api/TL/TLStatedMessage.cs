namespace Telegram.Api.TL
{
    public abstract class TLStatedMessageBase : TLObject
    {
        public TLMessageBase Message { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public TLInt Pts { get; set; }

        public abstract TLStatedMessageBase GetEmptyObject();

        public virtual TLInt GetSeq()
        {
            return null;
        }
    }

    public interface ILinks
    {
        TLVector<TLLinkBase> Links { get; set; }
    }

    public class TLStatedMessage : TLStatedMessageBase
    {
        public const uint Signature = TLConstructors.TLStatedMessage;

        public TLInt Seq { get; set; }

        public override TLInt GetSeq()
        {
            return Seq;
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Message = GetObject<TLMessageBase>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLStatedMessageBase GetEmptyObject()
        {
            return new TLStatedMessage
            {
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Pts = Pts,
                Seq = Seq
            };
        }
    }

    public class TLStatedMessage24 : TLStatedMessageBase, IMultiPts
    {
        public const uint Signature = TLConstructors.TLStatedMessage24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Message = GetObject<TLMessageBase>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLStatedMessageBase GetEmptyObject()
        {
            return new TLStatedMessage24
            {
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Pts = Pts,
                PtsCount = PtsCount
            };
        }
    }

    public class TLStatedMessageLink : TLStatedMessage, ILinks
    {
        public new const uint Signature = TLConstructors.TLStatedMessageLink;

        public TLVector<TLLinkBase> Links { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Message = GetObject<TLMessageBase>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Links = GetObject<TLVector<TLLinkBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLStatedMessageBase GetEmptyObject()
        {
            return new TLStatedMessageLink
            {
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Links = new TLVector<TLLinkBase>(Links.Count),
                Pts = Pts,
                Seq = Seq
            };
        }
    }

    public class TLStatedMessageLink24 : TLStatedMessage24, ILinks
    {
        public new const uint Signature = TLConstructors.TLStatedMessageLink24;

        public TLVector<TLLinkBase> Links { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Message = GetObject<TLMessageBase>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            Links = GetObject<TLVector<TLLinkBase>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLStatedMessageBase GetEmptyObject()
        {
            return new TLStatedMessageLink24
            {
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Links = new TLVector<TLLinkBase>(Links.Count),
                Pts = Pts,
                PtsCount = PtsCount
            };
        }
    }
}
