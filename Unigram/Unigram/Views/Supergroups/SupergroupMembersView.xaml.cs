using System;
using System.ComponentModel;
using System.Reactive.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Supergroups;
using Unigram.Views.Chats;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Unigram.Views.Supergroups
{
    public sealed partial class SupergroupMembersView : HostedUserControl, IProfileTab, IBasicAndSupergroupDelegate, INavigablePage, ISearchablePage
    {
        public SupergroupMembersViewModel ViewModel => DataContext as SupergroupMembersViewModel;

        public SupergroupMembersView()
        {
            InitializeComponent();

            var observable = Observable.FromEventPattern<TextChangedEventArgs>(SearchField, "TextChanged");
            var throttled = observable.Throttle(TimeSpan.FromMilliseconds(Constants.TypingTimeout)).ObserveOnDispatcher().Subscribe(x =>
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    ViewModel.Search?.Clear();
                }
                else
                {
                    ViewModel.Find(SearchField.Text);
                }
            });
        }

        public void Search()
        {
            Search_Click(null, null);
        }

        public int Index { get => 0; }
        public string Text { get => Strings.Resources.ChannelMembers; }

        public ListViewBase GetSelector()
        {
            return ScrollingHost;
        }

        public ScrollViewer GetScrollViewer()
        {
            return ScrollingHost.GetScrollViewer();
        }

        private bool _isLocked;

        private bool _isEmbedded;
        public bool IsEmbedded
        {
            get => _isEmbedded;
            set
            {
                Update(value, _isLocked);
            }
        }

        public void Update(bool embedded, bool locked)
        {
            _isEmbedded = embedded;
            _isLocked = locked;

            Header.Visibility = embedded ? Visibility.Collapsed : Visibility.Visible;
            ListHeader.Visibility = embedded ? Visibility.Collapsed : Visibility.Visible;
            ScrollingHost.Padding = new Thickness(0, embedded ? 12 : embedded ? 12 + 16 : 16, 0, 0);
            ScrollingHost.ItemsPanelCornerRadius = new CornerRadius(embedded ? 0 : 8, embedded ? 0 : 8, 8, 8);
            //ListHeader.Height = embedded && !locked ? 12 : embedded ? 12 + 16 : 16;

            if (embedded)
            {
                Footer.Visibility = Visibility.Collapsed;
            }
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

        private async void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatMember member)
            {
                var response = await ViewModel.ProtoService.SendAsync(new CreatePrivateChat(member.UserId, false));
                if (response is Chat chat)
                {
                    ViewModel.NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                }
            }
        }

        #region Context menu

        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var member = element.Tag as ChatMember;

            var chat = ViewModel.Chat;
            if (chat == null || member == null)
            {
                return;
            }

            ChatMemberStatus status = null;
            if (chat.Type is ChatTypeBasicGroup basic)
            {
                status = ViewModel.ProtoService.GetBasicGroup(basic.BasicGroupId)?.Status;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                status = ViewModel.ProtoService.GetSupergroup(super.SupergroupId)?.Status;
            }

            if (status == null)
            {
                return;
            }

            if (chat.Type is ChatTypeSupergroup)
            {
                flyout.CreateFlyoutItem(MemberPromote_Loaded, ViewModel.MemberPromoteCommand, chat.Type, status, member, Strings.Resources.SetAsAdmin, new FontIcon { Glyph = "\uE734" });
                flyout.CreateFlyoutItem(MemberRestrict_Loaded, ViewModel.MemberRestrictCommand, chat.Type, status, member, Strings.Resources.KickFromSupergroup, new FontIcon { Glyph = "\uE72E" });
            }

            flyout.CreateFlyoutItem(MemberRemove_Loaded, ViewModel.MemberRemoveCommand, chat.Type, status, member, Strings.Resources.KickFromGroup, new FontIcon { Glyph = "\uF140" });

            args.ShowAt(flyout, element);
        }

        private bool MemberPromote_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusAdministrator)
            {
                return false;
            }

            if (member.UserId == ViewModel.CacheService.Options.MyId)
            {
                return false;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.CanPromoteMembers;
        }

        private bool MemberRestrict_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusRestricted || member.Status is ChatMemberStatusAdministrator admin && !admin.CanBeEdited)
            {
                return false;
            }

            if (member.UserId == ViewModel.CacheService.Options.MyId)
            {
                return false;
            }

            if (chatType is ChatTypeSupergroup supergroup && supergroup.IsChannel)
            {
                return false;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.CanRestrictMembers;
        }

        private bool MemberRemove_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusAdministrator admin && !admin.CanBeEdited)
            {
                return false;
            }

            if (member.UserId == ViewModel.CacheService.Options.MyId)
            {
                return false;
            }

            if (chatType is ChatTypeBasicGroup && status is ChatMemberStatusAdministrator)
            {
                return member.InviterUserId == ViewModel.CacheService.Options.MyId;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.CanRestrictMembers;
        }

        #endregion

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContextRequested += Member_ContextRequested;
            }

            args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
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

            args.ItemContainer.Tag = args.Item;
            content.Tag = args.Item;

            var user = ViewModel.ProtoService.GetUser(member.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var title = content.Children[1] as TextBlock;
                title.Text = user.GetFullName();
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
                        label.Text = string.IsNullOrEmpty(administrator.CustomTitle) ? Strings.Resources.ChannelAdmin : administrator.CustomTitle;
                    }
                    else if (member.Status is ChatMemberStatusCreator creator)
                    {
                        label.Text = string.IsNullOrEmpty(creator.CustomTitle) ? Strings.Resources.ChannelCreator : creator.CustomTitle;
                    }
                    else
                    {
                        label.Text = string.Empty;
                    }
                }
                else
                {
                    subtitle.Text = ChannelParticipantToTypeConverter.Convert(ViewModel.ProtoService, member);
                    label.Text = string.Empty;
                }
            }
            else if (args.Phase == 2)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = PlaceholderHelper.GetUser(ViewModel.ProtoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(OnContainerContentChanging);
            }

            args.Handled = true;
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

        #region Delegate

        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Title.Text = group.IsChannel ? Strings.Resources.ChannelSubscribers : Strings.Resources.ChannelMembers;

            AddNew.Content = group.IsChannel ? Strings.Resources.AddSubscriber : Strings.Resources.AddMember;
            AddNewPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;

            Footer.Visibility = group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            Title.Text = Strings.Resources.ChannelMembers;

            AddNew.Content = Strings.Resources.AddMember;
            AddNewPanel.Visibility = group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;

            Footer.Visibility = Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo) { }
        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo) { }
        public void UpdateChat(Chat chat) { }
        public void UpdateChatTitle(Chat chat) { }
        public void UpdateChatPhoto(Chat chat) { }

        #endregion
    }
}
