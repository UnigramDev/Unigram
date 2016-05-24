namespace Telegram.Api.TL.Functions.Help
{
    public class TLGetNearestDC : TLObject
    {
        public const string Signature = "#1fb33026";

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }
}
