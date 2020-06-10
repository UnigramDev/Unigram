using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Services;

namespace Unigram.ViewModels
{
    public class ContactsViewModel : TLViewModelBase, IChildViewModel, IHandle<UpdateUserStatus>
    {
        private readonly IContactsService _contactsService;

        private readonly DisposableMutex _loadMoreLock;

        public ContactsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IContactsService contactsService)
            : base(protoService, cacheService, settingsService, aggregator)
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

                var confirm = await MessagePopup.ShowAsync(Strings.Resources.ContactsPermissionAlert, Strings.Resources.AppName, Strings.Resources.ContactsPermissionAlertContinue, Strings.Resources.ContactsPermissionAlertNotNow);
                if (confirm != Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    Settings.IsContactsSyncEnabled = false;
                    await _contactsService.RemoveAsync();
                }

                if (Settings.IsContactsSyncEnabled)
                {
                    ProtoService.Send(new GetContacts(), async result =>
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
            Aggregator.Subscribe(this);

            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SendAsync(new GetContacts());
                if (response is Telegram.Td.Api.Users users)
                {
                    var items = new List<User>();

                    foreach (var id in users.UserIds)
                    {
                        var user = ProtoService.GetUser(id);
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
            get { return Settings.IsContactsSortedByEpoch; }
            set
            {
                if (Settings.IsContactsSortedByEpoch != value)
                {
                    Settings.IsContactsSortedByEpoch = value;
                    Load(value);

                    RaisePropertyChanged(() => IsSortedByEpoch);
                }
            }
        }

        private async void Load(bool epoch)
        {
            using (await _loadMoreLock.WaitAsync())
            {
                var response = await ProtoService.SendAsync(new GetContacts());
                if (response is Telegram.Td.Api.Users users)
                {
                    var items = new List<User>();

                    foreach (var id in users.UserIds)
                    {
                        var user = ProtoService.GetUser(id);
                        if (user != null)
                        {
                            items.Add(user);
                        }
                    }

                    Items = new SortedObservableCollection<User>(new UserComparer(epoch));
                    Items.ReplaceWith(items);
                    RaisePropertyChanged(() => Items);
                }
            }
        }

        private SearchUsersCollection _search;
        public SearchUsersCollection Search
        {
            get
            {
                return _search;
            }
            set
            {
                Set(ref _search, value);
            }
        }

        #region Handle

        public void Handle(UpdateUserStatus update)
        {
            var user = CacheService.GetUser(update.UserId);
            if (user == null || user.Id == CacheService.Options.MyId || !user.IsContact)
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

        //public void Handle(TLUpdateContactLink update)
        //{
        //    BeginOnUIThread(() =>
        //    {
        //        //var contact = update.MyLink is TLContactLinkContact;
        //        //var already = Items.FirstOrDefault(x => x != null && x.Id == update.UserId);
        //        //if (already == null)
        //        //{
        //        //    if (contact)
        //        //    {
        //        //        var user = CacheService.GetUser(update.UserId) as TLUser;
        //        //        if (user != null)
        //        //        {
        //        //            Items.Add(user);
        //        //        }
        //        //    }
        //        //    return;
        //        //}

        //        //if (contact)
        //        //{
        //        //    Items.Add(already);
        //        //    return;
        //        //}

        //        //Items.Remove(already);
        //    });
        //}

        #endregion

        public SortedObservableCollection<User> Items { get; private set; }
    }

    public class UserComparer : IComparer<User>
    {
        private bool _epoch;

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
