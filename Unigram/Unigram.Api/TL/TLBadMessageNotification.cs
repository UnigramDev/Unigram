namespace Telegram.Api.TL
{
    public class TLBadMessageNotification : TLObject
    {
        public const uint Signature = TLConstructors.TLBadMessageNotification;

        public TLLong BadMessageId { get; set; }

        public TLInt BadMessageSequenceNumber { get; set; }

        public TLInt ErrorCode { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            BadMessageId = GetObject<TLLong>(bytes, ref position);
            BadMessageSequenceNumber = GetObject<TLInt>(bytes, ref position);
            ErrorCode = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }
    }
}