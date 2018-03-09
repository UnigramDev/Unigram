using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unigram.Common;
using Unigram.Core.Common;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation.Metadata;
using Windows.Storage;

namespace Unigram.Services
{
    public interface IContactsService
    {
        Task<Telegram.Td.Api.BaseObject> ImportAsync();
        Task ExportAsync(Telegram.Td.Api.Users result);

        Task RemoveAsync();
    }

    public class ContactsService : IContactsService
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private readonly DisposableMutex _syncLock;
        private readonly object _importedPhonesRoot;

        public ContactsService(IProtoService protoService, IEventAggregator aggregator)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _syncLock = new DisposableMutex();
            _importedPhonesRoot = new object();
        }

        #region Import

        public async Task<Telegram.Td.Api.BaseObject> ImportAsync()
        {
            using (await _syncLock.WaitAsync())
            {
                Debug.WriteLine("» Importing contacts");

                var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AllContactsReadOnly);
                if (store != null)
                {
                    return await ImportAsync(store);
                }

                Debug.WriteLine("» Importing contacts completed");
            }

            return null;
        }

        private async Task<Telegram.Td.Api.BaseObject> ImportAsync(ContactStore store)
        {
            var contacts = await store.FindContactsAsync();
            var importedPhones = new Dictionary<string, Contact>();

            foreach (var contact in contacts)
            {
                foreach (var phone in contact.Phones)
                {
                    importedPhones[phone.Number] = contact;
                }
            }

            var importingContacts = new List<Telegram.Td.Api.Contact>();

            foreach (var phone in importedPhones.Keys.Take(1300).ToList())
            {
                var contact = importedPhones[phone];
                var firstName = contact.FirstName ?? string.Empty;
                var lastName = contact.LastName ?? string.Empty;

                if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
                {
                    if (string.IsNullOrEmpty(contact.DisplayName))
                    {
                        continue;
                    }

                    firstName = contact.DisplayName;
                }

                if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
                {
                    var item = new Telegram.Td.Api.Contact
                    {
                        PhoneNumber = phone,
                        FirstName = firstName,
                        LastName = lastName
                    };

                    importingContacts.Add(item);
                }
            }

            return await _protoService.SendAsync(new Telegram.Td.Api.ChangeImportedContacts(importingContacts));
        }

        #endregion

        #region Export

        public async Task ExportAsync(Telegram.Td.Api.Users result)
        {
            using (await _syncLock.WaitAsync())
            {
                Debug.WriteLine("» Exporting contacts");

                var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
                if (store == null)
                {
                    return;
                }

                var contactList = await GetContactListAsync(store);
                var annotationList = await GetAnnotationListAsync();

                if (contactList != null && annotationList != null)
                {
                    await ExportAsync(contactList, annotationList, result);
                }

                Debug.WriteLine("» Exporting contacts completed");
            }
        }

        private async Task ExportAsync(ContactList contactList, ContactAnnotationList annotationList, Telegram.Td.Api.Users result)
        {
            if (result == null)
            {
                return;
            }

            foreach (var item in result.UserIds)
            {
                var user = _protoService.GetUser(item);

                var contact = await contactList.GetContactFromRemoteIdAsync("u" + user.Id);
                if (contact == null)
                {
                    contact = new Contact();
                }

                if (user.ProfilePhoto != null && user.ProfilePhoto.Small.Local.IsDownloadingCompleted)
                {
                    contact.SourceDisplayPicture = await StorageFile.GetFileFromPathAsync(user.ProfilePhoto.Small.Local.Path);
                }

                contact.FirstName = user.FirstName ?? string.Empty;
                contact.LastName = user.LastName ?? string.Empty;
                //contact.Nickname = item.Username ?? string.Empty;
                contact.RemoteId = "u" + user.Id;
                //contact.Id = item.Id.ToString();

                var phone = contact.Phones.FirstOrDefault();
                if (phone == null)
                {
                    phone = new ContactPhone();
                    phone.Kind = ContactPhoneKind.Mobile;
                    phone.Number = string.Format("+{0}", user.PhoneNumber);
                    contact.Phones.Add(phone);
                }
                else
                {
                    phone.Kind = ContactPhoneKind.Mobile;
                    phone.Number = string.Format("+{0}", user.PhoneNumber);
                }

                await contactList.SaveContactAsync(contact);

                ContactAnnotation annotation;
                var annotations = await annotationList.FindAnnotationsByRemoteIdAsync(user.Id.ToString());
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

                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
                {
                    annotation.SupportedOperations |= ContactAnnotationOperations.Share;
                }

                if (annotation.ProviderProperties.Count == 0)
                {
                    annotation.ProviderProperties.Add("ContactPanelAppID", Package.Current.Id.FamilyName + "!App");
                    annotation.ProviderProperties.Add("ContactShareAppID", Package.Current.Id.FamilyName + "!App");
                }

                var added = await annotationList.TrySaveAnnotationAsync(annotation);
            }
        }

        private async Task<ContactList> GetContactListAsync(ContactStore store)
        {
            ContactList contactList;
            var contactsList = await store.FindContactListsAsync();
            if (contactsList.Count == 0)
            {
                contactList = await store.CreateContactListAsync("Unigram");
                contactList.OtherAppWriteAccess = ContactListOtherAppWriteAccess.None;
                await contactList.SaveAsync();
            }
            else
            {
                contactList = contactsList[0];

                if (contactList.OtherAppWriteAccess != ContactListOtherAppWriteAccess.None)
                {
                    contactList.OtherAppWriteAccess = ContactListOtherAppWriteAccess.None;
                    await contactList.SaveAsync();
                }
            }

            return contactList;
        }

        private async Task<ContactAnnotationList> GetAnnotationListAsync()
        {
            var store = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
            if (store == null)
            {
                return null;
            }

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

        #endregion

        public async Task RemoveAsync()
        {
            using (await _syncLock.WaitAsync())
            {
                Debug.WriteLine("UNSYNCING CONTACTS");

                var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
                if (store == null)
                {
                    return;
                }

                var contactList = await GetContactListAsync(store);
                var annotationList = await GetAnnotationListAsync();

                await contactList.DeleteAsync();
                await annotationList.DeleteAsync();

                Debug.WriteLine("UNSYNCED CONTACTS");
            }
        }
    }
}
