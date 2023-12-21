//
// Copyright Fela Ameghino 2015-2023
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

                    if (user.AccentColorId != NameView.ColorId || user.BackgroundCustomEmojiId != NameView.CustomEmojiId)
                    {
                        changed = true;
                    }

                    if (user.ProfileAccentColorId != ProfileView.ColorId || user.ProfileBackgroundCustomEmojiId != ProfileView.CustomEmojiId)
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

                    if (user.AccentColorId != NameView.ColorId || user.BackgroundCustomEmojiId != NameView.CustomEmojiId)
                    {
                        ViewModel.ClientService.Send(new SetAccentColor(NameView.ColorId, NameView.CustomEmojiId));
                        changed = true;
                    }

                    if (user.ProfileAccentColorId != ProfileView.ColorId || user.ProfileBackgroundCustomEmojiId != ProfileView.CustomEmojiId)
                    {
                        ViewModel.ClientService.Send(new SetProfileAccentColor(ProfileView.ColorId, ProfileView.CustomEmojiId));
                        changed = true;
                    }

                    if (changed)
                    {
                        ToastPopup.Show(Strings.UserColorApplied, new LocalFileSource("ms-appx:///Assets/Toasts/Success.tgs"));
                    }

                    Frame.GoBack();
                }
                else
                {
                    ToastPopup.Show(new PremiumFeatureAccentColor());
                }
            }
        }

        //private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    UserControl showView = Navigation.SelectedIndex == 0
        //        ? NameView
        //        : ProfileView;
        //    UserControl hideView = Navigation.SelectedIndex == 1
        //        ? NameView
        //        : ProfileView;

        //    showView.Visibility = Visibility.Visible;
        //    hideView.Visibility = Visibility.Collapsed;

        //    // TODO: animation?
        //    //ContentRoot.Height = showView.ActualHeight;

        //    //var show = ElementCompositionPreview.GetElementVisual(showView);
        //    //var hide = ElementCompositionPreview.GetElementVisual(hideView);

        //    //var compositor = show.Compositor;
        //    //var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

        //    //batch.Completed += (s, args) =>
        //    //{

        //    //};

        //    //batch.End();

        //    //_currentView = showView;

        //    PurchaseCommand.Content = showView switch
        //    {
        //        ChooseNameColorView name => name.PrimaryButtonText,
        //        ChooseProfileColorView profile => profile.PrimaryButtonText,
        //        _ => string.Empty
        //    };
        //}
    }
}
