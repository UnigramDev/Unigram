using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Core.Common;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation.Metadata;

namespace Unigram.Services
{
    public interface IContactsService
    {
        Task<TdWindows.BaseObject> ImportAsync();
        Task ExportAsync(TdWindows.Users result);

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

        public async Task<TdWindows.BaseObject> ImportAsync()
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

        private async Task<TdWindows.BaseObject> ImportAsync(ContactStore store)
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

            var importedPhonesCache = GetImportedPhones();

            var importingContacts = new List<TdWindows.Contact>();
            var importingPhones = new List<string>();

            foreach (var phone in importedPhones.Keys.Take(1300).ToList())
            {
                if (!importedPhonesCache.ContainsKey(phone))
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
                        var item = new TdWindows.Contact
                        {
                            PhoneNumber = phone,
                            FirstName = firstName,
                            LastName = lastName
                        };

                        importingContacts.Add(item);
                        importingPhones.Add(phone);
                    }
                }
            }

            return await _protoService.SendAsync(new TdWindows.ChangeImportedContacts(importingContacts));
        }

        private void SaveImportedPhones(Dictionary<string, string> importedPhonesCache, List<string> importingPhones)
        {
            foreach (var current in importingPhones)
            {
                importedPhonesCache[current] = current;
            }

            var vector = new TLVector<string>(importedPhonesCache.Keys);

            lock (_importedPhonesRoot)
            {
                try
                {
                    var fileName = "importedPhones.dat";
                    var text = fileName + ".temp";
                    using (var file = File.Open(FileUtils.GetFileName(text), FileMode.Create))
                    {
                        using (var to = new BinaryWriter(file))
                        {
                            to.Write(vector.Count);

                            foreach (var line in vector)
                            {
                                to.Write(line);
                            }
                        }

                    }

                    File.Copy(FileUtils.GetFileName(text), FileUtils.GetFileName(fileName), true);
                }
                catch { }
            }
        }

        private Dictionary<string, string> GetImportedPhones()
        {
            var vector = new TLVector<string>();
            return vector.ToDictionary(x => x, y => y);
        }

        #endregion

        #region Export

        public async Task ExportAsync(TdWindows.Users result)
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

        private async Task ExportAsync(ContactList contactList, ContactAnnotationList annotationList, TdWindows.Users result)
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
