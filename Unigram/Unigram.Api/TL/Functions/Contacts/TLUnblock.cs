namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLUnblock : TLObject
    {
        public const string Signature = "#e54100bd";

        public TLInputUserBase Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
