//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Common.Chats;
using Telegram.Composition;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.Views;
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
    public sealed partial class ForumTopicCell : ControlEx, IForumTopicDelegate
    {
        private bool _selected;

        private ForumTopic _topic;
        private Chat _chat;

        private int _thumbnailId;

        private string _dateLabel;
        private string _stateLabel;

        private TopicListViewModel _viewModel;

        private bool _draft;

        private MessageTicksState _ticksState;

        public ForumTopicCell()
        {
            DefaultStyleKey = typeof(ForumTopicCell);
        }

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

        private IdentityIcon TypeIcon;
        private TextBlock TitleLabel;
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
        private FormattedTextBlock BriefText;
        private Span BriefLabel;
        private ImageBrush Minithumbnail;
        private Grid IconRoot;
        private Path IconPath;
        private TextBlock IconText;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            TypeIcon = GetTemplateChild(nameof(TypeIcon)) as IdentityIcon;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
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
            BriefText = GetTemplateChild(nameof(BriefText)) as FormattedTextBlock;
            BriefLabel = GetTemplateChild(nameof(BriefLabel)) as Span;
            Minithumbnail = GetTemplateChild(nameof(Minithumbnail)) as ImageBrush;
            IconRoot = GetTemplateChild(nameof(IconRoot)) as Grid;
            IconPath = GetTemplateChild(nameof(IconPath)) as Path;
            IconText = GetTemplateChild(nameof(IconText)) as TextBlock;

            _templateApplied = true;

            if (_topic != null)
            {
                UpdateForumTopic(_viewModel, _topic);
            }
        }

        #endregion

        public string GetAutomationName()
        {
            if (_viewModel == null)
            {
                return null;
            }

            if (_topic != null && _chat != null)
            {
                return UpdateAutomation(_viewModel.ClientService, _topic, _chat, _topic.LastMessage);
            }

            return null;
        }

        private string UpdateAutomation(IClientService clientService, ForumTopic topic, Chat chat, Message message)
        {
            var builder = new StringBuilder();

            {
                builder.Append(topic.Info.Name);
                builder.Append(", ");
            }

            if (topic.UnreadCount > 0)
            {
                builder.Append(Locale.Declension(Strings.R.NewMessages, topic.UnreadCount));
                builder.Append(", ");
            }

            if (topic.UnreadMentionCount > 0)
            {
                builder.Append(Locale.Declension(Strings.R.AccDescrMentionCount, topic.UnreadMentionCount));
                builder.Append(", ");
            }

            if (message == null)
            {
                //AutomationProperties.SetName(this, builder.ToString());
                return builder.ToString();
            }

            //if (!message.IsOutgoing && message.SenderUserId != 0 && !message.IsService())
            if (ChatCell.ShowFrom(clientService, null, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsOutgoing)
                {
                    //if (!(chat.Type is ChatTypePrivate priv && priv.UserId == fromUser?.Id) && !message.IsChannelPost)
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

            builder.Append(Automation.GetSummary(clientService, message));

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

        public void UpdateForumTopicLastMessage(ForumTopic topic)
        {
            if (topic == null || _chat == null || !_templateApplied)
            {
                return;
            }

            var from = UpdateFromLabel(_chat, topic, out bool draft);

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
            _dateLabel = UpdateTimeLabel(topic);
            _stateLabel = UpdateStateIcon(topic.LastReadOutboxMessageId, topic, topic.DraftMessage, topic.LastMessage, topic.LastMessage?.SendingState);
            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;

            UpdateBriefLabel(UpdateBriefLabel(topic, out MinithumbnailId thumbnail));
            UpdateMinithumbnail(thumbnail);
        }

        public void UpdateForumTopicReadInbox(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            PinnedIcon.Visibility = topic.UnreadCount == 0 /*&& !topic.IsMarkedAsUnread*/ && topic.IsPinned ? Visibility.Visible : Visibility.Collapsed;

            var unread = (topic.UnreadCount > 0 /*|| topic.IsMarkedAsUnread*/) ? topic.UnreadMentionCount == 1 && topic.UnreadCount == 1 ? Visibility.Collapsed : Visibility.Visible : Visibility.Collapsed;
            if (unread == Visibility.Visible)
            {
                UnreadBadge.Visibility = Visibility.Visible;
                //UnreadBadge.Text = topic.UnreadCount > 0 ? topic.UnreadCount.ToString() : string.Empty;
            }
            else
            {
                UnreadBadge.Visibility = Visibility.Collapsed;
            }

            //UpdateAutomation(_clientService, chat, chat.LastMessage);
        }

        public void UpdateForumTopicReadOutbox(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            _stateLabel = UpdateStateIcon(topic.LastReadOutboxMessageId, topic, topic.DraftMessage, topic.LastMessage, topic.LastMessage?.SendingState);
            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;
        }

        public void UpdateChatIsMarkedAsUnread(Chat chat)
        {

        }

        public void UpdateForumTopicUnreadMentionCount(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            UpdateForumTopicReadInbox(topic);

            var unread = topic.UnreadMentionCount > 0 || topic.UnreadReactionCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (unread == Visibility.Visible)
            {
                UnreadMentionsBadge.Visibility = Visibility.Visible;
                UnreadMentionsLabel.Text = topic.UnreadMentionCount > 0 ? Icons.Mention16 : Icons.HeartFilled12;
            }
            else
            {
                UnreadMentionsBadge.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateForumTopicNotificationSettings(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            var muted = _viewModel.ClientService.Notifications.IsMuted(_chat, topic);
            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.IsUnmuted = !muted;
        }

        public void UpdateForumTopicInfo(ForumTopic topic)
        {
            if (!_templateApplied)
            {
                return;
            }

            UpdateForumTopicName(topic);
            UpdateForumTopicIcon(topic);

            _dateLabel = UpdateTimeLabel(topic);
            TimeLabel.Text = _stateLabel + "\u00A0" + _dateLabel;
        }

        public void UpdateForumTopicName(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            TitleLabel.Text = topic.Info.Name;
        }

        public static Color[] ServerSupportedColors = new Color[6]
        {
            Color.FromArgb(0xFF, 0x6F, 0xB9, 0xF0), // blue
            Color.FromArgb(0xFF, 0xFF, 0xD6, 0x7E), // yellow
            Color.FromArgb(0xFF, 0xCB, 0x86, 0xDB), // violet
            Color.FromArgb(0xFF, 0x8E, 0xEE, 0x98), // green
            Color.FromArgb(0xFF, 0xFF, 0x93, 0xB2), // rose
            Color.FromArgb(0xFF, 0xFB, 0x6F, 0x5F), // orange
        };

        private static readonly Color[] _colorsTop = new Color[6]
        {
            Color.FromArgb(0xFF, 0x8A, 0xD3, 0xF9), // blue
            Color.FromArgb(0xFF, 0xF7, 0xCE, 0x79), // yellow
            Color.FromArgb(0xFF, 0x8C, 0xAF, 0xF9), // violet
            Color.FromArgb(0xFF, 0xAC, 0xDC, 0x89), // green
            Color.FromArgb(0xFF, 0xFF, 0xAF, 0xC7), // rose
            Color.FromArgb(0xFF, 0xEF, 0x8E, 0x67), // orange
        };

        private static readonly Color[] _colors = new Color[6]
        {
            Color.FromArgb(0xFF, 0x51, 0x9D, 0xEA), // blue
            Color.FromArgb(0xFF, 0xF2, 0xAC, 0x6A), // yellow
            Color.FromArgb(0xFF, 0x65, 0x60, 0xF6), // violet
            Color.FromArgb(0xFF, 0x75, 0xC8, 0x73), // green
            Color.FromArgb(0xFF, 0xF2, 0x74, 0x9A), // rose
            Color.FromArgb(0xFF, 0xEC, 0x5F, 0x6D), // orange
        };

        public static int FindIconColorIndex(int color)
        {
            static int Distance(Color a, Color b)
            {
                return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            }

            var value = color.ToColor();

            int distance = Distance(ServerSupportedColors[0], value);
            var index = 0;

            for (int i = 0; i < ServerSupportedColors.Length; i++)
            {
                int distanceLocal = Distance(ServerSupportedColors[i], value);
                if (distanceLocal < distance)
                {
                    distance = distanceLocal;
                    index = i;
                }
            }

            return index;
        }

        public static LinearGradientBrush GetIconGradient(ForumTopicIcon icon)
        {
            var index = FindIconColorIndex(icon.Color);

            var top = _colorsTop[index];
            var bottom = _colors[index];

            return new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop
                {
                    Color = top,
                    Offset = 0
                },
                new GradientStop
                {
                    Color = bottom,
                    Offset = 1
                }
            }, 90);
        }

        public void UpdateForumTopicIcon(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            if (topic.Info.IsGeneral || topic.Info.Icon.CustomEmojiId != 0)
            {
                TypeIcon.SetStatus(_viewModel.ClientService, topic.Info.Icon);
                IconRoot.Visibility = Visibility.Collapsed;
            }
            else
            {
                TypeIcon.ClearStatus();
                IconRoot.Visibility = Visibility.Visible;

                var brush = GetIconGradient(topic.Info.Icon);

                IconPath.Fill = brush;
                IconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                IconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
            }
        }

        public void UpdateForumTopicActions(ForumTopic topic, IDictionary<MessageSender, ChatAction> actions)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            if (actions != null && actions.Count > 0)
            {
                TypingLabel.Text = InputChatActionManager.GetTypingString(null, actions, _viewModel.ClientService, out ChatAction commonAction);
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

        public void UpdateForumTopic(TopicListViewModel viewModel, ForumTopic topic)
        {
            _viewModel = viewModel;
            _topic = topic;
            _chat = viewModel.ClientService.GetChat(topic.Info.ChatId);

            if (!_templateApplied)
            {
                return;
            }

            UpdateForumTopicName(topic);
            UpdateForumTopicIcon(topic);
            //UpdateChatEmojiStatus(topic);

            UpdateForumTopicLastMessage(topic);
            //UpdateChatReadInbox(chat);
            UpdateForumTopicUnreadMentionCount(topic);
            UpdateForumTopicNotificationSettings(topic);
            UpdateForumTopicActions(topic, _viewModel.ClientService.GetChatActions(topic.Info.ChatId, new MessageTopicForum(topic.Info.ForumTopicId)));
        }

        #endregion

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
            BriefText.SetText(_viewModel.ClientService, message);
            BriefText.SetQuery(string.Empty);
        }


        private FormattedText UpdateBriefLabel(ForumTopic topic, out MinithumbnailId thumbnail)
        {
            thumbnail = null;

            var topMessage = topic.LastMessage;
            if (topMessage != null)
            {
                return ChatCell.UpdateBriefLabel(topMessage.Content, topMessage.IsOutgoing, topic.DraftMessage, false, out thumbnail);
            }

            return string.Empty.AsFormattedText();
        }

        private string UpdateFromLabel(Chat chat, ForumTopic topic, out bool draft)
        {
            if (topic.DraftMessage is not null)
            {
                draft = true;
                return string.Format("{0}: \u200B​​​", Strings.Draft);
            }

            var message = topic.LastMessage;
            if (message == null)
            {
                if (topic.LastReadOutboxMessageId != 0 || topic.LastReadInboxMessageId != 0)
                {
                    draft = false;
                    return Strings.HistoryCleared;
                }

                draft = false;
                return string.Empty;
            }

            draft = false;
            return ChatCell.UpdateFromLabel(_viewModel.ClientService, chat, message);
        }

        private string UpdateStateIcon(long maxId, ForumTopic topic, DraftMessage draft, Message message, MessageSendingState state)
        {
            if (draft != null || message == null)
            {
                UpdateTicks(null);

                _ticksState = MessageTicksState.None;
                return string.Empty;
            }

            if (message.IsOutgoing /*&& IsOut(ViewModel)*/)
            {
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

        private string UpdateTimeLabel(ForumTopic topic)
        {
            var lastMessage = topic.LastMessage;
            if (lastMessage != null)
            {
                if (topic.Info.IsClosed)
                {
                    return Icons.LockClosedFilled12 + "\u00A0" + Formatter.DateExtended(lastMessage.Date);
                }

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
            if (chat == null)
            {
                return;
            }

            var context = WindowContext.ForXamlRoot(this);
            var service = context.NavigationServices.GetByFrameId($"Main{_viewModel.Session.Id}") as NavigationService;

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
            _ = viewModel.NavigatedToAsync(new ChatMessageTopic(chat.Id, new MessageTopicForum(_topic.Info.ForumTopicId)), Windows.UI.Xaml.Navigation.NavigationMode.New, new Telegram.Navigation.Services.NavigationState());

            void handler(object sender, object e)
            {
                Logger.Info("Unloaded");

                flyout.Closing -= handler;
                chatView.ViewModel.NavigatedFrom(null, false);
                chatView.Deactivate(true);
            }

            flyout.Closing += handler;

            var background = new ChatBackgroundControl();
            background.Update(_viewModel.ClientService, null);

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
                if (_viewModel.ClientService.CanPostMessages(chat) && e.DataView.AvailableFormats.Count > 0)
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
                service?.NavigateToChat(chat, topic: _topic.ToId(), state: new NavigationState
                {
                    { "package", e.DataView }
                });
            }
            catch { }

            base.OnDrop(e);
        }

        #region SelectionStroke

        private CompositionColorSource _selectionStrokeBrush;

        public SolidColorBrush SelectionStroke
        {
            get => (SolidColorBrush)GetValue(SelectionStrokeProperty);
            set => SetValue(SelectionStrokeProperty, value);
        }

        public static readonly DependencyProperty SelectionStrokeProperty =
            DependencyProperty.Register("SelectionStroke", typeof(SolidColorBrush), typeof(ForumTopicCell), new PropertyMetadata(null, OnSelectionStrokeChanged));

        private static void OnSelectionStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ForumTopicCell)d).OnSelectionStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
        }

        private void OnSelectionStrokeChanged(SolidColorBrush newValue, SolidColorBrush oldValue)
        {
            _selectionStrokeBrush?.PropertyChanged(newValue, IsConnected);
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
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ForumTopicCell), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ForumTopicCell)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
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
}
