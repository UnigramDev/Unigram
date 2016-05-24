namespace Telegram.Api.TL
{
    public class TLImportedContacts : TLObject
    {
        public const uint Signature = TLConstructors.TLImportedContacts;

        public TLVector<TLImportedContact> Imported { get; set; }

        public TLVector<TLLong> RetryContacts { get; set; } 

        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Imported = GetObject<TLVector<TLImportedContact>>(bytes, ref position);
            RetryContacts = GetObject<TLVector<TLLong>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public TLImportedContacts GetEmptyObject()
        {
            return new TLImportedContacts
            {
                Imported = new TLVector<TLImportedContact>(Imported.Count),
                RetryContacts = new TLVector<TLLong>(RetryContacts.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }
}
