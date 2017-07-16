using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL.Contacts
{
    public abstract partial class TLContactsContactsBase
    {
        public abstract TLContactsContactsBase GetEmptyObject();
    }

    public partial class TLContactsContacts
    {
        public override TLContactsContactsBase GetEmptyObject()
        {
            return new TLContactsContacts
            {
                Contacts = new TLVector<TLContact>(Contacts.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public partial class TLContactsContactsNotModified
    {
        public override TLContactsContactsBase GetEmptyObject()
        {
            return new TLContactsContactsNotModified();
        }
    }
}
