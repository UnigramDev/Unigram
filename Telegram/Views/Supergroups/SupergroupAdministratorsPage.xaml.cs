//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;

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
            var member = ScrollingHost.ItemFromContainer(sender) as ChatMember;

            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(ViewModel.EditMember, member, Strings.EditAdminRights, Icons.ShieldStar);
            flyout.CreateFlyoutItem(ViewModel.DismissMember, member, Strings.ChannelRemoveUserAdmin, Icons.SubtractCircle, destructive: true);
            flyout.ShowAt(sender, args);
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
            ChannelSignMessages.Visibility = group.CanChangeInfo() && group.IsChannel ? Visibility.Visible : Visibility.Collapsed;

            EventLog.Visibility = Visibility.Visible;
            AddNew.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Visibility = group.CanPromoteMembers() ? Visibility.Visible : Visibility.Collapsed;
            Footer.Text = group.IsChannel ? Strings.ChannelAdminsInfo : Strings.MegaAdminsInfo;

            HeaderPanel.Visibility = Visibility.Visible;

            ViewModel.UpdateSignMessages(group.SignMessages);
            ViewModel.UpdateShowMessageSender(group.ShowMessageSender);
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
            ChannelSignMessages.Visibility = Visibility.Collapsed;

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

        private string ConvertSignMessagesFooter(bool showMessageSender)
        {
            if (showMessageSender)
            {
                return Strings.ChannelSignProfilesInfo;
            }

            return Strings.ChannelSignMessagesInfo;
        }

        #endregion

    }
}
