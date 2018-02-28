using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using TdWindows;
using Telegram.Api.TL;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Services;
using Unigram.ViewModels.Supergroups;

namespace Unigram.ViewModels
{
    public class ContactsViewModel : UnigramViewModelBase
    {
        private IContactsService _contactsService;

        public ContactsViewModel(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, IContactsService contactsService)
            : base(protoService, cacheService, aggregator)
        {
            _contactsService = contactsService;

            Items = new SortedObservableCollection<User>(new UserComparer(true));
        }

        public void LoadContacts()
        {
            ProtoService.Send(new SearchContacts(string.Empty, int.MaxValue), async result =>
            {
                if (result is TdWindows.Users users)
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

                    await _contactsService.ExportAsync(users);
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

        //public void Handle(TLUpdateUserStatus message)
        //{
        //    BeginOnUIThread(() =>
        //    {
        //        //var first = Items.FirstOrDefault(x => x != null && x.Id == message.UserId);
        //        //if (first != null)
        //        //{
        //        //    Items.Remove(first);
        //        //}

        //        //var user = CacheService.GetUser(message.UserId) as TLUser;
        //        //if (user != null && user.IsContact && user.IsSelf == false)
        //        //{
        //        //    //var status = LastSeenHelper.GetLastSeen(user);
        //        //    //var listItem = new UsersPanelListItem(user as TLUser);
        //        //    //listItem.fullName = user.FullName;
        //        //    //listItem.LastSeen = status.Item1;
        //        //    //listItem.LastSeenEpoch = status.Item2;
        //        //    //listItem.Photo = listItem._parent.Photo;
        //        //    //listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

        //        //    Items.Add(user);
        //        //}
        //    });
        //}

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
