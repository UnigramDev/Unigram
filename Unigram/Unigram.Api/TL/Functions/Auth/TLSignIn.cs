namespace Telegram.Api.TL.Functions.Auth
{
    class TLSignIn : TLObject
    {
        public const string Signature = "#bcd51581";

        public TLString PhoneNumber { get; set; }

        public TLString PhoneCodeHash { get; set; }

        public TLString PhoneCode { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneNumber.ToBytes(),
                PhoneCodeHash.ToBytes(),
                PhoneCode.ToBytes());
        }
    }
}
