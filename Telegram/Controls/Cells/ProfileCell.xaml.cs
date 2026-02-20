//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
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
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Cells
{
    public sealed partial class ProfileCell : ContentControl
    {
        public ProfileCell()
        {
            DefaultStyleKey = typeof(ProfileCell);
        }

        #region InitializeComponent

        private Rectangle SelectionOutline;
        private ActiveStoriesSegments Segments;
        private ProfilePicture Photo;
        private Grid TitlePanel;
        private CustomEmojiIcon BotVerified;
        private TextBlock TitleLabel;
        private IdentityIcon Identity;
        private TextBlock SubtitleLabel;

        // Deferred
        private Border RestrictsNewChats;
        private ContentPresenter ContentPresenter;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            SelectionOutline = GetTemplateChild(nameof(SelectionOutline)) as Rectangle;
            Segments = GetTemplateChild(nameof(Segments)) as ActiveStoriesSegments;
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            TitlePanel = GetTemplateChild(nameof(TitlePanel)) as Grid;
            BotVerified = GetTemplateChild(nameof(BotVerified)) as CustomEmojiIcon;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            Identity = GetTemplateChild(nameof(Identity)) as IdentityIcon;
            SubtitleLabel = GetTemplateChild(nameof(SubtitleLabel)) as TextBlock;

            if (Content != null)
            {
                ContentPresenter ??= GetTemplateChild(nameof(ContentPresenter)) as ContentPresenter;
            }

            _templateApplied = true;

            if (_user != null)
            {
                UpdateUser(_clientService, _user, _photoSize, _phoneNumber);

                _clientService = null;
                _user = null;
            }
            else if (_chat != null)
            {
                UpdateChat(_clientService, _chat, _photoSize);

                _clientService = null;
                _chat = null;
            }
            else if (_member != null)
            {
                UpdateMessageSender(_clientService, _member);

                _clientService = null;
                _member = null;
            }
            else if (_element != null)
            {
                UpdateChatFolder(_clientService, _element);

                _clientService = null;
                _element = null;
            }

            if (_subtitle != null)
            {
                SubtitleLabel.Text = _subtitle;

                _subtitle = null;
            }
        }

        #endregion

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            if (newContent != null)
            {
                ContentPresenter ??= GetTemplateChild(nameof(ContentPresenter)) as ContentPresenter;
            }

            base.OnContentChanged(oldContent, newContent);
        }

        private event RoutedEventHandler _click;
        public event RoutedEventHandler Click
        {
            add { Segments?.IsEnabled = true; _click += value; }
            remove { Segments?.IsEnabled = false; _click -= value; }
        }

        private string _subtitle;
        public string Subtitle
        {
            set
            {
                if (_templateApplied)
                {
                    SubtitleLabel.Text = value;
                }
                else
                {
                    _subtitle = value;
                }
            }
        }

        public void UpdateUserInflated(IClientService clientService, User user, int photoSize, bool phoneNumber = false)
        {
            TitleLabel.Text = user.FullName();

            if (phoneNumber)
            {
                if (SettingsService.Current.Diagnostics.HidePhoneNumber)
                {
                    SubtitleLabel.Text = "+42 --- --- ----";
                }
                else
                {
                    SubtitleLabel.Text = PhoneNumber.Format(user.PhoneNumber);
                }
            }
            else if (user.Type is UserTypeBot bot)
            {
                SubtitleLabel.Text = bot.ActiveUserCount > 0 ? Locale.Declension(Strings.R.BotDAU, bot.ActiveUserCount) : Strings.Bot;
                SubtitleLabel.Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style;
            }
            else
            {
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
                SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            }

            Segments.Width = photoSize;
            Segments.Height = photoSize;
            Photo.Size = photoSize;
            Photo.Source = ProfilePictureSource.User(clientService, user);

            Identity.SetStatus(clientService, user, BotVerified);
        }

        private IClientService _clientService;
        private User _user;
        private Chat _chat;
        private int _photoSize;
        private bool _phoneNumber;

        public void UpdateUser(IClientService clientService, User user, int photoSize, bool phoneNumber = false)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _user = user;
                _photoSize = photoSize;
                _phoneNumber = phoneNumber;
                return;
            }

            TitleLabel.Text = user.FullName();

            if (phoneNumber)
            {
                if (SettingsService.Current.Diagnostics.HidePhoneNumber)
                {
                    SubtitleLabel.Text = "+42 --- --- ----";
                }
                else
                {
                    SubtitleLabel.Text = PhoneNumber.Format(user.PhoneNumber);
                }
            }
            else if (user.Type is UserTypeBot bot)
            {
                SubtitleLabel.Text = bot.ActiveUserCount > 0 ? Locale.Declension(Strings.R.BotDAU, bot.ActiveUserCount) : Strings.Bot;
                SubtitleLabel.Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style;
            }
            else
            {
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
                SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            }

            Segments.Width = photoSize;
            Segments.Height = photoSize;
            Photo.Size = photoSize;
            Photo.Source = ProfilePictureSource.User(clientService, user);

            Identity.SetStatus(clientService, user, BotVerified);
        }

        public void UpdateChat(IClientService clientService, Chat chat, int photoSize)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _chat = chat;
                _photoSize = photoSize;
                return;
            }

            TitleLabel.Text = chat.Title;

            //if (phoneNumber)
            //{
            //    if (SettingsService.Current.Diagnostics.HidePhoneNumber)
            //    {
            //        SubtitleLabel.Text = "+42 --- --- ----";
            //    }
            //    else
            //    {
            //        SubtitleLabel.Text = PhoneNumber.Format(user.PhoneNumber);
            //    }
            //}
            //else if (user.Type is UserTypeBot bot)
            //{
            //    SubtitleLabel.Text = bot.ActiveUserCount > 0 ? Locale.Declension(Strings.R.BotDAU, bot.ActiveUserCount) : Strings.Bot;
            //    SubtitleLabel.Style = BootStrapper.Current.Resources["InfoCaptionTextBlockStyle"] as Style;
            //}
            //else
            //{
            //    SubtitleLabel.Text = LastSeenConverter.GetLabel(user, false);
            //    SubtitleLabel.Style = BootStrapper.Current.Resources[user.Status is UserStatusOnline ? "AccentCaptionTextBlockStyle" : "InfoCaptionTextBlockStyle"] as Style;
            //}

            Segments.Width = photoSize;
            Segments.Height = photoSize;
            Photo.Size = photoSize;

            if (clientService.TryGetUser(chat, out User user))
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
            }
            else
            {
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
            }

            Identity.SetStatus(clientService, chat, BotVerified);
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
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
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
                Photo.Source = ProfilePictureSource.Chat(clientService, activeStories.Chat);
                Identity.SetStatus(clientService, activeStories.Chat, BotVerified);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateChatInviteLinkMember(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var member = args.Item as ChatInviteLinkMember;

            var user = clientService.GetUser(member.UserId);
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
                SubtitleLabel.Text = LastSeenConverter.GetLabel(user, true);
            }
            else if (args.Phase == 2)
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateFoundAffiliateProgram(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            var program = args.Item as FoundAffiliateProgram;

            var user = clientService.GetUser(program.BotUserId);
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
                var percent = program.Info.Parameters.CommissionPercent();
                var duration = program.Info.Parameters.Duration();

                SubtitleLabel.Text = string.Format("{0} • {1}", percent, duration);
            }
            else if (args.Phase == 2)
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateConnectedAffiliateProgram(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            args.ItemContainer.Tag = args.Item;
            Tag = args.Item;

            var program = args.Item as ConnectedAffiliateProgram;

            var user = clientService.GetUser(program.BotUserId);
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
                var percent = program.Parameters.CommissionPercent();
                var duration = program.Parameters.Duration();

                SubtitleLabel.Text = string.Format("{0} • {1}", percent, duration);
            }
            else if (args.Phase == 2)
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
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
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
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
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
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
                    Photo.Source = ProfilePictureSource.User(clientService, user);
                    Identity.SetStatus(clientService, user, BotVerified);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                    Identity.SetStatus(clientService, chat, BotVerified);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateBoostSlot(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var slot = args.Item as ChatBoostSlot;
            if (slot == null)
            {
                return;
            }

            args.ItemContainer.IsEnabled = slot.CooldownUntilDate == 0;

            var chat = clientService.GetChat(slot.CurrentlyBoostedChatId);
            if (chat == null)
            {
                return;
            }

            if (args.Phase == 0)
            {
                TitleLabel.Text = chat.Title;
            }
            else if (args.Phase == 1)
            {
                var diff = slot.CooldownUntilDate - DateTime.Now.ToTimestamp();
                if (diff > 0)
                {
                    SubtitleLabel.Text = string.Format(Strings.BoostingAvailableIn, diff.ToDuration());
                }
                else
                {
                    SubtitleLabel.Text = string.Format(Strings.BoostExpireOn, Formatter.Date(slot.ExpirationDate));
                }
            }
            else if (args.Phase == 2)
            {
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Identity.SetStatus(clientService, chat, BotVerified);

                SelectionOutline.RadiusX = 18;
                SelectionOutline.RadiusY = 18;
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        private SearchResult _searchResult;

        public void UpdateSearchResult(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var result = args.Item as SearchResult;

            args.ItemContainer.Tag = result.Chat;

            if (args.Phase == 0)
            {
                RecycleSearchResult();

                TitleLabel.Style = BootStrapper.Current.Resources[result?.Chat?.Type is ChatTypeSecret ? "SecretBodyTextBlockStyle" : "BodyTextBlockStyle"] as Style;

                if (result.Chat != null)
                {
                    TitleLabel.Text = clientService.GetTitle(result.Chat);
                }
                else if (result.User != null)
                {
                    TitleLabel.Text = result.User.FullName();
                    Identity.SetStatus(clientService, result.User, BotVerified);
                }

                long? userId;
                if (result.Chat?.Type is ChatTypePrivate privata)
                {
                    userId = privata.UserId;
                }
                else if (result.Chat?.Type is ChatTypeSecret secret)
                {
                    userId = secret.UserId;
                }
                else
                {
                    userId = result.User?.Id;
                }

                if (userId == null || result.RestrictsNewChats is false or null)
                {
                    RestrictsNewChats?.Visibility = Visibility.Collapsed;

                    if (userId != null)
                    {
                        _searchResult = result;
                        _searchResult.PropertyChanged += SearchResult_PropertyChanged;
                        _searchResult.CanSendMessageToUser();
                    }
                }
                else if (userId != null)
                {
                    RestrictsNewChats ??= GetTemplateChild(nameof(RestrictsNewChats)) as Border;
                    RestrictsNewChats.Visibility = Visibility.Visible;
                }

                Photo.Source = null;
                Identity.ClearStatus(BotVerified);
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
                    if (supergroup.IsDirectMessagesGroup)
                    {
                        SubtitleLabel.Text = Strings.MonoforumMessages;
                    }
                    else if (result.IsPublic)
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
                    highligher.Foreground = new SolidColorBrush(Theme.Accent);
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
                    Photo.Source = ProfilePictureSource.Chat(clientService, result.Chat);
                    Identity.SetStatus(clientService, result.Chat, BotVerified);
                }
                else if (result.User != null)
                {
                    Photo.Source = ProfilePictureSource.User(clientService, result.User);
                    Identity.SetStatus(clientService, result.User, BotVerified);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void RecycleSearchResult()
        {
            if (_searchResult != null)
            {
                _searchResult.PropertyChanged -= SearchResult_PropertyChanged;
                _searchResult = null;
            }
        }

        private void SearchResult_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is SearchResult result && result == _searchResult)
            {
                long? userId;
                if (result.Chat?.Type is ChatTypePrivate privata)
                {
                    userId = privata.UserId;
                }
                else if (result.Chat?.Type is ChatTypeSecret secret)
                {
                    userId = secret.UserId;
                }
                else
                {
                    userId = result.User?.Id;
                }

                if (userId == null || result.RestrictsNewChats is false or null)
                {
                    RestrictsNewChats?.Visibility = Visibility.Collapsed;
                }
                else if (userId != null)
                {
                    RestrictsNewChats ??= GetTemplateChild(nameof(RestrictsNewChats)) as Border;
                    RestrictsNewChats.Visibility = Visibility.Visible;
                }

                RecycleSearchResult();
            }
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
                    var infoLabel = Content as TextBlock;
                    infoLabel?.Text = string.IsNullOrEmpty(administrator.CustomTitle) ? Strings.ChannelAdmin : administrator.CustomTitle;
                }
                else if (member.Status is ChatMemberStatusCreator creator)
                {
                    var infoLabel = Content as TextBlock;
                    infoLabel?.Text = string.IsNullOrEmpty(creator.CustomTitle) ? Strings.ChannelCreator : creator.CustomTitle;
                }
                else
                {
                    var infoLabel = Content as TextBlock;
                    infoLabel?.Text = string.Empty;
                }
            }
            else if (args.Phase == 2)
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
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
                var value = clientService.Notifications.GetMuteFor(chat);
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
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Identity.SetStatus(clientService, chat, BotVerified);
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
                    Photo.Source = ProfilePictureSource.User(clientService, user);
                    Identity.SetStatus(clientService, user, BotVerified);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                    Identity.SetStatus(clientService, chat, BotVerified);
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
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateStoryViewer(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var interaction = args.Item as StoryInteraction;

            if (args.Phase == 0)
            {
                if (clientService.TryGetUser(interaction.ActorId, out User user))
                {
                    TitleLabel.Text = user.FullName();
                }
                else if (clientService.TryGetChat(interaction.ActorId, out Chat chat))
                {
                    TitleLabel.Text = chat.Title;
                }
            }
            else if (args.Phase == 1)
            {
                SubtitleLabel.Text = Locale.FormatDateAudio(interaction.InteractionDate);
            }
            else if (args.Phase == 2)
            {
                if (clientService.TryGetUser(interaction.ActorId, out User user))
                {
                    Segments.SetUser(clientService, user, 36);
                    Photo.Source = ProfilePictureSource.User(clientService, user);
                    Identity.SetStatus(clientService, user, BotVerified);
                }
                else if (clientService.TryGetChat(interaction.ActorId, out Chat chat))
                {
                    Segments.SetChat(clientService, chat, 36);
                    Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                    Identity.SetStatus(clientService, chat, BotVerified);
                }
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
                    Photo.Source = ProfilePictureSource.User(clientService, user);
                    Identity.SetStatus(clientService, user, BotVerified);
                }
                else if (messageSender is Chat chat)
                {
                    Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                    Identity.SetStatus(clientService, chat, BotVerified);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        private MessageSender _member;

        public void UpdateMessageSender(IClientService clientService, MessageSender member)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _member = member;
                return;
            }

            UpdateStyleNoSubtitle();

            var messageSender = clientService.GetMessageSender(member);
            if (messageSender == null)
            {
                return;
            }

            if (messageSender is User user)
            {
                TitleLabel.Text = user.FullName();

                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
            }
            else if (messageSender is Chat chat)
            {
                TitleLabel.Text = chat.Title;

                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Identity.SetStatus(clientService, chat, BotVerified);
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
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Identity.SetStatus(clientService, chat, BotVerified);
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
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Identity.SetStatus(clientService, chat, BotVerified);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSimilarChannel(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var chat = args.Item as Chat;

            if (args.Phase == 0)
            {
                TitleLabel.Text = chat.Title;
            }
            else if (args.Phase == 1)
            {
                if (clientService.TryGetSupergroup(chat, out Supergroup supergroup))
                {
                    SubtitleLabel.Text = Locale.Declension(Strings.R.Subscribers, supergroup.MemberCount);
                }
            }
            else if (args.Phase == 2)
            {
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Identity.SetStatus(clientService, chat, BotVerified);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        public void UpdateSimilarBot(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var user = args.Item as User;

            if (args.Phase == 0)
            {
                TitleLabel.Text = user.FullName();
            }
            else if (args.Phase == 1)
            {
                if (user.Type is UserTypeBot typeBot)
                {
                    SubtitleLabel.Text = typeBot.ActiveUserCount > 0 ? Locale.Declension(Strings.R.BotDAU, typeBot.ActiveUserCount) : Strings.Bot;
                }
            }
            else if (args.Phase == 2)
            {
                Photo.Source = ProfilePictureSource.User(clientService, user);
                Identity.SetStatus(clientService, user, BotVerified);
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
                var chat = clientService.GetChat(statistics.ChatId);
                if (chat == null)
                {
                    Photo.Source = null;
                    Photo.Visibility = Visibility.Collapsed;
                    Identity.ClearStatus(BotVerified);
                }
                else
                {
                    Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                    Photo.Visibility = Visibility.Visible;
                    Identity.SetStatus(clientService, chat, BotVerified);
                }
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        private ChatFolderElement _element;

        public void UpdateChatFolder(IClientService clientService, ChatFolderElement element)
        {
            if (!_templateApplied)
            {
                _clientService = clientService;
                _element = element;
                return;
            }

            UpdateStyleNoSubtitle();

            if (element is FolderChat folderChat && clientService.TryGetChat(folderChat.ChatId, out Chat chat))
            {
                TitleLabel.Text = clientService.GetTitle(chat);
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Identity.SetStatus(clientService, chat, BotVerified);
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

                    case ChatListFolderFlags.ExistingChats:
                        TitleLabel.Text = Strings.FilterExistingChats;
                        break;
                    case ChatListFolderFlags.NewChats:
                        TitleLabel.Text = Strings.FilterNewChats;
                        break;
                }

                Photo.Source = ProfilePictureSourceText.GetGlyph(MainPage.GetFolderIcon(flag.Flag), (int)flag.Flag);
                Identity.ClearStatus(BotVerified);
            }
        }

        public void UpdateLinkedChat(IClientService clientService, ContainerContentChangingEventArgs args, TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> callback)
        {
            var chat = args.Item as Chat;

            if (args.Phase == 0)
            {
                TitleLabel.Text = chat.Title;
            }
            else if (args.Phase == 1)
            {
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
            }
            else if (args.Phase == 2)
            {
                Photo.Source = ProfilePictureSource.Chat(clientService, chat);
                Identity.SetStatus(clientService, chat, BotVerified);
            }

            if (args.Phase < 2)
            {
                args.RegisterUpdateCallback(callback);
            }

            args.Handled = true;
        }

        private void UpdateStyleNoSubtitle()
        {
            TitlePanel.Margin = new Thickness(0, 0, 0, 2);
            TitlePanel.VerticalAlignment = VerticalAlignment.Center;
            SubtitleLabel.Visibility = Visibility.Collapsed;

            Grid.SetRowSpan(TitlePanel, 2);
        }



        private bool _skeletonCollapsed = true;

        public void ShowHideSkeleton(bool show)
        {
            if (_skeletonCollapsed && show)
            {
                _skeletonCollapsed = false;
                SizeChanged += OnSizeChanged;

                ShowSkeleton();
            }
            else if (_skeletonCollapsed is false && !show)
            {
                _skeletonCollapsed = true;
                SizeChanged -= OnSizeChanged;

                var visual = ElementCompositionPreview.GetElementChildVisual(this);
                var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, 1);
                animation.InsertKeyFrame(1, 0);

                visual.StartAnimation("Opacity", animation);
            }
        }

        private void ShowSkeleton()
        {
            var size = ActualSize;
            var itemHeight = 6 + 36 + 6;

            var rows = Math.Min(10, Math.Ceiling(size.Y / itemHeight));
            var shapes = new List<CanvasGeometry>();

            var borderTop = (float)BorderThickness.Top;
            var borderLeft = (float)BorderThickness.Left;

            var maxWidth = (int)Math.Clamp(size.X - 32 - 12 - 12 - 48 - 12, 80, 280);
            var random = new Random();

            shapes.Add(CanvasGeometry.CreateEllipse(null, borderLeft + 12 + 18, borderTop + 6 + 18, 18, 18));
            shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, borderLeft + 12 + 36 + 10, borderTop + 6, random.Next(80, maxWidth), 18, 4, 4));
            shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, borderLeft + 12 + 36 + 10, borderTop + 6 + 18 + 4, random.Next(80, maxWidth), 14, 4, 4));

            var compositor = BootStrapper.Current.Compositor;

            var geometries = shapes.ToArray();
            var path = compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding)));

            var transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);
            var foregroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
            var backgroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

            var lookup = ThemeService.GetLookup(ActualTheme);
            if (lookup.TryGet("MenuFlyoutItemBackgroundPointerOver", out Color color))
            {
                foregroundColor = color;
                backgroundColor = color;
            }

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(1, 0);
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, transparent));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, foregroundColor));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, transparent));

            var background = compositor.CreateRectangleGeometry();
            background.Size = size;
            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.FillBrush = compositor.CreateColorBrush(backgroundColor);

            var foreground = compositor.CreateRectangleGeometry();
            foreground.Size = size;
            var foregroundShape = compositor.CreateSpriteShape(foreground);
            foregroundShape.FillBrush = gradient;

            var clip = compositor.CreateGeometricClip(path);
            var visual = compositor.CreateShapeVisual();
            visual.Clip = clip;
            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.Size = size;

            var animation = compositor.CreateVector2KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector2(-size.X, 0));
            animation.InsertKeyFrame(1, new Vector2(size.X, 0));
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            foregroundShape.StartAnimation("Offset", animation);

            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_skeletonCollapsed)
            {
                ShowSkeleton();
            }
        }
    }
}
