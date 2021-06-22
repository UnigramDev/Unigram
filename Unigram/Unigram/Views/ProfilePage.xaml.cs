using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Users;
using Unigram.Views.Supergroups;
using Unigram.Views.Users;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Point = Windows.Foundation.Point;

namespace Unigram.Views
{
    public sealed partial class ProfilePage : HostedPage, IProfileDelegate
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfilePage()
        {
            InitializeComponent();
            DataContext = TLContainer.Current.Resolve<ProfileViewModel, IProfileDelegate>(this);
            SharedMedia.DataContext = ViewModel.ChatSharedMedia;
            SharedMedia.ViewModel.Delegate = SharedMedia;

            DescriptionLabel.AddHandler(ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>(About_ContextRequested), true);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SharedMedia.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SharedMedia.OnNavigatedFrom(e);
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = ViewModel.ProtoService.GetUser(chat);
                if (user == null || user.ProfilePhoto == null)
                {
                    return;
                }

                var userFull = ViewModel.ProtoService.GetUserFull(user.Id);
                if (userFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, ViewModel.Aggregator, user, userFull);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                var basicGroupFull = ViewModel.ProtoService.GetBasicGroupFull(chat);
                if (basicGroupFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.Aggregator, chat, basicGroupFull.Photo);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
            else if (chat.Type is ChatTypeSupergroup)
            {
                var supergroupFull = ViewModel.ProtoService.GetSupergroupFull(chat);
                if (supergroupFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.Aggregator, chat, supergroupFull.Photo);
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

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            var unmuted = ViewModel.CacheService.Notifications.GetMutedFor(chat) == 0;
            Notifications.IsOn = unmuted;
            NotificationGlyph.Text = unmuted ? Icons.Alert : Icons.AlertOff;

            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.ProtoService.GetTitle(chat);
            TitleInfo.Text = Title.Text;
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.Source = PlaceholderHelper.GetChat(ViewModel.ProtoService, chat, 64);
            PhotoInfo.Source = Photo.Source;
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            var unmuted = ViewModel.CacheService.Notifications.GetMutedFor(chat) == 0;
            NotificationGlyph.Text = unmuted ? Icons.Alert : Icons.AlertOff;
        }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
            SubtitleInfo.Text = Subtitle.Text;

            Verified.Visibility = user.IsVerified ? Visibility.Visible : Visibility.Collapsed;

            UserPhone.Badge = PhoneNumber.Format(user.PhoneNumber);
            UserPhone.Visibility = string.IsNullOrEmpty(user.PhoneNumber) ? Visibility.Collapsed : Visibility.Visible;

            Username.Badge = $"{user.Username}";
            Username.Visibility = string.IsNullOrEmpty(user.Username) ? Visibility.Collapsed : Visibility.Visible;

            Description.Content = user.Type is UserTypeBot ? Strings.Resources.DescriptionPlaceholder : Strings.Resources.UserBio;

            if (user.Id == ViewModel.CacheService.Options.MyId)
            {
                NotificationsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                NotificationsPanel.Visibility = Visibility.Visible;
            }

            if (secret)
            {
                UserStartSecret.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (user.Type is UserTypeBot ||
                    user.Id == ViewModel.CacheService.Options.MyId ||
                    LastSeenConverter.IsServiceUser(user) ||
                    LastSeenConverter.IsSupportUser(user) ||
                    user.Type is UserTypeDeleted)
                {
                    MiscPanel.Visibility = Visibility.Collapsed;
                    UserStartSecret.Visibility = Visibility.Collapsed;
                }
                else
                {
                    MiscPanel.Visibility = Visibility.Visible;
                    UserStartSecret.Visibility = Visibility.Visible;
                }

                SecretLifetime.Visibility = Visibility.Collapsed;
                SecretHashKey.Visibility = Visibility.Collapsed;
            }

            // Unused:
            Location.Visibility = Visibility.Collapsed;

            GroupLeave.Visibility = Visibility.Collapsed;
            GroupInvite.Visibility = Visibility.Collapsed;

            ChannelMembersPanel.Visibility = Visibility.Collapsed;
            MembersPanel.Visibility = Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (user.Type is UserTypeBot)
            {
                GetEntities(fullInfo.ShareText);
                Description.Visibility = string.IsNullOrEmpty(fullInfo.ShareText) ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                GetEntities(fullInfo.Bio);
                Description.Visibility = string.IsNullOrEmpty(fullInfo.Bio) ? Visibility.Collapsed : Visibility.Visible;
            }

            //UserCommonChats.Badge = fullInfo.GroupInCommonCount;
            //UserCommonChats.Visibility = fullInfo.GroupInCommonCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (fullInfo.GroupInCommonCount > 0)
            {
                SharedMedia.Tab = new UserCommonChatsView { DataContext = ViewModel.UserCommonChats, IsEmbedded = true };
            }
            else
            {
                SharedMedia.Tab = null;
            }

            Call.Visibility = fullInfo.CanBeCalled ? Visibility.Visible : Visibility.Collapsed;
            VideoCall.Visibility = fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;

            Edit.Visibility = Visibility.Collapsed;
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            if (secretChat.State is SecretChatStateReady)
            {
                SecretLifetime.Badge = chat.MessageTtlSetting > 0 ? Locale.FormatTtl(chat.MessageTtlSetting) : Strings.Resources.ShortMessageLifetimeForever;
                //SecretIdenticon.Source = PlaceholderHelper.GetIdenticon(secretChat.KeyHash, 24);

                MiscPanel.Visibility = Visibility.Visible;
                SecretLifetime.Visibility = Visibility.Visible;
                SecretHashKey.Visibility = Visibility.Visible;
            }
            else
            {
                MiscPanel.Visibility = Visibility.Collapsed;
                SecretLifetime.Visibility = Visibility.Collapsed;
                SecretHashKey.Visibility = Visibility.Collapsed;
            }
        }



        public void UpdateBasicGroup(Chat chat, BasicGroup group)
        {
            Subtitle.Text = Locale.Declension("Members", group.MemberCount);

            Description.Content = Strings.Resources.DescriptionPlaceholder;

            GroupInvite.Visibility = group.Status is ChatMemberStatusCreator || (group.Status is ChatMemberStatusAdministrator administrator && administrator.CanInviteUsers) || chat.Permissions.CanInviteUsers ? Visibility.Visible : Visibility.Collapsed;

            Edit.Visibility = chat.Permissions.CanChangeInfo || group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator ? Visibility.Visible : Visibility.Collapsed;
            Edit.Glyph = Icons.Edit;

            ToolTipService.SetToolTip(Edit, Strings.Resources.ChannelEdit);

            // Unused:
            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;

            Verified.Visibility = Visibility.Collapsed;
            UserPhone.Visibility = Visibility.Collapsed;
            Location.Visibility = Visibility.Collapsed;
            Username.Visibility = Visibility.Collapsed;

            Description.Visibility = Visibility.Collapsed;

            //UserCommonChats.Visibility = Visibility.Collapsed;
            UserStartSecret.Visibility = Visibility.Collapsed;

            MiscPanel.Visibility = Visibility.Collapsed;

            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;

            GroupLeave.Visibility = Visibility.Collapsed;

            ChannelMembersPanel.Visibility = Visibility.Collapsed;
            MembersPanel.Visibility = Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;

            SharedMedia.Tab = new SupergroupMembersView { DataContext = ViewModel.SupergroupMembers, IsEmbedded = true };
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            GetEntities(fullInfo.Description);
            Description.Visibility = string.IsNullOrEmpty(fullInfo.Description) ? Visibility.Collapsed : Visibility.Visible;

            ViewModel.Members = new SortedObservableCollection<ChatMember>(new ChatMemberComparer(ViewModel.ProtoService, true), fullInfo.Members);
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Subtitle.Text = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", group.MemberCount);

            Description.Content = Strings.Resources.DescriptionPlaceholder;

            Automation.SetToolTip(Edit, group.IsChannel ? Strings.Resources.ManageChannelMenu : Strings.Resources.ManageGroupMenu);

            Call.Visibility = Visibility.Collapsed;
            VideoCall.Visibility = Visibility.Collapsed;

            Edit.Visibility = group.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator ? Visibility.Visible : Visibility.Collapsed;
            Edit.Glyph = Icons.Edit;

            Verified.Visibility = group.IsVerified ? Visibility.Visible : Visibility.Collapsed;

            Username.Badge = $"{group.Username}";
            Username.Visibility = string.IsNullOrEmpty(group.Username) ? Visibility.Collapsed : Visibility.Visible;

            Location.Visibility = group.HasLocation ? Visibility.Visible : Visibility.Collapsed;

            if (group.IsChannel && group.Status is not ChatMemberStatusCreator && group.Status is not ChatMemberStatusLeft && group.Status is not ChatMemberStatusBanned)
            {
                MiscPanel.Visibility = Visibility.Visible;
                GroupLeave.Visibility = Visibility.Visible;
            }
            else
            {
                MiscPanel.Visibility = Visibility.Collapsed;
                GroupLeave.Visibility = Visibility.Collapsed;
            }

            GroupInvite.Visibility = !group.IsChannel && (group.Status is ChatMemberStatusCreator || (group.Status is ChatMemberStatusAdministrator administrator && administrator.CanInviteUsers) || chat.Permissions.CanInviteUsers) ? Visibility.Visible : Visibility.Collapsed;

            ChannelMembersPanel.Visibility = group.IsChannel && (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator) ? Visibility.Visible : Visibility.Collapsed;
            MembersPanel.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;

            if (!group.IsChannel && (ViewModel.Members == null || group.MemberCount < 200 && group.MemberCount != ViewModel.Members.Count))
            {
                ViewModel.Members = ViewModel.CreateMembers(group.Id);
            }

            // Unused:
            UserPhone.Visibility = Visibility.Collapsed;
            //UserCommonChats.Visibility = Visibility.Collapsed;
            UserStartSecret.Visibility = Visibility.Collapsed;
            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;

            if (group.IsChannel)
            {
                SharedMedia.Tab = null;
            }
            else
            {
                SharedMedia.Tab = new SupergroupMembersView { DataContext = ViewModel.SupergroupMembers, IsEmbedded = true };
            }
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            GetEntities(fullInfo.Description);
            Description.Visibility = string.IsNullOrEmpty(fullInfo.Description) ? Visibility.Collapsed : Visibility.Visible;

            Location.Visibility = fullInfo.Location != null ? Visibility.Visible : Visibility.Collapsed;
            Location.Badge = fullInfo.Location?.Address;

            Admins.Badge = fullInfo.AdministratorCount;
            //Admins.Visibility = fullInfo.AdministratorCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Banned.Badge = fullInfo.BannedCount;
            //Banned.Visibility = fullInfo.BannedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            //Restricted.Badge = fullInfo.RestrictedCount;
            //Restricted.Visibility = fullInfo.RestrictedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            Members.Badge = fullInfo.MemberCount;
            //Members.Visibility = fullInfo.CanGetMembers && group.IsChannel ? Visibility.Visible : Visibility.Collapsed;
        }



        public void UpdateFile(File file)
        {
            var chat = ViewModel.Chat;
            if (chat != null && chat.UpdateFile(file))
            {
                Photo.Source = PlaceholderHelper.GetChat(null, chat, 64);
            }

            //for (int i = 0; i < ScrollingHost.Items.Count; i++)
            //{
            //    var member = ScrollingHost.Items[i] as ChatMember;

            //    var user = ViewModel.ProtoService.GetUser(member.UserId);
            //    if (user == null)
            //    {
            //        return;
            //    }

            //    if (user.UpdateFile(file))
            //    {
            //        var container = ScrollingHost.ContainerFromIndex(i) as ListViewItem;
            //        if (container == null)
            //        {
            //            return;
            //        }

            //        var content = container.ContentTemplateRoot as Grid;

            //        var photo = content.Children[0] as ProfilePicture;
            //        photo.Source = PlaceholderHelper.GetUser(null, user, 36);
            //    }
            //}
        }

        #endregion

        #region Context menu

        private void About_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            MessageHelper.Hyperlink_ContextRequested(null, sender, args);
        }

        private void About_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
        }

        private void Description_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = FlyoutBase.GetAttachedFlyout(sender as FrameworkElement) as MenuFlyout;
            if (flyout == null)
            {
                return;
            }

            if (args.TryGetPosition(sender, out Point point))
            {
                if (point.X < 0 || point.Y < 0)
                {
                    point = new Point(Math.Max(point.X, 0), Math.Max(point.Y, 0));
                }

                flyout.ShowAt(sender, point);
            }
            else
            {
                flyout.ShowAt(sender as FrameworkElement);
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var userId = chat.Type is ChatTypePrivate privata ? privata.UserId : chat.Type is ChatTypeSecret secret ? secret.UserId : 0;
                if (userId != ViewModel.CacheService.Options.MyId)
                {
                    var user = ViewModel.CacheService.GetUser(userId);
                    if (user == null)
                    {
                        return;
                    }

                    var fullInfo = ViewModel.CacheService.GetUserFull(userId);
                    if (fullInfo == null)
                    {
                        return;
                    }

                    //if (fullInfo.CanBeCalled)
                    //{
                    //    callItem = menu.addItem(call_item, R.drawable.ic_call_white_24dp);
                    //}
                    if (user.IsContact)
                    {
                        flyout.CreateFlyoutItem(ViewModel.ShareCommand, Strings.Resources.ShareContact, new FontIcon { Glyph = Icons.Share });
                        flyout.CreateFlyoutItem(chat.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, chat.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact, new FontIcon { Glyph = chat.IsBlocked ? Icons.Block : Icons.Block });
                        flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.EditContact, new FontIcon { Glyph = Icons.Edit });
                        flyout.CreateFlyoutItem(ViewModel.DeleteCommand, Strings.Resources.DeleteContact, new FontIcon { Glyph = Icons.Delete });
                    }
                    else
                    {
                        if (user.Type is UserTypeBot bot)
                        {
                            if (bot.CanJoinGroups)
                            {
                                flyout.CreateFlyoutItem(ViewModel.InviteCommand, Strings.Resources.BotInvite, new FontIcon { Glyph = Icons.PersonAdd });
                            }

                            flyout.CreateFlyoutItem(() => { }, Strings.Resources.BotShare, new FontIcon { Glyph = Icons.Share });
                        }
                        else
                        {
                            flyout.CreateFlyoutItem(ViewModel.AddCommand, Strings.Resources.AddContact, new FontIcon { Glyph = Icons.PersonAdd });
                        }

                        if (user.PhoneNumber.Length > 0)
                        {
                            flyout.CreateFlyoutItem(ViewModel.ShareCommand, Strings.Resources.ShareContact, new FontIcon { Glyph = Icons.Share });
                            flyout.CreateFlyoutItem(chat.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, chat.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact, new FontIcon { Glyph = chat.IsBlocked ? Icons.Block : Icons.Block });
                        }
                        else
                        {
                            if (user.Type is UserTypeBot)
                            {
                                flyout.CreateFlyoutItem(chat.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, chat.IsBlocked ? Strings.Resources.BotRestart : Strings.Resources.BotStop, new FontIcon { Glyph = chat.IsBlocked ? Icons.Block : Icons.Block });
                            }
                            else
                            {
                                flyout.CreateFlyoutItem(chat.IsBlocked ? ViewModel.UnblockCommand : ViewModel.BlockCommand, chat.IsBlocked ? Strings.Resources.Unblock : Strings.Resources.BlockContact, new FontIcon { Glyph = chat.IsBlocked ? Icons.Block : Icons.Block });
                            }
                        }
                    }
                }
                else
                {
                    flyout.CreateFlyoutItem(ViewModel.ShareCommand, Strings.Resources.ShareContact, new FontIcon { Glyph = Icons.Share });
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

                var fullInfo = ViewModel.ProtoService.GetSupergroupFull(super.SupergroupId);

                if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    if (chat.VoiceChat.GroupCallId == 0 && supergroup.CanManageVoiceChats())
                    {
                        flyout.CreateFlyoutItem(ViewModel.CallCommand, false, Strings.Resources.StartVoipChat, new FontIcon { Glyph = Icons.VoiceChat });
                    }

                    if (supergroup.IsChannel)
                    {
                        //flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.ManageChannelMenu, new FontIcon { Glyph = Icons.Edit });
                    }
                    else
                    {
                        flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.ManageGroupMenu, new FontIcon { Glyph = Icons.Edit });
                    }
                }

                if (fullInfo != null && fullInfo.CanGetStatistics)
                {
                    flyout.CreateFlyoutItem(ViewModel.StatisticsCommand, Strings.Resources.Statistics, new FontIcon { Glyph = Icons.DataUsage });
                }

                if (!super.IsChannel)
                {
                    flyout.CreateFlyoutItem(ViewModel.MembersCommand, Strings.Resources.SearchMembers, new FontIcon { Glyph = Icons.Search });

                    if (supergroup.Status is not ChatMemberStatusCreator and not ChatMemberStatusLeft and not ChatMemberStatusBanned)
                    {
                        flyout.CreateFlyoutItem(ViewModel.DeleteCommand, Strings.Resources.LeaveMegaMenu, new FontIcon { Glyph = Icons.Delete });
                    }
                }
                else if (supergroup.HasLinkedChat)
                {
                    flyout.CreateFlyoutItem(ViewModel.DiscussCommand, Strings.Resources.ViewDiscussion, new FontIcon { Glyph = Icons.Comment });
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var basicGroup = ViewModel.ProtoService.GetBasicGroup(basic.BasicGroupId);
                if (basicGroup == null)
                {
                    return;
                }

                if (chat.VoiceChat.GroupCallId == 0 && basicGroup.CanManageVoiceChats())
                {
                    flyout.CreateFlyoutItem(ViewModel.CallCommand, false, Strings.Resources.StartVoipChat, new FontIcon { Glyph = Icons.VoiceChat });
                }

                if (chat.Permissions.CanChangeInfo || basicGroup.Status is ChatMemberStatusCreator || basicGroup.Status is ChatMemberStatusAdministrator)
                {
                    flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.ChannelEdit, new FontIcon { Glyph = Icons.Edit });
                }

                flyout.CreateFlyoutItem(ViewModel.MembersCommand, Strings.Resources.SearchMembers, new FontIcon { Glyph = Icons.Search });

                flyout.CreateFlyoutItem(ViewModel.DeleteCommand, Strings.Resources.DeleteAndExit, new FontIcon { Glyph = Icons.Delete });
            }

            //flyout.CreateFlyoutItem(null, Strings.Resources.AddShortcut, new FontIcon { Glyph = Icons.Pin });

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as Button, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight });
            }
        }

        #endregion

        #region Entities

        private void GetEntities(string text)
        {
            DescriptionSpan.Inlines.Clear();
            Description.BadgeLabel = text;

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
                else if (entity.Type is TextEntityTypePre or TextEntityTypePreCode)
                {
                    // TODO any additional
                    span.Inlines.Add(new Run { Text = text.Substring(entity.Offset, entity.Length), FontFamily = new FontFamily("Consolas") });
                }
                else if (entity.Type is TextEntityTypeUrl or TextEntityTypeEmailAddress or TextEntityTypePhoneNumber or TextEntityTypeMention or TextEntityTypeHashtag or TextEntityTypeCashtag or TextEntityTypeBotCommand)
                {
                    var hyperlink = new Hyperlink();
                    var data = text.Substring(entity.Offset, entity.Length);

                    hyperlink.Click += (s, args) => Entity_Click(entity.Type, data);
                    hyperlink.Inlines.Add(new Run { Text = data });
                    //hyperlink.Foreground = foreground;
                    span.Inlines.Add(hyperlink);

                    if (entity.Type is TextEntityTypeUrl)
                    {
                        MessageHelper.SetEntityData(hyperlink, data);
                    }
                }
                else if (entity.Type is TextEntityTypeTextUrl or TextEntityTypeMentionName)
                {
                    var hyperlink = new Hyperlink();
                    object data;
                    if (entity.Type is TextEntityTypeTextUrl textUrl)
                    {
                        data = textUrl.Url;
                        MessageHelper.SetEntityData(hyperlink, textUrl.Url);
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
            else if (type is TextEntityTypeHashtag or TextEntityTypeCashtag)
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

        private double _scrollingHost;
        private bool _scrollingHostDisabled = false;

        private double _sharedMedia;
        private bool _sharedMediaDisabled = true;

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs args)
        {
            var scrollViewer = sender as ScrollViewer;
            if (_scrollingHostDisabled)
            {
                if (!args.IsIntermediate)
                {
                    scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null, true);
                }

                _scrollingHost = scrollViewer.VerticalOffset;
                return;
            }

            if (scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 12 && _scrollingHost < scrollViewer.VerticalOffset)
            {
                _scrollingHostDisabled = true;
                SetScrollMode(false);

                SharedMedia.SetScrollMode(true);
                _sharedMediaDisabled = false;
            }

            _scrollingHost = scrollViewer.VerticalOffset;
        }

        private void SharedMedia_ViewChanged(object sender, ScrollViewerViewChangedEventArgs args)
        {
            var scrollViewer2 = sender as ScrollViewer;
            if (_sharedMediaDisabled)
            {
                if (!args.IsIntermediate)
                {
                    scrollViewer2.ChangeView(null, 12, null, false);
                }

                _sharedMedia = scrollViewer2.VerticalOffset;
                return;
            }

            if (scrollViewer2.VerticalOffset <= 12 && _sharedMedia > scrollViewer2.VerticalOffset)
            {
                SetScrollMode(true);
                _scrollingHostDisabled = false;

                _sharedMediaDisabled = true;
                SharedMedia.SetScrollMode(false);
            }

            _sharedMedia = scrollViewer2.VerticalOffset;
        }

        private void SetScrollMode(bool enable)
        {
            if (enable)
            {
                ScrollingHost.VerticalScrollMode = ScrollMode.Auto;
                ScrollingHost.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight - 48, null, false);

                ScrollingInfo.Visibility = Visibility.Visible;
                InfoPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ScrollingHost.ChangeView(null, ScrollingHost.ScrollableHeight, null, true);
                ScrollingHost.VerticalScrollMode = ScrollMode.Disabled;
                ScrollingHost.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

                ScrollingInfo.Visibility = Visibility.Collapsed;
                InfoPanel.Visibility = Visibility.Visible;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ScrollingHost.ViewChanged += ScrollingHost_ViewChanged;
            SharedMedia.ViewChanged += SharedMedia_ViewChanged;

            return;

            var properties = ElementCompositionPreview.GetScrollViewerManipulationPropertySet(ScrollingHost);
            var header = ElementCompositionPreview.GetElementVisual(HeaderPanel);
            var info = ElementCompositionPreview.GetElementVisual(ScrollingInfo);

            var photo = ElementCompositionPreview.GetElementVisual(Photo);
            var title = ElementCompositionPreview.GetElementVisual(LabelTitle);
            var subtitle = ElementCompositionPreview.GetElementVisual(Subtitle);
            var action = ElementCompositionPreview.GetElementVisual(SendMessage);

            var overlay = photo.Compositor.CreateSpriteVisual();
            var gradient = overlay.Compositor.CreateLinearGradientBrush();
            gradient.ColorStops.Add(overlay.Compositor.CreateColorGradientStop(0, ((SolidColorBrush)Navigation.BootStrapper.Current.Resources["PageHeaderBackgroundBrush"]).Color));
            gradient.ColorStops.Add(overlay.Compositor.CreateColorGradientStop(1, ((SolidColorBrush)Navigation.BootStrapper.Current.Resources["PageSubHeaderBackgroundBrush"]).Color));
            gradient.StartPoint = new Vector2();
            gradient.EndPoint = new Vector2(0, 1);
            overlay.Brush = gradient;
            overlay.Size = new Vector2((float)HeaderOverlay.ActualWidth, (float)HeaderOverlay.ActualHeight);

            HeaderOverlay.SizeChanged += (s, args) =>
            {
                overlay.Size = args.NewSize.ToVector2();
            };

            ElementCompositionPreview.SetElementChildVisual(HeaderOverlay, overlay);

            var animOverlay = header.Compositor.CreateExpressionAnimation("Min(76, -Min(scrollViewer.Translation.Y, 0)) / 38");
            animOverlay.SetReferenceParameter("scrollViewer", properties);

            overlay.StartAnimation("Scale.Y", animOverlay);

            var animClip = header.Compositor.CreateExpressionAnimation("Min(76, -Min(scrollViewer.Translation.Y, 0))");
            animClip.SetReferenceParameter("scrollViewer", properties);

            header.Clip = header.Compositor.CreateInsetClip(0, -32, -12, 0);
            header.Clip.StartAnimation("BottomInset", animClip);

            var animPhotoOffsetY = header.Compositor.CreateExpressionAnimation("-(Min(76, -Min(scrollViewer.Translation.Y, 0)) / 76 * 41)");
            animPhotoOffsetY.SetReferenceParameter("scrollViewer", properties);

            var animPhotoOffsetX = header.Compositor.CreateExpressionAnimation("Min(76, -Min(scrollViewer.Translation.Y, 0)) / 76 * 28");
            animPhotoOffsetX.SetReferenceParameter("scrollViewer", properties);

            var animPhotoScale = header.Compositor.CreateExpressionAnimation("1 -(Min(76, -Min(scrollViewer.Translation.Y, 0)) / 76 * (34 / 64))");
            animPhotoScale.SetReferenceParameter("scrollViewer", properties);

            photo.StartAnimation("Offset.Y", animPhotoOffsetY);
            photo.StartAnimation("Offset.X", animPhotoOffsetX);
            photo.StartAnimation("Scale.X", animPhotoScale);
            photo.StartAnimation("Scale.Y", animPhotoScale);


            var animTitleY = header.Compositor.CreateExpressionAnimation("-(Min(76, -Min(scrollViewer.Translation.Y, 0)) / 76 * 58)");
            animTitleY.SetReferenceParameter("scrollViewer", properties);

            var animTitleX = header.Compositor.CreateExpressionAnimation("-(Min(76, -Min(scrollViewer.Translation.Y, 0)) / 76 * 6)");
            animTitleX.SetReferenceParameter("scrollViewer", properties);

            title.StartAnimation("Offset.Y", animTitleY);
            title.StartAnimation("Offset.X", animTitleX);
            subtitle.StartAnimation("Offset.Y", animTitleY);
            subtitle.StartAnimation("Offset.X", animTitleX);


            var animInfoY = header.Compositor.CreateExpressionAnimation("-(Min(76, -Min(scrollViewer.Translation.Y, 0)) / 76 * 40)");
            animInfoY.SetReferenceParameter("scrollViewer", properties);

            var animOpacity = header.Compositor.CreateExpressionAnimation("1 -(Min(76, -Min(scrollViewer.Translation.Y, 0)) / 76)");
            animOpacity.SetReferenceParameter("scrollViewer", properties);

            info.StartAnimation("Offset.Y", animInfoY);
            info.StartAnimation("Opacity", animOpacity);

            action.CenterPoint = new Vector3(18);
            action.StartAnimation("Opacity", animOpacity);
            action.StartAnimation("Scale.X", animOpacity);
            action.StartAnimation("Scale.Y", animOpacity);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SharedMedia.Height = e.NewSize.Height - 16;
        }
    }
}
