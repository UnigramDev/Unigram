//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views.Profile
{
    public sealed partial class ProfileMembersTabPage : ProfileTabPage
    {
        public new ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfileMembersTabPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (ViewModel.ClientService.TryGetSupergroup(ViewModel.Chat, out Supergroup supergroup))
            {
                AddNew.Content = supergroup.IsChannel ? Strings.AddSubscriber : Strings.AddMember;
                AddNewPanel.Visibility = supergroup.CanInviteUsers(ViewModel.Chat) ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (ViewModel.ClientService.TryGetBasicGroup(ViewModel.Chat, out BasicGroup basicGroup))
            {
                AddNew.Content = Strings.AddMember;
                AddNewPanel.Visibility = basicGroup.CanInviteUsers(ViewModel.Chat) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatMember member)
            {
                ViewModel.NavigationService.NavigateToSender(member.MemberId);
            }
        }

        #region Context menu

        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var chat = ViewModel.Chat;
            var member = ScrollingHost.ItemFromContainer(sender) as ChatMember;

            if (chat == null || member == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            ChatMemberStatus status = null;
            if (chat.Type is ChatTypeBasicGroup basic)
            {
                status = ViewModel.ClientService.GetBasicGroup(basic.BasicGroupId)?.Status;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                status = ViewModel.ClientService.GetSupergroup(super.SupergroupId)?.Status;
            }

            if (status == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                flyout.CreateFlyoutItem(MemberPromote_Loaded, ViewModel.MembersTab.PromoteMember, chat, status, member, member.Status is ChatMemberStatusAdministrator ? Strings.EditAdminRights : Strings.SetAsAdmin, Icons.Star);
                flyout.CreateFlyoutItem(MemberRestrict_Loaded, ViewModel.MembersTab.RestrictMember, chat, status, member, member.Status is ChatMemberStatusRestricted ? Strings.ChangePermissions : Strings.KickFromSupergroup, Icons.LockClosed);
            }

            flyout.CreateFlyoutItem(MemberRemove_Loaded, ViewModel.MembersTab.RemoveMember, chat, status, member, chat.Type is ChatTypeSupergroup { IsChannel: true } ? Strings.ChannelRemoveUser : Strings.KickFromGroup, Icons.Block);

            flyout.ShowAt(sender, args);
        }

        private bool MemberPromote_Loaded(Chat chat, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator)
            {
                return false;
            }

            if (member.MemberId.IsUser(ViewModel.ClientService.Options.MyId))
            {
                return false;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanPromoteMembers;
        }

        private bool MemberRestrict_Loaded(Chat chat, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator { CanBeEdited: false })
            {
                return false;
            }

            if (member.MemberId.IsUser(ViewModel.ClientService.Options.MyId))
            {
                return false;
            }

            if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                return false;
            }

            return status is ChatMemberStatusCreator or ChatMemberStatusAdministrator { Rights.CanRestrictMembers: true };
        }

        private bool MemberRemove_Loaded(Chat chat, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator { CanBeEdited: false })
            {
                return false;
            }

            if (member.MemberId.IsUser(ViewModel.ClientService.Options.MyId))
            {
                return false;
            }

            if (chat.Type is ChatTypeBasicGroup && status is ChatMemberStatusAdministrator)
            {
                return member.InviterUserId == ViewModel.ClientService.Options.MyId;
            }

            return status is ChatMemberStatusCreator or ChatMemberStatusAdministrator { Rights.CanRestrictMembers: true };
        }

        #endregion

        #region Recycle

        protected override void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TableListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContextRequested += Member_ContextRequested;

                if (sender.ItemTemplateSelector == null)
                {
                    args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                }
            }

            if (sender.ItemTemplateSelector != null)
            {
                args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            try
            {
                if (args.InRecycleQueue || ViewModel == null)
                {
                    return;
                }
                else if (args.ItemContainer.ContentTemplateRoot is ProfileCell content)
                {
                    content.UpdateChatSharedMembers(ViewModel.ClientService, args, OnContainerContentChanging);
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex);
            }
        }

        #endregion
    }
}
