namespace Telegram.Api.TL.Account
{
    class TLGetWallPapers : TLObject
    {
        public const string Signature = "#c04cfac2";

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(TLUtils.SignatureToBytes(Signature));
        }
    }
}
