using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL.Contacts
{
    public partial class TLContactsImportedContacts
    {
        public TLContactsImportedContacts GetEmptyObject()
        {
            return new TLContactsImportedContacts
            {
                Imported = new TLVector<TLImportedContact>(Imported.Count),
                PopularInvites = new TLVector<TLPopularContact>(PopularInvites.Count),
                RetryContacts = new TLVector<long>(RetryContacts.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }
}
