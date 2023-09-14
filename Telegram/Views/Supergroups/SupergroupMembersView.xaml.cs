//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Supergroups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Supergroups
{
    public sealed partial class SupergroupMembersPage : HostedPage, IBasicAndSupergroupDelegate, ISearchablePage
    {
        public SupergroupMembersViewModel ViewModel => DataContext as SupergroupMembersViewModel;

        public SupergroupMembersPage()
        {
            InitializeComponent();
            Title = Strings.ChannelSubscribers;
        }

        public void Search()
        {
            SearchField.StartBringIntoView();
            SearchField.Focus(FocusState.Keyboard);
        }

        private bool _isLocked;

        private bool _isEmbedded;
        public bool IsEmbedded
        {
            get => _isEmbedded;
            set => Update(value, _isLocked);
        }

        public void Update(bool embedded, bool locked)
        {
            _isEmbedded = embedded;
            _isLocked = locked;

            ListHeader.Visibility = embedded ? Visibility.Collapsed : Visibility.Visible;
            ScrollingHost.Padding = new Thickness(0, embedded ? 12 : embedded ? 12 + 16 : 16, 0, 0);
            //ListHeader.Height = embedded && !locked ? 12 : embedded ? 12 + 16 : 16;

            if (embedded)
            {
                Footer.Visibility = Visibility.Collapsed;
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
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var member = ScrollingHost.ItemFromContainer(sender) as ChatMember;

            var chat = ViewModel.Chat;
            if (chat == null || member == null)
            {
                return;
            }

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
                flyout.CreateFlyoutItem(MemberPromote_Loaded, ViewModel.PromoteMember, chat.Type, status, member, member.Status is ChatMemberStatusAdministrator ? Strings.EditAdminRights : Strings.SetAsAdmin, Icons.Star);
                flyout.CreateFlyoutItem(MemberRestrict_Loaded, ViewModel.RestrictMemeber, chat.Type, status, member, Strings.KickFromSupergroup, Icons.LockClosed);
            }

            flyout.CreateFlyoutItem(MemberRemove_Loaded, ViewModel.RemoveMember, chat.Type, status, member, Strings.KickFromGroup, Icons.Block);

            args.ShowAt(flyout, element);
        }

        private bool MemberPromote_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
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

        private bool MemberRestrict_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusRestricted || member.Status is ChatMemberStatusAdministrator admin && !admin.CanBeEdited)
            {
                return false;
            }

            if (member.MemberId.IsUser(ViewModel.ClientService.Options.MyId))
            {
                return false;
            }

            if (chatType is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                return false;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanRestrictMembers;
        }

        private bool MemberRemove_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusAdministrator admin && !admin.CanBeEdited)
            {
                return false;
            }

            if (member.MemberId.IsUser(ViewModel.ClientService.Options.MyId))
            {
                return false;
            }

            if (chatType is ChatTypeBasicGroup && status is ChatMemberStatusAdministrator)
            {
                return member.InviterUserId == ViewModel.ClientService.Options.MyId;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanRestrictMembers;
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
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
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;

            var member = args.Item as ChatMember;
            if (member == null)
            {
                return;
            }

            var user = ViewModel.ClientService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                var label = content.Children[3] as TextBlock;

                if (_isEmbedded)
                {
                    subtitle.Text = LastSeenConverter.GetLabel(user, false);

                    if (member.Status is ChatMemberStatusAdministrator administrator)
                    {
                        label.Text = string.IsNullOrEmpty(administrator.CustomTitle) ? Strings.ChannelAdmin : administrator.CustomTitle;
                    }
                    else if (member.Status is ChatMemberStatusCreator creator)
                    {
                        label.Text = string.IsNullOrEmpty(creator.CustomTitle) ? Strings.ChannelCreator : creator.CustomTitle;
                    }
                    else
                    {
                        label.Text = string.Empty;
                    }
                }
                else
                {
                    subtitle.Text = ChannelParticipantToTypeConverter.Convert(ViewModel.ClientService, member);
                    label.Text = string.Empty;
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.SetUser(ViewModel.ClientService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
        }

        #endregion

        #region Delegate

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Title = group.IsChannel ? Strings.ChannelSubscribers : Strings.ChannelMembers;
            SearchField.PlaceholderText = group.IsChannel ? Strings.ChannelSubscribers : Strings.ChannelMembers;

            AddNew.Content = group.IsChannel ? Strings.AddSubscriber : Strings.AddMember;
            AddNewPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;

            Footer.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            SearchField.PlaceholderText = Strings.ChannelMembers;

            AddNew.Content = Strings.AddMember;
            AddNewPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;

            Footer.Visibility = Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            ViewModel.UpdateHiddenMembers(fullInfo.HasHiddenMembers);
            HideMembers.Visibility = fullInfo.CanHideMembers ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ViewModel.UpdateHiddenMembers(false);
            HideMembers.Visibility = fullInfo.CanHideMembers ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion
    }
}
