using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Windows.System;
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
        }

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
                var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.GpsDisabledAlert, Strings.Resources.AppName, Strings.Resources.ConnectingToProxyEnable, Strings.Resources.Cancel);
                if (confirm == ContentDialogResult.Primary)
                {
                    await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location"));
                }

                return;
            }

#if DEBUG
            // GPS doesn't work within the VM
            location = new Location(53.556064, 9.988436);
#endif

            var response = await ProtoService.SendAsync(new SearchChatsNearby(location));
            if (response is ChatsNearby nearby)
            {
                Users.ReplaceWith(nearby.UsersNearby);
                Chats.ReplaceWith(nearby.SupergroupsNearby);

                IsUsersEmpty = nearby.UsersNearby.IsEmpty();
                IsChatsEmpty = nearby.SupergroupsNearby.IsEmpty();
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
                Users.ReplaceWith(update.UsersNearby);
                IsUsersEmpty = update.UsersNearby.IsEmpty();
            });
        }

        public RelayCommand<ChatNearby> OpenChatCommand { get; }
        private void OpenChatExecute(ChatNearby nearby)
        {
            NavigationService.NavigateToChat(nearby.ChatId);
        }
    }
}
