using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Strings;
using Unigram.ViewModels.Channels;
using Unigram.ViewModels.Chats;
using Unigram.Views.Users;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Views.Channels
{
    public sealed partial class ChannelDetailsPage : Page, IMasterDetailPage
    {
        public ChannelDetailsViewModel ViewModel => DataContext as ChannelDetailsViewModel;

        public ChannelDetailsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChannelDetailsViewModel>();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    ViewModel.Search.Clear();
                }
                else
                {
                    ViewModel.Find(SearchField.Text);
                }
            });
        }

        public void OnBackRequested(HandledEventArgs args)
        {
            if (ContentPanel.Visibility == Visibility.Collapsed)
            {
                SearchField.Text = string.Empty;
                Search_LostFocus(null, null);
                args.Handled = true;
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var channel = ViewModel.Item as TLChannel;
            var channelFull = ViewModel.Full as TLChannelFull;
            if (channelFull != null && channelFull.ChatPhoto is TLPhoto && channel != null)
            {
                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.CacheService, channelFull, channel);
                await GalleryView.Current.ShowAsync(viewModel, () => Picture);
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLChannelParticipantBase participant && participant.User != null)
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

        private Visibility ConvertBooleans(int? first, bool second, bool third)
        {
            return first.HasValue && first > 0 && second && third ? Visibility.Visible : Visibility.Collapsed;
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

            var channel = ViewModel.Item as TLChannel;
            var full = ViewModel.Full as TLChannelFull;
            if (full == null || channel == null)
            {
                return;
            }

            if (channel.IsCreator || (channel.HasAdminRights && channel.AdminRights != null))
            {
                if (channel.IsMegaGroup)
                {
                    CreateFlyoutItem(ref flyout, ViewModel.EditCommand, Strings.Android.ManageGroupMenu);
                }
                else
                {
                    CreateFlyoutItem(ref flyout, ViewModel.EditCommand, Strings.Android.ManageChannelMenu);
                }
            }

            if (channel.IsMegaGroup)
            {
                CreateFlyoutItem(ref flyout, new RelayCommand(async () =>
                {
                    await Task.Delay(100);
                    Search_Click(null, null);
                }), Strings.Android.SearchMembers);

                if (!channel.IsCreator && !channel.IsLeft /*&& !channel.IsKicked*/)
                {
                    CreateFlyoutItem(ref flyout, null, Strings.Android.LeaveMegaMenu);
                }
            }

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
            var participant = element.DataContext as TLChannelParticipantBase;

            CreateFlyoutItem(ref flyout, ParticipantPromote_Loaded, ViewModel.ParticipantPromoteCommand, participant, Strings.Android.SetAsAdmin);
            CreateFlyoutItem(ref flyout, ParticipantRestrict_Loaded, ViewModel.ParticipantRestrictCommand, participant, Strings.Android.KickFromSupergroup);
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

        private void CreateFlyoutItem(ref MenuFlyout flyout, Func<TLChannelParticipantBase, Visibility> visibility, ICommand command, object parameter, string text)
        {
            var value = visibility(parameter as TLChannelParticipantBase);
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

        private Visibility ParticipantPromote_Loaded(TLChannelParticipantBase participant)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return Visibility.Collapsed;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsAddAdmins)) && !(participant is TLChannelParticipantAdmin))
            {
                return participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private Visibility ParticipantRestrict_Loaded(TLChannelParticipantBase participant)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return Visibility.Collapsed;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsBanUsers)) && ((participant is TLChannelParticipantAdmin admin && admin.IsCanEdit) || (!(participant is TLChannelParticipantBanned) && !(participant is TLChannelParticipantAdmin))))
            {
                return participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        private Visibility ParticipantRemove_Loaded(TLChannelParticipantBase participant)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return Visibility.Collapsed;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsBanUsers)) && ((participant is TLChannelParticipantAdmin admin && admin.IsCanEdit) || (!(participant is TLChannelParticipantBanned) && !(participant is TLChannelParticipantAdmin))))
            {
                return participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        #endregion

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            MainHeader.Visibility = Visibility.Collapsed;
            SearchField.Visibility = Visibility.Visible;

            SearchField.Focus(FocusState.Keyboard);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                MainHeader.Visibility = Visibility.Visible;
                SearchField.Visibility = Visibility.Collapsed;

                Focus(FocusState.Programmatic);
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchField.Text))
            {
                ContentPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ContentPanel.Visibility = Visibility.Collapsed;
            }
        }
    }
}
