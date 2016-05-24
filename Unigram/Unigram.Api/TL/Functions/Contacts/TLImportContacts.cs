namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLImportContacts : TLObject
    {
        public const string Signature = "#da30b32d";

        public TLVector<TLInputContactBase> Contacts { get; set; }

        public TLBool Replace { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Contacts.ToBytes(),
                Replace.ToBytes());
        }
    }
}
