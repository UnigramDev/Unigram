namespace Telegram.Api.TL.Functions.Account
{
    class TLGetPassword : TLObject
    {
        public const string Signature = "#548a30f5";

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
        }
    }
}
