namespace Telegram.Api.TL.Account
{
    class TLGetPasswordSettings : TLObject
    {
        public const string Signature = "#bc8d11bb";

        public TLString CurrentPasswordHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                CurrentPasswordHash.ToBytes());
        }
    }
}
