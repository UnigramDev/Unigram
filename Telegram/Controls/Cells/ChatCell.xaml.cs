//
// Copyright Fela Ameghino 2015-2024
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
using Telegram.Common;
using Telegram.Common.Chats;
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
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
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

    public sealed class ChatCell : ControlEx, IMultipleElement
    {
        private bool _selected;

        private Chat _chat;
        private ChatList _chatList;
        private StoryList _storyList;

        private Message _message;

        private SavedMessagesTopic _savedMessagesTopic;

        private int _thumbnailId;

        private string _dateLabel;
        private string _stateLabel;

        private IClientService _clientService;

        private Visual _onlineBadge;
        private bool _onlineCall;

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

            Connected += OnLoaded;
            Disconnected += OnUnloaded;
        }

        public event EventHandler<Chat> StoryClick;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_strokeToken == 0 && (_shapes != null || _ellipse != null))
            {
                Stroke?.RegisterColorChangedCallback(OnStrokeChanged, ref _strokeToken);
                OnStrokeChanged(Stroke, SolidColorBrush.ColorProperty);
            }

            if (_selectionStrokeToken == 0 && _stroke != null)
            {
                SelectionStroke?.RegisterColorChangedCallback(OnSelectionStrokeChanged, ref _selectionStrokeToken);
                OnSelectionStrokeChanged(SelectionStroke, SolidColorBrush.ColorProperty);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Stroke?.UnregisterColorChangedCallback(ref _strokeToken);
            SelectionStroke?.UnregisterColorChangedCallback(ref _selectionStrokeToken);
        }

        #region InitializeComponent

        private Grid PhotoPanel;
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
        private Rectangle DropVisual;
        private TextBlock UnreadMentionsLabel;
        private Run FromLabel;
        private Run DraftLabel;
        private RichTextBlock BriefText;
        private Span BriefLabel;
        private ImageBrush Minithumbnail;
        private Rectangle SelectionOutline;
        private ActiveStoriesSegments Segments;
        private ProfilePicture Photo;
        private Border OnlineBadge;
        private Border OnlineHeart;
        private StackPanel Folders;

        private Border CompactBadgeRoot;
        private BadgeControl CompactBadge;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            PhotoPanel = GetTemplateChild(nameof(PhotoPanel)) as Grid;
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
            DropVisual = GetTemplateChild(nameof(DropVisual)) as Rectangle;
            UnreadMentionsLabel = GetTemplateChild(nameof(UnreadMentionsLabel)) as TextBlock;
            FromLabel = GetTemplateChild(nameof(FromLabel)) as Run;
            DraftLabel = GetTemplateChild(nameof(DraftLabel)) as Run;
            BriefText = GetTemplateChild(nameof(BriefText)) as RichTextBlock;
            BriefLabel = GetTemplateChild(nameof(BriefLabel)) as Span;
            Minithumbnail = GetTemplateChild(nameof(Minithumbnail)) as ImageBrush;
            SelectionOutline = GetTemplateChild(nameof(SelectionOutline)) as Rectangle;
            Segments = GetTemplateChild(nameof(Segments)) as ActiveStoriesSegments;
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            Folders = GetTemplateChild(nameof(Folders)) as StackPanel;

            Segments.Click += Segments_Click;

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
                UpdateMessage(_clientService, _message);
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
                    Photo.Source = PlaceholderImage.GetGlyph(Icons.MyNotesFilled, 5);
                    Photo.Shape = ProfilePictureShape.Ellipse;
                    Identity.ClearStatus();
                }
                else if (savedMessagesTopic.Type is SavedMessagesTopicTypeAuthorHidden)
                {
                    TitleLabel.Text = Strings.AnonymousForward;
                    Photo.Source = PlaceholderImage.GetGlyph(Icons.AuthorHiddenFilled, 5);
                    Photo.Shape = ProfilePictureShape.Ellipse;
                    Identity.ClearStatus();
                }

                if (message != null)
                {
                    FromLabel.Text = UpdateFromLabel(clientService, null, message);
                }
            }

            MutedIcon.Visibility = Visibility.Collapsed;
            UnreadBadge.Visibility = Visibility.Collapsed;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;
            PinnedIcon.Visibility = savedMessagesTopic.IsPinned
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (message != null)
            {
                _dateLabel = Formatter.DateExtended(message.Date);
                _stateLabel = string.Empty;

                TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

                UpdateBriefLabel(UpdateBriefLabel(null, message.Content, message.IsOutgoing, false, false, out MinithumbnailId thumbnail));
                UpdateMinithumbnail(thumbnail);
            }
        }

        public void UpdateMessage(IClientService clientService, Message message)
        {
            _clientService = clientService;
            _message = message;

            if (!_templateApplied)
            {
                return;
            }

            var chat = clientService.GetChat(message.ChatId);
            if (chat == null)
            {
                return;
            }

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatEmojiStatus(chat);
            UpdateNotificationSettings(chat);

            PinnedIcon.Visibility = Visibility.Collapsed;
            UnreadBadge.Visibility = Visibility.Collapsed;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            FromLabel.Text = UpdateFromLabel(clientService, chat, message);
            _dateLabel = Formatter.DateExtended(message.Date);
            _stateLabel = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, null, message, message.SendingState);

            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            UpdateBriefLabel(UpdateBriefLabel(chat, message.Content, message.IsOutgoing, false, false, out MinithumbnailId thumbnail));
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
                Photo.Source = PlaceholderImage.GetGlyph(Icons.ArchiveFilled, 5);

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

        public void UpdateChatLastMessage(Chat chat, ChatPosition position = null)
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
                    FromLabel.Text = Icons.ZWJ;
                }
            }
            else
            {
                FromLabel.Text = from;

                if (_draft)
                {
                    DraftLabel.Text = Icons.ZWJ;
                }
            }

            _draft = draft;
            _dateLabel = UpdateTimeLabel(chat, position);
            _stateLabel = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            UpdateBriefLabel(UpdateBriefLabel(chat, position, out MinithumbnailId thumbnail));
            UpdateMinithumbnail(thumbnail);

            UpdateChatChatLists(chat);
        }

        public void UpdateChatReadInbox(Chat chat, ChatPosition position = null)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            position ??= chat.GetPosition(_chatList);

            PinnedIcon.Visibility = chat.UnreadCount == 0 && !chat.IsMarkedAsUnread && (position?.IsPinned ?? false) ? Visibility.Visible : Visibility.Collapsed;

            var unread = (chat.UnreadCount > 0 || chat.IsMarkedAsUnread) ? chat.UnreadMentionCount == 1 && chat.UnreadCount == 1 ? Visibility.Collapsed : Visibility.Visible : Visibility.Collapsed;
            if (unread == Visibility.Visible)
            {
                UnreadBadge.Visibility = Visibility.Visible;
                UnreadBadge.Text = chat.UnreadCount > 0 ? chat.UnreadCount.ToString() : string.Empty;
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

        public void UpdateChatUnreadMentionCount(Chat chat, ChatPosition position = null)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            UpdateChatReadInbox(chat, position);

            var unread = chat.UnreadMentionCount > 0 || chat.UnreadReactionCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (unread == Visibility.Visible)
            {
                UnreadMentionsBadge.Visibility = Visibility.Visible;
                UnreadMentionsLabel.Text = chat.UnreadMentionCount > 0 ? Icons.Mention16 : Icons.HeartFilled12;
            }
            else
            {
                UnreadMentionsBadge.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateNotificationSettings(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            var muted = _clientService.Notifications.GetMutedFor(chat) > 0;
            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.IsUnmuted = !muted;

            if (CompactBadge != null)
            {
                CompactBadge.IsUnmuted = !muted;
            }
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
            Photo.SetChat(_clientService, chat, 48);

            SelectionOutline.RadiusX = Photo.Shape == ProfilePictureShape.Ellipse ? 24 : 12;
            SelectionOutline.RadiusY = Photo.Shape == ProfilePictureShape.Ellipse ? 24 : 12;
        }

        public void UpdateChatEmojiStatus(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            Identity.SetStatus(_clientService, chat);
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

            UpdateOnlineBadge(chat.VideoChat?.HasParticipants ?? false, true);
        }

        public void UpdateUserStatus(Chat chat, UserStatus status)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            UpdateOnlineBadge(status is UserStatusOnline, false);
        }

        private void UpdateOnlineBadge(bool visible, bool activeCall)
        {
            if (OnlineBadge == null)
            {
                if (visible)
                {
                    OnlineBadge = GetTemplateChild(nameof(OnlineBadge)) as Border;
                    OnlineHeart = GetTemplateChild(nameof(OnlineHeart)) as Border;

                    _onlineBadge = ElementComposition.GetElementVisual(OnlineBadge);
                    _onlineBadge.CenterPoint = new Vector3(6);
                    //_onlineBadge.Opacity = 0;
                    //_onlineBadge.Scale = new Vector3(0);
                }
                else
                {
                    return;
                }
            }
            else if (OnlineBadge.Visibility == Visibility.Collapsed && !visible)
            {
                return;
            }

            if (_onlineCall != activeCall)
            {
                if (activeCall)
                {
                    OnlineBadge.Margin = new Thickness(0, 0, -1, -1);
                    OnlineBadge.Width = OnlineBadge.Height = 20;
                    OnlineBadge.CornerRadius = new CornerRadius(10);
                    OnlineHeart.Width = OnlineHeart.Height = 16;
                    OnlineHeart.CornerRadius = new CornerRadius(8);

                    _onlineBadge.CenterPoint = new Vector3(10);
                }
                else
                {
                    OnlineBadge.Margin = new Thickness(0, 0, 3, 0);
                    OnlineBadge.Width = OnlineBadge.Height = 12;
                    OnlineBadge.CornerRadius = new CornerRadius(6);
                    OnlineHeart.Width = OnlineHeart.Height = 8;
                    OnlineHeart.CornerRadius = new CornerRadius(4);

                    _onlineBadge.CenterPoint = new Vector3(6);
                }

                _onlineCall = activeCall;
            }

            OnlineBadge.Visibility = Visibility.Visible;

            var scale = _onlineBadge.Compositor.CreateVector3KeyFrameAnimation();
            //scale.InsertKeyFrame(0, new System.Numerics.Vector3(visible ? 0 : 1));
            scale.InsertKeyFrame(1, new Vector3(visible ? 1 : 0));

            var opacity = _onlineBadge.Compositor.CreateScalarKeyFrameAnimation();
            //opacity.InsertKeyFrame(0, visible ? 0 : 1);
            opacity.InsertKeyFrame(1, visible ? 1 : 0);

            _onlineBadge.StopAnimation("Scale");
            _onlineBadge.StopAnimation("Opacity");

            _onlineBadge.StartAnimation("Scale", scale);
            _onlineBadge.StartAnimation("Opacity", opacity);

            if (visible && activeCall)
            {
                var compositor = Window.Current.Compositor;

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

                var visual = compositor.CreateShapeVisual();
                visual.Shapes.Add(shape3);
                visual.Shapes.Add(shape2);
                visual.Shapes.Add(shape1);
                visual.Size = new Vector2(16, 16);
                visual.CenterPoint = new Vector3(8);

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

                ElementCompositionPreview.SetElementChildVisual(OnlineHeart, visual);
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
            UpdateChatUnreadMentionCount(chat, position);
            UpdateNotificationSettings(chat);
            UpdateChatActions(chat, _clientService.GetChatActions(chat.Id));

            if (_clientService.TryGetUser(chat, out User user) && user.Type is UserTypeRegular && user.Id != _clientService.Options.MyId && user.Id != 777000)
            {
                UpdateUserStatus(chat, user.Status);
            }
            else if (chat.VideoChat.GroupCallId != 0)
            {
                UpdateOnlineBadge(chat.VideoChat.HasParticipants, true);
            }
            else if (OnlineBadge != null)
            {
                OnlineBadge.Visibility = Visibility.Collapsed;
            }
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

                    BadgeControl badge;
                    if (i < Folders.Children.Count)
                    {
                        badge = Folders.Children[i] as BadgeControl;
                    }
                    else
                    {
                        badge = new BadgeControl
                        {
                            CornerRadius = new CornerRadius(4),
                            Margin = new Thickness(0, 0, 2, 0)
                        };

                        Folders.Children.Add(badge);
                    }

                    badge.Text = folder.Title;
                    badge.Foreground = foreground;
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
                LoadObject(ref CompactBadgeRoot, nameof(CompactBadgeRoot));
                LoadObject(ref CompactBadge, nameof(CompactBadge));

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

        private async void UpdateMinithumbnail(MinithumbnailId thumbnail)
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
                        await bitmap.SetSourceAsync(stream);
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

        private void UpdateBriefLabel(FormattedText message)
        {
            BriefLabel.Inlines.Clear();

            if (message != null)
            {
                var clean = message.ReplaceSpoilers();
                var previous = 0;

                if (message.Entities != null)
                {
                    foreach (var entity in clean.Entities)
                    {
                        if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                        {
                            continue;
                        }

                        if (entity.Offset > previous)
                        {
                            BriefLabel.Inlines.Add(new Run { Text = clean.Text.Substring(previous, entity.Offset - previous) });
                        }

                        var player = new CustomEmojiIcon();
                        player.LoopCount = 0;
                        player.Source = new CustomEmojiFileSource(_clientService, customEmoji.CustomEmojiId);
                        player.Style = BootStrapper.Current.Resources["InfoCustomEmojiStyle"] as Style;

                        var inline = new InlineUIContainer();
                        inline.Child = new CustomEmojiContainer(BriefText, player);

                        // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                        if (BriefLabel.Inlines.Empty())
                        {
                            BriefLabel.Inlines.Add(Icons.ZWNJ);
                        }

                        BriefLabel.Inlines.Add(inline);
                        BriefLabel.Inlines.Add(Icons.ZWNJ);

                        previous = entity.Offset + entity.Length;
                    }
                }

                if (clean.Text.Length > previous)
                {
                    BriefLabel.Inlines.Add(new Run { Text = clean.Text.Substring(previous) });
                }
            }
        }


        private FormattedText UpdateBriefLabel(Chat chat, ChatPosition position, out MinithumbnailId thumbnail)
        {
            thumbnail = null;

            if (position?.Source is ChatSourcePublicServiceAnnouncement psa && !string.IsNullOrEmpty(psa.Text))
            {
                return new FormattedText(psa.Text.Replace('\n', ' '), Array.Empty<TextEntity>());
            }

            var topMessage = chat.LastMessage;
            if (topMessage != null)
            {
                return UpdateBriefLabel(chat, topMessage.Content, topMessage.IsOutgoing, true, false, out thumbnail);
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = _clientService.GetSecretChat(secretType.SecretChatId);
                if (secret != null)
                {
                    if (secret.State is SecretChatStateReady)
                    {
                        return new FormattedText(secret.IsOutbound ? string.Format(Strings.EncryptedChatStartedOutgoing, _clientService.GetTitle(chat)) : Strings.EncryptedChatStartedIncoming, Array.Empty<TextEntity>());
                    }
                    else if (secret.State is SecretChatStatePending)
                    {
                        return new FormattedText(string.Format(Strings.AwaitingEncryption, _clientService.GetTitle(chat)), Array.Empty<TextEntity>());
                    }
                    else if (secret.State is SecretChatStateClosed)
                    {
                        return new FormattedText(Strings.EncryptionRejected, Array.Empty<TextEntity>());
                    }
                }
            }

            return new FormattedText(string.Empty, Array.Empty<TextEntity>());
        }

        public static FormattedText UpdateBriefLabel(Chat chat, MessageContent content, bool outgoing, bool draft, bool forceEmoji, out MinithumbnailId thumbnail)
        {
            thumbnail = null;

            if (draft && chat?.DraftMessage?.InputMessageText is InputMessageText draftText)
            {
                return draftText.Text;
            }

            static FormattedText Text(string text)
            {
                return new FormattedText(text, Array.Empty<TextEntity>());
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

            if (content is MessageGame gameMedia)
            {
                return Text("\U0001F3AE " + gameMedia.Game.Title);
            }
            else if (content is MessageVideoNote videoNote)
            {
                if (videoNote.VideoNote.Minithumbnail == null || videoNote.IsSecret || forceEmoji /*|| message.SelfDestructType is not null*/)
                {
                    return Text("\U0001F4F9 " + Strings.AttachRound);
                }

                thumbnail = new MinithumbnailId(videoNote.VideoNote.Video.Id, videoNote.VideoNote.Minithumbnail, true);
                return Text(Strings.AttachRound);
            }
            else if (content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return Text(Strings.AttachSticker);
                }

                return Text($"{sticker.Sticker.Emoji} {Strings.AttachSticker}");
            }
            else if (content is MessageVoiceNote voiceNote)
            {
                return Text1("\U0001F3A4 ", voiceNote.Caption, Strings.AttachAudio);
            }
            else if (content is MessageVideo video)
            {
                if (video.Video.Minithumbnail == null || video.IsSecret || forceEmoji)
                {
                    return Text1("\U0001F4F9 ", video.Caption, Strings.AttachVideo);
                }

                thumbnail = new MinithumbnailId(video.Video.VideoValue.Id, video.Video.Minithumbnail, false);
                return Text1(string.Empty, video.Caption, Strings.AttachVideo);
            }
            else if (content is MessageAnimation animation)
            {
                if (animation.Animation.Minithumbnail == null || animation.IsSecret || forceEmoji)
                {
                    return Text1("\U0001F47E ", animation.Caption, Strings.AttachGif);
                }

                thumbnail = new MinithumbnailId(animation.Animation.AnimationValue.Id, animation.Animation.Minithumbnail, false);
                return Text1(string.Empty, animation.Caption, Strings.AttachGif);
            }
            else if (content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null && title == null)
                {
                    return Text1("\U0001F3B5 ", audio.Caption, audio.Audio.FileName);
                }
                else
                {
                    return Text1("\U0001F3B5 ", audio.Caption, $"{performer ?? Strings.AudioUnknownArtist} - {title ?? Strings.AudioUnknownTitle}");
                }
            }
            else if (content is MessageDocument document)
            {
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    return Text1("\U0001F4CE ", document.Caption, Strings.AttachDocument);
                }

                return Text1("\U0001F4CE ", document.Caption, document.Document.FileName);
            }
            else if (content is MessageInvoice invoice)
            {
                return Text1("\U0001F4CB ", invoice.PaidMediaCaption, invoice.ProductInfo.Title);
            }
            else if (content is MessageContact)
            {
                return Text("\U0001F464 " + Strings.AttachContact);
            }
            else if (content is MessageLocation location)
            {
                return Text("\U0001F4CD " + (location.LivePeriod > 0 ? Strings.AttachLiveLocation : Strings.AttachLocation));
            }
            else if (content is MessageVenue)
            {
                return Text("\U0001F4CD " + Strings.AttachLocation);
            }
            else if (content is MessagePhoto photo)
            {
                if (photo.Photo.Minithumbnail == null || photo.IsSecret || forceEmoji)
                {
                    return Text1("\U0001F5BC ", photo.Caption, Strings.AttachPhoto);
                }

                thumbnail = new MinithumbnailId(photo.Photo.Sizes[^1].Photo.Id, photo.Photo.Minithumbnail, false);
                return Text1(string.Empty, photo.Caption, Strings.AttachPhoto);
            }
            else if (content is MessagePoll poll)
            {
                return Text1("\U0001F4CA ", poll.Poll.Question, Strings.Poll);
            }
            else if (content is MessageCall call)
            {
                return Text("\u260E " + call.ToOutcomeText(outgoing));
            }
            else if (content is MessageStory story && !story.ViaMention)
            {
                return Text(Strings.Story);
            }
            else if (content is MessageUnsupported)
            {
                return Text(Strings.UnsupportedAttachment);
            }
            else if (content is MessageAnimatedEmoji animatedEmoji)
            {
                if (animatedEmoji.AnimatedEmoji?.Sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
                {
                    return new FormattedText(animatedEmoji.Emoji, new[]
                    {
                        new TextEntity(0, animatedEmoji.Emoji.Length, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId))
                    });
                }

                return new FormattedText(animatedEmoji.Emoji, Array.Empty<TextEntity>());
            }
            else if (content is MessagePremiumGiveaway)
            {
                return Text(Strings.BoostingGiveaway);
            }
            else if (content is MessagePaidMedia paidMedia)
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

            return content switch
            {
                MessageText text => text.Text,
                MessageDice dice => new FormattedText(dice.Emoji, Array.Empty<TextEntity>()),
                _ => new FormattedText(string.Empty, Array.Empty<TextEntity>()),
            };
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
            if (message.IsService())
            {
                return MessageService.GetText(new MessageViewModel(clientService, null, null, chat, message));
            }

            var format = "{0}: \u200B";

            if (ShowFrom(clientService, chat, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsSaved(clientService.Options.MyId))
                {
                    return string.Format(format, clientService.GetTitle(message.ForwardInfo?.Origin, message.ImportInfo));
                }
                else if (message.SenderId.IsUser(clientService.Options.MyId))
                {
                    return string.Format(format, Strings.FromYou);
                }
                else if (fromUser != null)
                {
                    if (!string.IsNullOrEmpty(fromUser.FirstName))
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
                else if (fromChat != null && fromChat.Id != chat.Id)
                {
                    return string.Format(format, fromChat.Title);
                }
            }

            return string.Empty;
        }

        public static bool ShowFrom(IClientService clientService, Chat chat, Message message, out User senderUser, out Chat senderChat)
        {
            if (message.IsService())
            {
                senderUser = null;
                senderChat = null;
                return false;
            }

            if (message.IsOutgoing && message.ChatId != clientService.Options.MyId)
            {
                senderChat = null;
                return clientService.TryGetUser(message.SenderId, out senderUser)
                    || clientService.TryGetChat(message.SenderId, out senderChat);
            }

            if (chat?.Type is ChatTypeBasicGroup)
            {
                senderChat = null;
                return clientService.TryGetUser(message.SenderId, out senderUser);
            }

            if (chat?.Type is ChatTypeSupergroup supergroup)
            {
                senderUser = null;
                senderChat = null;
                return !supergroup.IsChannel
                    && clientService.TryGetUser(message.SenderId, out senderUser)
                    || clientService.TryGetChat(message.SenderId, out senderChat);
            }

            senderUser = null;
            senderChat = null;
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

        public void ShowPreview(HoldingEventArgs args)
        {
            var tooltip = new MenuFlyoutContent();

            var flyout = new MenuFlyout();
            flyout.MenuFlyoutPresenterStyle = new Style(typeof(MenuFlyoutPresenter));
            flyout.MenuFlyoutPresenterStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(0)));

            flyout.Items.Add(tooltip);

            var chat = _chat;
            var message = chat?.LastMessage;

            if (chat == null && _message != null)
            {
                chat = _clientService?.GetChat(_message.ChatId);
                message = _message;
            }

            if (chat == null)
            {
                return;
            }

            var grid = new Grid();
            var frame = new Frame
            {
                Width = 320,
                Height = 360
            };

            var service = new TLNavigationService(_clientService, null, frame, _clientService.SessionId, "ciccio"); // BootStrapper.Current.NavigationServiceFactory(BootStrapper.BackButton.Ignore, frame, _clientService.SessionId, "ciccio", false);
            service.NavigateToChat(_chat);

            grid.Children.Add(frame);
            grid.CornerRadius = new CornerRadius(8);

            tooltip.Content = grid;
            tooltip.Padding = new Thickness();
            tooltip.MaxWidth = double.PositiveInfinity;

            flyout.ShowAt(this, args.Position);
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
                    if (DropVisual != null)
                    {
                        DropVisual.Visibility = Visibility.Collapsed;
                    }

                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
                }
            }
            catch
            {
                if (DropVisual != null)
                {
                    DropVisual.Visibility = Visibility.Collapsed;
                }
            }

            base.OnDragEnter(e);
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            if (DropVisual != null)
            {
                DropVisual.Visibility = Visibility.Collapsed;
            }

            base.OnDragLeave(e);
        }

        protected override void OnDrop(DragEventArgs e)
        {
            if (DropVisual != null)
            {
                DropVisual.Visibility = Visibility.Collapsed;
            }

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

                var service = WindowContext.Current.NavigationServices.GetByFrameId($"Main{_clientService.SessionId}") as NavigationService;
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
            Photo.Source = type is ChatTypeSupergroup ? PlaceholderImage.GetNameForChat(title, color) : PlaceholderImage.GetNameForUser(title, color);

            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;

            MinithumbnailPanel.Visibility = Visibility.Collapsed;

            PinnedIcon.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Text = unread.ToString();
            UnreadBadge.IsUnmuted = !muted;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            FromLabel.Text = from;
            BriefLabel.Inlines.Add(new Run { Text = message });
            _dateLabel = Formatter.ShortTime.Format(date);
            _stateLabel = sent ? "\uE601" : string.Empty;

            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            if (_container != null)
            {
                _container.IsVisible = false;
            }

            if (online)
            {
                FindName(nameof(OnlineBadge));
            }
        }


        #region SelectionStroke

        private long _selectionStrokeToken;

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
            oldValue?.UnregisterColorChangedCallback(ref _selectionStrokeToken);

            if (newValue == null || _stroke == null)
            {
                return;
            }

            _stroke.FillBrush = Window.Current.Compositor.CreateColorBrush(newValue.Color);

            if (IsConnected)
            {
                newValue.RegisterColorChangedCallback(OnSelectionStrokeChanged, ref _selectionStrokeToken);
            }
        }

        private void OnSelectionStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _stroke == null)
            {
                return;
            }

            _stroke.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
        }

        #endregion

        #region Selection Animation

        private Visual _selectionOutline;
        private Visual _selectionPhoto;

        private CompositionPathGeometry _polygon;
        private CompositionSpriteShape _ellipse;
        private CompositionSpriteShape _stroke;
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

            var compositor = Window.Current.Compositor;
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
            shape2.FillBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);

            var outer = compositor.CreateEllipseGeometry();
            outer.Radius = new Vector2(10);
            outer.Center = new Vector2(10);

            var shape3 = compositor.CreateSpriteShape();
            shape3.Geometry = outer;
            shape3.FillBrush = GetBrush(SelectionStrokeProperty, ref _selectionStrokeToken, OnSelectionStrokeChanged);

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
            _ellipse = shape2;
            _stroke = shape3;
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
                var compositor = Window.Current.Compositor;

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

        private CompositionSpriteShape[] _shapes;

        private SpriteVisual _container;

        #region Stroke

        private long _strokeToken;

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
            oldValue?.UnregisterColorChangedCallback(ref _strokeToken);

            if (newValue == null || (_shapes == null && _ellipse == null))
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(newValue.Color);

            if (_shapes != null)
            {
                foreach (var shape in _shapes)
                {
                    shape.StrokeBrush = brush;
                }
            }

            if (_ellipse != null)
            {
                _ellipse.FillBrush = brush;
            }

            if (IsConnected)
            {
                newValue.RegisterColorChangedCallback(OnStrokeChanged, ref _strokeToken);
            }
        }

        private void OnStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || (_shapes == null && _ellipse == null))
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(solid.Color);

            if (_shapes != null)
            {
                foreach (var shape in _shapes)
                {
                    shape.StrokeBrush = brush;
                }
            }

            if (_ellipse != null)
            {
                _ellipse.FillBrush = brush;
            }
        }

        #endregion

        private CompositionBrush GetBrush(DependencyProperty dp, ref long token, DependencyPropertyChangedCallback callback)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
                if (IsConnected)
                {
                    solid.RegisterColorChangedCallback(callback, ref token);
                }

                return Window.Current.Compositor.CreateColorBrush(solid.Color);
            }

            return Window.Current.Compositor.CreateColorBrush(Colors.Black);
        }

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

            var line11 = Window.Current.Compositor.CreateLineGeometry();
            var line12 = Window.Current.Compositor.CreateLineGeometry();

            line11.Start = new Vector2(width - height + side + join - length - distance, height - side - length);
            line11.End = new Vector2(width - height + side + join - distance, height - side);

            line12.Start = new Vector2(width - height + side - distance, height - side);
            line12.End = new Vector2(width - side - distance, side);

            var shape11 = Window.Current.Compositor.CreateSpriteShape(line11);
            shape11.StrokeThickness = stroke;
            shape11.StrokeBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);
            shape11.IsStrokeNonScaling = true;
            shape11.StrokeStartCap = CompositionStrokeCap.Round;

            var shape12 = Window.Current.Compositor.CreateSpriteShape(line12);
            shape12.StrokeThickness = stroke;
            shape12.StrokeBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);
            shape12.IsStrokeNonScaling = true;
            shape12.StrokeEndCap = CompositionStrokeCap.Round;

            var visual1 = Window.Current.Compositor.CreateShapeVisual();
            visual1.Shapes.Add(shape12);
            visual1.Shapes.Add(shape11);
            visual1.Size = new Vector2(width, height);
            visual1.CenterPoint = new Vector3(width, height / 2f, 0);


            var line21 = Window.Current.Compositor.CreateLineGeometry();
            var line22 = Window.Current.Compositor.CreateLineGeometry();

            line21.Start = new Vector2(width - height + side + join - length, height - side - length);
            line21.End = new Vector2(width - height + side + join, height - side);

            line22.Start = new Vector2(width - height + side, height - side);
            line22.End = new Vector2(width - side, side);

            var shape21 = Window.Current.Compositor.CreateSpriteShape(line21);
            shape21.StrokeThickness = stroke;
            shape21.StrokeBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);
            shape21.StrokeStartCap = CompositionStrokeCap.Round;

            var shape22 = Window.Current.Compositor.CreateSpriteShape(line22);
            shape22.StrokeThickness = stroke;
            shape22.StrokeBrush = GetBrush(StrokeProperty, ref _strokeToken, OnStrokeChanged);
            shape22.StrokeEndCap = CompositionStrokeCap.Round;

            var visual2 = Window.Current.Compositor.CreateShapeVisual();
            visual2.Shapes.Add(shape22);
            visual2.Shapes.Add(shape21);
            visual2.Size = new Vector2(width, height);


            var container = Window.Current.Compositor.CreateSpriteVisual();
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
            _shapes = new[] { shape11, shape12, shape21, shape22 };
            _visual1 = visual1;
            _visual2 = visual2;
            _container = container;
        }

        private void UpdateTicks(bool? read, bool animate = false)
        {
            if (read == null)
            {
                if (_container != null)
                {
                    _container.IsVisible = false;
                }
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

            var linear = Window.Current.Compositor.CreateLinearEasingFunction();

            var anim11 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim11.InsertKeyFrame(0, 0);
            anim11.InsertKeyFrame(1, 1, linear);
            anim11.Duration = TimeSpan.FromMilliseconds(duration - percent * duration);

            var anim12 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim12.InsertKeyFrame(0, 0);
            anim12.InsertKeyFrame(1, 1);
            anim12.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            anim12.DelayTime = anim11.Duration;
            anim12.Duration = TimeSpan.FromMilliseconds(400);

            var anim22 = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim22.InsertKeyFrame(0, new Vector3(1));
            anim22.InsertKeyFrame(0.2f, new Vector3(1.1f));
            anim22.InsertKeyFrame(1, new Vector3(1));
            anim22.Duration = anim11.Duration + anim12.Duration;

            if (read)
            {
                _line11.StartAnimation("TrimEnd", anim11);
                _line12.StartAnimation("TrimEnd", anim12);
                _visual1.StartAnimation("Scale", anim22);

                var anim21 = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
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

        #region XamlMarkupHelper

        private void LoadObject<T>(ref T element, /*[CallerArgumentExpression("element")]*/string name)
            where T : DependencyObject
        {
            element ??= GetTemplateChild(name) as T;
        }

        private void UnloadObject<T>(ref T element)
            where T : DependencyObject
        {
            if (element != null)
            {
                XamlMarkupHelper.UnloadObject(element);
                element = null;
            }
        }

        #endregion

    }

    public class ChatFoldersPanel : Panel
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
