namespace Telegram.Api.TL.Functions.Account
{
    public class TLUpdateProfile : TLObject
    {
        public const string Signature = "#f0888d68";

        public TLString FirstName { get; set; }

        public TLString LastName { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                FirstName.ToBytes(),
                LastName.ToBytes());
        }
    }
}
