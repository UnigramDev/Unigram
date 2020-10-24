using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ChatsNearbyViewModel : TLViewModelBase, IHandle<UpdateUsersNearby>
    {
        private readonly ILocationService _locationService;

        public ChatsNearbyViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator, ILocationService locationService)
            : base(protoService, cacheService, settingsService, aggregator)
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

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var location = await _locationService.GetPositionAsync();
            if (location == null)
            {
                var confirm = await MessagePopup.ShowAsync(Strings.Resources.GpsDisabledAlert, Strings.Resources.AppName, Strings.Resources.ConnectingToProxyEnable, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location"));
                }

                return;
            }

            var response = await ProtoService.SendAsync(new SearchChatsNearby(location));
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

                RaisePropertyChanged(() => LoadMoreLabel);
                RaisePropertyChanged(() => LoadMoreVisibility);
            }

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(pageState, suspending);
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

                RaisePropertyChanged(() => LoadMoreLabel);
                RaisePropertyChanged(() => LoadMoreVisibility);
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
