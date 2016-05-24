namespace Telegram.Api.TL.Functions.Messages
{
    public class TLGetMessages : TLObject
    {
        public const string Signature = "#4222fa74";

        public TLVector<TLInt> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
