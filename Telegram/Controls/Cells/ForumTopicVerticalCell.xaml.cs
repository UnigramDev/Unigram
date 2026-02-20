//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Text;
using Telegram.Common;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Cells
{
    public sealed partial class ForumTopicVerticalCell : ControlEx, IForumTopicDelegate, IDirectMessagesTopicDelegate
    {
        private bool _selected;

        private bool _vertical;

        private ForumTopic _forumTopic;
        private DirectMessagesChatTopic _directMessagesChatTopic;
        private Chat _chat;

        private int _thumbnailId;

        private string _dateLabel;
        private string _stateLabel;

        private TopicListViewModel _viewModel;

        private bool _draft;

        private MessageTicksState _ticksState;

        public ForumTopicVerticalCell()
        {
            DefaultStyleKey = typeof(ForumTopicVerticalCell);
        }

        #region InitializeComponent

        private AnimatedImage Animated;
        private ProfilePicture Photo;
        private TextBlock TitleLabel;
        private Border PinnedBackground;
        private TextBlock PinnedIcon;
        private Border UnreadMentionsBadge;
        private BadgeControl UnreadBadge;
        private Rectangle DropVisual;
        private TextBlock UnreadMentionsLabel;
        private Grid IconRoot;
        private Path IconPath;
        private TextBlock IconText;
        private Path General;
        private Path AllTopics;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            Animated = GetTemplateChild(nameof(Animated)) as AnimatedImage;
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            PinnedBackground = GetTemplateChild(nameof(PinnedBackground)) as Border;
            PinnedIcon = GetTemplateChild(nameof(PinnedIcon)) as TextBlock;
            UnreadMentionsBadge = GetTemplateChild(nameof(UnreadMentionsBadge)) as Border;
            UnreadBadge = GetTemplateChild(nameof(UnreadBadge)) as BadgeControl;
            DropVisual = GetTemplateChild(nameof(DropVisual)) as Rectangle;
            UnreadMentionsLabel = GetTemplateChild(nameof(UnreadMentionsLabel)) as TextBlock;
            IconRoot = GetTemplateChild(nameof(IconRoot)) as Grid;
            IconPath = GetTemplateChild(nameof(IconPath)) as Path;
            IconText = GetTemplateChild(nameof(IconText)) as TextBlock;
            General = GetTemplateChild(nameof(General)) as Path;
            AllTopics = GetTemplateChild(nameof(AllTopics)) as Path;

            _templateApplied = true;

            if (_forumTopic != null)
            {
                UpdateForumTopic(_viewModel, _forumTopic);
            }
            else if (_directMessagesChatTopic != null)
            {
                UpdateDirectMessagesChatTopic(_viewModel, _directMessagesChatTopic);
            }
        }

        #endregion

        public void UpdateLayout(bool vertical)
        {
            _vertical = vertical;
        }

        public string GetAutomationName()
        {
            if (_viewModel == null)
            {
                return null;
            }

            if (_forumTopic != null && _chat != null)
            {
                return UpdateAutomation(_viewModel.ClientService, _forumTopic, _chat, _forumTopic.LastMessage);
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

        #region ForumTopic

        public void UpdateForumTopicLastMessage(ForumTopic topic)
        {
            // Not implemented for now
        }

        public void UpdateForumTopicReadInbox(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            if (topic.IsPinned)
            {
                var items = _viewModel.Items as TopicListViewModel.ForumTopicsCollection;
                var index = items.IndexOf(topic);
                var first = index == 1;
                var last = index == items.Count - 1 || !items[index + 1].IsPinned;

                var radiusBefore = !first ? 0 : 4;
                var radiusAfter = !last ? 0 : 4;

                if (_vertical)
                {
                    var marginBefore = !first ? -2 : 0;
                    var marginAfter = !last ? -2 : 0;

                    PinnedBackground.Margin = new Thickness(4, marginBefore, 4, marginAfter);
                    PinnedBackground.CornerRadius = new CornerRadius(radiusBefore, radiusBefore, radiusAfter, radiusAfter);
                }
                else
                {
                    PinnedBackground.CornerRadius = new CornerRadius(radiusBefore, radiusAfter, radiusAfter, radiusBefore);
                }

                PinnedBackground.Visibility = Visibility.Visible;
                PinnedIcon.Visibility = first
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                PinnedBackground.Visibility = Visibility.Collapsed;
                PinnedIcon.Visibility = Visibility.Collapsed;
            }

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
            // Not implemented for now
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
            //MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
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
        }

        public void UpdateForumTopicName(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            TitleLabel.Text = topic.Info.Name;
        }

        public void UpdateForumTopicIcon(ForumTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            if (topic.Info.Icon.CustomEmojiId != 0)
            {
                Animated.Source = new CustomEmojiFileSource(_viewModel.ClientService, topic.Info.Icon.CustomEmojiId);
                IconRoot.Visibility = Visibility.Collapsed;
                General.Visibility = Visibility.Collapsed;
                AllTopics.Visibility = Visibility.Collapsed;
            }
            else if (topic.Info.IsGeneral)
            {
                Animated.Source = null;
                IconRoot.Visibility = Visibility.Collapsed;
                General.Visibility = Visibility.Visible;
                AllTopics.Visibility = Visibility.Collapsed;
            }
            else if (topic.Info.ForumTopicId == 0)
            {
                Animated.Source = null;
                IconRoot.Visibility = Visibility.Collapsed;
                General.Visibility = Visibility.Collapsed;
                AllTopics.Visibility = Visibility.Visible;
            }
            else
            {
                Animated.Source = null;
                IconRoot.Visibility = Visibility.Visible;
                General.Visibility = Visibility.Collapsed;
                AllTopics.Visibility = Visibility.Collapsed;

                var brush = ForumTopicCell.GetIconGradient(topic.Info.Icon);

                IconPath.Fill = brush;
                IconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                IconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
            }
        }

        public void UpdateForumTopicActions(ForumTopic topic, IDictionary<MessageSender, ChatAction> actions)
        {
            // Not implemented for now
        }

        public void UpdateForumTopic(TopicListViewModel viewModel, ForumTopic topic)
        {
            _viewModel = viewModel;
            _forumTopic = topic;
            _chat = _viewModel.ClientService.GetChat(topic.Info.ChatId);

            if (!_templateApplied)
            {
                return;
            }

            UpdateForumTopicName(topic);
            UpdateForumTopicIcon(topic);
            //UpdateChatEmojiStatus(topic);

            //UpdateChatReadInbox(chat);
            UpdateForumTopicUnreadMentionCount(topic);
            UpdateForumTopicNotificationSettings(topic);
        }

        #endregion

        #region DirectMessagesChatTopic

        public void UpdateDirectMessagesChatTopicLastMessage(DirectMessagesChatTopic topic)
        {
            // Not implemented for now
        }

        public void UpdateDirectMessagesChatTopicReadInbox(DirectMessagesChatTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            var unread = (topic.UnreadCount > 0 || topic.IsMarkedAsUnread) ? topic.UnreadReactionCount > 0 ? Visibility.Collapsed : Visibility.Visible : Visibility.Collapsed;
            if (unread == Visibility.Visible)
            {
                UnreadBadge.Visibility = Visibility.Visible;
                UnreadBadge.Text = topic.UnreadCount > 0 ? topic.UnreadCount.ToString() : string.Empty;
            }
            else
            {
                UnreadBadge.Visibility = Visibility.Collapsed;
            }

            //UpdateAutomation(_clientService, chat, chat.LastMessage);
        }

        public void UpdateDirectMessagesChatTopicReadOutbox(DirectMessagesChatTopic topic)
        {
            // Not implemented for now
        }

        public void UpdateDirectMessagesChatIsMarkedAsUnread(Chat chat)
        {

        }

        public void UpdateDirectMessagesChatTopicUnreadMentionCount(DirectMessagesChatTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            UpdateDirectMessagesChatTopicReadInbox(topic);

            var unread = topic.UnreadReactionCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (unread == Visibility.Visible)
            {
                UnreadMentionsBadge.Visibility = Visibility.Visible;
                UnreadMentionsLabel.Text = Icons.HeartFilled12;
            }
            else
            {
                UnreadMentionsBadge.Visibility = Visibility.Collapsed;
            }
        }

        public void UpdateNotificationSettings(DirectMessagesChatTopic topic)
        {
            if (_viewModel == null || !_templateApplied)
            {
                return;
            }

            UnreadBadge.IsUnmuted = true;
        }

        public void UpdateDirectMessagesChatTopicActions(DirectMessagesChatTopic topic, IDictionary<MessageSender, ChatAction> actions)
        {
            // Not implemented for now
        }

        public void UpdateDirectMessagesChatTopic(TopicListViewModel viewModel, DirectMessagesChatTopic topic)
        {
            _viewModel = viewModel;
            _directMessagesChatTopic = topic;
            _chat = _viewModel.ClientService.GetChat(topic.ChatId);

            if (!_templateApplied)
            {
                return;
            }

            if (topic.SenderId == null)
            {
                TitleLabel.Text = Strings.AllTopicsShort;
                Photo.Source = null;
                AllTopics.Visibility = Visibility.Visible;
            }
            else
            {
                TitleLabel.Text = _viewModel.ClientService.GetTitle(topic.SenderId);
                Photo.Source = ProfilePictureSource.MessageSender(_viewModel.ClientService, topic.SenderId);
                AllTopics.Visibility = Visibility.Collapsed;
            }

            //UpdateDirectMessagesChatTopicName(topic);
            //UpdateDirectMessagesChatTopicIcon(topic);
            //UpdateChatEmojiStatus(topic);

            //UpdateChatReadInbox(chat);
            UpdateDirectMessagesChatTopicUnreadMentionCount(topic);
            UpdateNotificationSettings(topic);
        }

        #endregion

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

            if (_forumTopic != null)
            {
                _ = viewModel.NavigatedToAsync(new ChatMessageTopic(chat.Id, new MessageTopicForum(_forumTopic.Info.ForumTopicId)), Windows.UI.Xaml.Navigation.NavigationMode.New, new Telegram.Navigation.Services.NavigationState());
            }
            else if (_directMessagesChatTopic != null)
            {
                _ = viewModel.NavigatedToAsync(new ChatMessageTopic(chat.Id, new MessageTopicDirectMessages(_directMessagesChatTopic.Id)), Windows.UI.Xaml.Navigation.NavigationMode.New, new Telegram.Navigation.Services.NavigationState());
            }

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
                service?.NavigateToChat(chat, topic: _forumTopic.ToId(), state: new NavigationState
                {
                    { "package", e.DataView }
                });
            }
            catch { }

            base.OnDrop(e);
        }
    }
}
