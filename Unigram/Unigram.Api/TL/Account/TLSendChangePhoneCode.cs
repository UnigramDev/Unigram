namespace Telegram.Api.TL.Functions.Account
{
    class TLSendChangePhoneCode : TLObject
    {
        public const string Signature = "#a407a8f4";

        public TLString PhoneNumber { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneNumber.ToBytes());
        }
    }
}
