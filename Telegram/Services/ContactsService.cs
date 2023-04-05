//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Contacts;
using Windows.ApplicationModel.UserDataAccounts;
using Windows.Storage;
using Windows.UI.StartScreen;

namespace Telegram.Services
{
    public interface IContactsService
    {
        Task JumpListAsync();

        Task SyncAsync(Telegram.Td.Api.Users result);

        Task<Telegram.Td.Api.BaseObject> ImportAsync();
        Task ExportAsync(Telegram.Td.Api.Users result);

        Task RemoveAsync();
    }

    public class ContactsService : IContactsService
    //, IHandle<Telegram.Td.Api.UpdateAuthorizationState>/*, IHandle<Telegram.Td.Api.UpdateUser>*/
    {
        private readonly IClientService _clientService;
        private readonly ISettingsService _settingsService;
        private readonly IEventAggregator _aggregator;

        private readonly DisposableMutex _syncLock;
        private readonly object _importedPhonesRoot;

        private HashSet<long> _contacts;

        private CancellationTokenSource _syncToken;

        public ContactsService(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _settingsService = settingsService;
            _aggregator = aggregator;

            _syncLock = new DisposableMutex();
            _importedPhonesRoot = new object();

            _contacts = new HashSet<long>();

            _aggregator.Subscribe<Telegram.Td.Api.UpdateAuthorizationState>(this, Handle);
        }

        public async void Handle(Telegram.Td.Api.UpdateAuthorizationState update)
        {
            if (update.AuthorizationState is Telegram.Td.Api.AuthorizationStateReady && _settingsService.IsContactsSyncEnabled)
            {
                await SyncAsync(await _clientService.SendAsync(new Telegram.Td.Api.GetContacts()) as Telegram.Td.Api.Users);
            }
        }

        public async void Handle(Telegram.Td.Api.UpdateUser update)
        {
            if (update.User.IsContact && !_contacts.Contains(update.User.Id))
            {
                // New contact
                await SyncAsync(await _clientService.SendAsync(new Telegram.Td.Api.GetContacts()) as Telegram.Td.Api.Users);
            }
            else if (_contacts.Contains(update.User.Id) && !update.User.IsContact)
            {
                // Deleted contact
                await SyncAsync(await _clientService.SendAsync(new Telegram.Td.Api.GetContacts()) as Telegram.Td.Api.Users);
            }
        }

        public async Task JumpListAsync()
        {
            if (JumpList.IsSupported())
            {
                var current = await JumpList.LoadCurrentAsync();
                current.SystemGroupKind = JumpListSystemGroupKind.None;
                current.Items.Clear();

                var cloud = JumpListItem.CreateWithArguments(string.Format("from_id={0}", _clientService.Options.MyId), Strings.SavedMessages);
                cloud.Logo = new Uri("ms-appx:///Assets/JumpList/SavedMessages/SavedMessages.png");

                current.Items.Add(cloud);

                await current.SaveAsync();
            }
        }

        public async Task SyncAsync(Telegram.Td.Api.Users result)
        {
            try
            {
                if (result == null)
                {
                    return;
                }

                _syncToken ??= new CancellationTokenSource();

                using (await _syncLock.WaitAsync(_syncToken.Token))
                {
                    await ExportAsyncInternal(result);
                    await ImportAsyncInternal();
                }
            }
            catch
            {
                Logs.Logger.Warning(Logs.LogTarget.Contacts, "Sync contacts canceled");
                Debug.WriteLine("» Sync contacts canceled");
            }
        }

        #region Import

        public async Task<Telegram.Td.Api.BaseObject> ImportAsync()
        {
            try
            {
                _syncToken ??= new CancellationTokenSource();

                using (await _syncLock.WaitAsync(_syncToken.Token))
                {
                    return await ImportAsyncInternal();
                }
            }
            catch
            {
                Logs.Logger.Warning(Logs.LogTarget.Contacts, "Sync contacts canceled");
                Debug.WriteLine("» Sync contacts canceled");
            }

            return null;
        }

        private async Task<Telegram.Td.Api.BaseObject> ImportAsyncInternal()
        {
            Telegram.Td.Api.BaseObject result = null;

            Logs.Logger.Info(Logs.LogTarget.Contacts, "Importing contacts");
            Debug.WriteLine("» Importing contacts");

            var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AllContactsReadOnly);
            if (store != null)
            {
                result = await ImportAsync(store);
            }

            Logs.Logger.Info(Logs.LogTarget.Contacts, "Importing contacts completed");
            Debug.WriteLine("» Importing contacts completed");

            return result;
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

            foreach (var phone in importedPhones.Keys.ToList())
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

            return await _clientService.SendAsync(new Telegram.Td.Api.ChangeImportedContacts(importingContacts));
        }

        #endregion

        #region Export

        public async Task ExportAsync(Telegram.Td.Api.Users result)
        {
            try
            {
                _syncToken ??= new CancellationTokenSource();

                using (await _syncLock.WaitAsync(_syncToken.Token))
                {
                    await ExportAsyncInternal(result);
                }
            }
            catch
            {
                Logs.Logger.Warning(Logs.LogTarget.Contacts, "Sync contacts canceled");
                Debug.WriteLine("» Sync contacts canceled");
            }
        }

        private async Task ExportAsyncInternal(Telegram.Td.Api.Users result)
        {
            Logs.Logger.Info(Logs.LogTarget.Contacts, "Exporting contacts");
            Debug.WriteLine("» Exporting contacts");

            var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
            if (store == null)
            {
                return;
            }

            var userDataAccount = await GetUserDataAccountAsync();
            if (userDataAccount == null)
            {
                return;
            }

            var contactList = await GetContactListAsync(userDataAccount, store);
            var annotationList = await GetAnnotationListAsync(userDataAccount);

            if (contactList != null && annotationList != null)
            {
                await ExportAsync(contactList, annotationList, result);
            }

            Logs.Logger.Info(Logs.LogTarget.Contacts, "Exporting contacts completed");
            Debug.WriteLine("» Exporting contacts completed");
        }

        private async Task ExportAsync(ContactList contactList, ContactAnnotationList annotationList, Telegram.Td.Api.Users result)
        {
            if (result == null)
            {
                return;
            }

            var remove = new List<long>();

            var prev = _contacts;
            var next = result.UserIds.ToHashSet();

            if (prev != null)
            {
                foreach (var id in prev)
                {
                    if (!next.Contains(id))
                    {
                        remove.Add(id);
                    }
                }
            }

            _contacts = next;

            foreach (var item in remove)
            {
                var contact = await contactList.GetContactFromRemoteIdAsync("u" + item);
                if (contact != null)
                {
                    await contactList.DeleteContactAsync(contact);
                }
            }

            foreach (var item in result.UserIds)
            {
                var user = _clientService.GetUser(item);

                var contact = await contactList.GetContactFromRemoteIdAsync("u" + user.Id);
                contact ??= new Contact();

                if (user.ProfilePhoto != null && user.ProfilePhoto.Small.Local.IsDownloadingCompleted)
                {
                    contact.SourceDisplayPicture = await StorageFile.GetFileFromPathAsync(user.ProfilePhoto.Small.Local.Path);
                }

                contact.FirstName = user.FirstName;
                contact.LastName = user.LastName;
                //contact.Nickname = item.Username ?? string.Empty;
                contact.RemoteId = "u" + user.Id;
                //contact.Id = item.Id.ToString();

                if (user.PhoneNumber.Length > 0)
                {
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
                annotation.SupportedOperations = ContactAnnotationOperations.ContactProfile
                    | ContactAnnotationOperations.Message
                    | ContactAnnotationOperations.AudioCall
                    | ContactAnnotationOperations.VideoCall
                    | ContactAnnotationOperations.Share;

                if (annotation.ProviderProperties.Count == 0)
                {
                    annotation.ProviderProperties.Add("ContactPanelAppID", Package.Current.Id.FamilyName + "!App");
                    annotation.ProviderProperties.Add("ContactShareAppID", Package.Current.Id.FamilyName + "!App");
                }

                await annotationList.TrySaveAnnotationAsync(annotation);
            }
        }

        private async Task<UserDataAccount> GetUserDataAccountAsync()
        {
            try
            {
                var store = await UserDataAccountManager.RequestStoreAsync(UserDataAccountStoreAccessType.AppAccountsReadWrite);

                UserDataAccount userDataAccount = null;
                if (_clientService.Options.TryGetValue("x_user_data_account", out string id))
                {
                    userDataAccount = await store.GetAccountAsync(id);
                }

                if (userDataAccount == null)
                {
                    userDataAccount = await store.CreateAccountAsync($"{_clientService.Options.MyId}");
                    await _clientService.SendAsync(new Telegram.Td.Api.SetOption("x_user_data_account", new Telegram.Td.Api.OptionValueString(userDataAccount.Id)));
                }

                return userDataAccount;
            }
            catch
            {
                return null;
            }
        }

        private async Task<ContactList> GetContactListAsync(UserDataAccount userDataAccount, ContactStore store)
        {
            try
            {
                var user = _clientService.GetUser(_clientService.Options.MyId);
                var displayName = user?.FullName() ?? "Unigram";

                ContactList contactList = null;
                if (_clientService.Options.TryGetValue("x_contact_list", out string id))
                {
                    contactList = await store.GetContactListAsync(id);
                }

                if (contactList == null)
                {
                    contactList = await store.CreateContactListAsync(displayName, userDataAccount.Id);
                    await _clientService.SendAsync(new Telegram.Td.Api.SetOption("x_contact_list", new Telegram.Td.Api.OptionValueString(contactList.Id)));
                }

                contactList.DisplayName = displayName;
                contactList.OtherAppWriteAccess = ContactListOtherAppWriteAccess.None;
                await contactList.SaveAsync();

                return contactList;
            }
            catch
            {
                return null;
            }
        }

        private async Task<ContactAnnotationList> GetAnnotationListAsync(UserDataAccount userDataAccount)
        {
            try
            {
                var store = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
                if (store == null)
                {
                    return null;
                }

                ContactAnnotationList contactList = null;
                if (_clientService.Options.TryGetValue("x_annotation_list", out string id))
                {
                    contactList = await store.GetAnnotationListAsync(id);
                }

                if (contactList == null)
                {
                    contactList = await store.CreateAnnotationListAsync(userDataAccount.Id);
                    await _clientService.SendAsync(new Telegram.Td.Api.SetOption("x_annotation_list", new Telegram.Td.Api.OptionValueString(contactList.Id)));
                }

                return contactList;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        public async Task RemoveAsync()
        {
            if (_syncToken != null)
            {
                _syncToken.Cancel();
                _syncToken = null;
            }

            using (await _syncLock.WaitAsync())
            {
                Debug.WriteLine("UNSYNCING CONTACTS");

                var userDataAccount = await GetUserDataAccountAsync();
                if (userDataAccount == null)
                {
                    return;
                }

                await userDataAccount.DeleteAsync();

                await _clientService.SendAsync(new Telegram.Td.Api.SetOption("x_user_data_account", new Telegram.Td.Api.OptionValueEmpty()));
                await _clientService.SendAsync(new Telegram.Td.Api.SetOption("x_contact_list", new Telegram.Td.Api.OptionValueEmpty()));
                await _clientService.SendAsync(new Telegram.Td.Api.SetOption("x_annotation_list", new Telegram.Td.Api.OptionValueEmpty()));

                Debug.WriteLine("UNSYNCED CONTACTS");
            }
        }

        public static Task<long?> GetContactIdAsync(Contact contact)
        {
            if (contact == null)
            {
                return Task.FromResult<long?>(null);
            }

            return GetContactIdAsync(contact.Id);
        }

        public static async Task<long?> GetContactIdAsync(string contactId)
        {
            var annotationStore = await ContactManager.RequestAnnotationStoreAsync(ContactAnnotationStoreAccessType.AppAnnotationsReadWrite);
            var store = await ContactManager.RequestStoreAsync(ContactStoreAccessType.AppContactsReadWrite);
            if (store != null && annotationStore != null)
            {
                try
                {
                    var full = await store.GetContactAsync(contactId);
                    if (full == null)
                    {
                        return null;
                    }

                    var annotations = await annotationStore.FindAnnotationsForContactAsync(full);

                    var first = annotations.FirstOrDefault();
                    if (first == null)
                    {
                        return null;
                    }

                    var remote = first.RemoteId;
                    if (long.TryParse(remote.Substring(1), out long userId))
                    {
                        return userId;
                    }
                }
                catch (Exception ex)
                {
                    if ((uint)ex.HResult == 0x80004004)
                    {
                        return null;
                    }
                }
            }

            return null;
        }
    }
}
