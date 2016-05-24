namespace Telegram.Api.TL.Functions.Messages
{
    public class TLGetWebPagePreview : TLObject
    {
        public const string Signature = "#25223e24";

        public TLString Message { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Message.ToBytes());
        }
    }
}
