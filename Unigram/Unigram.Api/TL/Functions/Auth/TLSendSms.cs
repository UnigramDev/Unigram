namespace Telegram.Api.TL.Functions.Auth
{
    public class TLSendSms : TLObject
    {
        public const string Signature = "#da9f3e8";

        public TLString PhoneNumber { get; set; }

        public TLString PhoneCodeHash { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneNumber.ToBytes(),
                PhoneCodeHash.ToBytes());
        }
    }
}
