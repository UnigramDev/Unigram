namespace Telegram.Api.TL.Functions.Auth
{
    public class TLLogOut : TLObject
    {
        public const string Signature = "#5717da40";

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
        }
    }
}
