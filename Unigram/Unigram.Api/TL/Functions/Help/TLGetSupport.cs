namespace Telegram.Api.TL.Functions.Help
{
    public class TLGetSupport : TLObject
    {
        public const string Signature = "#9cdf08cd";

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }
}
