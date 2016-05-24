namespace Telegram.Api.TL.Functions.Messages
{
    public class TLGetChats : TLObject
    {
#if LAYER_40
        public const string Signature = "#3c6aa187";

        public TLVector<TLInt> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
#else
        public const string Signature = "#3c6aa187";

        public TLVector<TLInt> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
#endif
    }
}
