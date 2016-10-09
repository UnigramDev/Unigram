using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace Unigram.ViewModels
{
    public class ContactsViewModel : UnigramViewModelBase, IHandle, IHandle<TLUpdateUserStatus>
    {
        public ContactsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new SortedObservableCollection<UsersPanelListItem>(new UsersPanelListItemComparer());
        }

        public async Task getTLContacts()
        {
            var contacts = CacheService.GetContacts();
            foreach (var item in contacts.OfType<TLUser>())
            {
                var user = item as TLUser;
                if (user.IsSelf)
                {
                    continue;
                }

                var status = LastSeenHelper.GetLastSeen(user);
                var listItem = new UsersPanelListItem(user as TLUser);
                listItem.fullName = user.FullName;
                listItem.lastSeen = status.Item1;
                listItem.lastSeenEpoch = status.Item2;
                listItem.Photo = listItem._parent.Photo;
                listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                Items.Add(listItem);
            }

            var input = string.Join(",", contacts.Select(x => x.Id).Union(new[] { SettingsHelper.UserId }).OrderBy(x => x));
            var hash = MD5Core.GetHash(input);
            var hex = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();

            var response = await ProtoService.GetContactsAsync(hex);
            if (response.IsSucceeded && response.Value is TLContactsContacts)
            {
                var result = response.Value as TLContactsContacts;
                if (result != null)
                {
                    foreach (var item in result.Users.OfType<TLUser>())
                    {
                        var user = item as TLUser;
                        if (user.IsSelf)
                        {
                            continue;
                        }

                        var status = LastSeenHelper.GetLastSeen(user);
                        var listItem = new UsersPanelListItem(user as TLUser);
                        listItem.fullName = user.FullName;
                        listItem.lastSeen = status.Item1;
                        listItem.lastSeenEpoch = status.Item2;
                        listItem.Photo = listItem._parent.Photo;
                        listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                        Items.Add(listItem);
                    }
                }
            }

            Aggregator.Subscribe(this);
        }

        public async Task GetSelfAsync()
        {
            var response = await ProtoService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
            if (response.IsSucceeded)
            {
                var user = response.Value.FirstOrDefault() as TLUser;
                if (user != null)
                {
                    var status = LastSeenHelper.GetLastSeen(user);
                    var listItem = new UsersPanelListItem(user as TLUser);
                    listItem.fullName = user.FullName;
                    listItem.lastSeen = status.Item1;
                    listItem.lastSeenEpoch = status.Item2;
                    listItem.Photo = listItem._parent.Photo;
                    listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                    Self = listItem;
                }
            }
        }

        #region Handle

        public void Handle(TLUpdateUserStatus message)
        {
            Execute.BeginOnUIThread(() =>
            {
                var first = Items.FirstOrDefault(x => x._parent.Id == message.UserId);
                if (first != null)
                {
                    Items.Remove(first);
                }

                var user = CacheService.GetUser(message.UserId) as TLUser;
                if (user != null && user.IsContact && user.IsSelf == false)
                {
                    var status = LastSeenHelper.GetLastSeen(user);
                    var listItem = new UsersPanelListItem(user as TLUser);
                    listItem.fullName = user.FullName;
                    listItem.lastSeen = status.Item1;
                    listItem.lastSeenEpoch = status.Item2;
                    listItem.Photo = listItem._parent.Photo;
                    listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                    Items.Add(listItem);
                }
            });
        }

        #endregion

        public SortedObservableCollection<UsersPanelListItem> Items { get; private set; }

        private UsersPanelListItem _self;
        public UsersPanelListItem Self
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

    public class UsersPanelListItemComparer : IComparer<UsersPanelListItem>
    {
        public int Compare(UsersPanelListItem x, UsersPanelListItem y)
        {
            var epoch = y.lastSeenEpoch.CompareTo(x.lastSeenEpoch);
            if (epoch == 0)
            {
                return y.fullName.CompareTo(x.fullName);
            }

            return epoch;

            //var epoch = y.lastSeenEpoch - x.lastSeenEpoch;
            //if (epoch == 0)
            //{
            //    return x.fullName.CompareTo(y.fullName);
            //}

            //return epoch;
        }
    }
}
