namespace Telegram.Api.TL.Functions.Messages
{
    class TLRestoreMessages : TLObject
    {
        public const string Signature = "#395f9d7e";

        public TLVector<TLInt> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
