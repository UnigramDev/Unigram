//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Unigram.Controls
{
    public sealed partial class ProfileHeader : UserControl
    {
        public ProfileViewModel ViewModel => DataContext as ProfileViewModel;

        public ProfileHeader()
        {
            InitializeComponent();
            DescriptionLabel.AddHandler(ContextRequestedEvent, new TypedEventHandler<UIElement, ContextRequestedEventArgs>(About_ContextRequested), true);

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null && Chat != null)
            {
                SetChat(Chat);
            }
        }

        private Chat _chat;
        public Chat Chat
        {
            get => _chat;
            set => SetChat(value);
        }

        private void SetChat(Chat chat)
        {
            _chat = chat;

            // Perdoname madre por mi duplicated code
            if (chat == null || ViewModel == null)
            {
                return;
            }

            UpdateChat(chat);

            if (chat.Type is ChatTypePrivate privata)
            {
                var item = ViewModel.ClientService.GetUser(privata.UserId);
                var cache = ViewModel.ClientService.GetUserFull(privata.UserId);

                UpdateUser(chat, item, false);

                if (cache != null)
                {
                    UpdateUserFullInfo(chat, item, cache, false, false);
                }
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = ViewModel.ClientService.GetSecretChat(secretType.SecretChatId);
                var item = ViewModel.ClientService.GetUser(secretType.UserId);
                var cache = ViewModel.ClientService.GetUserFull(secretType.UserId);

                UpdateSecretChat(chat, secret);
                UpdateUser(chat, item, true);

                if (cache != null)
                {
                    UpdateUserFullInfo(chat, item, cache, true, false);
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ViewModel.ClientService.GetBasicGroup(basic.BasicGroupId);
                var cache = ViewModel.ClientService.GetBasicGroupFull(basic.BasicGroupId);

                UpdateBasicGroup(chat, item);

                if (cache != null)
                {
                    UpdateBasicGroupFullInfo(chat, item, cache);
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ViewModel.ClientService.GetSupergroup(super.SupergroupId);
                var cache = ViewModel.ClientService.GetSupergroupFull(super.SupergroupId);

                UpdateSupergroup(chat, item);

                if (cache != null)
                {
                    UpdateSupergroupFullInfo(chat, item, cache);
                }
            }
        }

        private async void Photo_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            await GalleryView.ShowAsync(ViewModel.ClientService, ViewModel.StorageService, ViewModel.Aggregator, chat, () => Photo);
        }

        #region Delegate

        public void UpdateChat(Chat chat)
        {
            if (ViewModel.ClientService.IsSavedMessages(chat))
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);

            UpdateChatNotificationSettings(chat);
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (ViewModel.Topic != null)
            {
                Title.Text = ViewModel.Topic.Name;
            }
            else
            {
                Title.Text = ViewModel.ClientService.GetTitle(chat);
            }
        }

        public void UpdateChatPhoto(Chat chat)
        {
            if (ViewModel.Topic != null)
            {
                FindName(nameof(Icon));
                Icon.SetCustomEmoji(ViewModel.ClientService, ViewModel.Topic.Icon.CustomEmojiId);
                Photo.Clear();
            }
            else
            {
                UnloadObject(Icon);
                Photo.SetChat(ViewModel.ClientService, chat, 140);
            }
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            var unmuted = ViewModel.ClientService.Notifications.GetMutedFor(chat) == 0;
            Notifications.Content = unmuted ? Strings.Resources.ChatsMute : Strings.Resources.ChatsUnmute;
            Notifications.Glyph = unmuted ? Icons.Alert : Icons.AlertOff;
        }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);

            Identity.SetStatus(ViewModel.ClientService, user);

            UserPhone.Badge = PhoneNumber.Format(user.PhoneNumber);
            UserPhone.Visibility = string.IsNullOrEmpty(user.PhoneNumber) ? Visibility.Collapsed : Visibility.Visible;

            if (user.HasActiveUsername(out string username))
            {
                Username.Badge = username;
                Username.Visibility = Visibility.Visible;
            }
            else
            {
                Username.Visibility = Visibility.Collapsed;
            }

            UpdateUsernames(user.Usernames);

            Description.Content = user.Type is UserTypeBot ? Strings.Resources.DescriptionPlaceholder : Strings.Resources.UserBio;

            if (secret is false)
            {
                MiscPanel.Visibility = Visibility.Collapsed;
                SecretLifetime.Visibility = Visibility.Collapsed;
                SecretHashKey.Visibility = Visibility.Collapsed;
            }

            if (user.PhoneNumber.Length > 0)
            {
                var info = Client.Execute(new GetPhoneNumberInfoSync("en", user.PhoneNumber)) as PhoneNumberInfo;
                if (info != null)
                {
                    AnonymousNumber.Visibility = info.IsAnonymous ? Visibility.Visible : Visibility.Collapsed;
                    AnonymousNumberSeparator.Visibility = info.IsAnonymous ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            OpenChat.Content = Strings.Resources.VoipGroupOpenChat;

            // Unused:
            Location.Visibility = Visibility.Collapsed;
            Edit.Visibility = Visibility.Collapsed;

            Join.Visibility = Visibility.Collapsed;

            ChannelMembersPanel.Visibility = Visibility.Collapsed;
            MembersPanel.Visibility = Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;
        }

        public void UpdateUserFullInfo(Chat chat, User user, UserFullInfo fullInfo, bool secret, bool accessToken)
        {
            if (user.Type is UserTypeBot && fullInfo.BotInfo != null)
            {
                GetEntities(fullInfo.BotInfo.ShareText);
                Description.Visibility = string.IsNullOrEmpty(fullInfo.BotInfo.ShareText) ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                ReplaceEntities(fullInfo.Bio);
                Description.Visibility = string.IsNullOrEmpty(fullInfo.Bio.Text) ? Visibility.Collapsed : Visibility.Visible;
            }

            //UserCommonChats.Badge = fullInfo.GroupInCommonCount;
            //UserCommonChats.Visibility = fullInfo.GroupInCommonCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            Call.Visibility = Visibility.Visible;
            Call.Content = Strings.Resources.Call;
            Call.Glyph = Icons.Phone;
            VideoCall.Visibility = fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Visible : Visibility.Collapsed;

            Search.Visibility = fullInfo.CanBeCalled && fullInfo.SupportsVideoCalls ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetColumn(Search, 2);
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            if (secretChat.State is SecretChatStateReady)
            {
                SecretLifetime.Badge = chat.MessageAutoDeleteTime > 0 ? Locale.FormatTtl(chat.MessageAutoDeleteTime) : Strings.Resources.ShortMessageLifetimeForever;
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

            Identity.ClearStatus();

            UserPhone.Visibility = Visibility.Collapsed;
            Location.Visibility = Visibility.Collapsed;
            Username.Visibility = Visibility.Collapsed;

            Description.Visibility = Visibility.Collapsed;

            //UserCommonChats.Visibility = Visibility.Collapsed;
            MiscPanel.Visibility = Visibility.Collapsed;

            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;

            ChannelMembersPanel.Visibility = Visibility.Collapsed;
            MembersPanel.Visibility = Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;

            Edit.Glyph = Icons.Edit;
            Edit.Content = Strings.Resources.ChannelEdit;

            if (chat.Permissions.CanChangeInfo || group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator)
            {
                Edit.Visibility = Visibility.Visible;
                Join.Visibility = Visibility.Collapsed;
            }
            else
            {
                Edit.Visibility = Visibility.Collapsed;
                Join.Visibility = Visibility.Visible;

                Join.Command = ViewModel.DeleteCommand;
                Join.Content = Strings.Resources.VoipGroupLeave;
                Join.Glyph = Icons.ArrowExit;
            }

            OpenChat.Content = Strings.Resources.VoipGroupOpenGroup;

            // Unused:
            if (chat.VideoChat.GroupCallId != 0 || group.CanManageVideoChats())
            {
                Call.Visibility = Visibility.Visible;
                Call.Content = Strings.Resources.VoipGroupVoiceChat;
                Call.Glyph = Icons.VideoChat;

                Search.Visibility = Visibility.Collapsed;
            }
            else
            {
                Call.Visibility = Visibility.Collapsed;

                Search.Visibility = Visibility.Visible;
                Grid.SetColumn(Search, 1);
            }

            VideoCall.Visibility = Visibility.Collapsed;

            AnonymousNumber.Visibility = Visibility.Collapsed;
            AnonymousNumberSeparator.Visibility = Visibility.Collapsed;
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            GetEntities(fullInfo.Description);
            Description.Visibility = string.IsNullOrEmpty(fullInfo.Description) ? Visibility.Collapsed : Visibility.Visible;
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            if (ViewModel.Topic != null)
            {
                Subtitle.Text = string.Format(Strings.Resources.TopicProfileStatus, chat.Title);
            }
            else
            {
                Subtitle.Text = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", group.MemberCount);
            }

            Description.Content = Strings.Resources.DescriptionPlaceholder;

            Identity.SetStatus(group);

            if (group.HasActiveUsername(out string username))
            {
                Username.Badge = username;
                Username.Visibility = Visibility.Visible;
            }
            else
            {
                Username.Visibility = Visibility.Collapsed;
            }

            UpdateUsernames(group.Usernames);

            Location.Visibility = group.HasLocation ? Visibility.Visible : Visibility.Collapsed;

            ChannelMembersPanel.Visibility = group.IsChannel && (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator) ? Visibility.Visible : Visibility.Collapsed;
            MembersPanel.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;

            if (chat.VideoChat.GroupCallId != 0 || group.CanManageVideoChats())
            {
                Call.Visibility = Visibility.Visible;
                Call.Content = Strings.Resources.VoipGroupVoiceChat;
                Call.Glyph = Icons.VideoChat;

                Search.Visibility = Visibility.Collapsed;
            }
            else
            {
                Call.Visibility = Visibility.Collapsed;

                Search.Visibility = Visibility.Visible;
                Grid.SetColumn(Search, 1);
            }

            VideoCall.Visibility = Visibility.Collapsed;

            if (group.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
            {
                Edit.Visibility = Visibility.Visible;
                Join.Visibility = Visibility.Collapsed;
            }
            else
            {
                Edit.Visibility = Visibility.Collapsed;
                Join.Visibility = Visibility.Visible;

                if (group.CanJoin())
                {
                    Join.Command = ViewModel.JoinCommand;
                    Join.Content = Strings.Resources.VoipChatJoin;
                    Join.Glyph = Icons.ArrowEnter;
                }
                else
                {
                    Join.Command = ViewModel.DeleteCommand;
                    Join.Content = Strings.Resources.VoipGroupLeave;
                    Join.Glyph = Icons.ArrowExit;
                }
            }

            Edit.Glyph = Icons.Edit;
            Edit.Content = Edit.Content = Strings.Resources.ChannelEdit; //group.IsChannel ? Strings.Resources.ManageChannelMenu : Strings.Resources.ManageGroupMenu;

            OpenChat.Content = group.IsChannel
                ? Strings.Resources.VoipGroupOpenChannel
                : Strings.Resources.VoipGroupOpenGroup;

            // Unused:
            MiscPanel.Visibility = Visibility.Collapsed;
            UserPhone.Visibility = Visibility.Collapsed;
            //UserCommonChats.Visibility = Visibility.Collapsed;
            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;

            AnonymousNumber.Visibility = Visibility.Collapsed;
            AnonymousNumberSeparator.Visibility = Visibility.Collapsed;
        }

        public void UpdateSupergroupFullInfo(Chat chat, Supergroup group, SupergroupFullInfo fullInfo)
        {
            if (ViewModel.Topic != null)
            {
                Subtitle.Text = string.Format(Strings.Resources.TopicProfileStatus, chat.Title);
            }
            else
            {
                Subtitle.Text = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", fullInfo.MemberCount);
            }

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

        private void UpdateUsernames(Usernames usernames)
        {
            if (usernames?.ActiveUsernames.Count > 1)
            {
                ActiveUsernames.Inlines.Clear();
                ActiveUsernames.Inlines.Add(new Run { Text = string.Format(Strings.Resources.UsernameAlso, string.Empty) });

                for (int i = 1; i < usernames.ActiveUsernames.Count; i++)
                {
                    if (i > 1)
                    {
                        ActiveUsernames.Inlines.Add(new Run { Text = ", " });
                    }

                    var hyperlink = new Hyperlink();
                    hyperlink.Inlines.Add(new Run { Text = $"@{usernames.ActiveUsernames[i]}" });

                    ActiveUsernames.Inlines.Add(hyperlink);
                }
            }
            else
            {
                ActiveUsernames.Inlines.Clear();
                ActiveUsernames.Inlines.Add(new Run { Text = Strings.Resources.Username });
            }
        }

        #endregion

        #region Context menu

        private void About_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            MessageHelper.Hyperlink_ContextRequested(ViewModel.TranslateService, sender, args);
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

            var user = chat.Type is ChatTypePrivate or ChatTypeSecret ? ViewModel.ClientService.GetUser(chat) : null;
            var basicGroup = chat.Type is ChatTypeBasicGroup basicGroupType ? ViewModel.ClientService.GetBasicGroup(basicGroupType.BasicGroupId) : null;
            var supergroup = chat.Type is ChatTypeSupergroup supergroupType ? ViewModel.ClientService.GetSupergroup(supergroupType.SupergroupId) : null;

            if ((user != null && user.Type is not UserTypeBot) || (basicGroup != null && basicGroup.CanChangeInfo()) || (supergroup != null && supergroup.CanChangeInfo()))
            {
                var icon = chat.MessageAutoDeleteTime switch
                {
                    60 * 60 * 24 => Icons.AutoDeleteDay,
                    60 * 60 * 24 * 7 => Icons.AutoDeleteWeek,
                    60 * 60 * 24 * 31 => Icons.AutoDeleteMonth,
                    _ => Icons.Timer
                };

                var autodelete = new MenuFlyoutSubItem();
                autodelete.Text = Strings.Resources.AutoDeletePopupTitle;
                autodelete.Icon = new FontIcon { Glyph = icon, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily };

                void AddToggle(int value, int? parameter, string text, string icon)
                {
                    var item = new ToggleMenuFlyoutItem();
                    item.Text = text;
                    item.IsChecked = parameter == null ? false : value == parameter;
                    item.CommandParameter = parameter;
                    item.Command = ViewModel.SetTimerCommand;
                    item.Icon = new FontIcon { Glyph = icon, FontFamily = BootStrapper.Current.Resources["TelegramThemeFontFamily"] as FontFamily };

                    autodelete.Items.Add(item);
                }

                AddToggle(chat.MessageAutoDeleteTime, 0, Strings.Resources.ShortMessageLifetimeForever, Icons.AutoDeleteOff);

                autodelete.CreateFlyoutSeparator();

                AddToggle(chat.MessageAutoDeleteTime, 60 * 60 * 24, Locale.FormatTtl(60 * 60 * 24), Icons.AutoDeleteDay);
                AddToggle(chat.MessageAutoDeleteTime, 60 * 60 * 24 * 7, Locale.FormatTtl(60 * 60 * 24 * 7), Icons.AutoDeleteWeek);
                AddToggle(chat.MessageAutoDeleteTime, 60 * 60 * 24 * 31, Locale.FormatTtl(60 * 60 * 24 * 31), Icons.AutoDeleteMonth);
                AddToggle(chat.MessageAutoDeleteTime, null, Strings.Resources.AutoDownloadCustom, Icons.Options);

                flyout.Items.Add(autodelete);
                flyout.CreateFlyoutSeparator();
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret && user != null)
            {
                var userId = chat.Type is ChatTypePrivate privata ? privata.UserId : chat.Type is ChatTypeSecret secret ? secret.UserId : 0;
                if (userId != ViewModel.ClientService.Options.MyId)
                {
                    var fullInfo = ViewModel.ClientService.GetUserFull(userId);
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

                    if (ViewModel.IsPremium && fullInfo.PremiumGiftOptions.Count > 0)
                    {
                        flyout.CreateFlyoutItem(ViewModel.GiftPremiumCommand, Strings.Resources.GiftPremium, new FontIcon { Glyph = Icons.GiftPremium });
                    }

                    if (user.Type is UserTypeRegular
                        && !LastSeenConverter.IsServiceUser(user)
                        && !LastSeenConverter.IsSupportUser(user))
                    {
                        flyout.CreateFlyoutItem(ViewModel.SecretChatCommand, Strings.Resources.StartEncryptedChat, new FontIcon { Glyph = Icons.LockClosed });
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
            if (chat.Type is ChatTypeSupergroup super && supergroup != null)
            {
                var fullInfo = ViewModel.ClientService.GetSupergroupFull(super.SupergroupId);

                if (supergroup.Status is ChatMemberStatusCreator or ChatMemberStatusAdministrator)
                {
                    if (supergroup.IsChannel)
                    {
                        //flyout.CreateFlyoutItem(ViewModel.EditCommand, Strings.Resources.ManageChannelMenu, new FontIcon { Glyph = Icons.Edit });
                    }
                    else if (supergroup.Status is ChatMemberStatusCreator || (supergroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanInviteUsers) || chat.Permissions.CanInviteUsers)
                    {
                        flyout.CreateFlyoutItem(ViewModel.InviteCommand, Strings.Resources.AddMember, new FontIcon { Glyph = Icons.PersonAdd });
                    }
                }

                if (fullInfo != null && fullInfo.CanGetStatistics)
                {
                    flyout.CreateFlyoutItem(ViewModel.StatisticsCommand, Strings.Resources.Statistics, new FontIcon { Glyph = Icons.DataUsage });
                }

                if (!super.IsChannel)
                {
                    flyout.CreateFlyoutItem(ViewModel.MembersCommand, Strings.Resources.SearchMembers, new FontIcon { Glyph = Icons.Search });
                }
                else if (supergroup.HasLinkedChat)
                {
                    flyout.CreateFlyoutItem(ViewModel.DiscussCommand, Strings.Resources.ViewDiscussion, new FontIcon { Glyph = Icons.Comment });
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic && basicGroup != null)
            {
                if (basicGroup.Status is ChatMemberStatusCreator || (basicGroup.Status is ChatMemberStatusAdministrator administrator && administrator.Rights.CanInviteUsers) || chat.Permissions.CanInviteUsers)
                {
                    flyout.CreateFlyoutItem(ViewModel.InviteCommand, Strings.Resources.AddMember, new FontIcon { Glyph = Icons.PersonAdd });
                }

                flyout.CreateFlyoutItem(ViewModel.MembersCommand, Strings.Resources.SearchMembers, new FontIcon { Glyph = Icons.Search });
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

            var response = Client.Execute(new GetTextEntities(text));
            if (response is TextEntities entities)
            {
                ReplaceEntities(DescriptionSpan, text, entities.Entities);
            }
            else
            {
                DescriptionSpan.Inlines.Add(new Run { Text = text });
            }
        }

        private void ReplaceEntities(FormattedText text)
        {
            DescriptionSpan.Inlines.Clear();
            Description.BadgeLabel = text.Text;

            ReplaceEntities(DescriptionSpan, text.Text, text.Entities);
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

        private void Notifications_Click(object sender, RoutedEventArgs e)
        {
            var chat = ViewModel.Chat;
            if (chat == null)
            {
                return;
            }

            var muted = ViewModel.ClientService.Notifications.GetMutedFor(chat) > 0;
            if (muted)
            {
                ViewModel.ToggleMuteCommand.Execute();
            }
            else
            {
                var silent = chat.DefaultDisableNotification;

                var flyout = new MenuFlyout();

                if (muted is false)
                {
                    flyout.CreateFlyoutItem(true, () => { },
                        silent ? Strings.Resources.SoundOn : Strings.Resources.SoundOff,
                        new FontIcon { Glyph = silent ? Icons.MusicNote2 : Icons.MusicNoteOff2 });
                }

                flyout.CreateFlyoutItem(ViewModel.MuteForCommand, 60 * 60, Strings.Resources.MuteFor1h, new FontIcon { Glyph = Icons.ClockAlarmHour });
                flyout.CreateFlyoutItem(ViewModel.MuteForCommand, null, Strings.Resources.MuteForPopup, new FontIcon { Glyph = Icons.AlertSnooze });

                var toggle = flyout.CreateFlyoutItem(
                    ViewModel.ToggleMuteCommand,
                    muted ? Strings.Resources.UnmuteNotifications : Strings.Resources.MuteNotifications,
                    new FontIcon { Glyph = muted ? Icons.Speaker : Icons.SpeakerOff });

                if (muted is false)
                {
                    toggle.Foreground = App.Current.Resources["DangerButtonBackground"] as Brush;
                }

                flyout.ShowAt(sender as FrameworkElement, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
            }
        }
    }
}
