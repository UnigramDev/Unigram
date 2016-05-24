namespace Telegram.Api.TL.Account
{
    class TLResetAuthorization : TLObject
    {
        public const string Signature = "#df77f3bc";

        public TLLong Hash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Hash.ToBytes());
        }
    }
}
