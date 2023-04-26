//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Common.Chats;
using Telegram.Controls.Chats;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
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

    public sealed class ChatCell : Control, IMultipleElement, IPlayerView
    {
        private bool _selected;

        private Chat _chat;
        private ChatList _chatList;

        private Message _message;

        private string _dateLabel;
        private string _stateLabel;

        private IClientService _clientService;

        private Visual _onlineBadge;
        private bool _onlineCall;

        private bool _compact;

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

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Stroke is SolidColorBrush stroke && _strokeToken == 0 && (_container != null || _visual != null))
            {
                _strokeToken = stroke.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnStrokeChanged);
            }

            if (SelectionStroke is SolidColorBrush selectionStroke && _selectionStrokeToken == 0 && _visual != null)
            {
                _selectionStrokeToken = selectionStroke.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnSelectionStrokeChanged);
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (Stroke is SolidColorBrush stroke && _strokeToken != 0)
            {
                stroke.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _strokeToken);
                _strokeToken = 0;
            }

            if (SelectionStroke is SolidColorBrush selectionStroke && _selectionStrokeToken != 0)
            {
                selectionStroke.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _selectionStrokeToken);
                _selectionStrokeToken = 0;
            }
        }

        #region InitializeComponent

        private Grid PhotoPanel;
        private ChatCellPanel LayoutRoot;
        private TextBlock TitleLabel;
        private IdentityIcon Identity;
        private FontIcon MutedIcon;
        private TextBlock TimeLabel;
        private Border MinithumbnailPanel;
        private ChatActionIndicator ChatActionIndicator;
        private TextBlock TypingLabel;
        private Border PinnedIcon;
        private Border UnreadMentionsBadge;
        private InfoBadge UnreadBadge;
        private Rectangle DropVisual;
        private TextBlock UnreadMentionsLabel;
        private TextBlock FromLabel;
        private TextBlock BriefLabel;
        private ImageBrush Minithumbnail;
        private Rectangle SelectionOutline;
        private ProfilePicture Photo;
        private Border OnlineBadge;
        private Border OnlineHeart;

        // Lazy loaded
        private CustomEmojiCanvas CustomEmoji;

        private Border CompactBadgeRoot;
        private InfoBadge CompactBadge;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            PhotoPanel = GetTemplateChild(nameof(PhotoPanel)) as Grid;
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as ChatCellPanel;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            Identity = GetTemplateChild(nameof(Identity)) as IdentityIcon;
            MutedIcon = GetTemplateChild(nameof(MutedIcon)) as FontIcon;
            TimeLabel = GetTemplateChild(nameof(TimeLabel)) as TextBlock;
            MinithumbnailPanel = GetTemplateChild(nameof(MinithumbnailPanel)) as Border;
            ChatActionIndicator = GetTemplateChild(nameof(ChatActionIndicator)) as ChatActionIndicator;
            TypingLabel = GetTemplateChild(nameof(TypingLabel)) as TextBlock;
            PinnedIcon = GetTemplateChild(nameof(PinnedIcon)) as Border;
            UnreadMentionsBadge = GetTemplateChild(nameof(UnreadMentionsBadge)) as Border;
            UnreadBadge = GetTemplateChild(nameof(UnreadBadge)) as InfoBadge;
            DropVisual = GetTemplateChild(nameof(DropVisual)) as Rectangle;
            UnreadMentionsLabel = GetTemplateChild(nameof(UnreadMentionsLabel)) as TextBlock;
            FromLabel = GetTemplateChild(nameof(FromLabel)) as TextBlock;
            BriefLabel = GetTemplateChild(nameof(BriefLabel)) as TextBlock;
            Minithumbnail = GetTemplateChild(nameof(Minithumbnail)) as ImageBrush;
            SelectionOutline = GetTemplateChild(nameof(SelectionOutline)) as Rectangle;
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;

            BriefLabel.SizeChanged += OnSizeChanged;

            var tooltip = new ToolTip();
            tooltip.Opened += ToolTip_Opened;
            tooltip.Closed += Tooltip_Closed;

            ToolTipService.SetToolTip(this, tooltip);

            _selectionPhoto = ElementCompositionPreview.GetElementVisual(Photo);
            _selectionOutline = ElementCompositionPreview.GetElementVisual(SelectionOutline);
            _selectionPhoto.CenterPoint = new Vector3(24);
            _selectionOutline.CenterPoint = new Vector3(24);
            _selectionOutline.Opacity = 0;

            _templateApplied = true;

            if (_chat != null)
            {
                UpdateChat(_clientService, _chat, _chatList);
            }
            else if (_chatList != null)
            {
                UpdateChatList(_clientService, _chatList);
            }
            else if (_message != null)
            {
                UpdateMessage(_clientService, _message);
            }
        }

        #endregion

        public void UpdateChat(IClientService clientService, Chat chat, ChatList chatList)
        {
            _clientService = clientService;

            Update(chat, chatList);
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
            UpdateChatType(chat);
            UpdateNotificationSettings(chat);

            PinnedIcon.Visibility = Visibility.Collapsed;
            UnreadBadge.Visibility = Visibility.Collapsed;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            FromLabel.Text = UpdateFromLabel(chat, message);
            _dateLabel = UpdateTimeLabel(message);
            _stateLabel = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, null, message, message.SendingState);

            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            UpdateBriefLabel(UpdateBriefLabel(chat, message, true, false));
            UpdateMinithumbnail(chat, chat.DraftMessage == null ? message : null);
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
                Photo.Source = PlaceholderHelper.GetGlyph(Icons.Archive, 0, 96);

                UnreadMentionsBadge.Visibility = Visibility.Collapsed;
                PinnedIcon.Visibility = Visibility.Collapsed;

                //DraftLabel.Text = string.Empty;
                _dateLabel = string.Empty;
                _stateLabel = string.Empty;
                TimeLabel.Text = string.Empty;

                MutedIcon.Visibility = Visibility.Collapsed;

                MinithumbnailPanel.Visibility = Visibility.Collapsed;

                VisualStateManager.GoToState(this, "Muted", false);

                UpdateTicks(null);

                var unreadCount = clientService.GetUnreadCount(chatList);
                UnreadBadge.Visibility = unreadCount.UnreadChatCount.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
                UnreadBadge.Value = unreadCount.UnreadChatCount.UnreadCount;

                if (CompactBadge != null)
                {
                    CompactBadgeRoot.Visibility = UnreadBadge.Visibility;
                    CompactBadge.Value = UnreadBadge.Value;
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

            FromLabel.Text = UpdateFromLabel(chat, position, out bool draft);

            _dateLabel = UpdateTimeLabel(chat, position);
            _stateLabel = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            UpdateBriefLabel(UpdateBriefLabel(chat, position));
            UpdateMinithumbnail(chat, chat.DraftMessage == null ? chat.LastMessage : null);
        }

        public void UpdateChatReadInbox(Chat chat, ChatPosition position = null)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            position ??= chat.GetPosition(_chatList);

            PinnedIcon.Visibility = chat.UnreadCount == 0 && !chat.IsMarkedAsUnread && (position?.IsPinned ?? false) ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = (chat.UnreadCount > 0 || chat.IsMarkedAsUnread) ? chat.UnreadMentionCount == 1 && chat.UnreadCount == 1 ? Visibility.Collapsed : Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Value = chat.UnreadCount;

            if (CompactBadge != null)
            {
                CompactBadgeRoot.Visibility = UnreadBadge.Visibility;
                CompactBadge.Value = UnreadBadge.Value;
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
            UnreadMentionsBadge.Visibility = chat.UnreadMentionCount > 0 || chat.UnreadReactionCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadMentionsLabel.Text = chat.UnreadMentionCount > 0 ? Icons.Mention16 : Icons.HeartFilled12;
        }

        public void UpdateNotificationSettings(Chat chat)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            var muted = _clientService.Notifications.GetMutedFor(chat) > 0;
            VisualStateManager.GoToState(this, muted ? "Muted" : "Unmuted", false);
            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
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

            Photo.SetChat(_clientService, chat, 48);

            SelectionOutline.RadiusX = Photo.Shape == ProfilePictureShape.Ellipse ? 24 : 12;
            SelectionOutline.RadiusY = Photo.Shape == ProfilePictureShape.Ellipse ? 24 : 12;
        }

        public void UpdateChatActions(Chat chat, IDictionary<MessageSender, ChatAction> actions)
        {
            if (_clientService == null || !_templateApplied)
            {
                return;
            }

            if (actions != null && actions.Count > 0)
            {
                TypingLabel.Text = InputChatActionManager.GetTypingString(chat, actions, _clientService.GetUser, _clientService.GetChat, out ChatAction commonAction);
                ChatActionIndicator.UpdateAction(commonAction);
                ChatActionIndicator.Visibility = Visibility.Visible;
                TypingLabel.Visibility = Visibility.Visible;
                FromLabel.Visibility = Visibility.Collapsed;
                BriefLabel.Visibility = Visibility.Collapsed;
                MinithumbnailPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ChatActionIndicator.Visibility = Visibility.Collapsed;
                ChatActionIndicator.UpdateAction(null);
                TypingLabel.Visibility = Visibility.Collapsed;
                FromLabel.Visibility = Visibility.Visible;
                BriefLabel.Visibility = Visibility.Visible;
                MinithumbnailPanel.Visibility = Minithumbnail.ImageSource != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateChatType(Chat chat)
        {
            Identity.SetStatus(_clientService, chat);
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

                    _onlineBadge = ElementCompositionPreview.GetElementVisual(OnlineBadge);
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

            Tag = chat;

            if (!_templateApplied)
            {
                return;
            }

            var position = chat.GetPosition(chatList);

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatType(chat);

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

        #endregion

        public void UpdateViewState(Chat chat, bool compact, bool animate)
        {
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
                CompactBadge.Value = UnreadBadge.Value;
            }

            ElementCompositionPreview.SetIsTranslationEnabled(LayoutRoot, true);

            var visual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            visual.Clip ??= visual.Compositor.CreateInsetClip();

            var x = LayoutRoot.ActualSize.X + 24;

            if (animate)
            {
                var offset0 = visual.Compositor.CreateVector3KeyFrameAnimation();
                offset0.InsertKeyFrame(0, new Vector3(compact ? 0 : -x, 0, 0));
                offset0.InsertKeyFrame(1, new Vector3(compact ? -x : 0, 0, 0));
                //offset0.Duration = Constants.FastAnimation;
                visual.StartAnimation("Translation", offset0);

                var clip0 = visual.Compositor.CreateScalarKeyFrameAnimation();
                clip0.InsertKeyFrame(0, compact ? -24 : x - 24);
                clip0.InsertKeyFrame(1, compact ? x - 24 : -24);
                //clip0.Duration = Constants.FastAnimation;
                visual.Clip.StartAnimation("LeftInset", clip0);
            }
            else
            {
                visual.Properties.InsertVector3("Translation", new Vector3(compact ? -x : 0, 0, 0));

                if (visual.Clip is InsetClip inset)
                {
                    inset.LeftInset = compact ? x - 24 : -24;
                }
            }

            if (CompactBadgeRoot != null)
            {
                var badge = ElementCompositionPreview.GetElementVisual(CompactBadgeRoot);
                badge.CenterPoint = new Vector3(UnreadBadge.ActualSize.X / 2 + 2, UnreadBadge.ActualSize.Y / 2 + 2, 0);

                if (animate)
                {
                    var scale0 = visual.Compositor.CreateVector3KeyFrameAnimation();
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

        private async void UpdateMinithumbnail(Chat chat, Message message)
        {
            if (chat.Type is ChatTypePrivate && message == null)
            {
                MinithumbnailPanel.Visibility = Visibility.Collapsed;
                Minithumbnail.ImageSource = null;

                return;
            }

            var thumbnail = message?.GetMinithumbnail(false);
            if (thumbnail != null)
            {
                double ratioX = (double)16 / thumbnail.Width;
                double ratioY = (double)16 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };

                Minithumbnail.ImageSource = bitmap;
                MinithumbnailPanel.Visibility = Visibility.Visible;

                MinithumbnailPanel.CornerRadius = new CornerRadius(message.Content is MessageVideoNote ? 9 : 2);

                using (var stream = new InMemoryRandomAccessStream())
                {
                    PlaceholderImageHelper.Current.WriteBytes(thumbnail.Data, stream);
                    await bitmap.SetSourceAsync(stream);
                }
            }
            else
            {
                MinithumbnailPanel.Visibility = Visibility.Collapsed;
                Minithumbnail.ImageSource = null;
            }
        }

        #region Custom emoji

        private readonly List<EmojiPosition> _positions = new();
        private bool _ignoreLayoutUpdated = true;

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _ignoreLayoutUpdated = false;
        }

        private void OnLayoutUpdated(object sender, object e)
        {
            if (_ignoreLayoutUpdated)
            {
                return;
            }

            if (_positions.Count > 0)
            {
                _ignoreLayoutUpdated = true;
                LoadCustomEmoji();
            }
            else
            {
                BriefLabel.LayoutUpdated -= OnLayoutUpdated;

                if (CustomEmoji != null)
                {
                    XamlMarkupHelper.UnloadObject(CustomEmoji);
                    CustomEmoji = null;
                }
            }
        }

        private void LoadCustomEmoji()
        {
            var positions = new List<EmojiPosition>();

            foreach (var item in _positions)
            {
                var pointer = BriefLabel.ContentStart.GetPositionAtOffset(item.X, LogicalDirection.Forward);
                if (pointer == null)
                {
                    continue;
                }

                var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
                if (rect.X + 20 > BriefLabel.ActualWidth && BriefLabel.IsTextTrimmed)
                {
                    break;
                }

                positions.Add(new EmojiPosition
                {
                    CustomEmojiId = item.CustomEmojiId,
                    X = (int)rect.X,
                    Y = (int)rect.Y
                });
            }

            if (positions.Count < 1)
            {
                BriefLabel.LayoutUpdated -= OnLayoutUpdated;

                if (CustomEmoji != null)
                {
                    XamlMarkupHelper.UnloadObject(CustomEmoji);
                    CustomEmoji = null;
                }
            }
            else
            {
                CustomEmoji ??= GetTemplateChild(nameof(CustomEmoji)) as CustomEmojiCanvas;
                CustomEmoji.UpdatePositions(positions);

                if (_playing)
                {
                    CustomEmoji.Play();
                }
            }
        }

        #endregion

        #region IPlayerView

        public bool IsAnimatable => CustomEmoji != null;

        public bool IsLoopingEnabled => true;

        private bool _playing;

        public bool Play()
        {
            CustomEmoji?.Play();

            _playing = true;
            return true;
        }

        public void Pause()
        {
            CustomEmoji?.Pause();

            _playing = false;
        }

        public void Unload()
        {
            CustomEmoji?.Unload();

            _playing = false;
        }

        #endregion

        private void UpdateBriefLabel(FormattedText message)
        {

            _positions.Clear();
            BriefLabel.Inlines.Clear();

            if (message != null)
            {
                var clean = message.ReplaceSpoilers();
                var previous = 0;

                var emoji = new HashSet<long>();
                var shift = 0;

                if (message.Entities != null)
                {
                    foreach (var entity in message.Entities)
                    {
                        if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                        {
                            continue;
                        }

                        if (entity.Offset > previous)
                        {
                            BriefLabel.Inlines.Add(new Run { Text = clean.Substring(previous, entity.Offset - previous) });
                            shift += 2;
                        }

                        _positions.Add(new EmojiPosition { X = shift + entity.Offset + 1, CustomEmojiId = customEmoji.CustomEmojiId });
                        BriefLabel.Inlines.Add(new Run { Text = clean.Substring(entity.Offset, entity.Length), FontFamily = BootStrapper.Current.Resources["SpoilerFontFamily"] as FontFamily });

                        emoji.Add(customEmoji.CustomEmojiId);
                        shift += 2;

                        previous = entity.Offset + entity.Length;
                    }
                }

                if (clean.Length > previous)
                {
                    BriefLabel.Inlines.Add(new Run { Text = clean.Substring(previous) });
                }

                BriefLabel.LayoutUpdated -= OnLayoutUpdated;

                if (emoji.Count > 0)
                {
                    CustomEmoji ??= GetTemplateChild(nameof(CustomEmoji)) as CustomEmojiCanvas;
                    CustomEmoji.UpdateEntities(_clientService, emoji);

                    if (_playing)
                    {
                        CustomEmoji.Play();
                    }

                    _ignoreLayoutUpdated = false;
                    BriefLabel.LayoutUpdated += OnLayoutUpdated;
                }
                else if (CustomEmoji != null)
                {
                    XamlMarkupHelper.UnloadObject(CustomEmoji);
                    CustomEmoji = null;
                }

            }
            else if (CustomEmoji != null)
            {
                BriefLabel.LayoutUpdated -= OnLayoutUpdated;

                XamlMarkupHelper.UnloadObject(CustomEmoji);
                CustomEmoji = null;
            }
        }


        private FormattedText UpdateBriefLabel(Chat chat, ChatPosition position)
        {
            if (position?.Source is ChatSourcePublicServiceAnnouncement psa && !string.IsNullOrEmpty(psa.Text))
            {
                return new FormattedText(psa.Text.Replace('\n', ' '), new TextEntity[0]);
            }

            var topMessage = chat.LastMessage;
            if (topMessage != null)
            {
                return UpdateBriefLabel(chat, topMessage, true, true);
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = _clientService.GetSecretChat(secretType.SecretChatId);
                if (secret != null)
                {
                    if (secret.State is SecretChatStateReady)
                    {
                        return new FormattedText(secret.IsOutbound ? string.Format(Strings.EncryptedChatStartedOutgoing, _clientService.GetTitle(chat)) : Strings.EncryptedChatStartedIncoming, new TextEntity[0]);
                    }
                    else if (secret.State is SecretChatStatePending)
                    {
                        return new FormattedText(string.Format(Strings.AwaitingEncryption, _clientService.GetTitle(chat)), new TextEntity[0]);
                    }
                    else if (secret.State is SecretChatStateClosed)
                    {
                        return new FormattedText(Strings.EncryptionRejected, new TextEntity[0]);
                    }
                }
            }

            return new FormattedText(string.Empty, new TextEntity[0]);
        }

        private FormattedText UpdateBriefLabel(Chat chat, Message message, bool showContent, bool draft)
        {
            if (chat.DraftMessage != null && draft)
            {
                switch (chat.DraftMessage.InputMessageText)
                {
                    case InputMessageText text:
                        return text.Text;
                }
            }

            if (!showContent)
            {
                return new FormattedText(Strings.Message, Array.Empty<TextEntity>());
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

            if (message.Content is MessageGame gameMedia)
            {
                return Text("\U0001F3AE " + gameMedia.Game.Title);
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                if (videoNote.VideoNote.Minithumbnail == null || videoNote.IsSecret)
                {
                    return Text("\U0001F4F9 " + Strings.AttachRound);
                }

                return Text(Strings.AttachRound);
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return Text(Strings.AttachSticker);
                }

                return Text($"{sticker.Sticker.Emoji} {Strings.AttachSticker}");
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                return Text1("\U0001F3A4 ", voiceNote.Caption, Strings.AttachAudio);
            }
            else if (message.Content is MessageVideo video)
            {
                if (video.Video.Minithumbnail == null || video.IsSecret)
                {
                    return Text1("\U0001F4F9 ", video.Caption, Strings.AttachVideo);
                }

                return Text1(string.Empty, video.Caption, Strings.AttachVideo);
            }
            else if (message.Content is MessageAnimation animation)
            {
                if (animation.Animation.Minithumbnail == null || animation.IsSecret)
                {
                    return Text1("\U0001F47E ", animation.Caption, Strings.AttachGif);
                }

                return Text1(string.Empty, animation.Caption, Strings.AttachGif);
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null || title == null)
                {
                    return Text1("\U0001F3B5 ", audio.Caption, Strings.AttachMusic);
                }
                else
                {
                    return Text1("\U0001F3B5 ", audio.Caption, $"{performer} - {title}");
                }
            }
            else if (message.Content is MessageDocument document)
            {
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    return Text1("\U0001F4CE ", document.Caption, Strings.AttachDocument);
                }

                return Text1("\U0001F4CE ", document.Caption, document.Document.FileName);
            }
            else if (message.Content is MessageInvoice invoice)
            {
                var caption = invoice.ExtendedMedia switch
                {
                    MessageExtendedMediaPreview extendedMediaPreview => extendedMediaPreview.Caption,
                    MessageExtendedMediaPhoto extendedMediaPhoto => extendedMediaPhoto.Caption,
                    MessageExtendedMediaVideo extendedMediaVideo => extendedMediaVideo.Caption,
                    MessageExtendedMediaUnsupported extendedMediaUnsupported => extendedMediaUnsupported.Caption,
                    _ => null
                };

                return Text1("\U0001F4CB ", caption, invoice.Title);
            }
            else if (message.Content is MessageContact)
            {
                return Text("\U0001F464 " + Strings.AttachContact);
            }
            else if (message.Content is MessageLocation location)
            {
                return Text("\U0001F4CD " + (location.LivePeriod > 0 ? Strings.AttachLiveLocation : Strings.AttachLocation));
            }
            else if (message.Content is MessageVenue)
            {
                return Text("\U0001F4CD " + Strings.AttachLocation);
            }
            else if (message.Content is MessagePhoto photo)
            {
                if (photo.Photo.Minithumbnail == null || photo.IsSecret)
                {
                    return Text1("\U0001F5BC ", photo.Caption, Strings.AttachPhoto);
                }

                return Text1(string.Empty, photo.Caption, Strings.AttachPhoto);
            }
            else if (message.Content is MessagePoll poll)
            {
                return Text("\U0001F4CA " + poll.Poll.Question);
            }
            else if (message.Content is MessageCall call)
            {
                return Text("\u260E " + call.ToOutcomeText(message.IsOutgoing));
            }
            else if (message.Content is MessageUnsupported)
            {
                return Text(Strings.UnsupportedAttachment);
            }

            return message.Content switch
            {
                MessageText text => text.Text,
                MessageAnimatedEmoji animatedEmoji => new FormattedText(animatedEmoji.Emoji, Array.Empty<TextEntity>()),
                MessageDice dice => new FormattedText(dice.Emoji, Array.Empty<TextEntity>()),
                _ => new FormattedText(string.Empty, Array.Empty<TextEntity>()),
            };
        }

        private string UpdateFromLabel(Chat chat, ChatPosition position, out bool draft)
        {
            if (position?.Source is ChatSourcePublicServiceAnnouncement psa && !string.IsNullOrEmpty(psa.Text))
            {
                draft = false;
                return string.Empty;
            }
            else if (chat.DraftMessage != null)
            {
                switch (chat.DraftMessage.InputMessageText)
                {
                    case InputMessageText:
                        draft = true;
                        return string.Format("{0}: \u200B​​​", Strings.Draft);
                }
            }

            var message = chat.LastMessage;
            if (message == null)
            {
                draft = false;
                return string.Empty;
            }

            draft = false;
            return UpdateFromLabel(chat, message);
        }

        private string UpdateFromLabel(Chat chat, Message message)
        {
            if (message.IsService())
            {
                return MessageService.GetText(new ViewModels.MessageViewModel(_clientService, null, null, message));
            }

            var format = "{0}: \u200B";

            if (ShowFrom(_clientService, chat, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsSaved(_clientService.Options.MyId))
                {
                    return string.Format(format, _clientService.GetTitle(message.ForwardInfo));
                }
                else if (message.IsOutgoing)
                {
                    if (!(chat.Type is ChatTypePrivate priv && priv.UserId == fromUser?.Id) && !message.IsChannelPost)
                    {
                        return string.Format(format, Strings.FromYou);
                    }
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

        private bool ShowFrom(IClientService clientService, Chat chat, Message message, out User senderUser, out Chat senderChat)
        {
            if (message.IsService())
            {
                senderUser = null;
                senderChat = null;
                return false;
            }

            if (message.IsOutgoing)
            {
                senderChat = null;
                return clientService.TryGetUser(message.SenderId, out senderUser)
                    || clientService.TryGetChat(message.SenderId, out senderChat);
            }

            if (chat.Type is ChatTypeBasicGroup)
            {
                senderChat = null;
                return clientService.TryGetUser(message.SenderId, out senderUser);
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
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
                return UpdateTimeLabel(lastMessage);
            }

            return string.Empty;
        }

        private string UpdateTimeLabel(Message message)
        {
            return Formatter.DateExtended(message.Date);
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var tooltip = sender as ToolTip;
            if (tooltip != null && _chat != null)
            {
                if (SettingsService.Current.Diagnostics.ChatPreviewToolTip)
                {
                    var playback = TLContainer.Current.Playback;
                    var settings = TLContainer.Current.Resolve<ISettingsService>(_clientService.SessionId);

                    var hidePreview = !settings.Notifications.GetShowPreview(_chat);
                    hidePreview |= _chat.HasProtectedContent;
                    hidePreview |= _chat.LastMessage == null;
                    hidePreview |= _chat.Type is ChatTypeSecret;

                    if (hidePreview || _chat.LastMessage.IsService())
                    {
                        tooltip.IsOpen = false;
                        return;
                    }

                    var delegato = new ChatMessageDelegate(_clientService, settings, _chat);
                    var message = new MessageViewModel(_clientService, playback, delegato, _chat.LastMessage);

                    var bubble = new MessageBubble();
                    if (message.IsOutgoing && !message.IsChannelPost)
                    {
                        bubble.Resources = new ThemeOutgoing();
                    }

                    // TODO: this is not correct, as MessageViewModel doesn't
                    // hold a strong reference to the IMessageDelegate object.
                    bubble.Tag = delegato;
                    bubble.UpdateMessage(message);

                    var grid = new Grid();
                    grid.Children.Add(bubble);

                    if (bubble.HasFloatingElements)
                    {
                        var presenter = new ChatBackgroundPresenter();
                        presenter.Update(_clientService, null);
                        presenter.CornerRadius = new CornerRadius(15);

                        void handler(object sender, SizeChangedEventArgs args)
                        {
                            bubble.SizeChanged -= handler;
                            presenter.Width = args.NewSize.Width;
                            presenter.Height = args.NewSize.Height;
                        }

                        bubble.SizeChanged += handler;

                        var canvas = new Canvas();
                        canvas.Children.Add(presenter);
                        grid.Children.Insert(0, canvas);
                    }

                    tooltip.Content = grid;
                    tooltip.Padding = new Thickness();
                    tooltip.CornerRadius = new CornerRadius(15);

                    if (message.ReplyToMessageId != 0 ||
                        message.Content is MessagePinMessage ||
                        message.Content is MessageGameScore ||
                        message.Content is MessagePaymentSuccessful)
                    {
                        message.ReplyToMessageState = ReplyToMessageState.Loading;

                        _clientService.Send(new GetRepliedMessage(message.ChatId, message.Id), response =>
                        {
                            if (response is Message result)
                            {
                                message.ReplyToMessage = new MessageViewModel(_clientService, playback, delegato, result);
                                message.ReplyToMessageState = ReplyToMessageState.None;
                            }
                            else
                            {
                                message.ReplyToMessageState = ReplyToMessageState.Deleted;
                            }

                            this.BeginOnUIThread(() =>
                            {
                                if (tooltip.IsOpen)
                                {
                                    bubble.UpdateMessageReply(message);
                                }
                            });
                        });
                    }
                }
                else if (BriefLabel.IsTextTrimmed)
                {
                    tooltip.Content = FromLabel.Text + BriefLabel.Text;
                }
                else
                {
                    tooltip.IsOpen = false;
                }
            }
        }

        private void Tooltip_Closed(object sender, RoutedEventArgs e)
        {
            if (sender is ToolTip tooltip)
            {
                tooltip.Content = new object();
            }
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
                if (service != null)
                {
                    App.DataPackages[chat.Id] = e.DataView;
                    service.NavigateToChat(chat);
                }
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
            Photo.Source = type is ChatTypeSupergroup ? PlaceholderHelper.GetNameForChat(title, 48, color) : PlaceholderHelper.GetNameForUser(title, 48, color);

            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
            VisualStateManager.GoToState(this, muted ? "Muted" : "Unmuted", false);

            MinithumbnailPanel.Visibility = Visibility.Collapsed;

            PinnedIcon.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Value = unread;
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            FromLabel.Text = from;
            BriefLabel.Text = message;
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
            if (oldValue != null && _selectionStrokeToken != 0)
            {
                oldValue.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _selectionStrokeToken);
                _selectionStrokeToken = 0;
            }

            if (newValue == null || _stroke == null)
            {
                return;
            }

            _stroke.FillBrush = Window.Current.Compositor.CreateColorBrush(newValue.Color);
            _selectionStrokeToken = newValue.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnSelectionStrokeChanged);
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

        public void UpdateState(bool selected, bool animate)
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
            if (oldValue != null && _strokeToken != 0)
            {
                oldValue.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _strokeToken);
                _strokeToken = 0;
            }

            if (newValue == null || _container == null || _ellipse == null)
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(newValue.Color);

            foreach (var shape in _shapes)
            {
                shape.StrokeBrush = brush;
            }

            _ellipse.FillBrush = brush;
            _strokeToken = newValue.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, OnStrokeChanged);
        }

        private void OnStrokeChanged(DependencyObject sender, DependencyProperty dp)
        {
            var solid = sender as SolidColorBrush;
            if (solid == null || _container == null || _ellipse == null)
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(solid.Color);

            foreach (var shape in _shapes)
            {
                shape.StrokeBrush = brush;
            }

            _ellipse.FillBrush = brush;
        }

        #endregion

        private CompositionBrush GetBrush(DependencyProperty dp, ref long token, DependencyPropertyChangedCallback callback)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
                if (token == 0)
                {
                    token = solid.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, callback);
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

    public class ChatCellPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var TitleLabel = Children[0];
            var Identity = Children[1];
            var MutedIcon = Children[2];
            var TimeLabel = Children[3];

            var FromLabel = Children[4];
            var MinithumbnailPanel = Children[5];
            var BriefLabel = Children[6];

            var shift = 0;
            var CustomEmoji = default(CustomEmojiCanvas);

            if (Children[7] is CustomEmojiCanvas)
            {
                shift++;
                CustomEmoji = Children[7] as CustomEmojiCanvas;
            }

            var ChatActionIndicator = Children[7 + shift];
            var TypingLabel = Children[8 + shift];
            var PinnedIcon = Children[9 + shift];
            var UnreadMentionsBadge = Children[10 + shift];
            var UnreadBadge = Children[11 + shift];

            TimeLabel.Measure(availableSize);
            Identity.Measure(availableSize);
            MutedIcon.Measure(availableSize);

            var line1Left = 12;
            var line1Right = availableSize.Width - 12 - TimeLabel.DesiredSize.Width;

            var titleWidth = Math.Max(0, line1Right - (line1Left + Identity.DesiredSize.Width + MutedIcon.DesiredSize.Width));

            TitleLabel.Measure(new Size(titleWidth, availableSize.Height));



            MinithumbnailPanel.Measure(availableSize);
            ChatActionIndicator.Measure(availableSize);
            PinnedIcon.Measure(availableSize);
            UnreadBadge.Measure(availableSize);
            UnreadMentionsBadge.Measure(availableSize);

            var line2RightPadding = Math.Max(PinnedIcon.DesiredSize.Width, UnreadBadge.DesiredSize.Width);

            var line2Left = 12d;
            var line2Right = availableSize.Width - 8 - line2RightPadding - UnreadMentionsBadge.DesiredSize.Width;

            var briefWidth = Math.Max(0, line2Right - line2Left - MinithumbnailPanel.DesiredSize.Width);

            FromLabel.Measure(new Size(briefWidth, availableSize.Height));
            BriefLabel.Measure(new Size(Math.Max(0, briefWidth - FromLabel.DesiredSize.Width), availableSize.Height));
            CustomEmoji?.Measure(new Size(briefWidth, availableSize.Height));
            TypingLabel.Measure(new Size(briefWidth + MinithumbnailPanel.DesiredSize.Width, availableSize.Height));

            if (Children.Count > 12 + shift)
            {
                Children[12 + shift].Measure(availableSize);
            }

            return base.MeasureOverride(availableSize);

            return new Size(availableSize.Width, 64);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var TitleLabel = Children[0];
            var Identity = Children[1];
            var MutedIcon = Children[2];
            var TimeLabel = Children[3];

            var FromLabel = Children[4];
            var MinithumbnailPanel = Children[5];
            var BriefLabel = Children[6];

            var shift = 0;
            var CustomEmoji = default(CustomEmojiCanvas);

            if (Children[7] is CustomEmojiCanvas)
            {
                shift++;
                CustomEmoji = Children[7] as CustomEmojiCanvas;
            }

            var ChatActionIndicator = Children[7 + shift];
            var TypingLabel = Children[8 + shift];
            var PinnedIcon = Children[9 + shift];
            var UnreadMentionsBadge = Children[10 + shift];
            var UnreadBadge = Children[11 + shift];

            var rect = new Rect();
            var min = 12d;

            rect.X = Math.Max(min, finalSize.Width - 8 - TimeLabel.DesiredSize.Width);
            rect.Y = 13;
            rect.Width = TimeLabel.DesiredSize.Width;
            rect.Height = TimeLabel.DesiredSize.Height;
            TimeLabel.Arrange(rect);

            var line1Right = finalSize.Width - 8 - TimeLabel.DesiredSize.Width;

            double titleWidth;
            if (min + TitleLabel.DesiredSize.Width + Identity.DesiredSize.Width + MutedIcon.DesiredSize.Width > line1Right)
            {
                titleWidth = Math.Max(0, line1Right - (min + Identity.DesiredSize.Width + MutedIcon.DesiredSize.Width));
            }
            else
            {
                titleWidth = TitleLabel.DesiredSize.Width;
            }

            rect.X = min;
            rect.Y = 12;
            rect.Width = titleWidth;
            rect.Height = TitleLabel.DesiredSize.Height;
            TitleLabel.Arrange(rect);

            rect.X = min + titleWidth;
            rect.Y = 14;
            rect.Width = Identity.DesiredSize.Width;
            rect.Height = Identity.DesiredSize.Height;
            Identity.Arrange(rect);

            rect.X = min + titleWidth + Identity.DesiredSize.Width;
            rect.Y = 14;
            rect.Width = MutedIcon.DesiredSize.Width;
            rect.Height = MutedIcon.DesiredSize.Height;
            MutedIcon.Arrange(rect);



            rect.X = Math.Max(min, finalSize.Width - 8 - PinnedIcon.DesiredSize.Width);
            rect.Y = 34;
            rect.Width = PinnedIcon.DesiredSize.Width;
            rect.Height = PinnedIcon.DesiredSize.Height;
            PinnedIcon.Arrange(rect);

            rect.X = finalSize.Width - 8 - UnreadBadge.DesiredSize.Width;
            rect.Y = 36;
            rect.Width = UnreadBadge.DesiredSize.Width;
            rect.Height = UnreadBadge.DesiredSize.Height;
            UnreadBadge.Arrange(rect);

            rect.X = finalSize.Width - 8 - UnreadBadge.DesiredSize.Width - UnreadMentionsBadge.DesiredSize.Width;
            rect.Y = 36;
            rect.Width = UnreadMentionsBadge.DesiredSize.Width;
            rect.Height = UnreadMentionsBadge.DesiredSize.Height;
            UnreadMentionsBadge.Arrange(rect);

            var line2RightPadding = Math.Max(PinnedIcon.DesiredSize.Width, UnreadBadge.DesiredSize.Width);

            var line2Left = min;
            var line2Right = finalSize.Width - 8 - line2RightPadding - UnreadMentionsBadge.DesiredSize.Width;

            rect.X = min;
            rect.Y = 34;
            rect.Width = FromLabel.DesiredSize.Width;
            rect.Height = FromLabel.DesiredSize.Height;
            FromLabel.Arrange(rect);

            rect.X = min + FromLabel.DesiredSize.Width;
            rect.Y = 34;
            rect.Width = MinithumbnailPanel.DesiredSize.Width;
            rect.Height = MinithumbnailPanel.DesiredSize.Height;
            MinithumbnailPanel.Arrange(rect);

            rect.X = min + FromLabel.DesiredSize.Width + MinithumbnailPanel.DesiredSize.Width;
            rect.Y = 34;
            rect.Width = BriefLabel.DesiredSize.Width;
            rect.Height = BriefLabel.DesiredSize.Height;
            BriefLabel.Arrange(rect);

            if (CustomEmoji != null)
            {
                rect.X -= 2;
                rect.Y -= 2;
                rect.Width += 4;
                rect.Height += 4;
                CustomEmoji.Arrange(rect);
            }

            rect.X = min;
            rect.Y = 34;
            rect.Width = ChatActionIndicator.DesiredSize.Width;
            rect.Height = ChatActionIndicator.DesiredSize.Height;
            ChatActionIndicator.Arrange(rect);

            line2Left = min + ChatActionIndicator.DesiredSize.Width;
            line2Right = finalSize.Width - 8 - line2RightPadding - UnreadMentionsBadge.DesiredSize.Width;

            var typingLabel = Math.Max(0, line2Right - line2Left);

            rect.X = min + ChatActionIndicator.DesiredSize.Width;
            rect.Y = 34;
            rect.Width = typingLabel;
            rect.Height = TypingLabel.DesiredSize.Height;
            TypingLabel.Arrange(rect);

            if (Children.Count > 12 + shift)
            {
                Children[12 + shift].Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            }

            return finalSize;
        }
    }
}
