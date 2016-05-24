namespace Telegram.Api.TL
{
    public abstract class TLContactsBase : TLObject
    {
        public abstract TLContactsBase GetEmptyObject();
    }

    public class TLContacts : TLContactsBase
    {
        public const uint Signature = TLConstructors.TLContacts;

        public TLVector<TLUserBase> Users { get; set; }

        public TLVector<TLContact> Contacts { get; set; } 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Contacts = GetObject<TLVector<TLContact>>(bytes, ref position);

            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLContactsBase GetEmptyObject()
        {
            return new TLContacts
            {
                Contacts = new TLVector<TLContact>(Contacts.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public class TLContactsNotModified : TLContactsBase
    {
        public const uint Signature = TLConstructors.TLContactsNotModified;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override TLContactsBase GetEmptyObject()
        {
            return new TLContactsNotModified();
        }
    }
}
