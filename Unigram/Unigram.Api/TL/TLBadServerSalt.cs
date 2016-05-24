namespace Telegram.Api.TL
{
    public class TLBadServerSalt : TLObject
    {
        public const uint Signature = TLConstructors.TLBadServerSalt;

        public TLLong BadMessageId { get; set; }

        public TLInt BadMessageSeqNo { get; set; }

        public TLInt ErrorCode { get; set; }

        public TLLong NewServerSalt { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            BadMessageId = GetObject<TLLong>(bytes, ref position);
            BadMessageSeqNo = GetObject<TLInt>(bytes, ref position);
            ErrorCode = GetObject<TLInt>(bytes, ref position);
            NewServerSalt = GetObject<TLLong>(bytes, ref position);

            return this;
        }
    }
}
