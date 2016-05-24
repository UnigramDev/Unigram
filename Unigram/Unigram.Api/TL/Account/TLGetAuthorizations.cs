namespace Telegram.Api.TL.Account
{
    class TLGetAuthorizations : TLObject
    {
        public const string Signature = "#e320c158";

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
        }
    }
}
