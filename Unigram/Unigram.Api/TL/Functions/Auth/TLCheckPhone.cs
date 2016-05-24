namespace Telegram.Api.TL.Functions.Auth
{
    public class TLCheckPhone : TLObject
    {
        public const string Signature = "#6fe51dfb";

        public TLString PhoneNumber { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneNumber.ToBytes());
        }
    }
}
