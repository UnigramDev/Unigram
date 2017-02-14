using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.ApplicationModel.Contacts;

namespace Unigram.Core.Services
{
    public interface IContactsService
    {
        Task SyncContactsAsync(TLContactsContactsBase result);
    }

    public class ContactsService : IContactsService
    {
        public async Task SyncContactsAsync(TLContactsContactsBase result)
        {
            var contactList = await GetContactListAsync();
            var annotationList = await GetAnnotationListAsync();

            if (contactList != null && annotationList != null)
            {
                await ExportContacts(contactList, annotationList, result);
            }
        }

        private async Task ExportContacts(ContactList contactList, ContactAnnotationList annotationList, TLContactsContactsBase result)
        {
            var contacts = result as TLContactsContacts;
            if (contacts != null)
            {
                foreach (var item in contacts.Users.OfType<TLUser>())
                {
                    var contact = await contactList.GetContactFromRemoteIdAsync(item.Id.ToString());
                    if (contact == null)
                    {
                        contact = new Contact();
                    }

                    contact.FirstName = item.FirstName ?? string.Empty;
                    contact.LastName = item.LastName ?? string.Empty;
                    contact.RemoteId = item.Id.ToString();
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
