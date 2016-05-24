namespace Telegram.Api.TL
{
    public class TLInvokeAfterMsg : TLObject
    {
        public const uint Signature = TLConstructors.TLInvokeAfterMsg;

        public TLLong MsgId { get; set; }

        public TLObject Object { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                MsgId.ToBytes(),
                Object.ToBytes());
        }
    }
}
