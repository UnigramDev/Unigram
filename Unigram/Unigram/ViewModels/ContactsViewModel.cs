using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
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

            aggregator.Subscribe(this);
        }

        public async Task getTLContacts()
        {
            var response = await ProtoService.GetContactsAsync(string.Empty);
            if (response.IsSucceeded && response.Value is TLContactsContacts)
            {
                var result = response.Value as TLContactsContacts;
                var temp = new List<UsersPanelListItem>();

                foreach (var item in result.Users)
                {
                    var user = item as TLUser;
                    if (user.IsSelf)
                    {
                        Self = user;
                        continue;
                    }

                    var status = LastSeenHelper.GetLastSeen(user);
                    var listItem = new UsersPanelListItem(user as TLUser);
                    listItem.fullName = user.FullName;
                    listItem.lastSeen = status;
                    listItem.Photo = listItem._parent.Photo;
                    listItem.PlaceHolderColor = BindConvert.Current.Bubble(listItem._parent.Id);

                    temp.Add(listItem);
                }

                //Super Inefficient Method below to sort alphabetically, TODO: FIX IT
                foreach (var item in temp.OrderByDescending(person => person.lastSeen))
                {
                    Items.Add(item);
                }
            }
        }

        #region Handle

        public void Handle(TLUpdateUserStatus message)
        {
            var first = Items.FirstOrDefault(x => x._parent.Id == message.UserId);
        }

        #endregion

        public SortedObservableCollection<UsersPanelListItem> Items { get; private set; }

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

    public class UsersPanelListItemComparer : IComparer<UsersPanelListItem>
    {
        public int Compare(UsersPanelListItem x, UsersPanelListItem y)
        {
            return 0;
            //return x.
        }
    }
}
