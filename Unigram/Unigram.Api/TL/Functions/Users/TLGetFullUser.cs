namespace Telegram.Api.TL.Functions.Users
{
    public class TLGetFullUser : TLObject
    {
        public const string Signature = "#ca30a5b1";

        public TLInputUserBase Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
