namespace Telegram.Api.TL.Account
{
    public class TLUpdateUserName : TLObject
    {
        public const string Signature = "#3e0bdd7c";

        public TLString Username { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Username.ToBytes());
        }
    }
}
