namespace Telegram.Api.TL.Functions.Messages
{
    public class TLDiscardEncryption : TLObject
    {
        public const string Signature = "#edd923c5";

        public TLInt ChatId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ChatId.ToBytes());
        }
    }
}