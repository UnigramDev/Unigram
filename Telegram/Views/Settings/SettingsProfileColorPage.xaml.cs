//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Settings
{
    public sealed partial class SettingsProfileColorPage : HostedPage
    {
        public SettingsProfileColorViewModel ViewModel => DataContext as SettingsProfileColorViewModel;

        public SettingsProfileColorPage()
        {
            InitializeComponent();
            Title = Strings.UserColorTabProfile;
        }

        private bool _confirmed;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            NameView.Initialize(ViewModel.ClientService, new MessageSenderUser(ViewModel.ClientService.Options.MyId));
            ProfileView.Initialize(ViewModel.ClientService, new MessageSenderUser(ViewModel.ClientService.Options.MyId));
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (_confirmed)
            {
                return;
            }

            if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
            {
                if (ViewModel.IsPremium)
                {
                    var changed = false;

                    if (user.AccentColorId != NameView.SelectedAccentColor?.Id || user.BackgroundCustomEmojiId != NameView.SelectedCustomEmojiId)
                    {
                        changed = true;
                    }

                    if (user.ProfileAccentColorId != ProfileView.SelectedAccentColor?.Id || user.ProfileBackgroundCustomEmojiId != ProfileView.SelectedCustomEmojiId)
                    {
                        changed = true;
                    }

                    if (changed)
                    {
                        ConfirmClose();
                        e.Cancel = true;
                    }
                }
            }
        }

        private async void ConfirmClose()
        {
            var confirm = await ViewModel.ShowPopupAsync(Strings.UserColorUnsavedMessage, Strings.UserColorUnsaved, Strings.ChatThemeSaveDialogDiscard, Strings.ChatThemeSaveDialogApply, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                _confirmed = true;
                Frame.GoBack();
            }
            else if (confirm == ContentDialogResult.Secondary)
            {
                _confirmed = true;
                PurchaseCommand_Click(null, null);
            }
        }

        private void PurchaseCommand_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ClientService.TryGetUser(ViewModel.ClientService.Options.MyId, out User user))
            {
                if (ViewModel.IsPremium)
                {
                    var changed = false;

                    if (user.AccentColorId != NameView.SelectedAccentColor?.Id || user.BackgroundCustomEmojiId != NameView.SelectedCustomEmojiId)
                    {
                        ViewModel.ClientService.Send(new SetAccentColor(NameView.SelectedAccentColor.Id, NameView.SelectedCustomEmojiId));
                        changed = true;
                    }

                    if (user.ProfileAccentColorId != ProfileView.SelectedAccentColor?.Id || user.ProfileBackgroundCustomEmojiId != ProfileView.SelectedCustomEmojiId)
                    {
                        ViewModel.ClientService.Send(new SetProfileAccentColor(ProfileView.SelectedAccentColor?.Id ?? -1, ProfileView.SelectedCustomEmojiId));
                        changed = true;
                    }

                    if (changed)
                    {
                        ToastPopup.Show(Strings.UserColorApplied, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));
                    }

                    _confirmed = true;
                    Frame.GoBack();
                }
                else
                {
                    ToastPopup.ShowFeature(ViewModel.NavigationService, new PremiumFeatureAccentColor());
                }
            }
        }
    }
}
