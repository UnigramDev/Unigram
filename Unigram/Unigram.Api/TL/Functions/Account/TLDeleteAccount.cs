namespace Telegram.Api.TL.Functions.Account
{
    public class TLDeleteAccount : TLObject
    {
        public const string Signature = "#418d4e0b";

        public TLString Reason { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Reason.ToBytes());
        }
    }
}
