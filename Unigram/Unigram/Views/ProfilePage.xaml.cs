using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Template10.Common;
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
using Telegram.Td.Api;
using Unigram.Converters;
using Unigram.Collections;
using Unigram.ViewModels.Chats;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Documents;
using Windows.UI.Text;
using Unigram.ViewModels.Delegates;
using System.Reactive.Linq;
using Unigram.Controls.Gallery;

namespace Unigram.Views
{
    public sealed partial class ProfilePage : Page, IProfileDelegate
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfilePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ProfileViewModel, IProfileDelegate>(this);

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

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var user = ViewModel.ProtoService.GetUser(chat);
                if (user == null || user.ProfilePhoto == null)
                {
                    return;
                }

                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, ViewModel.Aggregator, user);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
            else if (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup)
            {
                if (chat.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.Aggregator, chat);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
        }

        private void Notifications_Toggled(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleSwitch;
            if (toggle.FocusState != FocusState.Unfocused)
            {
                ViewModel.ToggleMuteCommand.Execute(toggle.IsOn);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
        }

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            Notifications.IsOn = chat.NotificationSettings.MuteFor == 0;

            Call.Visibility = Visibility.Collapsed;
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.ProtoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 64);
        }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);

            Verified.Visibility = user.IsVerified ? Visibility.Visible : Visibility.Collapsed;

            UserPhone.Content = PhoneNumber.Format(user.PhoneNumber);
            UserPhone.Visibility = string.IsNullOrEmpty(user.PhoneNumber) ? Visibility.Collapsed : Visibility.Visible;

            Username.Content = $"@{user.Username}";
            Username.Visibility = string.IsNullOrEmpty(user.Username) ? Visibility.Collapsed : Visibility.Visible;

            DescriptionTitle.Text = user.Type is UserTypeBot ? "About" : Strings.Resources.UserBio;
            DescriptionTitle.Visibility = Visibility.Visible;
            DescriptionLabel.Padding = new Thickness(12, 0, 12, 12);

            if (user.Id == ViewModel.ProtoService.GetMyId())
            {
                Notifications.Visibility = Visibility.Collapsed;
            }
            else
            {
                Notifications.Visibility = Visibility.Visible;
            }

            if (secret)
            {
                UserStartSecret.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (user.Type is UserTypeBot || user.Id == ViewModel.ProtoService.GetMyId())
                {
                    UserStartSecret.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UserStartSecret.Visibility = Visibility.Visible;
                }

                SecretLifetime.Visibility = Visibility.Collapsed;
                SecretHashKey.Visibility = Visibility.Collapsed;
            }

            // Unused:
            GroupLeave.Visibility = Visibility.Collapsed;
            GroupInvite.Visibility = Visibility.Collapsed;

            EventLog.Visibility = Visibility.Collapsed;
            Admins.Visibility = Visibility.Collapsed;
            Banned.Visibility = Visibility.Collapsed;
            Restricted.Visibility = Visibility.Collapsed;
            Members.Visibility = Visibility.Collapsed;
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (user.Type is UserTypeBot)
            {
                GetEntities(fullInfo.Bio);
            }
            else
            {
                DescriptionSpan.Inlines.Clear();
                DescriptionSpan.Inlines.Add(new Run { Text = fullInfo.Bio });
            }

            DescriptionPanel.Visibility = string.IsNullOrEmpty(fullInfo.Bio) ? Visibility.Collapsed : Visibility.Visible;

            UserCommonChats.Badge = fullInfo.GroupInCommonCount;
            UserCommonChats.Visibility = fullInfo.GroupInCommonCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Call.Visibility = fullInfo.CanBeCalled ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            if (secretChat.State is SecretChatStateReady ready)
            {
                SecretLifetime.Badge = Locale.FormatTtl(secretChat.Ttl);
                //SecretIdenticon.Source = PlaceholderHelper.GetIdenticon(secretChat.KeyHash, 24);

                SecretLifetime.Visibility = Visibility.Visible;
                SecretHashKey.Visibility = Visibility.Visible;
            }
            else
            {
                SecretLifetime.Visibility = Visibility.Collapsed;
                SecretHashKey.Visibility = Visibility.Collapsed;
            }
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            Subtitle.Text = Locale.Declension("Members", group.MemberCount);

            GroupInvite.Visibility = group.Status is ChatMemberStatusCreator || (group.Status is ChatMemberStatusAdministrator administrator && administrator.CanInviteUsers) || group.EveryoneIsAdministrator ? Visibility.Visible : Visibility.Collapsed;

            // Unused:
            Verified.Visibility = Visibility.Collapsed;
            UserPhone.Visibility = Visibility.Collapsed;
            Username.Visibility = Visibility.Collapsed;

            DescriptionPanel.Visibility = Visibility.Collapsed;

            UserCommonChats.Visibility = Visibility.Collapsed;
            UserStartSecret.Visibility = Visibility.Collapsed;

            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;

            GroupLeave.Visibility = Visibility.Collapsed;

            EventLog.Visibility = Visibility.Collapsed;
            Admins.Visibility = Visibility.Collapsed;
            Banned.Visibility = Visibility.Collapsed;
            Restricted.Visibility = Visibility.Collapsed;
            Members.Visibility = Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            ViewModel.Members = new SortedObservableCollection<ChatMember>(new ChatMemberComparer(ViewModel.ProtoService, true), fullInfo.Members);
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Subtitle.Text = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", group.MemberCount);

            Verified.Visibility = group.IsVerified ? Visibility.Visible : Visibility.Collapsed;

            Username.Content = $"@{group.Username}";
            Username.Visibility = string.IsNullOrEmpty(group.Username) ? Visibility.Collapsed : Visibility.Visible;

            DescriptionTitle.Visibility = Visibility.Collapsed;
            DescriptionLabel.Padding = new Thickness(12);

            if (group.IsChannel && !(group.Status is ChatMemberStatusCreator) && !(group.Status is ChatMemberStatusLeft) && !(group.Status is ChatMemberStatusBanned))
            {
                GroupLeave.Visibility = Visibility.Visible;
            }
            else
            {
                GroupLeave.Visibility = Visibility.Collapsed;
            }

            GroupInvite.Visibility = !group.IsChannel && group.CanInviteUsers() ? Visibility.Visible : Visibility.Collapsed;

            EventLog.Visibility = group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator ? Visibility.Visible : Visibility.Collapsed;

            if (!group.IsChannel)
            {
                ViewModel.Members = ViewModel.CreateMembers(group.Id);
            }

            // Unused:
            UserPhone.Visibility = Visibility.Collapsed;
            UserCommonChats.Visibility = Visibility.Collapsed;
            UserStartSecret.Visibility = Visibility.Collapsed;
            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            GetEntities(fullInfo.Description);
            DescriptionPanel.Visibility = string.IsNullOrEmpty(fullInfo.Description) ? Visibility.Collapsed : Visibility.Visible;

            Admins.Badge = fullInfo.AdministratorCount;
            Admins.Visibility = fullInfo.AdministratorCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Banned.Badge = fullInfo.BannedCount;
            Banned.Visibility = fullInfo.BannedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Restricted.Badge = fullInfo.RestrictedCount;
            Restricted.Visibility = fullInfo.RestrictedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Members.Badge = fullInfo.MemberCount;
            Members.Visibility = fullInfo.CanGetMembers && group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
        }



        public void UpdateFile(File file)
        {
            var chat = ViewModel.Chat;
            if (chat != null && chat.UpdateFile(file))
            {
                Photo.Source = PlaceholderHelper.GetChat(null, chat, 64);
            }

            for (int i = 0; i < ScrollingHost.Items.Count; i++)
            {
                var member = ScrollingHost.Items[i] as ChatMember;

                var user = ViewModel.ProtoService.GetUser(member.UserId);
                if (user == null)
                {
                    return;
                }

                if (user.UpdateFile(file))
                {
                    var container = ScrollingHost.ContainerFromIndex(i) as ListViewItem;
                    if (container == null)
                    {
                        return;
                    }

                    var content = container.ContentTemplateRoot as Grid;

                    var photo = content.Children[0] as ProfilePicture;
                    photo.Source = PlaceholderHelper.GetUser(null, user, 36);
                }
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

            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate || chat.Type is ChatTypeSecret)
            {
                var userId = chat.Type is ChatTypePrivate privata ? privata.UserId : chat.Type is ChatTypeSecret secret ? secret.UserId : 0;
                if (userId != ViewModel.ProtoService.GetMyId())
                {
                    var user = ViewModel.ProtoService.GetUser(userId);
                    if (user == null)
                    {
                        return;
                    }

                    var fullInfo = ViewModel.ProtoService.GetUserFull(userId);
                    if (fullInfo == null)
                    {
                        return;
                    }

                    //if (fullInfo.CanBeCalled)
                    //{
                    //    callItem = menu.addItem(call_item, R.drawable.ic_call_white_24dp);
                    //}
                    if (user.OutgoingLink is LinkStateIsContact)
                    {
                        CreateFlyoutItem(ref flyout, ViewModel.ShareCommand, Strings.Resources.ShareContact);
                        CreateFlyoutItem(ref flyout, fullInfo.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, fullInfo.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact);
                        CreateFlyoutItem(ref flyout, ViewModel.EditCommand, Strings.Resources.EditContact);
                        CreateFlyoutItem(ref flyout, ViewModel.DeleteCommand, Strings.Resources.DeleteContact);
                    }
                    else
                    {
                        if (user.Type is UserTypeBot bot)
                        {
                            if (bot.CanJoinGroups)
                            {
                                CreateFlyoutItem(ref flyout, ViewModel.InviteCommand, Strings.Resources.BotInvite);
                            }

                            CreateFlyoutItem(ref flyout, null, Strings.Resources.BotShare);
                        }

                        if (user.PhoneNumber != null && user.PhoneNumber.Length > 0)
                        {
                            CreateFlyoutItem(ref flyout, ViewModel.AddCommand, Strings.Resources.AddContact);
                            CreateFlyoutItem(ref flyout, ViewModel.ShareCommand, Strings.Resources.ShareContact);
                            CreateFlyoutItem(ref flyout, fullInfo.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, fullInfo.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact);
                        }
                        else
                        {
                            if (user.Type is UserTypeBot)
                            {
                                CreateFlyoutItem(ref flyout, fullInfo.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, fullInfo.IsBlocked ? Strings.Resources.BotRestart : Strings.Resources.BotStop);
                            }
                            else
                            {
                                CreateFlyoutItem(ref flyout, fullInfo.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, fullInfo.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact);
                            }
                        }
                    }
                }
                else
                {
                    CreateFlyoutItem(ref flyout, ViewModel.ShareCommand, Strings.Resources.ShareContact);
                }
            }
            //if (writeButton != null)
            //{
            //    boolean isChannel = ChatObject.isChannel(currentChat);
            //    if (isChannel && !ChatObject.canChangeChatInfo(currentChat) || !isChannel && !currentChat.admin && !currentChat.creator && currentChat.admins_enabled)
            //    {
            //        writeButton.setImageResource(R.drawable.floating_message);
            //        writeButton.setPadding(0, AndroidUtilities.dp(3), 0, 0);
            //    }
            //    else
            //    {
            //        writeButton.setImageResource(R.drawable.floating_camera);
            //        writeButton.setPadding(0, 0, 0, 0);
            //    }
            //}
            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                if (supergroup == null)
                {
                    return;
                }

                if (supergroup.Status is ChatMemberStatusCreator || supergroup.Status is ChatMemberStatusAdministrator)
                {
                    if (supergroup.IsChannel)
                    {
                        CreateFlyoutItem(ref flyout, ViewModel.EditCommand, Strings.Resources.ManageChannelMenu);
                    }
                    else
                    {
                        CreateFlyoutItem(ref flyout, ViewModel.EditCommand, Strings.Resources.ManageGroupMenu);
                    }
                }

                if (!supergroup.IsChannel)
                {
                    CreateFlyoutItem(ref flyout, new RelayCommand(() =>
                    {
                        flyout.Closed += (s, args) =>
                        {
                            Search_Click(null, null);
                        };

                    }), Strings.Resources.SearchMembers);

                    if (!(supergroup.Status is ChatMemberStatusCreator) && !(supergroup.Status is ChatMemberStatusLeft) && !(supergroup.Status is ChatMemberStatusBanned))
                    {
                        CreateFlyoutItem(ref flyout, ViewModel.DeleteCommand, Strings.Resources.LeaveMegaMenu);
                    }
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var basicGroup = ViewModel.ProtoService.GetBasicGroup(basic.BasicGroupId);
                if (basicGroup == null)
                {
                    return;
                }

                //if (!chat.admins_enabled || chat.creator || chat.admin)
                //{
                //    editItem = menu.addItem(edit_name, R.drawable.group_edit_profile);
                //}
                //item = menu.addItem(10, R.drawable.ic_ab_other);
                if (basicGroup.Status is ChatMemberStatusCreator)
                {
                    CreateFlyoutItem(ref flyout, ViewModel.SetAdminsCommand, Strings.Resources.SetAdmins);
                }
                if (!basicGroup.EveryoneIsAdministrator || basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator)
                {
                    CreateFlyoutItem(ref flyout, ViewModel.EditCommand, Strings.Resources.ChannelEdit);
                }

                CreateFlyoutItem(ref flyout, new RelayCommand(() =>
                {
                    flyout.Closed += (s, args) =>
                    {
                        Search_Click(null, null);
                    };

                }), Strings.Resources.SearchMembers);

                if (basicGroup.Status is ChatMemberStatusCreator && basicGroup.MemberCount > 0)
                {
                    CreateFlyoutItem(ref flyout, ViewModel.MigrateCommand, Strings.Resources.ConvertGroupMenu);
                }

                CreateFlyoutItem(ref flyout, ViewModel.DeleteCommand, Strings.Resources.DeleteAndExit);
            }

            CreateFlyoutItem(ref flyout, null, Strings.Resources.AddShortcut);

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


        private void Member_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var member = element.Tag as ChatMember;

            var chat = ViewModel.Chat;
            if (chat == null)
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
                CreateFlyoutItem(ref flyout, MemberPromote_Loaded, ViewModel.MemberPromoteCommand, chat.Type,status, member, Strings.Resources.SetAsAdmin);
                CreateFlyoutItem(ref flyout, MemberRestrict_Loaded, ViewModel.MemberRestrictCommand, chat.Type, status, member, Strings.Resources.KickFromSupergroup);
            }

            CreateFlyoutItem(ref flyout, MemberRemove_Loaded, ViewModel.MemberRemoveCommand, chat.Type, status, member, Strings.Resources.KickFromGroup);

            if (flyout.Items.Count > 0 && args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
        }

        private void CreateFlyoutItem(ref MenuFlyout flyout, Func<ChatType, ChatMemberStatus, ChatMember, Visibility> visibility, ICommand command, ChatType chatType, ChatMemberStatus status, object parameter, string text)
        {
            var value = visibility(chatType, status, parameter as ChatMember);
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

        private Visibility MemberPromote_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusAdministrator)
            {
                return Visibility.Collapsed;
            }

            if (member.UserId == ViewModel.ProtoService.GetMyId())
            {
                return Visibility.Collapsed;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.CanPromoteMembers ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility MemberRestrict_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusRestricted || member.Status is ChatMemberStatusAdministrator admin && !admin.CanBeEdited)
            {
                return Visibility.Collapsed;
            }

            if (member.UserId == ViewModel.ProtoService.GetMyId())
            {
                return Visibility.Collapsed;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.CanRestrictMembers ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility MemberRemove_Loaded(ChatType chatType, ChatMemberStatus status, ChatMember member)
        {
            if (member.Status is ChatMemberStatusCreator || member.Status is ChatMemberStatusAdministrator admin && !admin.CanBeEdited)
            {
                return Visibility.Collapsed;
            }

            if (member.UserId == ViewModel.ProtoService.GetMyId())
            {
                return Visibility.Collapsed;
            }

            if (chatType is ChatTypeBasicGroup && status is ChatMemberStatusAdministrator)
            {
                return member.InviterUserId == ViewModel.ProtoService.GetMyId() ? Visibility.Visible : Visibility.Collapsed;
            }

            return status is ChatMemberStatusCreator || status is ChatMemberStatusAdministrator administrator && administrator.CanRestrictMembers ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Recycle

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var member = args.Item as ChatMember;

            content.Tag = member;

            var user = ViewModel.ProtoService.GetUser(member.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                var photo = content.Children[0] as ProfilePicture;
                photo.Source = null;

                var title = content.Children[1] as TextBlock;
                if (title.Inlines.Count > 0)
                {
                    var label = title.Inlines[0] as Run;
                    label.Text = user.GetFullName();
                }
                else
                {
                    title.Text = user.GetFullName();
                }
            }
            else if (args.Phase == 1)
            {
                var subtitle = content.Children[2] as TextBlock;
                subtitle.Text = LastSeenConverter.GetLabel(user, false);
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

        #region Entities

        private void GetEntities(string text)
        {
            DescriptionSpan.Inlines.Clear();

            var response = ViewModel.ProtoService.Execute(new GetTextEntities(text));
            if (response is TextEntities entities)
            {
                ReplaceEntities(DescriptionSpan, text, entities.Entities);
            }
            else
            {
                DescriptionSpan.Inlines.Add(new Run { Text = text });
            }
        }

        private void ReplaceEntities(Span span, string text, IList<TextEntity> entities)
        {
            var previous = 0;

            foreach (var entity in entities.OrderBy(x => x.Offset))
            {
                if (entity.Offset > previous)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(previous, entity.Offset - previous) });
                }

                if (entity.Length + entity.Offset > text.Length)
                {
                    previous = entity.Offset + entity.Length;
                    continue;
                }

                if (entity.Type is TextEntityTypeBold)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontWeight = FontWeights.SemiBold });
                }
                else if (entity.Type is TextEntityTypeItalic)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontStyle = FontStyle.Italic });
                }
                else if (entity.Type is TextEntityTypeCode)
                {
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypePre || entity.Type is TextEntityTypePreCode)
                {
                    // TODO any additional
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypeUrl || entity.Type is TextEntityTypeEmailAddress || entity.Type is TextEntityTypePhoneNumber || entity.Type is TextEntityTypeMention || entity.Type is TextEntityTypeHashtag || entity.Type is TextEntityTypeCashtag || entity.Type is TextEntityTypeBotCommand)
                {
                    var hyperlink = new Hyperlink();
                    var data = text.Substring(entity.Offset, entity.Length);

                    hyperlink.Click += (s, args) => Entity_Click(entity.Type, data);
                    hyperlink.Inlines.Add(new Run { Text = data });
                    //hyperlink.Foreground = foreground;
                    span.Inlines.Add(hyperlink);

                    if (entity.Type is TextEntityTypeUrl)
                    {
                        MessageHelper.SetEntity(hyperlink, data);
                    }
                }
                else if (entity.Type is TextEntityTypeTextUrl || entity.Type is TextEntityTypeMentionName)
                {
                    var hyperlink = new Hyperlink();
                    object data;
                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        data = textUrl.Url;
                        MessageHelper.SetEntity(hyperlink, textUrl.Url);
                        ToolTipService.SetToolTip(hyperlink, textUrl.Url);
                    }
                    else if (entity.Type is TextEntityTypeMentionName mentionName)
                    {
                        data = mentionName.UserId;
                    }

                    hyperlink.Click += (s, args) => Entity_Click(entity.Type, null);
                    hyperlink.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length) });
                    //hyperlink.Foreground = foreground;
                    span.Inlines.Add(hyperlink);
                }

                previous = entity.Offset + entity.Length;
            }

            if (text.Length > previous)
            {
                span.Inlines.Add(new Run { Text = text.Substring(previous) });
            }
        }

        private void Entity_Click(TextEntityType type, string data)
        {
            if (type is TextEntityTypeBotCommand)
            {

            }
            else if (type is TextEntityTypeEmailAddress)
            {
                ViewModel.OpenUrl("mailto:" + data, false);
            }
            else if (type is TextEntityTypePhoneNumber)
            {
                ViewModel.OpenUrl("tel:" + data, false);
            }
            else if (type is TextEntityTypeHashtag || type is TextEntityTypeCashtag)
            {

            }
            else if (type is TextEntityTypeMention)
            {
                ViewModel.OpenUsername(data);
            }
            else if (type is TextEntityTypeMentionName mentionName)
            {
                ViewModel.OpenUser(mentionName.UserId);
            }
            else if (type is TextEntityTypeTextUrl textUrl)
            {
                ViewModel.OpenUrl(textUrl.Url, true);
            }
            else if (type is TextEntityTypeUrl)
            {
                ViewModel.OpenUrl(data, false);
            }
        }

        #endregion

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
