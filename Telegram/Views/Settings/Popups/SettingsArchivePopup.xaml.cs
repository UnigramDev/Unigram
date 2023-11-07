using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Settings.Popups
{
    public sealed partial class SettingsArchivePopup : ContentPopup
    {
        private readonly IClientService _clientService;

        public SettingsArchivePopup(IClientService clientService)
        {
            InitializeComponent();

            _clientService = clientService;

            Title = Strings.ArchiveSettings;

            PrimaryButtonText = Strings.Done;
            SecondaryButtonText = Strings.Cancel;

            if (clientService.IsPremium || clientService.Options.CanArchiveAndMuteNewChatsFromUnknownUsers)
            {
                NewChats.IsFaux = false;
                NewChatsLock.Visibility = Visibility.Collapsed;
            }
            else if (clientService.IsPremiumAvailable)
            {
                NewChats.IsFaux = true;
                NewChatsLock.Visibility = Visibility.Visible;
            }
            else
            {
                NewChatsPanel.Visibility = Visibility.Collapsed;
            }

            Initialize(clientService);
        }

        private async void Initialize(IClientService clientService)
        {
            UnmutedChats.IsEnabled = false;
            ChatsFromFolders.IsEnabled = false;
            NewChats.IsEnabled = false;

            var response = await clientService.SendAsync(new GetArchiveChatListSettings());
            if (response is ArchiveChatListSettings settings)
            {
                UnmutedChats.IsChecked = settings.KeepUnmutedChatsArchived;
                ChatsFromFolders.IsChecked = settings.KeepChatsFromFoldersArchived;
                NewChats.IsChecked = settings.ArchiveAndMuteNewChatsFromUnknownUsers;

                UnmutedChats.IsEnabled = true;
                ChatsFromFolders.IsEnabled = true;
                NewChats.IsEnabled = true;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _clientService.Send(new SetArchiveChatListSettings(new ArchiveChatListSettings
            {
                KeepUnmutedChatsArchived = UnmutedChats.IsChecked == true,
                KeepChatsFromFoldersArchived = ChatsFromFolders.IsChecked == true,
                ArchiveAndMuteNewChatsFromUnknownUsers = NewChats.IsChecked == true
            }));
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void NewChats_Click(object sender, RoutedEventArgs e)
        {
            if (NewChats.IsFaux)
            {
                Window.Current.ShowToast(NewChatsLock, Strings.UnlockPremium, Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.BottomRight, ElementTheme.Dark);
            }
        }
    }
}
