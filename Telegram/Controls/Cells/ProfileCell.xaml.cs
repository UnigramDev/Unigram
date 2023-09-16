//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Text;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Folders;
using Telegram.ViewModels.Stories;
using Telegram.Views;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ProfileCell : Grid
    {
        public ProfileCell()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler Click
        {
            add { Segments.IsEnabled = true; Segments.Click += value; }
            remove { Segments.IsEnabled = false; Segments.Click -= value; }
        }

        public double PhotoSize
        {
            get => Photo.Width;
            set => Photo.Width = Photo.Height = value;
        }

        public string Title
        {
            get => TitleLabel.Text;
            set => TitleLabel.Text = value;
        }

        public string Subtitle
        {
            get => SubtitleLabel.Text;
            set => SubtitleLabel.Text = value;
        }

        public void UpdateUser(IClientService clientService, User user, int photoSize, bool phoneNumber = false)
        {
            TitleLabel.Text = user.FullName();

            if (phoneNumber)
            {
#if DEBUG
                SubtitleLabel.Text = "+42 --- --- ----";
#else
                if (clientService.Options.TestMode)
                {
                    SubtitleLabel.Text = "+42 --- --- ----";
                }
                else
                {
                    SubtitleLabel.Text = PhoneNumber.Format(user.PhoneNumber);
                }
#endif
            }
            else
            {
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
                SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            }

            Photo.Width = Segments.Width = photoSize;
            Photo.Height = Segments.Height = photoSize;
            Photo.SetUser(clientService, user, photoSize);

            Identity.SetStatus(clientService, user);
        }

        public void UpdateUser(IClientService clientService, User user, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
                SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateActiveStories(IClientService clientService, ActiveStoriesViewModel activeStories, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            if (args.Phase == 0)
            {
                TitleLabel.Text = activeStories.Chat.Title;
            }
            else if (args.Phase == 1)
            {
                //SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
                //SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            }
            else if (args.Phase == 2)
            {
                Photo.SetChat(clientService, activeStories.Chat, 36);
                Identity.SetStatus(clientService, activeStories.Chat);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSupergroupMember(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            var member = args.Item as ChatMember;

            var user = clientService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(clientService, member);
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSupergroupAdminFilter(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            UpdateStyleNoSubtitle();

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            var member = args.Item as ChatMember;

            var user = clientService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSupergroupBanned(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var member = args.Item as ChatMember;

            var messageSender = clientService.GetMessageSender(member.MemberId);
            if (messageSender == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                if (messageSender is User user)
                {
                    TitleLabel.Text = user.FullName();
                }
                else if (messageSender is Chat chat)
                {
                    TitleLabel.Text = chat.Title;
                }
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(clientService, member);
            }
            else if (args.Phase == 2)
            {
                if (messageSender is User user)
                {
                    Photo.SetUser(clientService, user, 36);
                    Identity.SetStatus(clientService, user);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.SetChat(clientService, chat, 36);
                    Identity.SetStatus(clientService, chat);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSearchResult(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var result = args.Item as SearchResult;

            args.ItemContainer.Tag = result.Chat;

            if (args.Phase == 0)
            {
                TitleLabel.Style = BootStrapper.Current.Resources[result?.Chat?.Type is ChatTypeSecret ? "SecretBodyTextBlockStyle" : "BodyTextBlockStyle"] as Style;

                if (result.Chat != null)
                {
                    TitleLabel.Text = clientService.GetTitle(result.Chat);
                }
                else if (result.User != null)
                {
                }

                if (result.Chat != null)
                {
                    TitleLabel.Text = clientService.GetTitle(result.Chat);
                }
                else if (result.User != null)
                {
                    TitleLabel.Text = result.User.FullName();
                    Identity.SetStatus(clientService, result.User);
                }
            }
            else if (args.Phase == 1)
            {
                if (result.User != null || (result.Chat != null && result.Chat.Type is ChatTypePrivate or ChatTypeSecret))
                {
                    var user = result.User ?? clientService.GetUser(result.Chat);
                    if (result.IsPublic)
                    {
                        SubtitleLabel.Text = $"@{user.ActiveUsername(result.Query)}";
                    }
                    else if (clientService.IsSavedMessages(user))
                    {
                        SubtitleLabel.Text = Strings.ThisIsYou;
                    }
                    else
                    {
                        SubtitleLabel.Text = LastSeenConverter.GetLabel(user, true);
                    }
                }
                else if (result.Chat != null && result.Chat.Type is ChatTypeSupergroup super)
                {
                    var supergroup = clientService.GetSupergroup(super.SupergroupId);
                    if (result.IsPublic)
                    {
                        if (supergroup.MemberCount > 0)
                        {
                            SubtitleLabel.Text = string.Format("@{0}, {1}", supergroup.ActiveUsername(result.Query), Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount));
                        }
                        else
                        {
                            SubtitleLabel.Text = $"@{supergroup.ActiveUsername(result.Query)}";
                        }
                    }
                    else if (supergroup.MemberCount > 0)
                    {
                        SubtitleLabel.Text = Locale.Declension(supergroup.IsChannel ? Strings.R.Subscribers : Strings.R.Members, supergroup.MemberCount);
                    }
                    else
                    {
                        SubtitleLabel.Text = string.Empty;
                    }
                }
                else if (result.Chat != null && result.Chat.Type is ChatTypeBasicGroup basic)
                {
                    var basicGroup = clientService.GetBasicGroup(basic.BasicGroupId);
                    if (basicGroup.MemberCount > 0)
                    {
                        SubtitleLabel.Text = Locale.Declension(Strings.R.Members, basicGroup.MemberCount);
                    }
                    else
                    {
                        SubtitleLabel.Text = string.Empty;
                    }
                }
                else
                {
                    SubtitleLabel.Text = string.Empty;
                }

                if (SubtitleLabel.Text.StartsWith($"@{result.Query}", StringComparison.OrdinalIgnoreCase))
                {
                    var highligher = new TextHighlighter();
                    highligher.Foreground = new SolidColorBrush(Colors.Red);
                    highligher.Background = new SolidColorBrush(Colors.Transparent);
                    highligher.Ranges.Add(new TextRange { StartIndex = 1, Length = result.Query.Length });

                    SubtitleLabel.TextHighlighters.Add(highligher);
                }
                else
                {
                    SubtitleLabel.TextHighlighters.Clear();
                }
            }
            else if (args.Phase == 2)
            {
                if (result.Chat != null)
                {
                    Photo.SetChat(clientService, result.Chat, 36);
                    Identity.SetStatus(clientService, result.Chat);
                }
                else if (result.User != null)
                {
                    Photo.SetUser(clientService, result.User, 36);
                    Identity.SetStatus(clientService, result.User);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateChatSharedMembers(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var member = args.Item as ChatMember;
            if (member == null)
            {
                return;
            }

            var user = clientService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);

                if (member.Status is ChatMemberStatusAdministrator administrator)
                {
                    if (InfoLabel == null)
                    {
                        FindName(nameof(InfoLabel));
                    }

                    InfoLabel.Text = string.IsNullOrEmpty(administrator.CustomTitle) ? Strings.ChannelAdmin : administrator.CustomTitle;
                }
                else if (member.Status is ChatMemberStatusCreator creator)
                {
                    if (InfoLabel == null)
                    {
                        FindName(nameof(InfoLabel));
                    }

                    InfoLabel.Text = string.IsNullOrEmpty(creator.CustomTitle) ? Strings.ChannelCreator : creator.CustomTitle;
                }
                else if (InfoLabel != null)
                {
                    InfoLabel.Text = string.Empty;
                }
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateChatBoost(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var boost = args.Item as ChatBoost;
            if (boost == null)
            {
                return;
            }

            var user = clientService.GetUser(boost.UserId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = string.Format(Strings.BoostExpireOn, Formatter.DateAt(boost.ExpireDate));
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateNotificationException(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var chat = args.Item as Chat;
            if (chat == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = clientService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                var value = clientService.Notifications.GetMutedFor(chat);
                if (value == 0)
                {
                    var builder = new StringBuilder(Strings.NotificationExceptionsAlwaysOn);

                    if (!chat.NotificationSettings.UseDefaultShowPreview)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(chat.NotificationSettings.ShowPreview
                            ? Strings.NotificationExceptionsPreviewShow
                            : Strings.NotificationExceptionsPreviewHide);
                    }

                    if (!chat.NotificationSettings.UseDefaultSound)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(Strings.NotificationExceptionsSoundCustom);
                    }

                    SubtitleLabel.Text = builder.ToString();
                }
                else
                {
                    SubtitleLabel.Text = Strings.NotificationExceptionsAlwaysOff;
                }
            }
            else if (args.Phase == 2)
            {
                Photo.SetChat(clientService, chat, 36);
                Identity.SetStatus(clientService, chat);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateAddedReaction(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var reaction = args.Item as AddedReaction;

            var messageSender = clientService.GetMessageSender(reaction.SenderId);
            if (messageSender == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                if (messageSender is User user)
                {
                    TitleLabel.Text = user.FullName();
                }
                else if (messageSender is Chat chat)
                {
                    TitleLabel.Text = chat.Title;
                }
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = Locale.FormatDateAudio(reaction.Date);
            }
            else if (args.Phase == 2)
            {
                if (messageSender is User user)
                {
                    Photo.SetUser(clientService, user, 36);
                    Identity.SetStatus(clientService, user);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.SetChat(clientService, chat, 36);
                    Identity.SetStatus(clientService, chat);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateMessageViewer(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var viewer = args.Item as MessageViewer;

            var user = clientService.GetUser(viewer.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = Locale.FormatDateAudio(viewer.ViewDate);
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateStoryViewer(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var viewer = args.Item as StoryViewer;

            var user = clientService.GetUser(viewer.UserId);
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = Locale.FormatDateAudio(viewer.ViewDate);
            }
            else if (args.Phase == 2)
            {
                Segments.SetUser(clientService, user, 36);
                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateMessageSender(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            UpdateStyleNoSubtitle();

            var member = args.Item as MessageSender;

            var messageSender = clientService.GetMessageSender(member);
            if (messageSender == null)
            {
                return;
            }

            Tag = member;

            if (args.Phase == 0)
            {
                if (messageSender is User user)
                {
                    TitleLabel.Text = user.FullName();
                }
                else if (messageSender is Chat chat)
                {
                    TitleLabel.Text = chat.Title;
                }
            }
            else if (args.Phase == 1)
            {
                //SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(clientService, member);
            }
            else if (args.Phase == 2)
            {
                if (messageSender is User user)
                {
                    Photo.SetUser(clientService, user, 36);
                    Identity.SetStatus(clientService, user);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.SetChat(clientService, chat, 36);
                    Identity.SetStatus(clientService, chat);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateMessageSender(IClientService clientService, MessageSender member)
        {
            UpdateStyleNoSubtitle();

            var messageSender = clientService.GetMessageSender(member);
            if (messageSender == null)
            {
                return;
            }

            if (messageSender is User user)
            {
                TitleLabel.Text = user.FullName();

                Photo.SetUser(clientService, user, 36);
                Identity.SetStatus(clientService, user);
            }
            else if (messageSender is Chat chat)
            {
                TitleLabel.Text = chat.Title;

                Photo.SetChat(clientService, chat, 36);
                Identity.SetStatus(clientService, chat);
            }
        }

        public void UpdateMessageStatisticsSharer(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var message = args.Item as Message;

            var chat = clientService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            Tag = message;

            if (args.Phase == 0)
            {
                TitleLabel.Text = chat.Title;
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = Locale.Declension(Strings.R.Views, message.InteractionInfo?.ViewCount ?? 0);
            }
            else if (args.Phase == 2)
            {
                Photo.SetChat(clientService, chat, 36);
                Identity.SetStatus(clientService, chat);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateChat(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            UpdateStyleNoSubtitle();

            var chat = args.Item as Chat;

            Tag = chat;

            if (args.Phase == 0)
            {
                TitleLabel.Text = chat.Title;
            }
            else if (args.Phase == 1)
            {

            }
            else if (args.Phase == 2)
            {
                Photo.SetChat(clientService, chat, 36);
                Identity.SetStatus(clientService, chat);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateStatisticsByChat(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as ProfileCell;
            var statistics = args.Item as StorageStatisticsByChat;

            //if (chat == null)
            //{
            //    return;
            //}

            if (args.Phase == 0)
            {
                var chat = clientService.GetChat(statistics.ChatId);
                TitleLabel.Text = chat == null ? "Other Chats" : clientService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = FileSizeConverter.Convert(statistics.Size, true);
            }
            else if (args.Phase == 2)
            {
                if (statistics.ChatId == 0)
                {
                    Photo.Clear();
                    Photo.Visibility = Visibility.Collapsed;
                }
                else
                {
                    var chat = clientService.GetChat(statistics.ChatId);

                    Photo.SetChat(clientService, chat, 36);
                    Photo.Visibility = Visibility.Visible;
                    Identity.SetStatus(clientService, chat);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }
        }

        public void UpdateChatFolder(IClientService clientService, ChatFolderElement element)
        {
            UpdateStyleNoSubtitle();

            if (element is FolderChat chat)
            {
                TitleLabel.Text = clientService.GetTitle(chat.Chat);
                Photo.SetChat(clientService, chat.Chat, 36);
                Identity.SetStatus(clientService, chat.Chat);
            }
            else if (element is FolderFlag flag)
            {
                switch (flag.Flag)
                {
                    case ChatListFolderFlags.IncludeContacts:
                        TitleLabel.Text = Strings.FilterContacts;
                        break;
                    case ChatListFolderFlags.IncludeNonContacts:
                        TitleLabel.Text = Strings.FilterNonContacts;
                        break;
                    case ChatListFolderFlags.IncludeGroups:
                        TitleLabel.Text = Strings.FilterGroups;
                        break;
                    case ChatListFolderFlags.IncludeChannels:
                        TitleLabel.Text = Strings.FilterChannels;
                        break;
                    case ChatListFolderFlags.IncludeBots:
                        TitleLabel.Text = Strings.FilterBots;
                        break;

                    case ChatListFolderFlags.ExcludeMuted:
                        TitleLabel.Text = Strings.FilterMuted;
                        break;
                    case ChatListFolderFlags.ExcludeRead:
                        TitleLabel.Text = Strings.FilterRead;
                        break;
                    case ChatListFolderFlags.ExcludeArchived:
                        TitleLabel.Text = Strings.FilterArchived;
                        break;
                }

                Photo.Source = PlaceholderImage.GetGlyph(MainPage.GetFolderIcon(flag.Flag), (int)flag.Flag);
            }
        }


        private void UpdateStyleNoSubtitle()
        {
            TitlePanel.Margin = new Thickness(0, 0, 0, 2);
            TitlePanel.VerticalAlignment = VerticalAlignment.Center;
            SubtitleLabel.Visibility = Visibility.Collapsed;

            SetRowSpan(TitlePanel, 2);
        }




        #region Repeater :(

        public void UpdateLinkedChat(IClientService clientService, ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            var button = args.Element as Button;
            var chat = button.DataContext as Chat;

            TitleLabel.Text = clientService.GetTitle(chat);

            if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.HasActiveUsername(out string username))
                {
                    SubtitleLabel.Text = $"@{username}";
                }
                else
                {
                    SubtitleLabel.Text = Locale.Declension(supergroup.IsChannel ? Strings.R.Subscribers : Strings.R.Members, supergroup.MemberCount);
                }
            }

            Photo.SetChat(clientService, chat, 36);
            Identity.SetStatus(clientService, chat);
        }

        #endregion
    }
}
