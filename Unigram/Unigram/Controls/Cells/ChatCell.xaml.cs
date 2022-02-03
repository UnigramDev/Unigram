using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Common.Chats;
using Unigram.Controls.Chats;
using Unigram.Controls.Messages;
using Unigram.Converters;
using Unigram.Native;
using Unigram.Navigation;
using Unigram.Navigation.Services;
using Unigram.Services;
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

namespace Unigram.Controls.Cells
{
    public enum MessageTicksState
    {
        None,
        Pending,
        Failed,
        Sent,
        Read
    }

    public sealed class ChatCell : Control, IMultipleElement
    {
        private Chat _chat;
        private ChatList _chatList;

        private Message _message;

        private IProtoService _protoService;

        private Visual _onlineBadge;
        private bool _onlineCall;

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

        #region InitializeComponent

        private Grid PhotoPanel;
        private TextBlock TypeIcon;
        private TextBlock TitleLabel;
        private FontIcon VerifiedIcon;
        private FontIcon MutedIcon;
        private FontIcon StateIcon;
        private TextBlock TimeLabel;
        private Border MinithumbnailPanel;
        private TextBlock BriefInfo;
        private ChatActionIndicator ChatActionIndicator;
        private TextBlock TypingLabel;
        private Border PinnedIcon;
        private Border UnreadMentionsBadge;
        private Border UnreadBadge;
        private Border FailedBadge;
        private Rectangle DropVisual;
        private TextBlock FailedLabel;
        private TextBlock UnreadLabel;
        private TextBlock UnreadMentionsLabel;
        private Run FromLabel;
        private Run DraftLabel;
        private Run BriefLabel;
        private Image Minithumbnail;
        private Ellipse SelectionOutline;
        private ProfilePicture Photo;
        private Border OnlineBadge;
        private Border OnlineHeart;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            PhotoPanel = GetTemplateChild(nameof(PhotoPanel)) as Grid;
            TypeIcon = GetTemplateChild(nameof(TypeIcon)) as TextBlock;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            VerifiedIcon = GetTemplateChild(nameof(VerifiedIcon)) as FontIcon;
            MutedIcon = GetTemplateChild(nameof(MutedIcon)) as FontIcon;
            StateIcon = GetTemplateChild(nameof(StateIcon)) as FontIcon;
            TimeLabel = GetTemplateChild(nameof(TimeLabel)) as TextBlock;
            MinithumbnailPanel = GetTemplateChild(nameof(MinithumbnailPanel)) as Border;
            BriefInfo = GetTemplateChild(nameof(BriefInfo)) as TextBlock;
            ChatActionIndicator = GetTemplateChild(nameof(ChatActionIndicator)) as ChatActionIndicator;
            TypingLabel = GetTemplateChild(nameof(TypingLabel)) as TextBlock;
            PinnedIcon = GetTemplateChild(nameof(PinnedIcon)) as Border;
            UnreadMentionsBadge = GetTemplateChild(nameof(UnreadMentionsBadge)) as Border;
            UnreadBadge = GetTemplateChild(nameof(UnreadBadge)) as Border;
            FailedBadge = GetTemplateChild(nameof(FailedBadge)) as Border;
            DropVisual = GetTemplateChild(nameof(DropVisual)) as Rectangle;
            FailedLabel = GetTemplateChild(nameof(FailedLabel)) as TextBlock;
            UnreadLabel = GetTemplateChild(nameof(UnreadLabel)) as TextBlock;
            UnreadMentionsLabel = GetTemplateChild(nameof(UnreadMentionsLabel)) as TextBlock;
            FromLabel = GetTemplateChild(nameof(FromLabel)) as Run;
            DraftLabel = GetTemplateChild(nameof(DraftLabel)) as Run;
            BriefLabel = GetTemplateChild(nameof(BriefLabel)) as Run;
            Minithumbnail = GetTemplateChild(nameof(Minithumbnail)) as Image;
            SelectionOutline = GetTemplateChild(nameof(SelectionOutline)) as Ellipse;
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;

            var tooltip = new ToolTip();
            tooltip.Opened += ToolTip_Opened;

            ToolTipService.SetToolTip(BriefInfo, tooltip);

            InitializeSelection();
            InitializeTicks();

            _templateApplied = true;

            if (_chat != null)
            {
                UpdateChat(_protoService, _chat, _chatList);
            }
            else if (_chatList != null)
            {
                UpdateChatList(_protoService, _chatList);
            }
            else if (_message != null)
            {
                UpdateMessage(_protoService, _message);
            }
        }

        #endregion

        public void UpdateService(IProtoService protoService)
        {
            _protoService = protoService;
        }

        public void UpdateChat(IProtoService protoService, Chat chat, ChatList chatList)
        {
            _protoService = protoService;

            Update(chat, chatList);
        }

        public void UpdateMessage(IProtoService protoService, Message message)
        {
            _protoService = protoService;
            _message = message;

            if (!_templateApplied)
            {
                return;
            }

            var chat = protoService.GetChat(message.ChatId);
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

            DraftLabel.Text = string.Empty;
            FromLabel.Text = UpdateFromLabel(chat, message);
            BriefLabel.Text = UpdateBriefLabel(chat, message, true, false);
            TimeLabel.Text = UpdateTimeLabel(message);
            StateIcon.Glyph = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, null, message, message.SendingState);

            UpdateBriefSpoiler(chat.DraftMessage == null ? message : null);
            UpdateMinithumbnail(message);
        }

        public async void UpdateChatList(IProtoService protoService, ChatList chatList)
        {
            _protoService = protoService;
            _chatList = chatList;

            var response = await protoService.GetChatListAsync(chatList, 0, 20);
            if (response is Telegram.Td.Api.Chats chats)
            {
                Visibility = chats.ChatIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                if (!_templateApplied)
                {
                    return;
                }

                TitleLabel.Text = Strings.Resources.ArchivedChats;
                Photo.Source = PlaceholderHelper.GetGlyph(Icons.Archive, 0, 96);

                TypeIcon.Text = string.Empty;
                TypeIcon.Visibility = Visibility.Collapsed;
                VerifiedIcon.Visibility = Visibility.Collapsed;
                UnreadMentionsBadge.Visibility = Visibility.Collapsed;
                PinnedIcon.Visibility = Visibility.Collapsed;

                DraftLabel.Text = string.Empty;
                TimeLabel.Text = string.Empty;
                StateIcon.Glyph = string.Empty;
                FailedBadge.Visibility = Visibility.Collapsed;

                MutedIcon.Visibility = Visibility.Collapsed;

                MinithumbnailPanel.Visibility = Visibility.Collapsed;

                VisualStateManager.GoToState(this, "Muted", false);

                UpdateTicks(null);

                var unreadCount = protoService.GetUnreadCount(chatList);
                UnreadBadge.Visibility = unreadCount.UnreadChatCount.UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
                UnreadLabel.Text = $"{unreadCount.UnreadChatCount.UnreadCount}";

                BriefInfo.Inlines.Clear();

                foreach (var id in chats.ChatIds)
                {
                    var chat = protoService.GetChat(id);
                    if (chat == null)
                    {
                        continue;
                    }

                    if (BriefInfo.Inlines.Count > 0)
                    {
                        BriefInfo.Inlines.Add(new Run { Text = ", " });
                    }

                    var run = new Run { Text = _protoService.GetTitle(chat) };
                    if (chat.IsUnread())
                    {
                        run.Foreground = new SolidColorBrush(ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black);
                    }

                    BriefInfo.Inlines.Add(run);
                }
            }
        }

        public string GetAutomationName()
        {
            if (_protoService == null)
            {
                return null;
            }

            if (_chat != null)
            {
                return UpdateAutomation(_protoService, _chat, _chat.LastMessage);
            }
            else if (_message != null)
            {
                var chat = _protoService.GetChat(_message.ChatId);
                if (chat != null)
                {
                    return UpdateAutomation(_protoService, chat, _message);
                }
            }

            return null;
        }

        private string UpdateAutomation(IProtoService protoService, Chat chat, Message message)
        {
            var builder = new StringBuilder();
            if (chat.Type is ChatTypeSecret)
            {
                builder.Append(Strings.Resources.AccDescrSecretChat);
                builder.Append(". ");
            }

            if (chat.Type is ChatTypePrivate or ChatTypeSecret)
            {
                var user = protoService.GetUser(chat);
                if (user != null)
                {
                    if (user.Type is UserTypeBot)
                    {
                        builder.Append(Strings.Resources.Bot);
                        builder.Append(", ");
                    }
                    if (user.Id == protoService.Options.MyId)
                    {
                        builder.Append(Strings.Resources.SavedMessages);
                    }
                    else
                    {
                        builder.Append(user.GetFullName());
                    }

                    builder.Append(", ");
                }
            }
            else
            {
                if (chat.Type is ChatTypeSupergroup super && super.IsChannel)
                {
                    builder.Append(Strings.Resources.AccDescrChannel);
                }
                else
                {
                    builder.Append(Strings.Resources.AccDescrGroup);
                }

                builder.Append(", ");
                builder.Append(protoService.GetTitle(chat));
                builder.Append(", ");
            }

            if (chat.UnreadCount > 0)
            {
                builder.Append(Locale.Declension("NewMessages", chat.UnreadCount));
                builder.Append(", ");
            }

            if (message == null)
            {
                //AutomationProperties.SetName(this, builder.ToString());
                return builder.ToString();
            }

            //if (!message.IsOutgoing && message.SenderUserId != 0 && !message.IsService())
            if (ShowFrom(protoService, chat, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsOutgoing)
                {
                    if (!(chat.Type is ChatTypePrivate priv && priv.UserId == fromUser?.Id) && !message.IsChannelPost)
                    {
                        builder.Append(Strings.Resources.FromYou);
                        builder.Append(": ");
                    }
                }
                else if (fromUser != null)
                {
                    builder.Append(fromUser.GetFullName());
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
                builder.Append(Automation.GetSummary(protoService, message));
            }

            var date = Locale.FormatDateAudio(message.Date);
            if (message.IsOutgoing)
            {
                builder.Append(string.Format(Strings.Resources.AccDescrSentDate, date));
            }
            else
            {
                builder.Append(string.Format(Strings.Resources.AccDescrReceivedDate, date));
            }

            //AutomationProperties.SetName(this, builder.ToString());
            return builder.ToString();
        }

        #region Updates

        public void UpdateChatLastMessage(Chat chat, ChatPosition position = null)
        {
            if (chat == null || !_templateApplied)
            {
                return;
            }

            if (position == null)
            {
                position = chat.GetPosition(_chatList);
            }

            DraftLabel.Text = UpdateDraftLabel(chat);
            FromLabel.Text = UpdateFromLabel(chat, position);
            BriefLabel.Text = UpdateBriefLabel(chat, position);
            TimeLabel.Text = UpdateTimeLabel(chat, position);
            StateIcon.Glyph = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
            FailedBadge.Visibility = chat.LastMessage?.SendingState is MessageSendingStateFailed ? Visibility.Visible : Visibility.Collapsed;

            UpdateBriefSpoiler(chat.DraftMessage == null ? chat.LastMessage : null);
            UpdateMinithumbnail(chat.LastMessage);
        }

        public void UpdateChatReadInbox(Chat chat, ChatPosition position = null)
        {
            if (!_templateApplied)
            {
                return;
            }

            if (position == null)
            {
                position = chat.GetPosition(_chatList);
            }

            PinnedIcon.Visibility = chat.UnreadCount == 0 && !chat.IsMarkedAsUnread && (position?.IsPinned ?? false) ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = (chat.UnreadCount > 0 || chat.IsMarkedAsUnread) ? chat.UnreadMentionCount == 1 && chat.UnreadCount == 1 ? Visibility.Collapsed : Visibility.Visible : Visibility.Collapsed;
            UnreadLabel.Text = chat.UnreadCount > 0 ? chat.UnreadCount.ToString() : string.Empty;

            //UpdateAutomation(_protoService, chat, chat.LastMessage);
        }

        public void UpdateChatReadOutbox(Chat chat)
        {
            if (!_templateApplied)
            {
                return;
            }

            StateIcon.Glyph = UpdateStateIcon(chat.LastReadOutboxMessageId, chat, chat.DraftMessage, chat.LastMessage, chat.LastMessage?.SendingState);
        }

        public void UpdateChatIsMarkedAsUnread(Chat chat)
        {

        }

        public void UpdateChatUnreadMentionCount(Chat chat, ChatPosition position = null)
        {
            if (!_templateApplied)
            {
                return;
            }

            UpdateChatReadInbox(chat, position);
            UnreadMentionsBadge.Visibility = chat.UnreadMentionCount > 0 || chat.UnreadReactionCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadMentionsLabel.Text = chat.UnreadMentionCount > 0 ? Icons.Mention16 : Icons.HeartFilled16;
        }

        public void UpdateNotificationSettings(Chat chat)
        {
            if (!_templateApplied)
            {
                return;
            }

            var muted = _protoService.Notifications.GetMutedFor(chat) > 0;
            VisualStateManager.GoToState(this, muted ? "Muted" : "Unmuted", false);
            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateChatTitle(Chat chat)
        {
            if (!_templateApplied)
            {
                return;
            }

            TitleLabel.Text = _protoService.GetTitle(chat);
        }

        public void UpdateChatPhoto(Chat chat)
        {
            if (!_templateApplied)
            {
                return;
            }

            Photo.SetChat(_protoService, chat, 48);
        }

        public void UpdateChatActions(Chat chat, IDictionary<MessageSender, ChatAction> actions)
        {
            if (!_templateApplied)
            {
                return;
            }

            if (actions != null && actions.Count > 0)
            {
                TypingLabel.Text = InputChatActionManager.GetTypingString(chat, actions, _protoService.GetUser, _protoService.GetChat, out ChatAction commonAction);
                ChatActionIndicator.UpdateAction(commonAction);
                ChatActionIndicator.Visibility = Visibility.Visible;
                TypingLabel.Visibility = Visibility.Visible;
                BriefInfo.Visibility = Visibility.Collapsed;
                Minithumbnail.Visibility = Visibility.Collapsed;
            }
            else
            {
                ChatActionIndicator.Visibility = Visibility.Collapsed;
                TypingLabel.Visibility = Visibility.Collapsed;
                BriefInfo.Visibility = Visibility.Visible;
                Minithumbnail.Visibility = Visibility.Visible;
            }
        }

        private void UpdateChatType(Chat chat)
        {
            var type = UpdateType(chat);
            TypeIcon.Text = type ?? string.Empty;
            TypeIcon.Visibility = type == null ? Visibility.Collapsed : Visibility.Visible;

            var verified = false;
            if (chat.Type is ChatTypePrivate privata)
            {
                verified = _protoService.GetUser(privata.UserId)?.IsVerified ?? false;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                verified = _protoService.GetSupergroup(super.SupergroupId)?.IsVerified ?? false;
            }

            VerifiedIcon.Visibility = verified ? Visibility.Visible : Visibility.Collapsed;
        }

        public void UpdateChatVideoChat(Chat chat)
        {
            if (!_templateApplied)
            {
                return;
            }

            UpdateOnlineBadge(chat.VideoChat?.HasParticipants ?? false, true);
        }

        public void UpdateUserStatus(Chat chat, UserStatus status)
        {
            if (!_templateApplied)
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

            //UpdateViewState(chat, ChatFilterMode.None, false, false);

            UpdateChatTitle(chat);
            UpdateChatPhoto(chat);
            UpdateChatType(chat);

            UpdateChatLastMessage(chat, position);
            //UpdateChatReadInbox(chat);
            UpdateChatUnreadMentionCount(chat, position);
            UpdateNotificationSettings(chat);
            UpdateChatActions(chat, _protoService.GetChatActions(chat.Id));

            if (_protoService.TryGetUser(chat, out User user) && user.Type is UserTypeRegular && user.Id != _protoService.Options.MyId && user.Id != 777000)
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

        public void UpdateViewState(Chat chat, bool compact)
        {
            VisualStateManager.GoToState(this, chat.Type is ChatTypeSecret ? "Secret" : "Normal", false);
            VisualStateManager.GoToState(this, compact ? "Compact" : "Expanded", false);
        }

        private void UpdateMinithumbnail(Message message)
        {
            var thumbnail = message?.GetMinithumbnail(false);
            if (thumbnail != null && SettingsService.Current.Diagnostics.Minithumbnails)
            {
                double ratioX = (double)16 / thumbnail.Width;
                double ratioY = (double)16 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };

                using (var stream = new InMemoryRandomAccessStream())
                {
                    PlaceholderImageHelper.Current.WriteBytes(thumbnail.Data, stream);
                    bitmap.SetSource(stream);
                }

                Minithumbnail.Source = bitmap;
                MinithumbnailPanel.Visibility = Visibility.Visible;
            }
            else
            {
                MinithumbnailPanel.Visibility = Visibility.Collapsed;
                Minithumbnail.Source = null;
            }
        }

        private void UpdateBriefSpoiler(Message message)
        {
            BriefInfo.TextHighlighters.Clear();
            TextHighlighter spoiler = null;

            var offset = FromLabel.Text.Length + DraftLabel.Text.Length;
            var entities = message?.Content switch
            {
                MessageAnimation animation => animation.Caption.Entities,
                MessageAudio audio => audio.Caption.Entities,
                MessageDocument document => document.Caption.Entities,
                MessagePhoto photo => photo.Caption.Entities,
                MessageVideo video => video.Caption.Entities,
                MessageVoiceNote voiceNote => voiceNote.Caption.Entities,
                MessageText text => text.Text.Entities,
                _ => null
            };

            if (entities == null)
            {
                return;
            }

            foreach (var entity in entities)
            {
                if (entity.Type is TextEntityTypeSpoiler)
                {
                    spoiler ??= new TextHighlighter();
                    spoiler.Ranges.Add(new TextRange { StartIndex = offset + entity.Offset, Length = entity.Length });
                }
            }

            if (spoiler?.Ranges.Count > 0)
            {
                spoiler.Foreground = BriefInfo.Foreground;
                spoiler.Background = BriefInfo.Foreground;

                BriefInfo.TextHighlighters.Add(spoiler);
            }

        }

        private string UpdateBriefLabel(Chat chat, ChatPosition position)
        {
            if (position?.Source is ChatSourcePublicServiceAnnouncement psa && !string.IsNullOrEmpty(psa.Text))
            {
                return psa.Text.Replace('\n', ' ');
            }

            var topMessage = chat.LastMessage;
            if (topMessage != null)
            {
                return UpdateBriefLabel(chat, topMessage, true, true);
            }
            else if (chat.Type is ChatTypeSecret secretType)
            {
                var secret = _protoService.GetSecretChat(secretType.SecretChatId);
                if (secret != null)
                {
                    if (secret.State is SecretChatStateReady)
                    {
                        return secret.IsOutbound ? string.Format(Strings.Resources.EncryptedChatStartedOutgoing, _protoService.GetTitle(chat)) : Strings.Resources.EncryptedChatStartedIncoming;
                    }
                    else if (secret.State is SecretChatStatePending)
                    {
                        return string.Format(Strings.Resources.AwaitingEncryption, _protoService.GetTitle(chat));
                    }
                    else if (secret.State is SecretChatStateClosed)
                    {
                        return Strings.Resources.EncryptionRejected;
                    }
                }
            }

            return string.Empty;
        }

        private string UpdateBriefLabel(Chat chat, Message value, bool showContent, bool draft)
        {
            //if (ViewModel.DraftMessage is DraftMessage draft && !string.IsNullOrWhiteSpace(draft.InputMessageText.ToString()))
            //{
            //    return draft.Message;
            //}

            if (chat.DraftMessage != null && draft)
            {
                switch (chat.DraftMessage.InputMessageText)
                {
                    case InputMessageText text:
                        return text.Text.Text.Replace('\n', ' ');
                }
            }

            //if (value is TLMessageEmpty messageEmpty)
            //{
            //    return string.Empty;
            //}

            //if (value is TLMessageService messageService)
            //{
            //    return string.Empty;
            //}

            if (!showContent)
            {
                return Strings.Resources.Message;
            }

            return value.Content switch
            {
                MessageAnimation animation => animation.Caption.Text.Replace('\n', ' '),
                MessageAudio audio => audio.Caption.Text.Replace('\n', ' '),
                MessageDocument document => document.Caption.Text.Replace('\n', ' '),
                MessagePhoto photo => photo.Caption.Text.Replace('\n', ' '),
                MessageVideo video => video.Caption.Text.Replace('\n', ' '),
                MessageVoiceNote voiceNote => voiceNote.Caption.Text.Replace('\n', ' '),
                MessageText text => text.Text.Text.Replace('\n', ' '),
                MessageAnimatedEmoji animatedEmoji => animatedEmoji.Emoji,
                MessageDice dice => dice.Emoji,
                _ => string.Empty,
            };
        }

        private string UpdateDraftLabel(Chat chat)
        {
            if (chat.DraftMessage != null)
            {
                switch (chat.DraftMessage.InputMessageText)
                {
                    case InputMessageText:
                        return string.Format("{0}: ", Strings.Resources.Draft);
                }
            }

            return string.Empty;
        }

        private string UpdateFromLabel(Chat chat, ChatPosition position)
        {
            if (position?.Source is ChatSourcePublicServiceAnnouncement psa && !string.IsNullOrEmpty(psa.Text))
            {
                return string.Empty;
            }
            else if (chat.DraftMessage != null)
            {
                switch (chat.DraftMessage.InputMessageText)
                {
                    case InputMessageText:
                        return string.Empty;
                }
            }

            var message = chat.LastMessage;
            if (message == null)
            {
                return string.Empty;
            }

            return UpdateFromLabel(chat, message);
        }

        private string UpdateFromLabel(Chat chat, Message message)
        {
            if (message.IsService())
            {
                return MessageService.GetText(new ViewModels.MessageViewModel(_protoService, null, null, message));
            }

            var format = "{0}: ";
            var result = string.Empty;

            if (ShowFrom(_protoService, chat, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsSaved(_protoService.Options.MyId))
                {
                    result = string.Format(format, _protoService.GetTitle(message.ForwardInfo));
                }
                else if (message.IsOutgoing)
                {
                    if (!(chat.Type is ChatTypePrivate priv && priv.UserId == fromUser?.Id) && !message.IsChannelPost)
                    {
                        result = string.Format(format, Strings.Resources.FromYou);
                    }
                }
                else if (fromUser != null)
                {
                    if (!string.IsNullOrEmpty(fromUser.FirstName))
                    {
                        result = string.Format(format, fromUser.FirstName.Trim());
                    }
                    else if (!string.IsNullOrEmpty(fromUser.LastName))
                    {
                        result = string.Format(format, fromUser.LastName.Trim());
                    }
                    else if (!string.IsNullOrEmpty(fromUser.Username))
                    {
                        result = string.Format(format, fromUser.Username.Trim());
                    }
                    else if (fromUser.Type is UserTypeDeleted)
                    {
                        result = string.Format(format, Strings.Resources.HiddenName);
                    }
                    else
                    {
                        result = string.Format(format, fromUser.Id);
                    }
                }
                else if (fromChat != null && fromChat.Id != chat.Id)
                {
                    result = string.Format(format, fromChat.Title);
                }
            }

            if (message.Content is MessageGame gameMedia)
            {
                return result + "\uD83C\uDFAE " + gameMedia.Game.Title;
            }
            if (message.Content is MessageExpiredVideo)
            {
                return result + Strings.Resources.AttachVideoExpired;
            }
            else if (message.Content is MessageExpiredPhoto)
            {
                return result + Strings.Resources.AttachPhotoExpired;
            }
            else if (message.Content is MessageVideoNote)
            {
                return result + Strings.Resources.AttachRound;
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return result + Strings.Resources.AttachSticker;
                }

                return result + $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}";
            }

            static string GetCaption(string caption)
            {
                return string.IsNullOrEmpty(caption) ? string.Empty : ", ";
            }

            if (message.Content is MessageVoiceNote voiceNote)
            {
                return result + Strings.Resources.AttachAudio + GetCaption(voiceNote.Caption.Text);
            }
            else if (message.Content is MessageVideo video)
            {
                return result + (video.IsSecret ? Strings.Resources.AttachDestructingVideo : Strings.Resources.AttachVideo) + GetCaption(video.Caption.Text);
            }
            else if (message.Content is MessageAnimation animation)
            {
                return result + Strings.Resources.AttachGif + GetCaption(animation.Caption.Text);
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null || title == null)
                {
                    return result + Strings.Resources.AttachMusic + GetCaption(audio.Caption.Text);
                }
                else
                {
                    return $"{result}\uD83C\uDFB5 {performer} - {title}" + GetCaption(audio.Caption.Text);
                }
            }
            else if (message.Content is MessageDocument document)
            {
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    return result + Strings.Resources.AttachDocument + GetCaption(document.Caption.Text);
                }

                return result + document.Document.FileName + GetCaption(document.Caption.Text);
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return result + invoice.Title;
            }
            else if (message.Content is MessageContact)
            {
                return result + Strings.Resources.AttachContact;
            }
            else if (message.Content is MessageLocation location)
            {
                return result + (location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation);
            }
            else if (message.Content is MessageVenue)
            {
                return result + Strings.Resources.AttachLocation;
            }
            else if (message.Content is MessagePhoto photo)
            {
                return result + (photo.IsSecret ? Strings.Resources.AttachDestructingPhoto : Strings.Resources.AttachPhoto) + GetCaption(photo.Caption.Text);
            }
            else if (message.Content is MessagePoll poll)
            {
                return result + "\uD83D\uDCCA " + poll.Poll.Question;
            }
            else if (message.Content is MessageCall call)
            {
                return result + call.ToOutcomeText(message.IsOutgoing);
            }
            else if (message.Content is MessageUnsupported)
            {
                return result + Strings.Resources.UnsupportedAttachment;
            }

            return result;
        }

        private bool ShowFrom(ICacheService cacheService, Chat chat, Message message, out User senderUser, out Chat senderChat)
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
                return cacheService.TryGetUser(message.SenderId, out senderUser)
                    || cacheService.TryGetChat(message.SenderId, out senderChat);
            }

            if (chat.Type is ChatTypeBasicGroup)
            {
                senderChat = null;
                return cacheService.TryGetUser(message.SenderId, out senderUser);
            }

            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                senderUser = null;
                senderChat = null;
                return !supergroup.IsChannel
                    && cacheService.TryGetUser(message.SenderId, out senderUser)
                    || cacheService.TryGetChat(message.SenderId, out senderChat);
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
                if (chat.Type is ChatTypePrivate privata && privata.UserId == _protoService.Options.MyId)
                {
                    if (message.SendingState is MessageSendingStateFailed)
                    {
                        // TODO: 
                        return "\uE599"; // Failed
                    }
                    else if (message.SendingState is MessageSendingStatePending)
                    {
                        return "\uE600"; // Pending
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
                    return "\uE599"; // Failed
                }
                else if (message.SendingState is MessageSendingStatePending)
                {
                    UpdateTicks(null);

                    _ticksState = MessageTicksState.Pending;
                    return "\uE600"; // Pending
                }
                else if (message.Id <= maxId)
                {
                    UpdateTicks(true, _ticksState == MessageTicksState.Sent);

                    _ticksState = MessageTicksState.Read;
                    return _container != null ? "\uE603" : "\uE601"; // Read
                }

                UpdateTicks(false, _ticksState == MessageTicksState.Pending);

                _ticksState = MessageTicksState.Sent;
                return _container != null ? "\uE603" : "\uE602"; // Unread
            }

            UpdateTicks(null);

            _ticksState = MessageTicksState.None;
            return string.Empty;
        }

        private string UpdateTimeLabel(Chat chat, ChatPosition position)
        {
            if (position?.Source is ChatSourceMtprotoProxy)
            {
                return Strings.Resources.UseProxySponsor;
            }
            else if (position?.Source is ChatSourcePublicServiceAnnouncement psa)
            {
                var type = LocaleService.Current.GetString("PsaType_" + psa.Type);
                if (type.Length > 0)
                {
                    return type;
                }

                return Strings.Resources.PsaTypeDefault;
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
            return Converter.DateExtended(message.Date);
        }

        private string UpdateType(Chat chat)
        {
            if (chat.Type is ChatTypeSupergroup supergroup)
            {
                return supergroup.IsChannel ? Icons.MegaphoneFilled16 : Icons.PeopleFilled16;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                return Icons.PeopleFilled16;
            }
            else if (chat.Type is ChatTypeSecret)
            {
                return Icons.LockClosedFilled16;
            }
            else if (chat.Type is ChatTypePrivate privata && _protoService != null)
            {
                if (_protoService.IsRepliesChat(chat))
                {
                    return null;
                }

                var user = _protoService.GetUser(privata.UserId);
                if (user != null && user.Type is UserTypeBot)
                {
                    return Icons.BotFilled;
                }
            }

            return null;
        }

        private void ToolTip_Opened(object sender, RoutedEventArgs e)
        {
            var tooltip = sender as ToolTip;
            if (tooltip != null)
            {
                if (BriefInfo.IsTextTrimmed)
                {
                    tooltip.Content = BriefInfo.Text;
                }
                else
                {
                    tooltip.IsOpen = false;
                }
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
                if (_protoService.CanPostMessages(chat) && e.DataView.AvailableFormats.Count > 0)
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

                var service = WindowContext.GetForCurrentView().NavigationServices.GetByFrameId($"Main{_protoService.SessionId}") as NavigationService;
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
            TitleLabel.Text = title;
            Photo.Source = type is ChatTypeSupergroup ? PlaceholderHelper.GetNameForChat(title, 48, color) : PlaceholderHelper.GetNameForUser(title, 48, color);
            //UpdateChatType(chat);
            TypeIcon.Text = type is ChatTypeSupergroup ? Icons.People : string.Empty;
            TypeIcon.Visibility = type is ChatTypeSupergroup ? Visibility.Visible : Visibility.Collapsed;

            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
            VisualStateManager.GoToState(this, muted ? "Muted" : "Unmuted", false);

            VerifiedIcon.Visibility = Visibility.Collapsed;
            MinithumbnailPanel.Visibility = Visibility.Collapsed;

            PinnedIcon.Visibility = pinned ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadLabel.Text = unread.ToString();
            UnreadMentionsBadge.Visibility = Visibility.Collapsed;

            DraftLabel.Text = string.Empty;
            FromLabel.Text = from;
            BriefLabel.Text = message;
            TimeLabel.Text = Converter.ShortTime.Format(date);
            StateIcon.Glyph = sent ? "\uE601" : string.Empty;

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
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(ChatCell), new PropertyMetadata(default(Color), OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as ChatCell;
            var solid = e.NewValue as SolidColorBrush;

            sender._selectionStrokeToken = solid.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, sender.OnSelectionStrokeChanged);

            if (e.OldValue is SolidColorBrush old && sender._selectionStrokeToken != 0)
            {
                old.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, sender._selectionStrokeToken);
            }

            if (solid == null || sender._stroke == null)
            {
                return;
            }

            sender._stroke.FillBrush = Window.Current.Compositor.CreateColorBrush(solid.Color);
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
            shape2.FillBrush = GetBrush(StrokeProperty);

            var outer = compositor.CreateEllipseGeometry();
            outer.Radius = new Vector2(10);
            outer.Center = new Vector2(10);

            var shape3 = compositor.CreateSpriteShape();
            shape3.Geometry = outer;
            shape3.FillBrush = GetBrush(SelectionStrokeProperty);

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

            _selectionPhoto = ElementCompositionPreview.GetElementVisual(Photo);
            _selectionOutline = ElementCompositionPreview.GetElementVisual(SelectionOutline);
            _selectionPhoto.CenterPoint = new Vector3(24);
            _selectionOutline.CenterPoint = new Vector3(24);
            _selectionOutline.Opacity = 0;
        }

        public void UpdateState(bool selected, bool animate)
        {
            if (animate)
            {
                var compositor = Window.Current.Compositor;

                var anim3 = compositor.CreateScalarKeyFrameAnimation();
                anim3.InsertKeyFrame(selected ? 0 : 1, 0);
                anim3.InsertKeyFrame(selected ? 1 : 0, 1);

                if (_visual != null)
                {
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
                }


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
                if (_visual != null)
                {
                    _polygon.TrimEnd = selected ? 1 : 0;
                    _visual.Scale = new Vector3(selected ? 1 : 0);
                    _visual.Opacity = selected ? 1 : 0;
                }

                _selectionPhoto.Scale = new Vector3(selected ? 40f / 48f : 1);
                _selectionOutline.Scale = new Vector3(selected ? 1 : 40f / 48f);
                _selectionOutline.Opacity = selected ? 1 : 0;
            }
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
            var sender = d as ChatCell;
            var solid = e.NewValue as SolidColorBrush;

            if (e.OldValue is SolidColorBrush old && sender._strokeToken != 0)
            {
                old.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, sender._strokeToken);
            }

            sender._strokeToken = solid.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, sender.OnStrokeChanged);

            if (solid == null || sender._container == null || sender._ellipse == null)
            {
                return;
            }

            var brush = Window.Current.Compositor.CreateColorBrush(solid.Color);

            foreach (var shape in sender._shapes)
            {
                shape.StrokeBrush = brush;
            }

            sender._ellipse.FillBrush = brush;
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

        private CompositionBrush GetBrush(DependencyProperty dp)
        {
            var value = GetValue(dp);
            if (value is SolidColorBrush solid)
            {
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
            shape11.StrokeBrush = GetBrush(StrokeProperty);
            shape11.IsStrokeNonScaling = true;
            shape11.StrokeStartCap = CompositionStrokeCap.Round;

            var shape12 = Window.Current.Compositor.CreateSpriteShape(line12);
            shape12.StrokeThickness = stroke;
            shape12.StrokeBrush = GetBrush(StrokeProperty);
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
            shape21.StrokeBrush = GetBrush(StrokeProperty);
            shape21.StrokeStartCap = CompositionStrokeCap.Round;

            var shape22 = Window.Current.Compositor.CreateSpriteShape(line22);
            shape22.StrokeThickness = stroke;
            shape22.StrokeBrush = GetBrush(StrokeProperty);
            shape22.StrokeEndCap = CompositionStrokeCap.Round;

            var visual2 = Window.Current.Compositor.CreateShapeVisual();
            visual2.Shapes.Add(shape22);
            visual2.Shapes.Add(shape21);
            visual2.Size = new Vector2(width, height);


            var container = Window.Current.Compositor.CreateSpriteVisual();
            container.Children.InsertAtTop(visual2);
            container.Children.InsertAtTop(visual1);
            container.Size = new Vector2(width, height);

            ElementCompositionPreview.SetElementChildVisual(StateIcon, container);

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
            if (_container == null)
            {
                return;
            }

            if (read == null)
            {
                _container.IsVisible = false;
            }
            else if (animate)
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

    }

    public class ChatCellPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var PhotoPanel = Children[0];

            var TypeIcon = Children[1];
            var TitleLabel = Children[2];
            var VerifiedIcon = Children[3];
            var MutedIcon = Children[4];
            var StateIcon = Children[5];
            var TimeLabel = Children[6];

            var MinithumbnailPanel = Children[7];
            var BriefInfo = Children[8];
            var ChatActionIndicator = Children[9];
            var TypingLabel = Children[10];
            var PinnedIcon = Children[11];
            var UnreadMentionsBadge = Children[12];
            var UnreadBadge = Children[13];
            var FailedBadge = Children[14];

            PhotoPanel.Measure(availableSize);

            TimeLabel.Measure(availableSize);
            StateIcon.Measure(availableSize);
            TypeIcon.Measure(availableSize);
            VerifiedIcon.Measure(availableSize);
            MutedIcon.Measure(availableSize);

            var line1Left = 8 + PhotoPanel.DesiredSize.Width + 12 + TypeIcon.DesiredSize.Width;
            var line1Right = availableSize.Width - 12 - TimeLabel.DesiredSize.Width - StateIcon.DesiredSize.Width;

            var titleWidth = Math.Max(0, line1Right - (line1Left + VerifiedIcon.DesiredSize.Width + MutedIcon.DesiredSize.Width));

            TitleLabel.Measure(new Size(titleWidth, availableSize.Height));



            MinithumbnailPanel.Measure(availableSize);
            ChatActionIndicator.Measure(availableSize);
            PinnedIcon.Measure(availableSize);
            UnreadBadge.Measure(availableSize);
            UnreadMentionsBadge.Measure(availableSize);
            FailedBadge.Measure(availableSize);

            var line2RightPadding = Math.Max(Math.Max(PinnedIcon.DesiredSize.Width, UnreadBadge.DesiredSize.Width), FailedBadge.DesiredSize.Width);

            var line2Left = 8 + PhotoPanel.DesiredSize.Width + 12 + MinithumbnailPanel.DesiredSize.Width;
            var line2Right = availableSize.Width - 8 - line2RightPadding - UnreadMentionsBadge.DesiredSize.Width;

            var briefWidth = Math.Max(0, line2Right - line2Left);

            BriefInfo.Measure(new Size(briefWidth, availableSize.Height));
            TypingLabel.Measure(new Size(briefWidth + MinithumbnailPanel.DesiredSize.Width + ChatActionIndicator.DesiredSize.Width, availableSize.Height));

            if (Children.Count > 14)
            {
                Children[14].Measure(availableSize);
            }

            return base.MeasureOverride(availableSize);

            return new Size(availableSize.Width, PhotoPanel.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var PhotoPanel = Children[0];

            var TypeIcon = Children[1];
            var TitleLabel = Children[2];
            var VerifiedIcon = Children[3];
            var MutedIcon = Children[4];
            var StateIcon = Children[5];
            var TimeLabel = Children[6];

            var MinithumbnailPanel = Children[7];
            var BriefInfo = Children[8];
            var ChatActionIndicator = Children[9];
            var TypingLabel = Children[10];
            var PinnedIcon = Children[11];
            var UnreadMentionsBadge = Children[12];
            var UnreadBadge = Children[13];
            var FailedBadge = Children[14];

            var rect = new Rect();
            var min = 8 + PhotoPanel.DesiredSize.Width + 12;

            rect.X = 8;
            rect.Y = 0;
            rect.Width = PhotoPanel.DesiredSize.Width;
            rect.Height = PhotoPanel.DesiredSize.Height;
            PhotoPanel.Arrange(rect);

            rect.X = Math.Max(min, finalSize.Width - 8 - TimeLabel.DesiredSize.Width);
            rect.Y = 13;
            rect.Width = TimeLabel.DesiredSize.Width;
            rect.Height = TimeLabel.DesiredSize.Height;
            TimeLabel.Arrange(rect);

            rect.X = Math.Max(min, finalSize.Width - 8 - TimeLabel.DesiredSize.Width - StateIcon.DesiredSize.Width);
            rect.Y = 13;
            rect.Width = StateIcon.DesiredSize.Width;
            rect.Height = StateIcon.DesiredSize.Height;
            StateIcon.Arrange(rect);

            rect.X = min;
            rect.Y = 13;
            rect.Width = TypeIcon.DesiredSize.Width;
            rect.Height = TypeIcon.DesiredSize.Height;
            TypeIcon.Arrange(rect);

            var line1Left = min + TypeIcon.DesiredSize.Width;
            var line1Right = finalSize.Width - 8 - TimeLabel.DesiredSize.Width - StateIcon.DesiredSize.Width;

            double titleWidth;
            if (line1Left + TitleLabel.DesiredSize.Width + VerifiedIcon.DesiredSize.Width + MutedIcon.DesiredSize.Width > line1Right)
            {
                titleWidth = Math.Max(0, line1Right - (line1Left + VerifiedIcon.DesiredSize.Width + MutedIcon.DesiredSize.Width));
            }
            else
            {
                titleWidth = TitleLabel.DesiredSize.Width;
            }

            rect.X = min + TypeIcon.DesiredSize.Width;
            rect.Y = 12;
            rect.Width = titleWidth;
            rect.Height = TitleLabel.DesiredSize.Height;
            TitleLabel.Arrange(rect);

            rect.X = min + TypeIcon.DesiredSize.Width + titleWidth;
            rect.Y = 13;
            rect.Width = VerifiedIcon.DesiredSize.Width;
            rect.Height = VerifiedIcon.DesiredSize.Height;
            VerifiedIcon.Arrange(rect);

            rect.X = min + TypeIcon.DesiredSize.Width + titleWidth + VerifiedIcon.DesiredSize.Width;
            rect.Y = 13;
            rect.Width = MutedIcon.DesiredSize.Width;
            rect.Height = MutedIcon.DesiredSize.Height;
            MutedIcon.Arrange(rect);



            rect.X = min;
            rect.Y = 36;
            rect.Width = MinithumbnailPanel.DesiredSize.Width;
            rect.Height = MinithumbnailPanel.DesiredSize.Height;
            MinithumbnailPanel.Arrange(rect);

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

            rect.X = Math.Max(min, finalSize.Width - 8 - FailedBadge.DesiredSize.Width);
            rect.Y = 36;
            rect.Width = FailedBadge.DesiredSize.Width;
            rect.Height = FailedBadge.DesiredSize.Height;
            FailedBadge.Arrange(rect);

            var line2RightPadding = Math.Max(Math.Max(PinnedIcon.DesiredSize.Width, UnreadBadge.DesiredSize.Width), FailedBadge.DesiredSize.Width);

            var line2Left = min + MinithumbnailPanel.DesiredSize.Width;
            var line2Right = finalSize.Width - 8 - line2RightPadding - UnreadMentionsBadge.DesiredSize.Width;

            var briefWidth = Math.Max(0, line2Right - line2Left);

            rect.X = min + MinithumbnailPanel.DesiredSize.Width;
            rect.Y = 34;
            rect.Width = briefWidth;
            rect.Height = BriefInfo.DesiredSize.Height;
            BriefInfo.Arrange(rect);

            rect.X = min;
            rect.Y = 34;
            rect.Width = ChatActionIndicator.DesiredSize.Width;
            rect.Height = ChatActionIndicator.DesiredSize.Height;
            ChatActionIndicator.Arrange(rect);

            rect.X = min + ChatActionIndicator.DesiredSize.Width;
            rect.Y = 34;
            rect.Width = briefWidth + MinithumbnailPanel.DesiredSize.Width;
            rect.Height = TypingLabel.DesiredSize.Height;
            TypingLabel.Arrange(rect);

            if (Children.Count > 14)
            {
                Children[14].Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            }

            return finalSize;
        }
    }
}
