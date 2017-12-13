using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Views;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.Views.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Telegram.Api.Helpers;
using Windows.Storage.Pickers;
using Unigram.Controls.Views;
using Unigram.Controls;
using Unigram.Common;
using Windows.UI.Xaml.Media.Animation;
using System.Windows.Input;

namespace Unigram.Views.Chats
{
    public sealed partial class ChatDetailsPage : Page
    {
        public ChatDetailsViewModel ViewModel => DataContext as ChatDetailsViewModel;

        public ChatDetailsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChatDetailsViewModel>();
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Item as TLChat;
            var chatFull = ViewModel.Full as TLChatFull;
            if (chatFull != null && chatFull.ChatPhoto is TLPhoto && chat != null)
            {
                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.CacheService, chatFull, chat);
                await GalleryView.Current.ShowAsync(viewModel, () => Picture);
            }
        }

        #region Context menu

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var chat = ViewModel.Item as TLChat;
            var full = ViewModel.Full as TLChatFull;
            if (full == null || chat == null)
            {
                return;
            }

            if (chat.IsCreator)
            {
                CreateFlyoutItem(ref flyout, null, Strings.Android.SetAdmins);
            }
            if (!chat.IsAdminsEnabled || chat.IsCreator || chat.IsAdmin)
            {
                CreateFlyoutItem(ref flyout, ViewModel.EditCommand, Strings.Android.ChannelEdit);
            }

            CreateFlyoutItem(ref flyout, null, Strings.Android.SearchMembers);

            if (chat.IsCreator && (full == null || (full.Participants is TLChatParticipants participants && participants.Participants.Count > 0)))
            {
                CreateFlyoutItem(ref flyout, ViewModel.MigrateCommand, Strings.Android.ConvertGroupMenu);
            }
            CreateFlyoutItem(ref flyout, null, Strings.Android.DeleteAndExit);

            CreateFlyoutItem(ref flyout, null, Strings.Android.AddShortcut);

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt((Button)sender);
            }
        }

        private void Participant_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var participant = element.DataContext as TLChatParticipantBase;

            CreateFlyoutItem(ref flyout, ParticipantRemove_Loaded, ViewModel.ParticipantRemoveCommand, participant, Strings.Android.KickFromGroup);

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, Func<TLChatParticipantBase, Visibility> visibility, ICommand command, object parameter, string text)
        {
            var value = visibility(parameter as TLChatParticipantBase);
            if (value == Visibility.Visible)
            {
                var flyoutItem = new MenuFlyoutItem();
                //flyoutItem.Loaded += (s, args) => flyoutItem.Visibility = visibility(parameter as TLMessageCommonBase);
                flyoutItem.Command = command;
                flyoutItem.CommandParameter = parameter;
                flyoutItem.Text = text;

                flyout.Items.Add(flyoutItem);
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

        private Visibility ParticipantRemove_Loaded(TLChatParticipantBase participantBase)
        {
            switch (participantBase)
            {
                case TLChatParticipant participant:
                    return participant.InviterId == SettingsHelper.UserId ? Visibility.Visible : Visibility.Collapsed;
                case TLChatParticipantAdmin admin:
                    return admin.InviterId == SettingsHelper.UserId ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        #endregion

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLChatParticipantBase participant && participant.User != null)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), participant.User.ToPeer());
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
    }
}
