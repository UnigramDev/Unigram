using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels.Supergroups;

namespace Unigram.ViewModels
{
    public class ContactsViewModel : TLViewModelBase, IHandle<UpdateUserStatus>
    {
        private IContactsService _contactsService;

        public ContactsViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, IContactsService contactsService)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _contactsService = contactsService;
            aggregator.Subscribe(this);

            Items = new SortedObservableCollection<User>(new UserComparer(true));
        }

        public void LoadContacts()
        {
            ProtoService.Send(new GetContacts(), async result =>
            {
                if (result is Telegram.Td.Api.Users users)
                {
                    BeginOnUIThread(() =>
                    {
                        foreach (var id in users.UserIds)
                        {
                            var user = ProtoService.GetUser(id);
                            if (user != null)
                            {
                                Items.Add(user);
                            }
                        }
                    });

                    if (Settings.IsContactsSyncEnabled)
                    {
                        await _contactsService.SyncAsync(users);
                    }
                }
            });
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
            BeginOnUIThread(() =>
            {
                var first = Items.FirstOrDefault(x => x != null && x.Id == update.UserId);
                if (first != null)
                {
                    Items.Remove(first);
                }

                var user = CacheService.GetUser(update.UserId);
                if (user != null && user.OutgoingLink is LinkStateIsContact)
                {
                    //var status = LastSeenHelper.GetLastSeen(user);
                    //var listItem = new UsersPanelListItem(user as TLUser);
                    //listItem.fullName = user.FullName;
                    //listItem.LastSeen = status.Item1;
                    //listItem.LastSeenEpoch = status.Item2;
                    //listItem.Photo = listItem._parent.Photo;
                    //listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                    Items.Add(user);
                }
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
                    var fullName = x.FirstName.CompareTo(y.FirstName);
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
                var fullName = x.FirstName.CompareTo(y.FirstName);
                if (fullName == 0)
                {
                    return y.Id.CompareTo(x.Id);
                }

                return fullName;
            }
        }
    }
}
