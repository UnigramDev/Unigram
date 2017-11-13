using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.TL;
using Telegram.Api.TL.Contacts;
using Unigram.Common;
using Unigram.Core.Common;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation.Metadata;

namespace Unigram.Core.Services
{
    public interface IContactsService
    {
        Task ImportAsync();
        Task ExportAsync(TLContactsContacts result);

        Task RemoveAsync();
    }

    public class ContactsService : IContactsService
    {
        private readonly IMTProtoService _protoService;
        private readonly ITelegramEventAggregator _aggregator;

        private readonly DisposableMutex _syncLock;
        private readonly object _importedPhonesRoot;

        public ContactsService(IMTProtoService protoService, ITelegramEventAggregator aggregator)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _syncLock = new DisposableMutex();
            _importedPhonesRoot = new object();
        }

        #region Import

        public async Task ImportAsync()
        {
            using (await _syncLock.WaitAsync())
            {
                Debug.WriteLine("» Importing contacts");

                var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AllContactsReadOnly);
                if (store != null)
                {
                    await ImportAsync(store);
                }

                Debug.WriteLine("» Importing contacts completed");
            }
        }

        private async Task ImportAsync(ContactStore store)
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

            var importingContacts = new TLVector<TLInputContactBase>();
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
                        var item = new TLInputPhoneContact
                        {
                            Phone = phone,
                            FirstName = firstName,
                            LastName = lastName,
                            ClientId = importedPhones[phone].GetHashCode()
                        };

                        importingContacts.Add(item);
                        importingPhones.Add(phone);
                    }
                }
            }

            if (importingContacts.IsEmpty())
            {
                return;
            }

            //base.IsWorking = true;
            _protoService.ImportContactsAsync(importingContacts, result =>
            {
                //Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                //{
                //    this.IsWorking = false;
                //    this.Status = ((this.Items.get_Count() == 0 && this.LazyItems.get_Count() == 0 && result.Users.Count == 0) ? string.Format("{0}", AppResources.NoContactsHere) : string.Empty);
                //    int count = result.RetryContacts.Count;
                //    if (count > 0)
                //    {
                //        Telegram.Api.Helpers.Execute.ShowDebugMessage("contacts.importContacts error: retryContacts count=" + count);
                //    }
                //    this.InsertContacts(result.Users);
                //});

                _aggregator.Publish(new TLUpdateContactsReset());
                SaveImportedPhones(importedPhonesCache, importingPhones);
            }, 
            fault =>
            {
                Telegram.Api.Helpers.Execute.BeginOnUIThread(delegate
                {
                    //this.IsWorking = false;
                    //this.Status = string.Empty;
                    Telegram.Api.Helpers.Execute.ShowDebugMessage("contacts.importContacts error: " + fault);
                });
            });
        }

        private void SaveImportedPhones(Dictionary<string, string> importedPhonesCache, List<string> importingPhones)
        {
            foreach (var current in importingPhones)
            {
                importedPhonesCache[current] = current;
            }

            var vector = new TLVector<string>(importedPhonesCache.Keys);
            TLUtils.SaveObjectToMTProtoFile(_importedPhonesRoot, "importedPhones.dat", vector);
        }

        private Dictionary<string, string> GetImportedPhones()
        {
            var vector = TLUtils.OpenObjectFromMTProtoFile<TLVector<string>>(_importedPhonesRoot, "importedPhones.dat") ?? new TLVector<string>();
            return vector.ToDictionary(x => x, y => y);
        }

        #endregion

        #region Export

        public async Task ExportAsync(TLContactsContacts result)
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

        private async Task ExportAsync(ContactList contactList, ContactAnnotationList annotationList, TLContactsContactsBase result)
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
                    //contact.Nickname = item.Username ?? string.Empty;
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

                    if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 5))
                    {
                        annotation.SupportedOperations |= ContactAnnotationOperations.Share;
                    }

                    if (annotation.ProviderProperties.Count == 0)
                    {
                        annotation.ProviderProperties.Add("ContactPanelAppID", Package.Current.Id.FamilyName + "!App");
                        annotation.ProviderProperties.Add("ContactShareAppID", Package.Current.Id.FamilyName + "!App");
                    }

                    await annotationList.TrySaveAnnotationAsync(annotation);
                }
            }
        }

        private async Task<ContactList> GetContactListAsync(ContactStore store)
        {
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
