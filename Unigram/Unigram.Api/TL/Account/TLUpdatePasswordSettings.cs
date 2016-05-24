namespace Telegram.Api.TL.Account
{
    class TLUpdatePasswordSettings : TLObject
    {
        public const string Signature = "#fa7c4b86";

        public TLString CurrentPasswordHash { get; set; }

        public TLPasswordInputSettings NewSettings { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                CurrentPasswordHash.ToBytes(),
                NewSettings.ToBytes());
        }
    }
}
