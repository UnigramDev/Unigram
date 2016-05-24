namespace Telegram.Api.TL.Functions.Auth
{
    public class TLSendCall : TLObject
    {
        public const string Signature = "#3c51564";

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
