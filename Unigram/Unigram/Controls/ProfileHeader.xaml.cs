using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Gallery;
using Unigram.Converters;
using Unigram.ViewModels;
using Unigram.ViewModels.Chats;
using Unigram.ViewModels.Users;
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
                var item = ViewModel.ProtoService.GetUser(privata.UserId);
                var cache = ViewModel.ProtoService.GetUserFull(privata.UserId);

                UpdateUser(chat, item, false);

                if (cache != null)
                {
                    UpdateUserFullInfo(chat, item, cache, false, false);
                }
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = ViewModel.ProtoService.GetSecretChat(secretType.SecretChatId);
                var item = ViewModel.ProtoService.GetUser(secretType.UserId);
                var cache = ViewModel.ProtoService.GetUserFull(secretType.UserId);

                UpdateSecretChat(chat, secret);
                UpdateUser(chat, item, true);

                if (cache != null)
                {
                    UpdateUserFullInfo(chat, item, cache, true, false);
                }
            }
            else if (chat.Type is ChatTypeBasicGroup basic)
            {
                var item = ViewModel.ProtoService.GetBasicGroup(basic.BasicGroupId);
                var cache = ViewModel.ProtoService.GetBasicGroupFull(basic.BasicGroupId);

                UpdateBasicGroup(chat, item);

                if (cache != null)
                {
                    UpdateBasicGroupFullInfo(chat, item, cache);
                }
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                var item = ViewModel.ProtoService.GetSupergroup(super.SupergroupId);
                var cache = ViewModel.ProtoService.GetSupergroupFull(super.SupergroupId);

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

                var viewModel = new UserPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, user, userFull);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                var basicGroupFull = ViewModel.ProtoService.GetBasicGroupFull(chat);
                if (basicGroupFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, chat, basicGroupFull.Photo);
                await GalleryView.GetForCurrentView().ShowAsync(viewModel, () => Photo);
            }
            else if (chat.Type is ChatTypeSupergroup)
            {
                var supergroupFull = ViewModel.ProtoService.GetSupergroupFull(chat);
                if (supergroupFull?.Photo == null)
                {
                    return;
                }

                var viewModel = new ChatPhotosViewModel(ViewModel.ProtoService, ViewModel.StorageService, ViewModel.Aggregator, chat, supergroupFull.Photo);
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
        }

        public void UpdateChatTitle(Chat chat)
        {
            Title.Text = ViewModel.ProtoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            Photo.SetChat(ViewModel.ProtoService, chat, 64);
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            var unmuted = ViewModel.CacheService.Notifications.GetMutedFor(chat) == 0;
            NotificationGlyph.Text = unmuted ? Icons.Alert : Icons.AlertOff;
        }

        public void UpdateUser(Chat chat, User user, bool secret)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);

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
        }

        public void UpdateUserStatus(Chat chat, User user)
        {
            Subtitle.Text = LastSeenConverter.GetLabel(user, true);
        }



        public void UpdateSecretChat(Chat chat, SecretChat secretChat)
        {
            if (secretChat.State is SecretChatStateReady)
            {
                SecretLifetime.Badge = chat.MessageTtl > 0 ? Locale.FormatTtl(chat.MessageTtl) : Strings.Resources.ShortMessageLifetimeForever;
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
        }

        public void UpdateBasicGroupFullInfo(Chat chat, BasicGroup group, BasicGroupFullInfo fullInfo)
        {
            GetEntities(fullInfo.Description);
            Description.Visibility = string.IsNullOrEmpty(fullInfo.Description) ? Visibility.Collapsed : Visibility.Visible;
        }



        public void UpdateSupergroup(Chat chat, Supergroup group)
        {
            Subtitle.Text = Locale.Declension(group.IsChannel ? "Subscribers" : "Members", group.MemberCount);

            Description.Content = Strings.Resources.DescriptionPlaceholder;

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

            ChannelMembersPanel.Visibility = group.IsChannel && (group.Status is ChatMemberStatusCreator || group.Status is ChatMemberStatusAdministrator) ? Visibility.Visible : Visibility.Collapsed;
            MembersPanel.Visibility = group.IsChannel ? Visibility.Collapsed : Visibility.Collapsed;
            //Admins.Visibility = Visibility.Collapsed;
            //Banned.Visibility = Visibility.Collapsed;
            //Restricted.Visibility = Visibility.Collapsed;
            //Members.Visibility = Visibility.Collapsed;

            // Unused:
            UserPhone.Visibility = Visibility.Collapsed;
            //UserCommonChats.Visibility = Visibility.Collapsed;
            UserStartSecret.Visibility = Visibility.Collapsed;
            SecretLifetime.Visibility = Visibility.Collapsed;
            SecretHashKey.Visibility = Visibility.Collapsed;
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
    }
}
