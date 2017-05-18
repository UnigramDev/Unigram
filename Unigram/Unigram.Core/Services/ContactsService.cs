using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Contacts;

namespace Unigram.Core.Services
{
    public interface IContactsService
    {
        Task SyncContactsAsync(TLContactsContactsBase result);

        Task UnsyncContactsAsync();
    }

    public class ContactsService : IContactsService
    {
        private readonly DisposableMutex _syncLock;

        public ContactsService()
        {
            _syncLock = new DisposableMutex();
        }

        public async Task SyncContactsAsync(TLContactsContactsBase result)
        {
            using (await _syncLock.WaitAsync())
            {
                Debug.WriteLine("SYNCING CONTACTS");

                var contactList = await GetContactListAsync();
                var annotationList = await GetAnnotationListAsync();

                if (contactList != null && annotationList != null)
                {
                    await ExportContacts(contactList, annotationList, result);
                }

                Debug.WriteLine("SYNCED CONTACTS");
            }
        }

        public async Task UnsyncContactsAsync()
        {
            using (await _syncLock.WaitAsync())
            {
                Debug.WriteLine("UNSYNCING CONTACTS");

                var contactList = await GetContactListAsync();
                var annotationList = await GetAnnotationListAsync();

                await contactList.DeleteAsync();
                await annotationList.DeleteAsync();

                Debug.WriteLine("UNSYNCED CONTACTS");
            }
        }

        private async Task ExportContacts(ContactList contactList, ContactAnnotationList annotationList, TLContactsContactsBase result)
        {
            var contacts = result as TLContactsContacts;
            if (contacts != null)
            {
                foreach (var item in contacts.Users.OfType<TLUser>())
                {
                    var contact = await contactList.GetContactFromRemoteIdAsync("u" + item.Id);
                    if (contact == null)
                    {
                        contact = new Contact();
                    }

                    contact.FirstName = item.FirstName ?? string.Empty;
                    contact.LastName = item.LastName ?? string.Empty;
                    contact.RemoteId = "u" + item.Id;
                    //contact.Id = item.Id.ToString();

                    var phone = contact.Phones.FirstOrDefault();
                    if (phone == null)
                    {
                        phone = new ContactPhone();
                        phone.Kind = ContactPhoneKind.Mobile;
                        phone.Number = string.Format("+{0}", item.Phone);
                        contact.Phones.Add(phone);
                    }
                    else
                    {
                        phone.Kind = ContactPhoneKind.Mobile;
                        phone.Number = string.Format("+{0}", item.Phone);
                    }

                    await contactList.SaveContactAsync(contact);

                    ContactAnnotation annotation;
                    var annotations = await annotationList.FindAnnotationsByRemoteIdAsync(item.Id.ToString());
                    if (annotations.Count == 0)
                    {
                        annotation = new ContactAnnotation();
                    }
                    else
                    {
                        annotation = annotations[0];
                    }

                    annotation.ContactId = contact.Id;
                    annotation.RemoteId = contact.RemoteId;
                    annotation.SupportedOperations = ContactAnnotationOperations.ContactProfile | ContactAnnotationOperations.Message | ContactAnnotationOperations.AudioCall;

                    if (annotation.ProviderProperties.Count == 0)
                    {
                        annotation.ProviderProperties.Add("ContactPanelAppID", Package.Current.Id.FamilyName + "!App");
                        annotation.ProviderProperties.Add("ContactShareAppID", Package.Current.Id.FamilyName + "!App");
                    }

                    await annotationList.TrySaveAnnotationAsync(annotation);
                }
            }
        }

        private async Task<ContactList> GetContactListAsync()
        {
            var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
            if (store == null) return null;

            ContactList contactList;
            var contactsList = await store.FindContactListsAsync();
            if (contactsList.Count == 0)
            {
                contactList = await store.CreateContactListAsync("Unigram");
            }
            else
            {
                contactList = contactsList[0];
            }

            return contactList;
        }

        private async Task<ContactAnnotationList> GetAnnotationListAsync()
        {
            var store = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
            if (store == null) return null;

            ContactAnnotationList contactList;
            var contactsList = await store.FindAnnotationListsAsync();
            if (contactsList.Count == 0)
            {
                contactList = await store.CreateAnnotationListAsync();
            }
            else
            {
                contactList = contactsList[0];
            }

            return contactList;
        }

    }
}
