namespace Telegram.Api.TL.Functions.Account
{
    class TLChangePhone : TLObject
    {
        public const string Signature = "#70c32edb";

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