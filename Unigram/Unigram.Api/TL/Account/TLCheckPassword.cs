namespace Telegram.Api.TL.Functions.Account
{
    class TLCheckPassword : TLObject
    {
        public const string Signature = "#a63011e";

        public TLString PasswordHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PasswordHash.ToBytes());
        }
    }
}
