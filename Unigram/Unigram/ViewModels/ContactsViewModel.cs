using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Telegram.Api.TL.Contacts;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Core.Services;

namespace Unigram.ViewModels
{
    public class ContactsViewModel : UnigramViewModelBase, IHandle<TLUpdateUserStatus>, IHandle<TLUpdateContactLink>, IHandle<TLUpdateContactsReset>
    {
        private IContactsService _contactsService;

        public ContactsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IContactsService contactsService)
            : base(protoService, cacheService, aggregator)
        {
            _contactsService = contactsService;

            Items = new SortedObservableCollection<TLUser>(new TLUserComparer(true));
            Search = new ObservableCollection<KeyedList<string, TLObject>>();
        }

        public async void LoadContacts()
        {
            await LoadContactsAsync();
        }

        public async Task LoadContactsAsync()
        {
            Items.Clear();

            var contacts = CacheService.GetContacts();
            foreach (var item in contacts.OfType<TLUser>())
            {
                var user = item as TLUser;
                if (user.IsSelf)
                {
                    continue;
                }

                Items.Add(user);
            }

            var savedCount = ApplicationSettings.Current.ContactsSavedCount;
            var hash = CalculateContactsHash(savedCount, contacts.OrderBy(x => x.Id));

            var response = await ProtoService.GetContactsAsync(hash);
            if (response.IsSucceeded && response.Result is TLContactsContacts result)
            {
                ApplicationSettings.Current.ContactsSavedCount = result.SavedCount;

                BeginOnUIThread(() =>
                {
                    Items.Clear();

                    foreach (var item in result.Users.OfType<TLUser>())
                    {
                        var user = item as TLUser;
                        if (user.IsSelf)
                        {
                            continue;
                        }

                        Items.Add(user);
                    }
                });

                if (ApplicationSettings.Current.IsContactsSyncEnabled)
                {
                    await _contactsService.ExportAsync(result);
                }
            }

            await _contactsService.ImportAsync();

            Aggregator.Subscribe(this);
        }

        public static int CalculateContactsHash(int savedCount, IEnumerable<TLUserBase> contacts)
        {
            if (contacts == null)
            {
                return 0;
            }

            long acc = 0;
            acc = ((acc * 20261) + 0x80000000L + savedCount) % 0x80000000L;

            foreach (var contact in contacts)
            {
                acc = ((acc * 20261) + 0x80000000L + contact.Id) % 0x80000000L;
            }

            return (int)acc;
        }

        public async Task GetSelfAsync()
        {
            var cached = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
            if (cached != null)
            {
                //var status = LastSeenHelper.GetLastSeen(cached);
                //var listItem = new UsersPanelListItem(cached as TLUser);
                //listItem.fullName = cached.FullName;
                //listItem.lastSeen = status.Item1;
                //listItem.lastSeenEpoch = status.Item2;
                //listItem.Photo = listItem._parent.Photo;
                //listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                Self = cached;
            }
            else
            {
                var response = await ProtoService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
                if (response.IsSucceeded)
                {
                    var user = response.Result.FirstOrDefault() as TLUser;
                    if (user != null)
                    {
                        //var status = LastSeenHelper.GetLastSeen(user);
                        //var listItem = new UsersPanelListItem(user as TLUser);
                        //listItem.fullName = user.FullName;
                        //listItem.lastSeen = status.Item1;
                        //listItem.lastSeenEpoch = status.Item2;
                        //listItem.Photo = listItem._parent.Photo;
                        //listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                        Self = user;
                    }
                }
            }
        }

        #region Search

        private string _searchQuery;
        public string SearchQuery
        {
            get
            {
                return _searchQuery;
            }
            set
            {
                Set(ref _searchQuery, value);
                SearchSync(value);
            }
        }

        public void SearchSync(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                Search.Clear();
                return;
            }

            var queries = LocaleHelper.GetQuery(query);

            var contacts = CacheService.GetContacts().OfType<TLUser>().Where(x => x.IsLike(queries, StringComparison.OrdinalIgnoreCase)).ToList();
            if (query.Equals(_searchQuery))
            {
                if (Search.Count > 1) Search.RemoveAt(1);
                if (Search.Count > 0) Search.RemoveAt(0);
                //if (contacts.Count > 0) Search.Add(new KeyedList<string, TLObject>(null, contacts));
                Search.Add(new KeyedList<string, TLObject>(null, contacts));
            }
        }

        public async Task SearchAsync(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                Search.Clear();
                return;
            }

            var result = await ProtoService.SearchAsync(query, 100);
            if (query.Equals(_searchQuery))
            {
                if (Search.Count > 1) Search.RemoveAt(1);
                if (result.IsSucceeded && result.Result.Users.Count > 0) Search.Add(new KeyedList<string, TLObject>(Strings.Android.GlobalSearch, result.Result.Users));
            }

            //SearchQuery = query;
        }

        #endregion

        #region Handle

        public void Handle(TLUpdateUserStatus message)
        {
            BeginOnUIThread(() =>
            {
                var first = Items.FirstOrDefault(x => x.Id == message.UserId);
                if (first != null)
                {
                    Items.Remove(first);
                }

                var user = CacheService.GetUser(message.UserId) as TLUser;
                if (user != null && user.IsContact && user.IsSelf == false)
                {
                    //var status = LastSeenHelper.GetLastSeen(user);
                    //var listItem = new UsersPanelListItem(user as TLUser);
                    //listItem.fullName = user.FullName;
                    //listItem.lastSeen = status.Item1;
                    //listItem.lastSeenEpoch = status.Item2;
                    //listItem.Photo = listItem._parent.Photo;
                    //listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                    Items.Add(user);
                }
            });
        }

        public void Handle(TLUpdateContactLink update)
        {
            BeginOnUIThread(() =>
            {
                var contact = update.MyLink is TLContactLinkContact;
                var already = Items.FirstOrDefault(x => x.Id == update.UserId);
                if (already == null)
                {
                    if (contact)
                    {
                        var user = CacheService.GetUser(update.UserId) as TLUser;
                        if (user != null)
                        {
                            Items.Add(user);
                        }
                    }
                    return;
                }

                if (contact)
                {
                    Items.Add(already);
                    return;
                }

                Items.Remove(already);
            });
        }

        public void Handle(TLUpdateContactsReset update)
        {
            LoadContacts();
        }

        #endregion

        public SortedObservableCollection<TLUser> Items { get; private set; }

        public ObservableCollection<KeyedList<string, TLObject>> Search { get; private set; }

        private TLUser _self;
        public TLUser Self
        {
            get
            {
                return _self;
            }
            set
            {
                Set(ref _self, value);
            }
        }
    }

    public class TLUserComparer : IComparer<TLUser>
    {
        private bool _epoch;

        public TLUserComparer(bool epoch)
        {
            _epoch = epoch;
        }

        public int Compare(TLUser x, TLUser y)
        {
            if (_epoch)
            {
                var epoch = LastSeenConverter.GetIndex(y).CompareTo(LastSeenConverter.GetIndex(x));
                if (epoch == 0)
                {
                    var fullName = x.FullName.CompareTo(y.FullName);
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
                var fullName = x.FullName.CompareTo(y.FullName);
                if (fullName == 0)
                {
                    return y.Id.CompareTo(x.Id);
                }

                return fullName;
            }
        }
    }
}
