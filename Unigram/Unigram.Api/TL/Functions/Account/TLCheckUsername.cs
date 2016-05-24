namespace Telegram.Api.TL.Account
{
    public class TLCheckUsername : TLObject
    {
        public const string Signature = "#2714d86c";

        public TLString Username { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Username.ToBytes());
        }
    }
}
