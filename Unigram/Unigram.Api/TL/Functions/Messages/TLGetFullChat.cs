namespace Telegram.Api.TL.Functions.Messages
{
    class TLGetFullChat : TLObject
    {
#if LAYER_40
        public const string Signature = "#3b831c66";

        public TLInt ChatId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes());
        }
#else
        public const string Signature = "#3b831c66";

        public TLInt ChatId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes());
        }
#endif
    }
}
