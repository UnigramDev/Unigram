//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.System;

namespace Unigram.ViewModels
{
    public class ChatsNearbyViewModel : TLViewModelBase
        , IHandle
        //, IHandle<UpdateUsersNearby>
    {
        private readonly ILocationService _locationService;

        public ChatsNearbyViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService)
            : base(clientService, settingsService, aggregator)
        {
            _locationService = locationService;

            Users = new MvxObservableCollection<ChatNearby>();
            Chats = new MvxObservableCollection<ChatNearby>();

            OpenChatCommand = new RelayCommand<ChatNearby>(OpenChatExecute);
            LoadMoreCommand = new RelayCommand(LoadMoreExecute);
        }

        private bool _loadedMore;

        private IList<ChatNearby> _remainingUsers;
        public MvxObservableCollection<ChatNearby> Users { get; private set; }

        private bool _isUsersEmpty;
        public bool IsUsersEmpty
        {
            get => _isUsersEmpty;
            set => Set(ref _isUsersEmpty, value);
        }

        public MvxObservableCollection<ChatNearby> Chats { get; private set; }

        private bool _isChatsEmpty;
        public bool IsChatsEmpty
        {
            get => _isChatsEmpty;
            set => Set(ref _isChatsEmpty, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            var location = await _locationService.GetPositionAsync(XamlRoot);
            if (location == null)
            {
                var confirm = await MessagePopup.ShowAsync(XamlRoot, Strings.Resources.GpsDisabledAlert, Strings.Resources.AppName, Strings.Resources.ConnectingToProxyEnable, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location"));
                }

                return;
            }

            var response = await ClientService.SendAsync(new SearchChatsNearby(location));
            if (response is ChatsNearby nearby)
            {
                if (nearby.UsersNearby.Count > 5)
                {
                    _remainingUsers = nearby.UsersNearby.Skip(5).ToList();
                    Users.ReplaceWith(nearby.UsersNearby.Take(5));
                }
                else
                {
                    Users.ReplaceWith(nearby.UsersNearby);
                }

                Chats.ReplaceWith(nearby.SupergroupsNearby);

                IsUsersEmpty = nearby.UsersNearby.IsEmpty();
                IsChatsEmpty = nearby.SupergroupsNearby.IsEmpty();

                RaisePropertyChanged(nameof(LoadMoreLabel));
                RaisePropertyChanged(nameof(LoadMoreVisibility));
            }
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateUsersNearby>(this, Handle);
        }

        public void Handle(UpdateUsersNearby update)
        {
            BeginOnUIThread(() =>
            {
                if (update.UsersNearby.Count > 5 && !_loadedMore)
                {
                    _remainingUsers = update.UsersNearby.Skip(5).ToList();
                    Users.ReplaceWith(update.UsersNearby.Take(5));
                }
                else
                {
                    Users.ReplaceWith(update.UsersNearby);
                }

                IsUsersEmpty = update.UsersNearby.IsEmpty();
            });
        }

        public RelayCommand<ChatNearby> OpenChatCommand { get; }
        private void OpenChatExecute(ChatNearby nearby)
        {
            NavigationService.NavigateToChat(nearby.ChatId);
        }

        public RelayCommand LoadMoreCommand { get; }
        private void LoadMoreExecute()
        {
            if (_remainingUsers != null)
            {
                Users.AddRange(_remainingUsers);

                _remainingUsers = null;
                _loadedMore = true;

                RaisePropertyChanged(nameof(LoadMoreLabel));
                RaisePropertyChanged(nameof(LoadMoreVisibility));
            }
        }

        public string LoadMoreLabel
        {
            get
            {
                if (_remainingUsers != null && _remainingUsers.Count > 0)
                {
                    return Locale.Declension("ShowVotes", _remainingUsers.Count);
                }

                return null;
            }
        }

        public Visibility LoadMoreVisibility
        {
            get
            {
                return _remainingUsers != null && _remainingUsers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}
