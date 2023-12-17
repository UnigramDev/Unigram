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
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public class ContactsViewModel : MultiViewModelBase, IChildViewModel
    {
        private readonly IVoipService _voipService;

        private readonly DisposableMutex _loadMoreLock;
        private readonly UserComparer _comparer;

        public ContactsViewModel(IClientService clientService, ISettingsService settingsService, IVoipService voipService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _voipService = voipService;

            _loadMoreLock = new DisposableMutex();
            _comparer = new UserComparer(Settings.IsContactsSortedByEpoch);

            Items = new SortedObservableCollection<User>(_comparer);
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

                    foreach (var user in ClientService.GetUsers(users.UserIds))
                    {
                        items.Add(user);
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

        #region Context menu

        public void SendMessage(User user)
        {
            NavigationService.NavigateToUser(user.Id, true);
        }

        public void VoiceCall(User user)
        {
            Call(user, false);
        }

        public void VideoCall(User user)
        {
            Call(user, true);
        }

        private void Call(User user, bool video)
        {
            _voipService.StartWithUser(user.Id, video);
        }

        public async void CreateSecretChat(User user)
        {
            var confirm = await ShowPopupAsync(Strings.AreYouSureSecretChat, Strings.AreYouSureSecretChatTitle, Strings.Start, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            var response = await ClientService.SendAsync(new CreateNewSecretChat(user.Id));
            if (response is Chat result)
            {
                NavigationService.NavigateToChat(result);
            }
        }

        #endregion


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
