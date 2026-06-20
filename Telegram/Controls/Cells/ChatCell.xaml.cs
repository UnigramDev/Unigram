//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Common;
using Telegram.Common.Chats;
using Telegram.Composition;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.Views;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Cells
{
    public enum MessageTicksState
    {
        None,
        Pending,
        Failed,
        Sent,
        Read
    }

    public sealed partial class ChatCell : ControlEx2, IMultipleElement
    {
        private bool _selected;

        private Chat _chat;
        private ChatList _chatList;
        private StoryList _storyList;

        private Message _message;
        private bool _showAsSender;

        private SavedMessagesTopic _savedMessagesTopic;

        private int _thumbnailId;

        private string _dateLabel;
        private string _stateLabel;

        private IClientService _clientService;

        private bool _compact;
        private bool _draft;

        // Used only to prevent garbage collection
        private CompositionAnimation _size1;
        private CompositionAnimation _size2;
        private CompositionAnimation _offset1;
        private CompositionAnimation _offset2;
        private CompositionAnimation _offset3;

        private MessageTicksState _ticksState;

        public ChatCell()
        {
            DefaultStyleKey = typeof(ChatCell);
        }

        public event EventHandler<Chat> StoryClick;

        protected override void OnLoaded()
        {
            _strokeBrush?.Register();
            _selectionStrokeBrush?.Register();
        }

        protected override void OnUnloaded()
        {
            _strokeBrush?.Unregister();
            _selectionStrokeBrush?.Unregister();
        }

        #region InitializeComponent

        private Grid PhotoPanel;
        private CustomEmojiIcon BotVerified;
        private TextBlock TitleLabel;
        private IdentityIcon Identity;
        private TextBlock MutedIcon;
        private TextBlock TimeLabel;
        private Grid PreviewPanel;
        private Border MinithumbnailPanel;
        private ChatActionIndicator ChatActionIndicator;
        private TextBlock TypingLabel;
        private TextBlock PinnedIcon;
        private Border UnreadMentionsBadge;
        private BadgeControl UnreadBadge;
        private Button BotOpen;
        private Rectangle DropVisual;
        private TextBlock UnreadMentionsLabel;
        private SolidColorBrush UnreadMentionsBrush;
        private Run FromLabel;
        private Run DraftLabel;
        private FormattedTextBlock BriefText;
        private Span BriefLabel;
        private ImageBrush Minithumbnail;
        private Rectangle SelectionOutline;
        private ActiveStoriesSegments Segments;
        private ProfilePicture Photo;
        private Border OnlineBadge;
        private Border OnlineHeart;
        private StackPanel Folders;

        private Grid AutoDeleteBadge;
        private TextBlock AutoDeleteLabel;

        private Border LiveBadge;

        private BadgeControl DirectMessagesGroup;

        private Border CompactBadgeRoot;
        private BadgeControl CompactBadge;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            PhotoPanel = GetTemplateChild(nameof(PhotoPanel)) as Grid;
            BotVerified = GetTemplateChild(nameof(BotVerified)) as CustomEmojiIcon;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            Identity = GetTemplateChild(nameof(Identity)) as IdentityIcon;
            MutedIcon = GetTemplateChild(nameof(MutedIcon)) as TextBlock;
            TimeLabel = GetTemplateChild(nameof(TimeLabel)) as TextBlock;
            PreviewPanel = GetTemplateChild(nameof(PreviewPanel)) as Grid;
            MinithumbnailPanel = GetTemplateChild(nameof(MinithumbnailPanel)) as Border;
            ChatActionIndicator = GetTemplateChild(nameof(ChatActionIndicator)) as ChatActionIndicator;
            TypingLabel = GetTemplateChild(nameof(TypingLabel)) as TextBlock;
            PinnedIcon = GetTemplateChild(nameof(PinnedIcon)) as TextBlock;
            UnreadMentionsBadge = GetTemplateChild(nameof(UnreadMentionsBadge)) as Border;
            UnreadBadge = GetTemplateChild(nameof(UnreadBadge)) as BadgeControl;
            BotOpen = GetTemplateChild(nameof(BotOpen)) as Button;
            DropVisual = GetTemplateChild(nameof(DropVisual)) as Rectangle;
            UnreadMentionsLabel = GetTemplateChild(nameof(UnreadMentionsLabel)) as TextBlock;
            FromLabel = GetTemplateChild(nameof(FromLabel)) as Run;
            DraftLabel = GetTemplateChild(nameof(DraftLabel)) as Run;
            BriefText = GetTemplateChild(nameof(BriefText)) as FormattedTextBlock;
            BriefLabel = GetTemplateChild(nameof(BriefLabel)) as Span;
            Minithumbnail = GetTemplateChild(nameof(Minithumbnail)) as ImageBrush;
            SelectionOutline = GetTemplateChild(nameof(SelectionOutline)) as Rectangle;
            Segments = GetTemplateChild(nameof(Segments)) as ActiveStoriesSegments;
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            Folders = GetTemplateChild(nameof(Folders)) as StackPanel;

            UnreadMentionsBrush = new SolidColorBrush();
            UnreadMentionsLabel.Foreground = UnreadMentionsBrush;

            Segments.Click += Segments_Click;
            BotOpen.Click += BotOpen_Click;

            _selectionPhoto = ElementComposition.GetElementVisual(Segments);
            _selectionOutline = ElementComposition.GetElementVisual(SelectionOutline);
            _selectionPhoto.CenterPoint = new Vector3(24);
            _selectionOutline.CenterPoint = new Vector3(24);
            _selectionOutline.Opacity = 0;

            _templateApplied = true;

            if (_chat != null)
            {
                UpdateChat(_clientService, _chat, _chatList);
            }
            else if (_chatList != null && _storyList != null)
            {
                UpdateChatList(_clientService, _chatList);
                UpdateStoryList(_clientService, _storyList);
            }
            else if (_message != null)
            {
                UpdateMessage(_clientService, _message, _showAsSender);
            }
            else if (_savedMessagesTopic != null)
            {
                UpdateSavedMessagesTopic(_clientService, _savedMessagesTopic);
            }
        }

        private void Segments_Click(object sender, RoutedEventArgs e)
        {
            StoryClick?.Invoke(sender, _chat);
        }

        private void BotOpen_Click(object sender, RoutedEventArgs e)
        {
            if (_chat == null || !_clientService.TryGetUser(_chat, out User user))
            {
                return;
            }

            var navigationService = WindowContext.GetNavigationService(XamlRoot);
            if (navigationService != null)
            {
                MessageHelper.NavigateToMainWebApp(_clientService, navigationService, user, string.Empty, new WebAppOpenModeFullSize());
            }
        }

        #endregion

        public void UpdateChat(IClientService clientService, Chat chat, ChatList chatList)
        {
            _clientService = clientService;

            Update(chat, chatList);
        }

        public void UpdateSavedMessagesTopic(IClientService clientService, SavedMessagesTopic savedMessagesTopic)
        {
            _clientService = clientService;
            _savedMessagesTopic = savedMessagesTopic;

            if (!_templateApplied)
            {
                return;
            }

            var message = savedMessagesTopic.LastMessage;

            if (savedMessagesTopic.Type is SavedMessagesTopicTypeSavedFromChat savedFromChat && clientService.TryGetChat(savedFromChat.ChatId, out Chat chat))
            {
                UpdateChatTitle(chat);
                UpdateChatPhoto(chat);
                UpdateChatEmojiStatus(chat);

                if (message != null)
                {
                    FromLabel.Text = UpdateFromLabel(clientService, chat, message);
                }
            }
            else
            {
                if (savedMessagesTopic.Type is SavedMessagesTopicTypeMyNotes)
                {
                    TitleLabel.Text = Strings.MyNotes;
                    Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.MyNotesFilled, 5);
                    Identity.ClearStatus();
                    BotVerified.Visibility = Visibility.Collapsed;
                }
                else if (savedMessagesTopic.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    TitleLabel.Text = Strings.AnonymousForward;
                    Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.AuthorHiddenFilled, 5);
                    Identity.ClearStatus();
                    BotVerified.Visibility = Visibility.Collapsed;
                }

                if (message != null)
                {
                    FromLabel.Text = UpdateFromLabel(clientService, null, message);
                }
            }

            MutedIcon.Visibility = Visibility.Collapsed;
            UnreadBadge.Visibility = Visibility.Collapsed;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;
            BotOpen.Visibility = Visibility.Collapsed;
            PinnedIcon.Visibility = savedMessagesTopic.IsPinned
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (message != null)
            {
                _dateLabel = Formatter.DateExtended(message.Date);
                _stateLabel = string.Empty;

                TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

                UpdateBriefLabel(null, UpdateBriefLabel(message.Content, message.IsOutgoing, null, false, out MinithumbnailId thumbnail));
                UpdateMinithumbnail(thumbnail);
            }
        }

        public void UpdateMessage(IClientService clientService, Message message, bool showAsSender)
        {
            _clientService = clientService;
            _message = message;
            _showAsSender = showAsSender;

            if (!_templateApplied)
            {
                return;
            }

            var chat = clientService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            if (showAsSender)
            {
                TitleLabel.Text = _clientService.GetTitle(message.SenderId);
                Photo.Source = ProfilePictureSource.MessageSender(_clientService, message.SenderId);
                MutedIcon.Visibility = Visibility.Collapsed;
            }
            else
            {
                UpdateChatTitle(chat);
                UpdateChatPhoto(chat);
                UpdateChatEmojiStatus(chat);
                UpdateChatNotificationSettings(chat);
            }

            PinnedIcon.Visibility = Visibility.Collapsed;
            UnreadBadge.Visibility = Visibility.Collapsed;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;
            BotOpen.Visibility = Visibility.Collapsed;

            if (showAsSender)
            {
                FromLabel.Text = string.Empty;
            }
            else
            {
                FromLabel.Text = UpdateFromLabel(clientService, chat, message);
            }

            _dateLabel = Formatter.DateExtended(message.Date);
            _stateLabel = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, null, showAsSender ? null : message, message.SendingState);

            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            UpdateBriefLabel(chat, UpdateBriefLabel(message.Content, message.IsOutgoing, null, false, out MinithumbnailId thumbnail));
            UpdateMinithumbnail(thumbnail);
        }

        public async void UpdateChatList(IClientService clientService, ChatList chatList)
        {
            _clientService = clientService;
            _chatList = chatList;

            var response = await clientService.GetChatListAsync(chatList, 0, 20);
            if (response is Telegram.Td.Api.Chats chats)
            {
                Visibility = chats.ChatIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                if (!_templateApplied)
                {
                    return;
                }

                TitleLabel.Text = Strings.ArchivedChats;
                Photo.Source = ProfilePictureSourceText.GetGlyph(Icons.ArchiveFilled, 5);

                UnreadMentionsBadge.Visibility = Visibility.Collapsed;
                PinnedIcon.Visibility = Visibility.Collapsed;

                //DraftLabel.Text = string.Empty;
                _dateLabel = string.Empty;
                _stateLabel = string.Empty;
                TimeLabel.Text = string.Empty;

                MutedIcon.Visibility = Visibility.Collapsed;

                MinithumbnailPanel.Visibility = Visibility.Collapsed;

                UpdateTicks(null);

                var unreadCount = clientService.GetUnreadCount(chatList);
                UnreadBadge.Visibility = unreadCount.UnreadChatCount.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
                UnreadBadge.Text = unreadCount.UnreadChatCount.UnreadCount.ToString();
                UnreadBadge.IsUnmuted = false;

                if (CompactBadge != null)
                {
                    CompactBadgeRoot.Visibility = UnreadBadge.Visibility;
                    CompactBadge.Text = UnreadBadge.Text;
                    CompactBadge.IsUnmuted = false;
                }

                BriefLabel.Inlines.Clear();

                foreach (var id in chats.ChatIds)
                {
                    var chat = clientService.GetChat(id);
                    if (chat == null)
                    {
                        continue;
                    }

                    if (BriefLabel.Inlines.Count > 0)
                    {
                        BriefLabel.Inlines.Add(new Run { Text = ", " });
                    }

                    var run = new Run { Text = _clientService.GetTitle(chat) };
                    if (chat.IsUnread())
                    {
                        run.Foreground = new SolidColorBrush(ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black);
                    }

                    BriefLabel.Inlines.Add(run);
                }
            }
        }

        public async void UpdateStoryList(IClientService clientService, StoryList storyList)
        {
            _clientService = clientService;
            _storyList = storyList;

            if (!_templateApplied)
            {
                return;
            }

            var response = await clientService.GetStoryListAsync(storyList, 0, 10);
            if (response is Telegram.Td.Api.Chats chats)
            {
                var count = 0;
                var unread = 0;

                foreach (var activeStories in clientService.GetActiveStorieses(chats.ChatIds))
                {
                    if (activeStories.Stories.Count > 0 && activeStories.Stories[^1].StoryId > activeStories.MaxReadStoryId)
                    {
                        unread++;
                    }

                    count++;
                }

                Segments.UpdateSegments(48, count, unread);
            }
            else
            {
                Segments.UpdateSegments(48, 0, 0);
            }
        }

        public string GetAutomationName()
        {
            if (_clientService == null)
            {
                return null;
            }

            if (_chat != null)
            {
                return UpdateAutomation(_clientService, _chat, _chat.LastMessage);
            }
            else if (_message != null)
            {
                var chat = _clientService.GetChat(_message.ChatId);
                if (chat != null)
                {
                    return UpdateAutomation(_clientService, chat, _message);
                }
            }

            return null;
        }

        private string UpdateAutomation(IClientService clientService, Chat chat, Message message)
        {
            var builder = new StringBuilder();
            if (chat.Type is ChatTypeSecret)
            {
                builder.Append(Strings.AccDescrSecretChat);
                builder.Append(". ");
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = clientService.GetUser(chat);
                if (user != null)
                {
                    if (user.Type is UserTypeBot)
                    {
                        builder.Append(Strings.Bot);
                        builder.Append(", ");
                    }

                    if (user.Id == clientService.Options.MyId)
                    {
                        builder.Append(Strings.SavedMessages);
                    }
                    else
                    {
                        builder.Append(user.FullName());
                    }

                    builder.Append(", ");

                    var identity = Identity?.CurrentType switch
                    {
                        IdentityIconType.Fake => Strings.FakeMessage,
                        IdentityIconType.Scam => Strings.ScamMessage,
                        IdentityIconType.Premium => Strings.AccDescrPremium,
                        IdentityIconType.Verified => Strings.AccDescrVerified,
                        _ => null
                    };

                    if (identity != null)
                    {
                        builder.Append(identity);
                        builder.Append(", ");
                    }

                    if (user.Type is UserTypeRegular && user.Status is UserStatusOnline && user.Id != clientService.Options.MyId)
                    {
                        builder.Append(Strings.Online);
                        builder.Append(", ");
                    }
                }
            }
            else
            {
                if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
                {
                    builder.Append(Strings.AccDescrChannel);
                }
                else
                {
                    builder.Append(Strings.AccDescrGroup);
                }

                builder.Append(", ");
                builder.Append(clientService.GetTitle(chat));
                builder.Append(", ");

                var identity = Identity?.CurrentType switch
                {
                    IdentityIconType.Fake => Strings.FakeMessage,
                    IdentityIconType.Scam => Strings.ScamMessage,
                    IdentityIconType.Premium => Strings.AccDescrPremium,
                    IdentityIconType.Verified => Strings.AccDescrVerified,
                    _ => null
                };

                if (identity != null)
                {
                    builder.Append(identity);
                    builder.Append(", ");
                }
            }

            if (chat.UnreadCount > 0)
            {
                builder.Append(Locale.Declension(Strings.R.NewMessages, chat.UnreadCount));
                builder.Append(", ");
            }

            if (chat.UnreadMentionCount > 0)
            {
                builder.Append(Locale.Declension(Strings.R.AccDescrMentionCount, chat.UnreadMentionCount));
                builder.Append(", ");
            }

            if (chat.UnreadReactionCount > 0)
            {
                builder.Append(Strings.AccDescrMentionReaction);
                builder.Append(", ");
            }

            if (_clientService.Notifications.IsMuted(chat))
            {
                builder.Append(Strings.AccDescrNotificationsMuted);
                builder.Append(", ");
            }

            if (message == null)
            {
                //AutomationProperties.SetName(this, builder.ToString());
                return builder.ToString();
            }

            //if (!message.IsOutgoing && message.SenderUserId != 0 && !message.IsService())
            if (ShowFrom(clientService, chat, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsOutgoing)
                {
                    if (!(chat.Type is ChatTypePrivate priv && priv.UserId == fromUser?.Id) && !message.IsChannelPost)
                    {
                        builder.Append(Strings.FromYou);
                        builder.Append(": ");
                    }
                }
                else if (fromUser != null)
                {
                    builder.Append(fromUser.FullName());
                    builder.Append(": ");
                }
                else if (fromChat != null && fromChat.Id != chat.Id)
                {
                    builder.Append(fromChat.Title);
                    builder.Append(": ");
                }
            }

            if (chat.Type is ChatTypeSecret == false)
            {
                builder.Append(Automation.GetSummary(clientService, message));
            }

            var date = Locale.FormatDateAudio(message.Date);
            if (message.IsOutgoing)
            {
                builder.Append(string.Format(Strings.AccDescrSentDate, date));
            }
            else
            {
                builder.Append(string.Format(Strings.AccDescrReceivedDate, date));
            }

            //AutomationProperties.SetName(this, builder.ToString());
            return builder.ToString();
        }

        #region Updates

        public void UpdateChatLastMessage(Chat chat, ChatPosition position = null, bool updateChatLists = true)
        {
            if (chat == null || _clientService == null || !_templateApplied)
            {
                return;
            }

            position ??= chat.GetPosition(_chatList);

            var from = UpdateFromLabel(chat, position, out bool draft);

            if (draft)
            {
                DraftLabel.Text = from;

                if (!_draft)
                {
                    FromLabel.Text = string.Empty;
                }
            }
            else
            {
                FromLabel.Text = from;

                if (_draft)
                {
                    DraftLabel.Text = string.Empty;
                }
            }

            _draft = draft;
            _dateLabel = UpdateTimeLabel(chat, position);
            _stateLabel = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            UpdateBriefLabel(chat, UpdateBriefLabel(chat, position, out MinithumbnailId thumbnail));
            UpdateMinithumbnail(thumbnail);

            if (updateChatLists)
            {
                UpdateChatChatLists(chat);
            }
        }

        public void UpdateChatReadInbox(Chat chat, ChatPosition position = null, bool updateBotOpen = true)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            position ??= chat.GetPosition(_chatList);

            var unreadCount = chat.ViewAsTopics
                ? _clientService.UnreadTopicCount(chat.Id)
                : chat.UnreadCount;

            PinnedIcon.Visibility = unreadCount == 0
                && chat.UnreadMentionCount == 0
                && chat.UnreadPollVoteCount == 0
                && !chat.IsMarkedAsUnread
                && (position?.IsPinned ?? false) ? Visibility.Visible : Visibility.Collapsed;

            var unread = (unreadCount > 0 || chat.IsMarkedAsUnread) ? chat.UnreadMentionCount == 1 && unreadCount == 1 ? Visibility.Collapsed : Visibility.Visible : Visibility.Collapsed;
            if (unread == Visibility.Visible)
            {
                UnreadBadge.Visibility = Visibility.Visible;
                UnreadBadge.Text = unreadCount > 0 ? unreadCount.ToString() : string.Empty;
            }
            else
            {
                UnreadBadge.Visibility = Visibility.Collapsed;
            }

            if (CompactBadge != null)
            {
                CompactBadgeRoot.Visibility = UnreadBadge.Visibility;
                CompactBadge.Text = UnreadBadge.Text;
            }

            //UpdateAutomation(_clientService, chat, chat.LastMessage);

            if (updateBotOpen)
            {
                UpdateBotOpen(chat);
            }
        }

        public void UpdateChatReadOutbox(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            _stateLabel = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;
        }

        public void UpdateChatIsMarkedAsUnread(Chat chat)
        {

        }

        public void UpdateChatUnreadMentionCount(Chat chat, ChatPosition position = null, bool updateBotOpen = true)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            UpdateChatReadInbox(chat, position, false);

            var unread = chat.UnreadMentionCount > 0 || chat.UnreadReactionCount > 0 || chat.UnreadPollVoteCount > 0;
            if (unread)
            {
                UnreadMentionsBadge.Visibility = Visibility.Visible;
                UnreadMentionsLabel.Text = chat.UnreadMentionCount > 0 ? Icons.MentionFilled : chat.UnreadReactionCount > 0 ? Icons.HeartFilled : Icons.PollFilled;
                UnreadMentionsBrush.Color = chat.UnreadMentionCount > 0 ? Color.FromArgb(0xFF, 0x00, 0x7a, 0xff) : chat.UnreadReactionCount > 0 ? Color.FromArgb(0xFF, 0xff, 0x2d, 0x55) : Color.FromArgb(0xFF, 0xaf, 0x52, 0xde);
                PinnedIcon.Opacity = 0;
            }
            else
            {
                UnreadMentionsBadge.Visibility = Visibility.Collapsed;
                PinnedIcon.Opacity = 1;
            }

            if (updateBotOpen)
            {
                UpdateBotOpen(chat);
            }
        }

        private void UpdateBotOpen(Chat chat)
        {
            if (UnreadMentionsBadge.Visibility == Visibility.Collapsed
                && UnreadBadge.Visibility == Visibility.Collapsed
                && _clientService.TryGetUser(chat, out User user)
                && user.Type is UserTypeBot { HasMainWebApp: true })
            {
                BotOpen.Visibility = Visibility.Visible;
            }
            else
            {
                BotOpen.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateChatNotificationSettings(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            var muted = _clientService.Notifications.IsMuted(chat);
            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.IsUnmuted = !muted;

            CompactBadge?.IsUnmuted = !muted;
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            if (chat.Type is ChatTypeSecret)
            {
                TitleLabel.Text = Icons.LockClosedFilled14 + "\u00A0" + _clientService.GetTitle(chat);
            }
            else
            {
                TitleLabel.Text = _clientService.GetTitle(chat);
            }
        }

        public void UpdateChatPhoto(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            Segments.SetChat(_clientService, chat, 48);
            Photo.Source = ProfilePictureSource.Chat(_clientService, chat);

            SelectionOutline.RadiusX = Photo.ComputedShape == ProfilePictureShape.Superellipse ? 12 : 24;
            SelectionOutline.RadiusY = Photo.ComputedShape == ProfilePictureShape.Superellipse ? 12 : 24;

            if (Segments.HasLiveBadge)
            {
                LoadTemplateChild(ref LiveBadge);
            }
            else if (LiveBadge != null)
            {
                UnloadTemplateChild(ref LiveBadge);
            }
        }

        public void UpdateChatEmojiStatus(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            long? verification;
            if (_clientService.TryGetUser(chat, out User user) && user.Id != _clientService.Options.MyId)
            {
                verification = user.VerificationStatus?.BotVerificationIconCustomEmojiId;
                Identity.SetStatus(_clientService, user, true);

                UnloadTemplateChild(ref DirectMessagesGroup);
            }
            else if (_clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                verification = supergroup.VerificationStatus?.BotVerificationIconCustomEmojiId;
                Identity.SetStatus(_clientService, chat);

                if (supergroup.IsDirectMessagesGroup)
                {
                    LoadTemplateChild(ref DirectMessagesGroup);
                }
                else
                {
                    UnloadTemplateChild(ref DirectMessagesGroup);
                }
            }
            else
            {
                verification = null;
                Identity.ClearStatus();

                UnloadTemplateChild(ref DirectMessagesGroup);
            }

            if (verification is not null and not 0)
            {
                BotVerified.Source = new CustomEmojiFileSource(_clientService, verification.Value);
                BotVerified.Visibility = Visibility.Visible;
            }
            else
            {
                BotVerified.Source = null;
                BotVerified.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateChatActiveStories(ChatActiveStories activeStories)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            if (activeStories.ChatId != _clientService.Options.MyId)
            {
                Segments.UpdateActiveStories(activeStories, 48, true);

                if (Segments.HasLiveBadge)
                {
                    LoadTemplateChild(ref LiveBadge);
                }
                else if (LiveBadge != null)
                {
                    UnloadTemplateChild(ref LiveBadge);
                }
            }
        }

        public void UpdateChatActions(Chat chat, IDictionary<MessageSender, ChatAction> actions)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            if (actions != null && actions.Count > 0)
            {
                TypingLabel.Text = InputChatActionManager.GetTypingString(chat.Type, actions, _clientService, out ChatAction commonAction);
                ChatActionIndicator.UpdateAction(commonAction);
                ChatActionIndicator.Visibility = Visibility.Visible;
                TypingLabel.Visibility = Visibility.Visible;
                BriefText.Visibility = Visibility.Collapsed;
            }
            else
            {
                ChatActionIndicator.Visibility = Visibility.Collapsed;
                ChatActionIndicator.UpdateAction(null);
                TypingLabel.Visibility = Visibility.Collapsed;
                BriefText.Visibility = Visibility.Visible;
            }
        }

        public void UpdateChatVideoChat(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            ShowHideOnlineStatus(chat.VideoChat?.HasParticipants ?? false, true, chat.MessageAutoDeleteTime, true);
        }

        public void UpdateUserStatus(Chat chat, UserStatus status)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            ShowHideOnlineStatus(status is UserStatusOnline, false, chat.MessageAutoDeleteTime, true);
        }

        public void UpdateChatMessageAutoDeleteTime(Chat chat, bool animate)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            if (_clientService.TryGetUser(chat, out User user) && user.Type is UserTypeRegular && user.Id != _clientService.Options.MyId && !user.IsSupport)
            {
                ShowHideOnlineStatus(user.Status is UserStatusOnline, false, chat.MessageAutoDeleteTime, animate);
            }
            else if (chat.VideoChat.GroupCallId != 0)
            {
                ShowHideOnlineStatus(chat.VideoChat.HasParticipants, true, chat.MessageAutoDeleteTime, animate);
            }
            else
            {
                ShowHideOnlineStatus(false, false, chat.MessageAutoDeleteTime, animate);
            }
        }

        private bool _onlineStatusCollapsed = true;
        private bool _onlineStatusActiveCall;

        private void ShowHideOnlineStatus(bool show, bool activeCall, int autoDeleteTime, bool animate)
        {
            if (show)
            {
                autoDeleteTime = 0;
            }

            if (_onlineStatusCollapsed != show && _onlineStatusActiveCall == activeCall && _autoDeleteTime == autoDeleteTime)
            {
                return;
            }

            _onlineStatusCollapsed = !show;
            ShowHideAutoDelete(autoDeleteTime, animate);

            if (OnlineBadge == null)
            {
                if (show)
                {
                    OnlineBadge = GetTemplateChild(nameof(OnlineBadge)) as Border;
                    OnlineHeart = GetTemplateChild(nameof(OnlineHeart)) as Border;

                    //_onlineBadge.Opacity = 0;
                    //_onlineBadge.Scale = new Vector3(0);
                }
                else
                {
                    return;
                }
            }

            if (_onlineStatusActiveCall != activeCall)
            {
                if (activeCall)
                {
                    OnlineBadge.Margin = new Thickness(0, 0, -1, -1);
                    OnlineBadge.Width = OnlineBadge.Height = 20;
                    OnlineBadge.CornerRadius = new CornerRadius(10);
                    OnlineHeart.Width = OnlineHeart.Height = 16;
                    OnlineHeart.CornerRadius = new CornerRadius(8);
                }
                else
                {
                    OnlineBadge.Margin = new Thickness(0, 0, 3, 0);
                    OnlineBadge.Width = OnlineBadge.Height = 12;
                    OnlineBadge.CornerRadius = new CornerRadius(6);
                    OnlineHeart.Width = OnlineHeart.Height = 8;
                    OnlineHeart.CornerRadius = new CornerRadius(4);
                }

                _onlineStatusActiveCall = activeCall;
            }

            if (!animate)
            {
                OnlineBadge.Visibility = show
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            OnlineBadge.Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(OnlineBadge);
            visual.CenterPoint = new Vector3(activeCall ? 10 : 6);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_onlineStatusCollapsed)
                {
                    OnlineBadge.Visibility = Visibility.Collapsed;
                }
            };

            var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(1, new Vector3(show ? 1 : 0));

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Opacity", opacity);

            batch.End();

            if (show && activeCall)
            {
                if (_size1 != null)
                {
                    return;
                }

                var compositor = BootStrapper.Current.Compositor;

                var line1 = compositor.CreateRoundedRectangleGeometry();
                line1.CornerRadius = Vector2.One;
                line1.Size = new Vector2(2, 2);
                line1.Offset = new Vector2(3, 7);

                var shape1 = compositor.CreateSpriteShape();
                shape1.Geometry = line1;
                shape1.FillBrush = compositor.CreateColorBrush(Colors.White);

                var line2 = compositor.CreateRoundedRectangleGeometry();
                line2.CornerRadius = Vector2.One;
                line2.Size = new Vector2(2, 2);
                line2.Offset = new Vector2(7, 7);

                var shape2 = compositor.CreateSpriteShape();
                shape2.Geometry = line2;
                shape2.FillBrush = compositor.CreateColorBrush(Colors.White);

                var line3 = compositor.CreateRoundedRectangleGeometry();
                line3.CornerRadius = Vector2.One;
                line3.Size = new Vector2(2, 2);
                line3.Offset = new Vector2(11, 7);

                var shape3 = compositor.CreateSpriteShape();
                shape3.Geometry = line3;
                shape3.FillBrush = compositor.CreateColorBrush(Colors.White);

                var shape = compositor.CreateShapeVisual();
                shape.Shapes.Add(shape3);
                shape.Shapes.Add(shape2);
                shape.Shapes.Add(shape1);
                shape.Size = new Vector2(16, 16);
                shape.CenterPoint = new Vector3(8);

                var size1 = compositor.CreateVector2KeyFrameAnimation();
                var size2 = compositor.CreateVector2KeyFrameAnimation();
                var offset1 = compositor.CreateVector2KeyFrameAnimation();
                var offset2 = compositor.CreateVector2KeyFrameAnimation();
                var offset3 = compositor.CreateVector2KeyFrameAnimation();

                // 1
                size1.InsertKeyFrame(0.0f, new Vector2(2, 4));
                offset1.InsertKeyFrame(0.0f, new Vector2(3, 6));

                size2.InsertKeyFrame(0.0f, new Vector2(2, 10));
                offset2.InsertKeyFrame(0.0f, new Vector2(7, 3));

                offset3.InsertKeyFrame(0.0f, new Vector2(11, 6));

                // 2
                size1.InsertKeyFrame(0.25f, new Vector2(2, 10));
                offset1.InsertKeyFrame(0.25f, new Vector2(3, 3));

                size2.InsertKeyFrame(0.25f, new Vector2(2, 4));
                offset2.InsertKeyFrame(0.25f, new Vector2(7, 6));

                offset3.InsertKeyFrame(0.25f, new Vector2(11, 3));

                // 3
                size1.InsertKeyFrame(0.50f, new Vector2(2, 4));
                offset1.InsertKeyFrame(0.50f, new Vector2(3, 6));

                size2.InsertKeyFrame(0.50f, new Vector2(2, 8));
                offset2.InsertKeyFrame(0.50f, new Vector2(7, 4));

                offset3.InsertKeyFrame(0.50f, new Vector2(11, 6));

                // 4
                size1.InsertKeyFrame(0.75f, new Vector2(2, 8));
                offset1.InsertKeyFrame(0.75f, new Vector2(3, 4));

                size2.InsertKeyFrame(0.75f, new Vector2(2, 4));
                offset2.InsertKeyFrame(0.75f, new Vector2(7, 6));

                offset3.InsertKeyFrame(0.75f, new Vector2(11, 4));

                // 1
                size1.InsertKeyFrame(1.0f, new Vector2(2, 4));
                offset1.InsertKeyFrame(1.0f, new Vector2(3, 6));

                size2.InsertKeyFrame(1.0f, new Vector2(2, 10));
                offset2.InsertKeyFrame(1.0f, new Vector2(7, 3));

                offset3.InsertKeyFrame(1.0f, new Vector2(11, 6));

                size1.IterationBehavior = AnimationIterationBehavior.Forever;
                size1.Duration *= 8;
                offset1.IterationBehavior = AnimationIterationBehavior.Forever;
                offset1.Duration *= 8;
                size2.IterationBehavior = AnimationIterationBehavior.Forever;
                size2.Duration *= 8;
                offset2.IterationBehavior = AnimationIterationBehavior.Forever;
                offset2.Duration *= 8;
                offset3.IterationBehavior = AnimationIterationBehavior.Forever;
                offset3.Duration *= 8;

                line1.StartAnimation("Size", size1);
                line2.StartAnimation("Size", size2);
                line3.StartAnimation("Size", size1);

                line1.StartAnimation("Offset", offset1);
                line2.StartAnimation("Offset", offset2);
                line3.StartAnimation("Offset", offset3);

                _size1 = size1;
                _size2 = size2;
                _offset1 = offset1;
                _offset2 = offset2;
                _offset3 = offset3;

                ElementCompositionPreview.SetElementChildVisual(OnlineHeart, shape);
            }
            else
            {
                _size1 = null;
                _size2 = null;
                _offset1 = null;
                _offset2 = null;
                _offset3 = null;

                ElementCompositionPreview.SetElementChildVisual(OnlineHeart, null);
            }
        }

        private int _autoDeleteTime = 0;

        private void ShowHideAutoDelete(int autoDeleteTime, bool animate)
        {
            if (_autoDeleteTime == autoDeleteTime)
            {
                return;
            }

            var prevShow = _autoDeleteTime != 0;
            var nextShow = autoDeleteTime != 0;

            _autoDeleteTime = autoDeleteTime;

            if (AutoDeleteBadge == null)
            {
                if (autoDeleteTime != 0)
                {
                    AutoDeleteBadge = GetTemplateChild(nameof(AutoDeleteBadge)) as Grid;
                    AutoDeleteLabel = GetTemplateChild(nameof(AutoDeleteLabel)) as TextBlock;
                }
                else
                {
                    return;
                }
            }

            if (nextShow)
            {
                AutoDeleteLabel.Text = Locale.FormatAutoDelete(autoDeleteTime);

                if (prevShow)
                {
                    return;
                }
            }

            if (!animate)
            {
                AutoDeleteBadge.Visibility = autoDeleteTime != 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                return;
            }

            AutoDeleteBadge.Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(AutoDeleteBadge);
            visual.CenterPoint = new Vector3(11);

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_autoDeleteTime == 0)
                {
                    AutoDeleteBadge.Visibility = Visibility.Collapsed;
                }
            };

            var scale = visual.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(1, new Vector3(nextShow ? 1 : 0));

            var opacity = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(1, nextShow ? 1 : 0);

            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Opacity", opacity);

            batch.End();
        }

        private void Update(Chat chat, ChatList chatList)
        {
            _chat = chat;
            _chatList = chatList;

            if (!_templateApplied)
            {
                return;
            }

            var position = chat.GetPosition(chatList);

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatEmojiStatus(chat);

            UpdateChatLastMessage(chat, position);
            //UpdateChatReadInbox(chat);
            UpdateChatUnreadMentionCount(chat, position, false);
            UpdateChatNotificationSettings(chat);
            UpdateChatActions(chat, _clientService.GetChatActions(chat.Id));
            UpdateChatMessageAutoDeleteTime(chat, false);

            UpdateBotOpen(chat);
        }

        public void UpdateChatChatLists(Chat chat)
        {
            if (!_templateApplied || _clientService == null)
            {
                return;
            }

            if (!_clientService.IsPremium || !_clientService.AreTagsEnabled)
            {
                Folders.Children.Clear();
                return;
            }

            var folders = _clientService.GetChatFolders(chat);

            for (int i = 0; i < Math.Max(folders.Count, Folders.Children.Count); i++)
            {
                if (i < folders.Count)
                {
                    var folder = folders[i];
                    var foreground = _clientService.GetAccentBrush(folder.ColorId);

                    Border badge;
                    RichTextBlock block;
                    Paragraph paragraph;
                    if (i < Folders.Children.Count)
                    {
                        badge = Folders.Children[i] as Border;
                        block = badge.Child as RichTextBlock;
                        paragraph = block.Blocks[0] as Paragraph;
                    }
                    else
                    {
                        badge = new Border
                        {
                            Height = 16,
                            MinWidth = 16,
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Bottom,
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(0, 0, 2, 0)
                        };

                        block = new RichTextBlock
                        {
                            TextLineBounds = TextLineBounds.Tight,
                            TextAlignment = TextAlignment.Center,
                            OpticalMarginAlignment = OpticalMarginAlignment.TrimSideBearings,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 1,
                            FontSize = 11,
                            Padding = new Thickness(4, 0, 4, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            IsTextSelectionEnabled = false
                        };

                        paragraph = new Paragraph();

                        block.Blocks.Add(paragraph);
                        badge.Child = block;

                        Folders.Children.Add(badge);
                    }

                    CustomEmojiIcon.Add(block, paragraph.Inlines, _clientService, folder.Name, 14);

                    block.Foreground = foreground;
                    badge.Background = foreground.WithOpacity(0.2);
                }
                else
                {
                    Folders.Children.RemoveAt(i);
                }
            }
        }

        #endregion

        public void UpdateViewState(Chat chat, bool compact, bool animate)
        {
            UpdateChatChatLists(chat);
            VisualStateManager.GoToState(this, chat.Type is ChatTypeSecret ? "Secret" : "Normal", false);

            if (_compact == compact || !_templateApplied)
            {
                return;
            }

            _compact = compact;

            if (compact)
            {
                LoadTemplateChild(ref CompactBadgeRoot);
                LoadTemplateChild(ref CompactBadge);

                CompactBadgeRoot.Visibility = UnreadBadge.Visibility;
                CompactBadge.Text = UnreadBadge.Text;
                CompactBadge.IsUnmuted = UnreadBadge.IsUnmuted;
            }

            //ElementCompositionPreview.SetIsTranslationEnabled(LayoutRoot, true);

            //var visual = ElementComposition.GetElementVisual(LayoutRoot);
            //visual.Clip ??= visual.Compositor.CreateInsetClip();

            //var x = LayoutRoot.ActualSize.X + 24;

            //if (animate)
            //{
            //    var offset0 = visual.Compositor.CreateVector3KeyFrameAnimation();
            //    offset0.InsertKeyFrame(0, new Vector3(compact ? 0 : -x, 0, 0));
            //    offset0.InsertKeyFrame(1, new Vector3(compact ? -x : 0, 0, 0));
            //    //offset0.Duration = Constants.FastAnimation;
            //    visual.StartAnimation("Translation", offset0);

            //    var clip0 = visual.Compositor.CreateScalarKeyFrameAnimation();
            //    clip0.InsertKeyFrame(0, compact ? -24 : x - 24);
            //    clip0.InsertKeyFrame(1, compact ? x - 24 : -24);
            //    //clip0.Duration = Constants.FastAnimation;
            //    visual.Clip.StartAnimation("LeftInset", clip0);
            //}
            //else
            //{
            //    visual.Properties.InsertVector3("Translation", new Vector3(compact ? -x : 0, 0, 0));

            //    if (visual.Clip is InsetClip inset)
            //    {
            //        inset.LeftInset = compact ? x - 24 : -24;
            //    }
            //}

            if (CompactBadgeRoot != null)
            {
                var badge = ElementComposition.GetElementVisual(CompactBadgeRoot);
                badge.CenterPoint = new Vector3(UnreadBadge.ActualSize.X / 2 + 2, UnreadBadge.ActualSize.Y / 2 + 2, 0);

                if (animate)
                {
                    var scale0 = badge.Compositor.CreateVector3KeyFrameAnimation();
                    scale0.InsertKeyFrame(0, compact ? Vector3.Zero : Vector3.One);
                    scale0.InsertKeyFrame(1, compact ? Vector3.One : Vector3.Zero);
                    scale0.Duration = Constants.FastAnimation;
                    badge.StartAnimation("Scale", scale0);
                }
                else
                {
                    badge.Scale = compact ? Vector3.One : Vector3.Zero;
                }
            }
        }

        private void UpdateMinithumbnail(MinithumbnailId thumbnail)
        {
            if (thumbnail != null)
            {
                if (_thumbnailId == thumbnail.Id)
                {
                    return;
                }

                _thumbnailId = thumbnail.Id;

                double ratioX = (double)16 / thumbnail.Width;
                double ratioY = (double)16 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                var bitmap = new BitmapImage
                {
                    DecodePixelWidth = width,
                    DecodePixelHeight = height,
                    DecodePixelType = DecodePixelType.Logical
                };

                Minithumbnail.ImageSource = bitmap;
                MinithumbnailPanel.Visibility = Visibility.Visible;

                MinithumbnailPanel.CornerRadius = new CornerRadius(thumbnail.IsVideoNote ? 9 : 2);

                using (var stream = new InMemoryRandomAccessStream())
                {
                    try
                    {
                        PlaceholderImageHelper.WriteBytes(thumbnail.Data, stream);
                        _ = bitmap.SetSourceAsync(stream);
                    }
                    catch
                    {
                        // Throws when the data is not a valid encoded image,
                        // not so frequent, but if it happens during ContainerContentChanging it crashes the app.
                    }
                }
            }
            else
            {
                _thumbnailId = 0;

                MinithumbnailPanel.Visibility = Visibility.Collapsed;
                Minithumbnail.ImageSource = null;
            }
        }

        private static readonly Regex _verificationCodes = new("\\b\\d{5,8}\\b", RegexOptions.Compiled);
        private static readonly Regex _gatewayCodes = new("\\b\\d{4,8}\\b", RegexOptions.Compiled);

        private void UpdateBriefLabel(Chat chat, FormattedText message)
        {
            if (message != null && chat?.Id == _clientService.Options.TelegramServiceNotificationsChatId || chat?.Id == _clientService.Options.VerificationCodesBotChatId)
            {
                var pattern = chat?.Id == _clientService.Options.TelegramServiceNotificationsChatId
                    ? _verificationCodes
                    : _gatewayCodes;

                var match = pattern.Match(message.Text);
                if (match.Success)
                {
                    message = new FormattedText(message.Text, message.Entities.ToList());
                    message.Entities.Add(new TextEntity(match.Index, match.Length, new TextEntityTypeSpoiler()));
                }
            }

            BriefText.SetText(_clientService, message);
            BriefText.SetQuery(string.Empty);
        }

        private FormattedText UpdateBriefLabel(Chat chat, ChatPosition position, out MinithumbnailId thumbnail)
        {
            thumbnail = null;

            if (position?.Source is ChatSourcePublicServiceAnnouncement psa && !string.IsNullOrEmpty(psa.Text))
            {
                return psa.Text.Replace('\n', ' ').AsFormattedText();
            }

            var topMessage = chat.LastMessage;
            if (topMessage != null)
            {
                FormattedText text;
                if (_clientService.TryGetMediaAlbum(chat.Id, topMessage.MediaAlbumId, out MessageAlbumLastMessage album))
                {
                    text = UpdateBriefLabel(album, topMessage.IsOutgoing, chat.DraftMessage, false, out thumbnail);
                }
                else
                {
                    text = UpdateBriefLabel(topMessage.Content, topMessage.IsOutgoing, chat.DraftMessage, false, out thumbnail);
                }

                // TODO: this is better than nothing, although it's not the best 
                if (topMessage.ForwardInfo != null && !topMessage.IsSaved(_clientService.Options.MyId))
                {
                    return TdExtensions.Concat(ClientEx.CustomEmoji(Icons.ShareFilled + Icons.Space), text);
                }

                return text;
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = _clientService.GetSecretChat(secretType.SecretChatId);
                if (secret != null)
                {
                    if (secret.State is SecretChatStateReady)
                    {
                        return (secret.IsOutbound ? string.Format(Strings.EncryptedChatStartedOutgoing, _clientService.GetTitle(chat)) : Strings.EncryptedChatStartedIncoming).AsFormattedText();
                    }
                    else if (secret.State is SecretChatStatePending)
                    {
                        return string.Format(Strings.AwaitingEncryption, _clientService.GetTitle(chat)).AsFormattedText();
                    }
                    else if (secret.State is SecretChatStateClosed)
                    {
                        return Strings.EncryptionRejected.AsFormattedText();
                    }
                }
            }

            return string.Empty.AsFormattedText();
        }

        public static FormattedText UpdateBriefLabel(MessageContent content, bool outgoing, DraftMessage draft, bool forceEmoji, out MinithumbnailId thumbnail)
        {
            thumbnail = null;

            if (draft?.Content is DraftMessageContentText draftText)
            {
                return draftText.Text;
            }
            else if (draft?.Content is DraftMessageContentRichMessage draftRichMessage)
            {

            }
            else if (draft?.Content is DraftMessageContentVoiceNote draftVoiceNote)
            {

            }
            else if (draft?.Content is DraftMessageContentVideoNote draftVideoNote)
            {

            }

            static FormattedText Text(string text)
            {
                return text.AsFormattedText();
            }

            static FormattedText Text1(string text, FormattedText formatted, string fallback)
            {
                if (formatted?.Text.Length > 0)
                {
                    var entities = new TextEntity[formatted.Entities.Count];

                    for (int i = 0; i < formatted.Entities.Count; i++)
                    {
                        TextEntity entity = formatted.Entities[i];
                        entities[i] = new TextEntity(entity.Offset + text.Length, entity.Length, entity.Type);
                    }

                    return new FormattedText(text + formatted.Text, entities);
                }

                return Text(text + fallback);
            }

            switch (content)
            {
                case MessageGame gameMedia:
                    return Text("\U0001F3AE " + gameMedia.Game.Title);
                case MessageVideoNote videoNote:
                    if (videoNote.VideoNote.Minithumbnail == null || videoNote.IsSecret || forceEmoji /*|| message.SelfDestructType is not null*/)
                    {
                        return Text("\U0001F4F9 " + Strings.AttachRound);
                    }

                    thumbnail = new MinithumbnailId(videoNote.VideoNote.Video.Id, videoNote.VideoNote.Minithumbnail, true);
                    return Text(Strings.AttachRound);
                case MessageSticker sticker:
                    if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                    {
                        return Text(Strings.AttachSticker);
                    }

                    return Text($"{sticker.Sticker.Emoji} {Strings.AttachSticker}");
                case MessageVoiceNote voiceNote:
                    return Text1("\U0001F3A4 ", voiceNote.Caption, Strings.AttachAudio);
                case MessageVideo video:
                    if ((video.Cover?.Minithumbnail == null && video.Video.Minithumbnail == null) || video.IsSecret || forceEmoji)
                    {
                        return Text1("\U0001F4F9 ", video.Caption, Strings.AttachVideo);
                    }

                    if (video.Cover != null)
                    {
                        thumbnail = new MinithumbnailId(video.Video.VideoValue.Id, video.Cover.Minithumbnail, false);
                    }
                    else
                    {
                        thumbnail = new MinithumbnailId(video.Video.VideoValue.Id, video.Video.Minithumbnail, false);
                    }

                    return Text1(string.Empty, video.Caption, Strings.AttachVideo);
                case MessageAnimation animation:
                    if (animation.Animation.Minithumbnail == null || animation.IsSecret || forceEmoji)
                    {
                        return Text1("\U0001F47E ", animation.Caption, Strings.AttachGif);
                    }

                    thumbnail = new MinithumbnailId(animation.Animation.AnimationValue.Id, animation.Animation.Minithumbnail, false);
                    return Text1(string.Empty, animation.Caption, Strings.AttachGif);
                case MessageAudio audio:
                    return Text1("\U0001F3B5 ", audio.Caption, audio.Audio.GetTitle());
                case MessageDocument document:
                    if (string.IsNullOrEmpty(document.Document.FileName))
                    {
                        return Text1("\U0001F4CE ", document.Caption, Strings.AttachDocument);
                    }

                    return Text1("\U0001F4CE ", document.Caption, document.Document.FileName);
                case MessageInvoice invoice:
                    return Text1("\U0001F4CB ", invoice.PaidMediaCaption, invoice.ProductInfo.Title);
                case MessageContact:
                    return Text("\U0001F464 " + Strings.AttachContact);
                case MessageLocation:
                    return Text("\U0001F4CD " + Strings.AttachLocation);
                case MessageLiveLocation:
                    return Text("\U0001F4CD " + Strings.AttachLiveLocation);
                case MessageVenue:
                    return Text("\U0001F4CD " + Strings.AttachLocation);
                case MessagePhoto photo:
                    if (photo.Photo.Minithumbnail == null || photo.IsSecret || forceEmoji)
                    {
                        return Text1("\U0001F5BC ", photo.Caption, photo.Video != null ? Strings.AttachLivePhoto : Strings.AttachPhoto);
                    }

                    thumbnail = new MinithumbnailId(photo.Photo.Sizes[^1].Photo.Id, photo.Photo.Minithumbnail, false);
                    return Text1(string.Empty, photo.Caption, photo.Video != null ? Strings.AttachLivePhoto : Strings.AttachPhoto);
                case MessagePoll poll:
                    return Text1("\U0001F4CA ", poll.Poll.Question, Strings.Poll);
                case MessageChecklist checklist:
                    return Text1("\u2611 ", checklist.List.Title, Strings.Todo);
                case MessageCall call:
                    return Text("\u260E " + call.ToOutcomeText(outgoing));
                case MessageGroupCall groupCall:
                    return Text("\u260E " + groupCall.ToOutcomeText(outgoing));
                case MessageStory story when !story.ViaMention:
                    return Text(Strings.Story);
                case MessageUnsupported:
                    return Text(Strings.UnsupportedAttachment);
                case MessageAnimatedEmoji animatedEmoji:
                    {
                        if (animatedEmoji.AnimatedEmoji?.Sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
                        {
                            return ClientEx.CustomEmoji(animatedEmoji.Emoji, customEmoji.CustomEmojiId);
                        }

                        return animatedEmoji.Emoji.AsFormattedText();
                    }

                case MessageGiveaway:
                    return Text(Strings.BoostingGiveaway);
                case MessageGiveawayWinners:
                    return Text(Strings.BoostingGiveawayResults);
                case MessagePaidMedia paidMedia:
                    {
                        if (paidMedia.Media.All(x => x.IsPhoto()))
                        {
                            return Text1(Icons.Premium + "\u2004", paidMedia.Caption, paidMedia.Media.Count > 1 ? Locale.Declension(Strings.R.Photos, paidMedia.Media.Count) : Strings.AttachPhoto);
                        }
                        else if (paidMedia.Media.All(x => x.IsVideo()))
                        {
                            return Text1(Icons.Premium + "\u2004", paidMedia.Caption, paidMedia.Media.Count > 1 ? Locale.Declension(Strings.R.Videos, paidMedia.Media.Count) : Strings.AttachVideo);
                        }

                        return Text1(Icons.Premium + "\u2004", paidMedia.Caption, Locale.Declension(Strings.R.Media, paidMedia.Media.Count));
                    }

                case MessageAlbumLastMessage album:
                    {
                        if (album.PhotosCount > 0 && album.VideosCount == 0)
                        {
                            if (album.LastMessage is MessagePhoto albumPhoto && albumPhoto.Photo.Minithumbnail != null)
                            {
                                thumbnail = new MinithumbnailId(albumPhoto.Photo.Sizes[^1].Photo.Id, albumPhoto.Photo.Minithumbnail, false);
                                return Text1(string.Empty, album.Caption, album.PhotosCount > 1 ? Locale.Declension(Strings.R.Photos, album.PhotosCount) : albumPhoto.Video != null ? Strings.AttachLivePhoto : Strings.AttachPhoto);
                            }

                            return Text1("\U0001F5BC ", album.Caption, album.PhotosCount > 1 ? Locale.Declension(Strings.R.Photos, album.PhotosCount) : Strings.AttachPhoto);
                        }
                        else if (album.VideosCount > 0 && album.PhotosCount == 0)
                        {
                            if (album.LastMessage is MessageVideo albumVideo && (albumVideo.Cover?.Minithumbnail != null || albumVideo.Video.Minithumbnail != null))
                            {
                                if (albumVideo.Cover != null)
                                {
                                    thumbnail = new MinithumbnailId(albumVideo.Video.VideoValue.Id, albumVideo.Cover.Minithumbnail, false);
                                }
                                else
                                {
                                    thumbnail = new MinithumbnailId(albumVideo.Video.VideoValue.Id, albumVideo.Video.Minithumbnail, false);
                                }

                                return Text1(string.Empty, album.Caption, album.VideosCount > 1 ? Locale.Declension(Strings.R.Videos, album.VideosCount) : Strings.AttachVideo);
                            }

                            return Text1("\U0001F4F9 ", album.Caption, album.VideosCount > 1 ? Locale.Declension(Strings.R.Videos, album.VideosCount) : Strings.AttachVideo);
                        }

                        return Text1("\U0001F5BC ", album.Caption, Locale.Declension(Strings.R.Media, album.PhotosCount + album.VideosCount));
                    }
                case MessageText text:
                    return text.Text;
                case MessageRichMessage richMessage:
                    return richMessage.Message.ToFormattedText();
                case MessageDice dice:
                    return dice.Emoji.AsFormattedText();
                case MessageStakeDice dice:
                    return Constants.StakeDice.AsFormattedText();
                default:
                    return string.Empty.AsFormattedText();
            }
        }

        private string UpdateFromLabel(Chat chat, ChatPosition position, out bool draft)
        {
            if (position?.Source is ChatSourcePublicServiceAnnouncement { Text.Length: > 0 })
            {
                draft = false;
                return string.Empty;
            }
            else if (chat.DraftMessage is not null)
            {
                draft = true;
                return string.Format("{0}: \u200B​​​", Strings.Draft);
            }

            var message = chat.LastMessage;
            if (message == null)
            {
                if (chat.LastReadOutboxMessageId != 0 || chat.LastReadInboxMessageId != 0)
                {
                    draft = false;
                    return Strings.HistoryCleared;
                }

                draft = false;
                return string.Empty;
            }

            draft = false;
            return UpdateFromLabel(_clientService, chat, message);
        }

        public static string UpdateFromLabel(IClientService clientService, Chat chat, Message message)
        {
            if (message.Content.IsService())
            {
                if (chat == null)
                {
                    clientService.TryGetChat(message.ChatId, out chat);
                }

                return MessageService.GetText(new MessageViewModel(clientService, null as IMessageDelegate, chat, null, null, message));
            }

            var format = "{0}: \u200B";

            if (ShowFrom(clientService, chat, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsSaved(clientService.Options.MyId) || message.ChatId == clientService.Options.VerificationCodesBotChatId)
                {
                    if (message.ForwardInfo?.Origin is MessageOriginUser originUser)
                    {
                        fromUser = clientService.GetUser(originUser.SenderUserId);
                    }
                    else if (message.ForwardInfo?.Origin is MessageOriginChat originChat)
                    {
                        fromUser = null;
                        fromChat = clientService.GetChat(originChat.SenderChatId);
                    }
                    else if (message.ForwardInfo?.Origin is MessageOriginChannel originChannel)
                    {
                        fromUser = null;
                        fromChat = clientService.GetChat(originChannel.ChatId);
                    }
                    else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser originHiddenUser)
                    {
                        return string.Format(format, originHiddenUser.SenderName);
                    }
                    else if (message.ImportInfo != null)
                    {
                        return string.Format(format, message.ImportInfo.SenderName);
                    }
                }

                if (fromUser != null)
                {
                    if (fromUser.Id == clientService.Options.MyId)
                    {
                        if (fromUser.Id != chat?.Id)
                        {
                            return string.Format(format, Strings.FromYou);
                        }
                    }
                    else if (!string.IsNullOrEmpty(fromUser.FirstName))
                    {
                        return string.Format(format, fromUser.FirstName.Trim());
                    }
                    else if (!string.IsNullOrEmpty(fromUser.LastName))
                    {
                        return string.Format(format, fromUser.LastName.Trim());
                    }
                    else if (fromUser.Type is UserTypeDeleted)
                    {
                        return string.Format(format, Strings.HiddenName);
                    }
                    else
                    {
                        return string.Format(format, fromUser.Id);
                    }
                }
                else if (fromChat != null && fromChat.Id != chat?.Id)
                {
                    return string.Format(format, fromChat.Title);
                }
            }

            return string.Empty;
        }

        public static bool ShowFrom(IClientService clientService, Chat chat, Message message, out User senderUser, out Chat senderChat)
        {
            senderUser = null;
            senderChat = null;

            if (message.Content.IsService())
            {
                return false;
            }

            if (message.TopicId is MessageTopicSavedMessages savedMessages && clientService.TryGetSavedMessagesTopic(savedMessages.SavedMessagesTopicId, out SavedMessagesTopic topic))
            {
                if (topic.Type is SavedMessagesTopicTypeMyNotes or SavedMessagesTopicTypeAuthorHidden)
                {
                    return false;
                }
            }

            if (chat?.Type is not ChatTypePrivate and not ChatTypeSecret
                || message.ChatId == clientService.Options.MyId
                || message.ChatId == clientService.Options.RepliesBotChatId
                || message.ChatId == clientService.Options.VerificationCodesBotChatId
                || message.GuestBotCallerId != null)
            {
                senderChat = null;
                return clientService.TryGetUser(message.SenderId, out senderUser)
                    || clientService.TryGetChat(message.SenderId, out senderChat);
            }

            return false;
        }

        private string UpdateStateIcon(long maxId, Chat chat, DraftMessage draft, Message message, MessageSendingState state)
        {
            if (draft != null || message == null)
            {
                UpdateTicks(null);

                _ticksState = MessageTicksState.None;
                return string.Empty;
            }

            if (message.IsOutgoing /*&& IsOut(ViewModel)*/)
            {
                if (chat.Type is ChatTypePrivate privata && privata.UserId == _clientService.Options.MyId)
                {
                    if (message.SendingState is MessageSendingStateFailed)
                    {
                        // TODO: 
                        return "failed"; // Failed
                    }
                    else if (message.SendingState is MessageSendingStatePending)
                    {
                        return "\uEA06"; // Pending
                    }

                    UpdateTicks(null);

                    _ticksState = MessageTicksState.None;
                    return string.Empty;
                }

                if (message.SendingState is MessageSendingStateFailed)
                {
                    UpdateTicks(null);

                    _ticksState = MessageTicksState.Failed;

                    // TODO: 
                    return "failed"; // Failed
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    UpdateTicks(null);

                    _ticksState = MessageTicksState.Pending;
                    return "\uEA06"; // Pending
                }
                else if (message.Id <= maxId)
                {
                    UpdateTicks(true, _ticksState == MessageTicksState.Sent);

                    _ticksState = MessageTicksState.Read;
                    return "\uEA07"; // Read
                }

                UpdateTicks(false, _ticksState == MessageTicksState.Pending);

                _ticksState = MessageTicksState.Sent;
                return "\uEA07"; // Unread
            }

            UpdateTicks(null);

            _ticksState = MessageTicksState.None;
            return string.Empty;
        }

        private string UpdateTimeLabel(Chat chat, ChatPosition position)
        {
            if (position?.Source is ChatSourceMtprotoProxy)
            {
                return Strings.UseProxySponsor;
            }
            else if (position?.Source is ChatSourcePublicServiceAnnouncement psa)
            {
                var type = LocaleService.Current.GetString("PsaType_" + psa.Type);
                if (type.Length > 0)
                {
                    return type;
                }

                return Strings.PsaTypeDefault;
            }

            var lastMessage = chat.LastMessage;
            if (lastMessage != null)
            {
                return Formatter.DateExtended(lastMessage.Date);
            }

            return string.Empty;
        }

        public void ShowPreview(Point? position)
        {
            Logger.Info();

            var tooltip = new MenuFlyoutContent();

            var flyout = new MenuFlyout();
            flyout.MenuFlyoutPresenterStyle = new Style(typeof(MenuFlyoutPresenter));
            flyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));

            flyout.Items.Add(tooltip);

            var chat = _chat;
            if (chat == null && _message != null)
            {
                chat = _clientService?.GetChat(_message.ChatId);
            }

            if (chat == null)
            {
                return;
            }

            var context = WindowContext.ForXamlRoot(this);
            var service = context.NavigationServices.GetByFrameId($"Main{_clientService.SessionId}") as NavigationService;

            var grid = new Grid();
            var chatView = new ChatView
            {
                FromPreview = true,
                Width = 320,
                Height = 360
            };

            var viewModel = service.Session.Resolve<DialogViewModel, IDialogDelegate>(chatView);
            viewModel.NavigationService = service;
            viewModel.Dispatcher = service.Dispatcher;
            chatView.Activate(viewModel);
            _ = viewModel.NavigatedToAsync(chat.Id, Windows.UI.Xaml.Navigation.NavigationMode.New, new Telegram.Navigation.Services.NavigationState());

            void handler(object sender, object e)
            {
                Logger.Info("Unloaded");

                chatView.Unloaded -= handler;
                chatView.ViewModel.NavigatedFrom(null, false);
                chatView.Deactivate(true);
            }

            chatView.Unloaded += handler;

            var background = new ChatBackgroundControl();
            background.Update(_clientService, null);

            grid.Children.Add(background);
            grid.Children.Add(chatView);
            grid.CornerRadius = new CornerRadius(8);

            tooltip.Content = grid;
            tooltip.Padding = new Thickness();
            tooltip.MaxWidth = double.PositiveInfinity;

            flyout.ShowAt(this, position ?? this.TransformToPointerPosition());
        }

        protected override void OnDragEnter(DragEventArgs e)
        {
            var chat = _chat;
            if (chat == null)
            {
                return;
            }

            try
            {
                if (_clientService.CanPostMessages(chat) && e.DataView.AvailableFormats.Count > 0)
                {
                    if (DropVisual == null)
                    {
                        FindName(nameof(DropVisual));
                    }

                    DropVisual.Visibility = Visibility.Visible;
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                }
                else
                {
                    DropVisual?.Visibility = Visibility.Collapsed;

                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
                }
            }
            catch
            {
                DropVisual?.Visibility = Visibility.Collapsed;
            }

            base.OnDragEnter(e);
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            DropVisual?.Visibility = Visibility.Collapsed;

            base.OnDragLeave(e);
        }

        protected override void OnDrop(DragEventArgs e)
        {
            DropVisual?.Visibility = Visibility.Collapsed;

            try
            {
                if (e.DataView.AvailableFormats.Count == 0)
                {
                    return;
                }

                var chat = _chat;
                if (chat == null)
                {
                    return;
                }

                var service = WindowContext.GetNavigationService(this);
                service?.NavigateToChat(chat, state: new NavigationState
                {
                    { "package", e.DataView }
                });
            }
            catch { }

            base.OnDrop(e);
        }

        public void Mockup(ChatType type, int color, string title, string from, string message, bool sent, int unread, bool muted, bool pinned, DateTime date, bool online = false)
        {
            if (!_templateApplied)
            {
                void loaded(object o, RoutedEventArgs e)
                {
                    Loaded -= loaded;
                    Mockup(type, color, title, from, message, sent, unread, muted, pinned, date, online);
                }

                Loaded += loaded;
                return;
            }

            TitleLabel.Text = title;
            Photo.Source = type is ChatTypeSupergroup ? ProfilePictureSourceText.GetNameForChat(title, color) : ProfilePictureSourceText.GetNameForUser(title, color);

            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;

            MinithumbnailPanel.Visibility = Visibility.Collapsed;

            PinnedIcon.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Text = unread.ToString();
            UnreadBadge.IsUnmuted = !muted;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            FromLabel.Text = from;
            BriefLabel.Inlines.Add(new Run { Text = message });
            _dateLabel = Formatter.Time(date);
            _stateLabel = sent ? "\uE601" : string.Empty;

            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            _container?.IsVisible = false;

            if (online)
            {
                FindName(nameof(OnlineBadge));
            }
        }


        #region SelectionStroke

        private CompositionColorSource _selectionStrokeBrush;

        public SolidColorBrush SelectionStroke
        {
            get => (SolidColorBrush)GetValue(SelectionStrokeProperty);
            set => SetValue(SelectionStrokeProperty, value);
        }

        public static readonly DependencyProperty SelectionStrokeProperty =
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(ChatCell), new PropertyMetadata(null, OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatCell)d).OnSelectionStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnSelectionStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _selectionStrokeBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion

        #region Selection Animation

        private Visual _selectionOutline;
        private Visual _selectionPhoto;

        private CompositionPathGeometry _polygon;
        private ShapeVisual _visual;

        private void InitializeSelection()
        {
            static CompositionPath GetCheckMark()
            {
                CanvasGeometry result;
                using (var builder = new CanvasPathBuilder(null))
                {
                    //builder.BeginFigure(new Vector2(3.821f, 7.819f));
                    //builder.AddLine(new Vector2(6.503f, 10.501f));
                    //builder.AddLine(new Vector2(12.153f, 4.832f));
                    builder.BeginFigure(new Vector2(5.821f, 9.819f));
                    builder.AddLine(new Vector2(7.503f, 12.501f));
                    builder.AddLine(new Vector2(14.153f, 6.832f));
                    builder.EndFigure(CanvasFigureLoop.Open);
                    result = CanvasGeometry.CreatePath(builder);
                }
                return new CompositionPath(result);
            }

            var compositor = BootStrapper.Current.Compositor;
            //12.711,5.352 11.648,4.289 6.5,9.438 4.352,7.289 3.289,8.352 6.5,11.563

            var polygon = compositor.CreatePathGeometry();
            polygon.Path = GetCheckMark();

            var shape1 = compositor.CreateSpriteShape();
            shape1.Geometry = polygon;
            shape1.StrokeThickness = 1.5f;
            shape1.StrokeBrush = compositor.CreateColorBrush(Colors.White);

            var ellipse = compositor.CreateEllipseGeometry();
            ellipse.Radius = new Vector2(8);
            ellipse.Center = new Vector2(10);

            var shape2 = compositor.CreateSpriteShape();
            shape2.Geometry = ellipse;
            shape2.FillBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);

            var outer = compositor.CreateEllipseGeometry();
            outer.Radius = new Vector2(10);
            outer.Center = new Vector2(10);

            var shape3 = compositor.CreateSpriteShape();
            shape3.Geometry = outer;
            shape3.FillBrush = _selectionStrokeBrush ??= new CompositionColorSource(SelectionStroke, IsConnected);

            var visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape3);
            visual.Shapes.Add(shape2);
            visual.Shapes.Add(shape1);
            visual.Size = new Vector2(20, 20);
            visual.Offset = new Vector3(48 - 19, 48 - 19, 0);
            visual.CenterPoint = new Vector3(8);
            visual.Scale = new Vector3(0);

            ElementCompositionPreview.SetElementChildVisual(PhotoPanel, visual);

            _polygon = polygon;
            _visual = visual;
        }

        public void UpdateState(bool selected, bool animate, bool multiple)
        {
            if (_selected == selected)
            {
                return;
            }

            if (_visual == null)
            {
                InitializeSelection();
            }

            if (animate)
            {
                var compositor = BootStrapper.Current.Compositor;

                var anim3 = compositor.CreateScalarKeyFrameAnimation();
                anim3.InsertKeyFrame(selected ? 0 : 1, 0);
                anim3.InsertKeyFrame(selected ? 1 : 0, 1);

                var anim1 = compositor.CreateScalarKeyFrameAnimation();
                anim1.InsertKeyFrame(selected ? 0 : 1, 0);
                anim1.InsertKeyFrame(selected ? 1 : 0, 1);
                anim1.DelayTime = TimeSpan.FromMilliseconds(anim1.Duration.TotalMilliseconds / 2);
                anim1.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

                var anim2 = compositor.CreateVector3KeyFrameAnimation();
                anim2.InsertKeyFrame(selected ? 0 : 1, new Vector3(0));
                anim2.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));

                _polygon.StartAnimation("TrimEnd", anim1);
                _visual.StartAnimation("Scale", anim2);
                _visual.StartAnimation("Opacity", anim3);

                var anim4 = compositor.CreateVector3KeyFrameAnimation();
                anim4.InsertKeyFrame(selected ? 0 : 1, new Vector3(1));
                anim4.InsertKeyFrame(selected ? 1 : 0, new Vector3(40f / 48f));

                var anim5 = compositor.CreateVector3KeyFrameAnimation();
                anim5.InsertKeyFrame(selected ? 1 : 0, new Vector3(1));
                anim5.InsertKeyFrame(selected ? 0 : 1, new Vector3(40f / 48f));

                _selectionPhoto.StartAnimation("Scale", anim4);
                _selectionOutline.StartAnimation("Scale", anim5);
                _selectionOutline.StartAnimation("Opacity", anim3);
            }
            else
            {
                _polygon.TrimEnd = selected ? 1 : 0;
                _visual.Scale = new Vector3(selected ? 1 : 0);
                _visual.Opacity = selected ? 1 : 0;

                _selectionPhoto.Scale = new Vector3(selected ? 40f / 48f : 1);
                _selectionOutline.Scale = new Vector3(selected ? 1 : 40f / 48f);
                _selectionOutline.Opacity = selected ? 1 : 0;
            }

            _selected = selected;
        }

        #endregion

        #region Tick Animation

        private CompositionGeometry _line11;
        private CompositionGeometry _line12;
        private ShapeVisual _visual1;

        private CompositionGeometry _line21;
        private CompositionGeometry _line22;
        private ShapeVisual _visual2;

        private SpriteVisual _container;

        #region Stroke

        private CompositionColorSource _strokeBrush;

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ChatCell), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChatCell)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _strokeBrush?.PropertyChanged(newValue, IsConnected);
        }

        #endregion

        private void InitializeTicks()
        {
            var width = 18f;
            var height = 10f;
            var stroke = 1.33f;
            var distance = 4;

            var sqrt = MathF.Sqrt(2);

            var side = stroke / sqrt / 2f;
            var diagonal = height * sqrt;
            var length = diagonal / 2f / sqrt;

            var join = stroke / 2 * sqrt;

            var compositor = BootStrapper.Current.Compositor;

            var line11 = compositor.CreateLineGeometry();
            var line12 = compositor.CreateLineGeometry();

            line11.Start = new Vector2(width - height + side + join - length - distance, height - side - length);
            line11.End = new Vector2(width - height + side + join - distance, height - side);

            line12.Start = new Vector2(width - height + side - distance, height - side);
            line12.End = new Vector2(width - side - distance, side);

            var shape11 = compositor.CreateSpriteShape(line11);
            shape11.StrokeThickness = stroke;
            shape11.StrokeBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);
            shape11.IsStrokeNonScaling = true;
            shape11.StrokeStartCap = CompositionStrokeCap.Round;

            var shape12 = compositor.CreateSpriteShape(line12);
            shape12.StrokeThickness = stroke;
            shape12.StrokeBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);
            shape12.IsStrokeNonScaling = true;
            shape12.StrokeEndCap = CompositionStrokeCap.Round;

            var visual1 = compositor.CreateShapeVisual();
            visual1.Shapes.Add(shape12);
            visual1.Shapes.Add(shape11);
            visual1.Size = new Vector2(width, height);
            visual1.CenterPoint = new Vector3(width, height / 2f, 0);


            var line21 = compositor.CreateLineGeometry();
            var line22 = compositor.CreateLineGeometry();

            line21.Start = new Vector2(width - height + side + join - length, height - side - length);
            line21.End = new Vector2(width - height + side + join, height - side);

            line22.Start = new Vector2(width - height + side, height - side);
            line22.End = new Vector2(width - side, side);

            var shape21 = compositor.CreateSpriteShape(line21);
            shape21.StrokeThickness = stroke;
            shape21.StrokeBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);
            shape21.StrokeStartCap = CompositionStrokeCap.Round;

            var shape22 = compositor.CreateSpriteShape(line22);
            shape22.StrokeThickness = stroke;
            shape22.StrokeBrush = _strokeBrush ??= new CompositionColorSource(Stroke, IsConnected);
            shape22.StrokeEndCap = CompositionStrokeCap.Round;

            var visual2 = compositor.CreateShapeVisual();
            visual2.Shapes.Add(shape22);
            visual2.Shapes.Add(shape21);
            visual2.Size = new Vector2(width, height);


            var container = compositor.CreateSpriteVisual();
            container.Children.InsertAtTop(visual2);
            container.Children.InsertAtTop(visual1);
            container.Size = new Vector2(width, height);
            container.AnchorPoint = new Vector2(0, 0);
            container.Offset = new Vector3(0, 3, 0);
            container.RelativeOffsetAdjustment = new Vector3(0, 0, 0);

            ElementCompositionPreview.SetElementChildVisual(TimeLabel, container);

            _line11 = line11;
            _line12 = line12;
            _line21 = line21;
            _line22 = line22;
            _visual1 = visual1;
            _visual2 = visual2;
            _container = container;
        }

        private void UpdateTicks(bool? read, bool animate = false)
        {
            if (read == null)
            {
                _container?.IsVisible = false;
            }
            else
            {
                if (_container == null)
                {
                    InitializeTicks();
                }

                if (animate)
                {
                    AnimateTicks(read == true);
                }
                else
                {
                    _line11.TrimEnd = read == true ? 1 : 0;
                    _line12.TrimEnd = read == true ? 1 : 0;

                    _line21.TrimStart = read == true ? 1 : 0;

                    _container.IsVisible = true;
                }
            }
        }

        private void AnimateTicks(bool read)
        {
            _container.IsVisible = true;

            var height = 10f;
            var stroke = 2f;

            var sqrt = (float)Math.Sqrt(2);

            var diagonal = height * sqrt;
            var length = diagonal / 2f / sqrt;

            var duration = 250;
            var percent = stroke / length;

            var compositor = BootStrapper.Current.Compositor;

            var linear = compositor.CreateLinearEasingFunction();

            var anim11 = compositor.CreateScalarKeyFrameAnimation();
            anim11.InsertKeyFrame(0, 0);
            anim11.InsertKeyFrame(1, 1, linear);
            anim11.Duration = TimeSpan.FromMilliseconds(duration - percent * duration);

            var anim12 = compositor.CreateScalarKeyFrameAnimation();
            anim12.InsertKeyFrame(0, 0);
            anim12.InsertKeyFrame(1, 1);
            anim12.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            anim12.DelayTime = anim11.Duration;
            anim12.Duration = TimeSpan.FromMilliseconds(400);

            var anim22 = compositor.CreateVector3KeyFrameAnimation();
            anim22.InsertKeyFrame(0, new Vector3(1));
            anim22.InsertKeyFrame(0.2f, new Vector3(1.1f));
            anim22.InsertKeyFrame(1, new Vector3(1));
            anim22.Duration = anim11.Duration + anim12.Duration;

            if (read)
            {
                _line11.StartAnimation("TrimEnd", anim11);
                _line12.StartAnimation("TrimEnd", anim12);
                _visual1.StartAnimation("Scale", anim22);

                var anim21 = compositor.CreateScalarKeyFrameAnimation();
                anim21.InsertKeyFrame(0, 0);
                anim21.InsertKeyFrame(1, 1, linear);
                anim11.Duration = TimeSpan.FromMilliseconds(duration);

                _line21.StartAnimation("TrimStart", anim21);
            }
            else
            {
                _line11.TrimEnd = 0;
                _line12.TrimEnd = 0;

                _line21.TrimStart = 0;

                _line21.StartAnimation("TrimEnd", anim11);
                _line22.StartAnimation("TrimEnd", anim12);
                _visual2.StartAnimation("Scale", anim22);
            }
        }

        #endregion
    }

    public partial class ChatFoldersPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var width = 0d;
            var height = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                child.Measure(availableSize);

                if (width + child.DesiredSize.Width > availableSize.Width)
                {
                    break;
                }
                else
                {
                    width += child.DesiredSize.Width;
                    height = Math.Max(height, child.DesiredSize.Height);
                }
            }

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];

                if (width + child.DesiredSize.Width > finalSize.Width)
                {
                    child.Arrange(new Rect(0, 0, 0, 0));
                    break;
                }
                else
                {
                    child.Arrange(new Rect(width, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                    width += child.DesiredSize.Width;
                }
            }

            return finalSize;
        }
    }
}
