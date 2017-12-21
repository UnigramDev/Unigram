using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Template10.Common;
using Template10.Controls;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Views;
using Unigram.ViewModels;
using Unigram.ViewModels.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Unigram.Common;
using System.Windows.Input;

namespace Unigram.Views.Users
{
    public sealed partial class UserDetailsPage : Page
    {
        public UserDetailsViewModel ViewModel => DataContext as UserDetailsViewModel;

        public UserDetailsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<UserDetailsViewModel>();
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var user = ViewModel.Item as TLUser;
            var userFull = ViewModel.Full as TLUserFull;
            if (userFull != null && userFull.ProfilePhoto is TLPhoto && user != null)
            {
                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, userFull, user);
                await GalleryView.Current.ShowAsync(viewModel, () => Photo);
            }
        }

        private void Notifications_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (toggle.FocusState != FocusState.Unfocused)
            {
                ViewModel.ToggleMuteCommand.Execute();
            }
        }

        #region Context menu

        private void About_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            MessageHelper.Hyperlink_ContextRequested(sender, args);
        }

        private void About_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var user = ViewModel.Item as TLUser;
            var full = ViewModel.Full as TLUserFull;
            if (full == null || user == null)
            {
                return;
            }

            if (user.IsSelf)
            {
                CreateFlyoutItem(ref flyout, null, Strings.Android.ShareContact);
            }
            else
            {
                if (user.IsContact)
                {
                    CreateFlyoutItem(ref flyout, null, Strings.Android.ShareContact);
                    CreateFlyoutItem(ref flyout, !full.IsBlocked ? ViewModel.BlockCommand : ViewModel.UnblockCommand, !full.IsBlocked ? Strings.Android.BlockContact : Strings.Android.Unblock);
                    CreateFlyoutItem(ref flyout, ViewModel.EditCommand, Strings.Android.EditContact);
                    CreateFlyoutItem(ref flyout, ViewModel.DeleteCommand, Strings.Android.DeleteContact);
                }
                else
                {
                    if (user.IsBot)
                    {
                        if (!user.IsBotNochats)
                        {
                            CreateFlyoutItem(ref flyout, null, Strings.Android.BotInvite);
                        }

                        CreateFlyoutItem(ref flyout, null, Strings.Android.BotShare);
                    }

                    if (user.Phone != null && user.Phone.Length > 0)
                    {
                        CreateFlyoutItem(ref flyout, ViewModel.AddCommand, Strings.Android.AddContact);
                        CreateFlyoutItem(ref flyout, null, Strings.Android.ShareContact);
                        CreateFlyoutItem(ref flyout, !full.IsBlocked ? ViewModel.BlockCommand : ViewModel.UnblockCommand, !full.IsBlocked ? Strings.Android.BlockContact : Strings.Android.Unblock);
                    }
                    else
                    {
                        if (user.IsBot)
                        {
                            CreateFlyoutItem(ref flyout, !full.IsBlocked ? ViewModel.BlockCommand : ViewModel.UnblockCommand, !full.IsBlocked ? Strings.Android.BotStop : Strings.Android.BotRestart);
                        }
                        else
                        {
                            CreateFlyoutItem(ref flyout, !full.IsBlocked ? ViewModel.BlockCommand : ViewModel.UnblockCommand, !full.IsBlocked ? Strings.Android.BlockContact : Strings.Android.Unblock);
                        }
                    }
                }
            }

            CreateFlyoutItem(ref flyout, null, Strings.Android.AddShortcut);

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt((Button)sender);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, ICommand command, string text)
        {
            var flyoutItem = new MenuFlyoutItem();
            flyoutItem.IsEnabled = command != null;
            flyoutItem.Command = command;
            flyoutItem.Text = text;

            flyout.Items.Add(flyoutItem);
        }

        #endregion
    }
}
