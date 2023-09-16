//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupAdministratorsPage : HostedPage, IBasicAndSupergroupDelegate
    {
        public SupergroupAdministratorsViewModel ViewModel => DataContext as SupergroupAdministratorsViewModel;

        public SupergroupAdministratorsPage()
        {
            InitializeComponent();
            Title = Strings.ChannelAdministrators;
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ViewModel.EditMember(e.ClickedItem as ChatMember);
        }

        #region Context menu

        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var member = ScrollingHost.ItemFromContainer(sender) as ChatMember;

            flyout.CreateFlyoutItem(ViewModel.EditMember, member, Strings.EditAdminRights, Icons.ShieldStar);
            flyout.CreateFlyoutItem(ViewModel.DismissMember, member, Strings.ChannelRemoveUserAdmin, Icons.SubtractCircle, destructive: true);

            args.ShowAt(flyout, element);
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Member_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
            {
                content.UpdateSupergroupMember(ViewModel.ClientService, args, OnContainerContentChanging);
            }
        }

        #endregion

        #region Binding

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            HeaderPanel.Footer = group.CanDeleteMessages() && !group.IsChannel ? Strings.ChannelAntiSpamInfo : string.Empty;
            AntiSpam.Visibility = group.CanDeleteMessages() && !group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            EventLog.Visibility = Visibility.Visible;
            AddNew.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = group.IsChannel ? Strings.ChannelAdminsInfo : Strings.MegaAdminsInfo;

            HeaderPanel.Visibility = Visibility.Visible;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ViewModel.UpdateIsAggressiveAntiSpamEnabled(fullInfo.HasAggressiveAntiSpamEnabled);
            AntiSpam.Visibility = fullInfo.CanToggleAggressiveAntiSpam ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            HeaderPanel.Footer = string.Empty;
            AntiSpam.Visibility = Visibility.Collapsed;

            EventLog.Visibility = Visibility.Collapsed;
            AddNew.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = Strings.MegaAdminsInfo;

            HeaderPanel.Visibility = EventLog.Visibility == Visibility.Visible || AddNew.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ViewModel.UpdateIsAggressiveAntiSpamEnabled(false);
            AntiSpam.Visibility = fullInfo.CanToggleAggressiveAntiSpam ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

    }
}
