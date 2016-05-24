namespace Telegram.Api.TL
{
    public class TLMessageDetailedInfoBase : TLObject
    {
        public TLLong AnswerMessageId { get; set; }

        public TLInt Bytes { get; set; }

        public TLInt Status { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            AnswerMessageId = GetObject<TLLong>(bytes, ref position);
            Bytes = GetObject<TLInt>(bytes, ref position);
            Status = GetObject<TLInt>(bytes, ref position);

            return this;
        }
    }

    public class TLMessageDetailedInfo : TLMessageDetailedInfoBase
    {
        public const uint Signature = TLConstructors.TLMessageDetailedInfo;

        public TLLong MessageId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            MessageId = GetObject<TLLong>(bytes, ref position);

            return base.FromBytes(bytes, ref position);
        }
    }

    public class TLMessageNewDetailedInfo : TLMessageDetailedInfoBase
    {
        public const uint Signature = TLConstructors.TLMessageNewDetailedInfo;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return base.FromBytes(bytes, ref position);
        }
    }

    public class TLMessagesAllInfo : TLObject
    {
        public const uint Signature = TLConstructors.TLMessagesAllInfo;

        public TLVector<TLLong> MessageIds { get; set; } 

        public TLString Info { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            MessageIds = GetObject<TLVector<TLLong>>(bytes, ref position);
            Info = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }
}
