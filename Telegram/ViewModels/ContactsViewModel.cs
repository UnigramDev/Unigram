//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;

namespace Telegram.ViewModels
{
    public class ContactsViewModel : ViewModelBase, IChildViewModel
    {
        private readonly IContactsService _contactsService;

        private readonly DisposableMutex _loadMoreLock;

        public ContactsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, IContactsService contactsService)
            : base(clientService, settingsService, aggregator)
        {
            _contactsService = contactsService;

            _loadMoreLock = new DisposableMutex();

            Items = new SortedObservableCollection<User>(new UserComparer(Settings.IsContactsSortedByEpoch));

            Initialize();
        }

        private async void Initialize()
        {
            if (!Settings.IsContactsSyncRequested)
            {
                Settings.IsContactsSyncRequested = true;

                var confirm = await ShowPopupAsync(Strings.ContactsPermissionAlert, Strings.AppName, Strings.ContactsPermissionAlertContinue, Strings.ContactsPermissionAlertNotNow);
                if (confirm != Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    Settings.IsContactsSyncEnabled = false;
                }

                if (Settings.IsContactsSyncEnabled)
                {
                    ClientService.Send(new GetContacts(), async result =>
                    {
                        if (result is Telegram.Td.Api.Users users)
                        {
                            await _contactsService.SyncAsync(users);
                        }
                    });
                }
            }
        }

        public async void Activate()
        {
            Aggregator.Subscribe<UpdateUserStatus>(this, Handle)
                .Subscribe<UpdateUserIsContact>(Handle);

            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ClientService.SendAsync(new GetContacts());
                if (response is Telegram.Td.Api.Users users)
                {
                    var items = new List<User>();

                    foreach (var id in users.UserIds)
                    {
                        var user = ClientService.GetUser(id);
                        if (user != null)
                        {
                            items.Add(user);
                        }
                    }

                    Items.ReplaceWith(items);
                }
            }
        }

        public void Deactivate()
        {
            Aggregator.Unsubscribe(this);
        }

        public bool IsSortedByEpoch
        {
            get => Settings.IsContactsSortedByEpoch;
            set
            {
                if (Settings.IsContactsSortedByEpoch != value)
                {
                    Settings.IsContactsSortedByEpoch = value;
                    Load(value);

                    RaisePropertyChanged(nameof(IsSortedByEpoch));
                }
            }
        }

        private async void Load(bool epoch)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ClientService.SendAsync(new GetContacts());
                if (response is Telegram.Td.Api.Users users)
                {
                    var items = new List<User>();

                    foreach (var id in users.UserIds)
                    {
                        var user = ClientService.GetUser(id);
                        if (user != null)
                        {
                            items.Add(user);
                        }
                    }

                    Items = new SortedObservableCollection<User>(new UserComparer(epoch));
                    Items.ReplaceWith(items);
                    RaisePropertyChanged(nameof(Items));
                }
            }
        }

        private SearchUsersCollection _search;
        public SearchUsersCollection Search
        {
            get => _search;
            set => Set(ref _search, value);
        }

        #region Handle

        public void Handle(UpdateUserStatus update)
        {
            var user = ClientService.GetUser(update.UserId);
            if (user == null || user.Id == ClientService.Options.MyId || !user.IsContact)
            {
                return;
            }

            BeginOnUIThread(() =>
            {
                var first = Items.FirstOrDefault(x => x != null && x.Id == update.UserId);
                if (first != null)
                {
                    Items.Remove(first);
                }

                Items.Add(user);
            });
        }

        public void Handle(UpdateUserIsContact update)
        {
            var user = ClientService.GetUser(update.UserId);
            if (user == null || user.Id == ClientService.Options.MyId)
            {
                return;
            }

            BeginOnUIThread(() =>
            {
                var first = Items.FirstOrDefault(x => x != null && x.Id == update.UserId);
                if (first != null)
                {
                    Items.Remove(first);
                }

                if (user.IsContact)
                {
                    Items.Add(user);
                }
            });
        }

        #endregion

        public SortedObservableCollection<User> Items { get; private set; }
    }

    public class UserComparer : IComparer<User>
    {
        private readonly bool _epoch;

        public UserComparer(bool epoch)
        {
            _epoch = epoch;
        }

        public int Compare(User x, User y)
        {
            if (_epoch)
            {
                var epoch = LastSeenConverter.GetIndex(y).CompareTo(LastSeenConverter.GetIndex(x));
                if (epoch == 0)
                {
                    var nameX = x.FirstName.Length > 0 ? x.FirstName : x.LastName;
                    var nameY = y.FirstName.Length > 0 ? y.FirstName : y.LastName;

                    var fullName = nameX.CompareTo(nameY);
                    if (fullName == 0)
                    {
                        return y.Id.CompareTo(x.Id);
                    }

                    return fullName;
                }

                return epoch;
            }
            else
            {
                if (x.Type is UserTypeDeleted)
                {
                    return 1;
                }

                var nameX = x.FirstName.Length > 0 ? x.FirstName : x.LastName;
                var nameY = y.FirstName.Length > 0 ? y.FirstName : y.LastName;

                var fullName = nameX.CompareTo(nameY);
                if (fullName == 0)
                {
                    return y.Id.CompareTo(x.Id);
                }

                return fullName;
            }
        }
    }
}
