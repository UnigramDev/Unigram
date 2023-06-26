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
    public class ContactsViewModel : MultiViewModelBase, IChildViewModel
    {
        private readonly DisposableMutex _loadMoreLock;
        private readonly UserComparer _comparer;

        public ContactsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _loadMoreLock = new DisposableMutex();
            _comparer = new UserComparer(Settings.IsContactsSortedByEpoch);

            Items = new SortedObservableCollection<User>(_comparer);

            Stories = new StoriesViewModel(clientService, settingsService, aggregator);
            Stories.HiddenStories = true;

            Groups = new object[]
            {
                Stories,
                this
            };
        }

        public object[] Groups { get; }

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
                    var stories = new List<ActiveStoriesViewModel>();

                    foreach (var user in ClientService.GetUsers(users.UserIds))
                    {
                        items.Add(user);

                        if (user.StoriesAreHidden && user.HasActiveStories)
                        {
                            stories.Add(new ActiveStoriesViewModel(ClientService, user.Id));
                        }
                    }

                    Items.ReplaceWith(items);

                    // TODO: ???
                    if (Stories.HasMoreItems)
                    {
                        await Stories.LoadMoreItemsAsync(1);
                    }
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
                _comparer.ByEpoch = epoch;

                var response = await ClientService.SendAsync(new GetContacts());
                if (response is Telegram.Td.Api.Users users)
                {
                    var items = new List<User>();

                    foreach (var user in ClientService.GetUsers(users.UserIds))
                    {
                        items.Add(user);
                    }

                    Items.ReplaceWith(items);
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

        public SortedObservableCollection<User> Items { get; }
        public StoriesViewModel Stories { get; }
    }

    public class UserComparer : IComparer<User>
    {
        public UserComparer(bool epoch)
        {
            ByEpoch = epoch;
        }

        public bool ByEpoch { get; set; }

        public int Compare(User x, User y)
        {
            if (ByEpoch)
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
