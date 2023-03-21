//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Folders;
using Telegram.Views;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
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

            Photo.Width = photoSize;
            Photo.Height = photoSize;
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
                        SubtitleLabel.Text = Locale.Declension(supergroup.IsChannel ? "Subscribers" : "Members", supergroup.MemberCount);
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

            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

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

        public void UpdateNotificationException(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
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
                TitleLabel.Text = clientService.GetTitle(chat);
            }
            else if (args.Phase == 1)
            {
                //SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
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

        public void UpdateStatisticsByChat(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
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
                    Photo.Source = null;
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

        public void UpdateChatFilter(IClientService clientService, ChatFilterElement element)
        {
            UpdateStyleNoSubtitle();

            if (element is FilterChat chat)
            {
                TitleLabel.Text = clientService.GetTitle(chat.Chat);
                Photo.SetChat(clientService, chat.Chat, 36);
                Identity.SetStatus(clientService, chat.Chat);
            }
            else if (element is FilterFlag flag)
            {
                switch (flag.Flag)
                {
                    case ChatListFilterFlags.IncludeContacts:
                        TitleLabel.Text = Strings.FilterContacts;
                        break;
                    case ChatListFilterFlags.IncludeNonContacts:
                        TitleLabel.Text = Strings.FilterNonContacts;
                        break;
                    case ChatListFilterFlags.IncludeGroups:
                        TitleLabel.Text = Strings.FilterGroups;
                        break;
                    case ChatListFilterFlags.IncludeChannels:
                        TitleLabel.Text = Strings.FilterChannels;
                        break;
                    case ChatListFilterFlags.IncludeBots:
                        TitleLabel.Text = Strings.FilterBots;
                        break;

                    case ChatListFilterFlags.ExcludeMuted:
                        TitleLabel.Text = Strings.FilterMuted;
                        break;
                    case ChatListFilterFlags.ExcludeRead:
                        TitleLabel.Text = Strings.FilterRead;
                        break;
                    case ChatListFilterFlags.ExcludeArchived:
                        TitleLabel.Text = Strings.FilterArchived;
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
