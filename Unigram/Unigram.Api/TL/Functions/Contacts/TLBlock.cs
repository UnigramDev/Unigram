namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLBlock : TLObject
    {
        public const string Signature = "#332b49fc";

        public TLInputUserBase Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
