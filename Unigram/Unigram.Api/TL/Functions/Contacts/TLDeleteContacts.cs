namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLDeleteContacts : TLObject
    {
        public const string Signature = "#59ab389e";

        public TLVector<TLInputUserBase> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
