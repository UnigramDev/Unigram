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
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Core.Services;

namespace Unigram.ViewModels
{
    public class ContactsViewModel : UnigramViewModelBase, IHandle, IHandle<TLUpdateUserStatus>
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
            //var contacts = CacheService.GetContacts();
            //foreach (var item in contacts.OfType<TLUser>())
            //{
            //    var user = item as TLUser;
            //    if (user.IsSelf)
            //    {
            //        continue;
            //    }

            //    //var status = LastSeenHelper.GetLastSeen(user);
            //    //var listItem = new UsersPanelListItem(user as TLUser);
            //    //listItem.fullName = user.FullName;
            //    //listItem.lastSeen = status.Item1;
            //    //listItem.lastSeenEpoch = status.Item2;
            //    //listItem.Photo = listItem._parent.Photo;
            //    //listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

            //    Items.Add(user);
            //}

            var contacts = new TLUser[0];

            var input = string.Join(",", contacts.Select(x => x.Id).Union(new[] { SettingsHelper.UserId }).OrderBy(x => x));
            var hash = Utils.ComputeMD5(input);
            var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            var response = await ProtoService.GetContactsAsync(hex);
            if (response.IsSucceeded)
            {
                var result = response.Result as TLContactsContacts;
                if (result != null)
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        foreach (var item in result.Users.OfType<TLUser>())
                        {
                            var user = item as TLUser;
                            if (user.IsSelf)
                            {
                                continue;
                            }

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

                    await _contactsService.SyncContactsAsync(response.Result);
                }
            }

            Aggregator.Subscribe(this);
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

        public async Task SearchAsync(string query)
        {
            Search.Clear();

            var contacts = CacheService.GetContacts().Where(x => CultureInfo.CurrentCulture.CompareInfo.IndexOf(x.FullName, query, CompareOptions.IgnoreCase) >= 0).ToList();
            if (contacts.Count > 0)
            {
                Search.Add(new KeyedList<string, TLObject>("Contacts", contacts));
            }

            var result = await ProtoService.SearchAsync(query, 100);
            if (result.IsSucceeded)
            {
                Search.Add(new KeyedList<string, TLObject>("Global search", result.Result.Users));
            }
        }

        #region Handle

        public void Handle(TLUpdateUserStatus message)
        {
            Execute.BeginOnUIThread(() =>
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
                var epoch = LastSeenHelper.GetLastSeen(y).Item2.CompareTo(LastSeenHelper.GetLastSeen(x).Item2);
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
