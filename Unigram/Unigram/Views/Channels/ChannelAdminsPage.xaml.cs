using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
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
    public sealed partial class ChannelAdminsPage : Page
    {
        public ChannelAdminsViewModel ViewModel => DataContext as ChannelAdminsViewModel;

        public ChannelAdminsPage()
        {
            InitializeComponent();
            DataContext = UnigramContainer.Current.ResolveType<ChannelAdminsViewModel>();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TLChannelParticipantAdmin participant && participant.User != null)
            {
                ViewModel.NavigationService.Navigate(typeof(ChannelAdminRightsPage), TLTuple.Create(ViewModel.Item.ToPeer(), participant));
            }
        }

        #region Context menu

        private void Participant_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var participant = element.DataContext as TLChannelParticipantBase;

            CreateFlyoutItem(ref flyout, ParticipantDismiss_Loaded, ViewModel.ParticipantDismissCommand, participant, Strings.Android.ChannelRemoveUserAdmin);

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

        private Visibility ParticipantDismiss_Loaded(TLChannelParticipantBase participant)
        {
            var channel = ViewModel.Item as TLChannel;
            if (channel == null)
            {
                return Visibility.Collapsed;
            }

            if ((channel.IsCreator || (channel.HasAdminRights && (channel.AdminRights.IsAddAdmins || channel.AdminRights.IsBanUsers))) && ((participant is TLChannelParticipantAdmin admin && admin.IsCanEdit) || participant is TLChannelParticipantBanned))
            {
                return participant is TLChannelParticipantCreator || participant.User.IsSelf ? Visibility.Collapsed : Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        #endregion

        #region Binding

        private string ConvertType(string broadcast, string mega)
        {
            if (ViewModel.Item is TLChannel channel)
            {
                return LocaleHelper.GetString(channel.IsBroadcast ? broadcast : mega);
            }

            return null;
        }

        #endregion
    }
}
