using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Controls;
using Unigram.ViewModels.Channels;
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

namespace Unigram.Views.Channels
{
    public sealed partial class ChannelParticipantsPage : Page, IMasterDetailPage
    {
        public ChannelParticipantsViewModel ViewModel => DataContext as ChannelParticipantsViewModel;

        public ChannelParticipantsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChannelParticipantsViewModel>();

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

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLChannelParticipantBase participant && participant.User != null)
            {
                ViewModel.NavigationService.Navigate(typeof(UserDetailsPage), participant.User.ToPeer());
            }
        }

        #region Context menu

        private void Participant_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var participant = element.DataContext as TLChannelParticipantBase;

            CreateFlyoutItem(ref flyout, ParticipantPromote_Loaded, ViewModel.ParticipantPromoteCommand, participant, Strings.Android.SetAsAdmin);
            CreateFlyoutItem(ref flyout, ParticipantRemove_Loaded, ViewModel.ParticipantRemoveCommand, participant, Strings.Android.ChannelRemoveUser);

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
