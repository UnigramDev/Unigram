namespace Telegram.Api.TL.Functions.Messages
{
    class TLDeleteMessages : TLObject
    {
        public const string Signature = "#a5f18925";
   
        public TLVector<TLInt> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
