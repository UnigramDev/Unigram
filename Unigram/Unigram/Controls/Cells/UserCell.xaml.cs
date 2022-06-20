using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Navigation;
using Unigram.Services;
using Unigram.ViewModels;
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

        public void UpdateUser(IProtoService protoService, User user, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            if (args.Phase == 0)
            {
                TitleLabel.Text = user.GetFullName();
                Premium.Visibility = user.IsPremium && protoService.IsPremiumAvailable
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
                SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(protoService, user, 36);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSupergroupPermissions(IProtoService protoService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var member = args.Item as ChatMember;

            var user = protoService.GetMessageSender(member.MemberId) as User;
            if (user == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.GetFullName();
                Premium.Visibility = user.IsPremium && protoService.IsPremiumAvailable
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = ChannelParticipantToTypeConverter.Convert(protoService, member);
            }
            else if (args.Phase == 2)
            {
                Photo.SetUser(protoService, user, 36);
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
                    Premium.Visibility = user.IsPremium && protoService.IsPremiumAvailable
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
                else if (messageSender is Chat chat)
                {
                    TitleLabel.Text = chat.Title;
                    Premium.Visibility = Visibility.Collapsed;
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
                }
                else if (messageSender is Chat chat)
                {
                    Photo.SetChat(protoService, chat, 36);
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
                    TitleLabel.Text = result.User.GetFullName();
                }

                var verified = false;
                var premium = false;

                if (result.Chat != null)
                {
                    if (protoService.TryGetUser(result.Chat, out User user))
                    {
                        verified = user.IsVerified;
                        premium = user.IsPremium && protoService.IsPremiumAvailable && user.Id != protoService.Options.MyId;
                    }
                    else if (protoService.TryGetSupergroup(result.Chat, out Supergroup supergroup))
                    {
                        verified = supergroup.IsVerified;
                        premium = false;
                    }
                }
                else if (result.User != null)
                {
                    verified = result.User.IsVerified;
                    premium = result.User.IsPremium && protoService.IsPremiumAvailable;
                }

                if (premium || verified)
                {
                    Premium.Glyph = premium ? Icons.Premium16 : Icons.Verified16;
                    Premium.Visibility = Visibility.Visible;
                }
                else
                {
                    Premium.Visibility = Visibility.Collapsed;
                }
            }
            else if (args.Phase == 1)
            {
                if (result.User != null || (result.Chat != null && (result.Chat.Type is ChatTypePrivate privata || result.Chat.Type is ChatTypeSecret)))
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
                }
                else if (result.User != null)
                {
                    Photo.SetUser(protoService, result.User, 36);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

    }
}
