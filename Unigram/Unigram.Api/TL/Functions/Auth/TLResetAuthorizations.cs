namespace Telegram.Api.TL.Functions.Auth
{
    public class TLResetAuthorizations : TLObject
    {
        public const string Signature = "#9fab0d1a";

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature));
        }
    }
}
