namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLDeleteContact : TLObject
    {
        public const string Signature = "#8e953744";

        public TLInputUserBase Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
