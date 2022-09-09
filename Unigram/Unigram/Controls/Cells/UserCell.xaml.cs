using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.ViewModels;
using Unigram.ViewModels.Folders;
using Unigram.Views;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Cells
{
    public sealed partial class UserCell : Grid
    {
        public UserCell()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler Click
        {
            add { Photo.IsEnabled = true; Photo.Click += value; }
            remove { Photo.IsEnabled = false; Photo.Click -= value; }
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

        public void UpdateUser(IProtoService protoService, User user, int photoSize, bool phoneNumber = false)
        {
            TitleLabel.Text = user.GetFullName();

            if (phoneNumber)
            {
#if DEBUG
                SubtitleLabel.Text = "+42 --- --- ----";
#else
                if (protoService.Options.TestMode)
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
            
            Photo.Width = photoSize;
            Photo.Height = photoSize;
            Photo.SetUser(protoService, user, photoSize);

            Identity.SetStatus(protoService, user);
        }

        public void UpdateUser(IProtoService protoService, User user, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            if (args.Phase == 0)
            {
                TitleLabel.Text = user.GetFullName();
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
                SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(protoService, user, 36);
                Identity.SetStatus(protoService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSupergroupMember(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            var member = args.Item as ChatMember;

            var user = protoService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.GetFullName();
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(protoService, member);
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(protoService, user, 36);
                Identity.SetStatus(protoService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSupergroupBanned(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var member = args.Item as ChatMember;

            var messageSender = protoService.GetMessageSender(member.MemberId);
            if (messageSender == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                if (messageSender is User user)
                {
                    TitleLabel.Text = user.GetFullName();
                }
                else if (messageSender is Chat chat)
                {
                    TitleLabel.Text = chat.Title;
                }
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(protoService, member);
            }
            else if (args.Phase == 2)
            {
                if (messageSender is User user)
                {
                    Photo.SetUser(protoService, user, 36);
                    Identity.SetStatus(protoService, user);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.SetChat(protoService, chat, 36);
                    Identity.SetStatus(protoService, chat);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSearchResult(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var result = args.Item as SearchResult;

            args.ItemContainer.Tag = result.Chat;

            if (args.Phase == 0)
            {
                TitleLabel.Style = BootStrapper.Current.Resources[result?.Chat?.Type is ChatTypeSecret ? "SecretBodyTextBlockStyle" : "BodyTextBlockStyle"] as Style;

                if (result.Chat != null)
                {
                    TitleLabel.Text = protoService.GetTitle(result.Chat);
                }
                else if (result.User != null)
                {
                }

                if (result.Chat != null)
                {
                    TitleLabel.Text = protoService.GetTitle(result.Chat);
                }
                else if (result.User != null)
                {
                    TitleLabel.Text = result.User.GetFullName();
                    Identity.SetStatus(protoService, result.User);
                }
            }
            else if (args.Phase == 1)
            {
                if (result.User != null || (result.Chat != null && result.Chat.Type is ChatTypePrivate or ChatTypeSecret))
                {
                    var user = result.User ?? protoService.GetUser(result.Chat);
                    if (result.IsPublic)
                    {
                        SubtitleLabel.Text = $"@{user.Username}";
                    }
                    else if (protoService.IsSavedMessages(user))
                    {
                        SubtitleLabel.Text = Strings.Resources.ThisIsYou;
                    }
                    else
                    {
                        SubtitleLabel.Text = LastSeenConverter.GetLabel(user, true);
                    }
                }
                else if (result.Chat != null && result.Chat.Type is ChatTypeSupergroup super)
                {
                    var supergroup = protoService.GetSupergroup(super.SupergroupId);
                    if (result.IsPublic)
                    {
                        if (supergroup.MemberCount > 0)
                        {
                            SubtitleLabel.Text = string.Format("@{0}, {1}", supergroup.Username, Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount));
                        }
                        else
                        {
                            SubtitleLabel.Text = $"@{supergroup.Username}";
                        }
                    }
                    else if (supergroup.MemberCount > 0)
                    {
                        SubtitleLabel.Text = Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount);
                    }
                    else
                    {
                        SubtitleLabel.Text = string.Empty;
                    }
                }
                else if (result.Chat != null && result.Chat.Type is ChatTypeBasicGroup basic)
                {
                    var basicGroup = protoService.GetBasicGroup(basic.BasicGroupId);
                    if (basicGroup.MemberCount > 0)
                    {
                        SubtitleLabel.Text = Locale.Declension("Members", basicGroup.MemberCount);
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
                    Photo.SetChat(protoService, result.Chat, 36);
                    Identity.SetStatus(protoService, result.Chat);
                }
                else if (result.User != null)
                {
                    Photo.SetUser(protoService, result.User, 36);
                    Identity.SetStatus(protoService, result.User);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateChatSharedMembers(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var member = args.Item as ChatMember;
            if (member == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            var user = protoService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.GetFullName();
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

                    InfoLabel.Text = string.IsNullOrEmpty(administrator.CustomTitle) ? Strings.Resources.ChannelAdmin : administrator.CustomTitle;
                }
                else if (member.Status is ChatMemberStatusCreator creator)
                {
                    if (InfoLabel == null)
                    {
                        FindName(nameof(InfoLabel));
                    }

                    InfoLabel.Text = string.IsNullOrEmpty(creator.CustomTitle) ? Strings.Resources.ChannelCreator : creator.CustomTitle;
                }
                else if (InfoLabel != null)
                {
                    InfoLabel.Text = string.Empty;
                }
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(protoService, user, 36);
                Identity.SetStatus(protoService, user);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateNotificationException(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            UpdateStyleNoSubtitle();

            var chat = args.Item as Chat;
            if (chat == null)
            {
                return;
            }

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            if (args.Phase == 0)
            {
                TitleLabel.Text = protoService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                //SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
            }
            else if (args.Phase == 2)
            {
                Photo.SetChat(protoService, chat, 36);
                Identity.SetStatus(protoService, chat);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateMessageSender(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            UpdateStyleNoSubtitle();

            var member = args.Item as MessageSender;

            var messageSender = protoService.GetMessageSender(member);
            if (messageSender == null)
            {
                return;
            }

            Tag = member;

            if (args.Phase == 0)
            {
                if (messageSender is User user)
                {
                    TitleLabel.Text = user.GetFullName();
                }
                else if (messageSender is Chat chat)
                {
                    TitleLabel.Text = chat.Title;
                }
            }
            else if (args.Phase == 1)
            {
                //SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(protoService, member);
            }
            else if (args.Phase == 2)
            {
                if (messageSender is User user)
                {
                    Photo.SetUser(protoService, user, 36);
                    Identity.SetStatus(protoService, user);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.SetChat(protoService, chat, 36);
                    Identity.SetStatus(protoService, chat);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateStatisticsByChat(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as UserCell;
            var statistics = args.Item as StorageStatisticsByChat;

            //if (chat == null)
            //{
            //    return;
            //}

            if (args.Phase == 0)
            {
                var chat = protoService.GetChat(statistics.ChatId);
                TitleLabel.Text = chat == null ? "Other Chats" : protoService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = FileSizeConverter.Convert(statistics.Size, true);
            }
            else if (args.Phase == 2)
            {
                if (statistics.ChatId == 0)
                {
                    Photo.Source = null;
                    Photo.Visibility = Visibility.Collapsed;
                }
                else
                {
                    var chat = protoService.GetChat(statistics.ChatId);

                    Photo.SetChat(protoService, chat, 36);
                    Photo.Visibility = Visibility.Visible;
                    Identity.SetStatus(protoService, chat);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }
        }

        public void UpdateChatFilter(IProtoService protoService, ChatFilterElement element)
        {
            UpdateStyleNoSubtitle();

            if (element is FilterChat chat)
            {
                TitleLabel.Text = protoService.GetTitle(chat.Chat);
                Photo.SetChat(protoService, chat.Chat, 36);
                Identity.SetStatus(protoService, chat.Chat);
            }
            else if (element is FilterFlag flag)
            {
                switch (flag.Flag)
                {
                    case ChatListFilterFlags.IncludeContacts:
                        TitleLabel.Text = Strings.Resources.FilterContacts;
                        break;
                    case ChatListFilterFlags.IncludeNonContacts:
                        TitleLabel.Text = Strings.Resources.FilterNonContacts;
                        break;
                    case ChatListFilterFlags.IncludeGroups:
                        TitleLabel.Text = Strings.Resources.FilterGroups;
                        break;
                    case ChatListFilterFlags.IncludeChannels:
                        TitleLabel.Text = Strings.Resources.FilterChannels;
                        break;
                    case ChatListFilterFlags.IncludeBots:
                        TitleLabel.Text = Strings.Resources.FilterBots;
                        break;

                    case ChatListFilterFlags.ExcludeMuted:
                        TitleLabel.Text = Strings.Resources.FilterMuted;
                        break;
                    case ChatListFilterFlags.ExcludeRead:
                        TitleLabel.Text = Strings.Resources.FilterRead;
                        break;
                    case ChatListFilterFlags.ExcludeArchived:
                        TitleLabel.Text = Strings.Resources.FilterArchived;
                        break;
                }

                Photo.Source = PlaceholderHelper.GetGlyph(MainPage.GetFilterIcon(flag.Flag), (int)flag.Flag, 36);
            }
        }


        private void UpdateStyleNoSubtitle()
        {
            TitlePanel.Margin = new Thickness(0, 0, 0, 2);
            TitlePanel.VerticalAlignment = VerticalAlignment.Center;
            SubtitleLabel.Visibility = Visibility.Collapsed;

            Grid.SetRowSpan(TitlePanel, 2);
        }
    }
}
