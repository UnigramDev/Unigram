//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages
{
    public partial class MessageService : Button, IReactionsDelegate
    {
        private MessageViewModel _message;

        public MessageService()
        {
            DefaultStyleKey = typeof(MessageService);

            Telegram.Common.Instrumentation.Register(this);
        }

        public MessageViewModel Message => _message;

        #region ContentOpacity

        public double ContentOpacity
        {
            get { return (double)GetValue(ContentOpacityProperty); }
            set { SetValue(ContentOpacityProperty, value); }
        }

        public static readonly DependencyProperty ContentOpacityProperty =
            DependencyProperty.Register("ContentOpacity", typeof(double), typeof(MessageService), new PropertyMetadata(1d));

        #endregion

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            var content = FindName("Text") as FormattedTextBlock;
            if (content != null)
            {
                content.TextEntityClick += Message_TextEntityClick;
            }

            if (_message != null)
            {
                UpdateMessageInteractionInfo(_message);
            }
        }

        private void Message_TextEntityClick(object sender, TextEntityClickEventArgs e)
        {
            if (_message is not MessageViewModel message || message.Delegate == null)
            {
                return;
            }

            if (e.Type is TextEntityTypeMention && e.Text is string username)
            {
                message.Delegate.OpenUsername(username);
            }
            else if (e.Type is TextEntityTypeMentionName mentionName)
            {
                message.Delegate.OpenUser(mentionName.UserId);
            }
            else if (e.Type is TextEntityTypeTextUrl textUrl)
            {
                message.Delegate.OpenUrl(textUrl.Url, true, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
            else if (e.Type is TextEntityTypeUrl && e.Text is string url)
            {
                message.Delegate.OpenUrl(url, false, new OpenUrlSourceChat(message.ChatId, message.SenderId));
            }
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var content = FindName("Text") as FormattedTextBlock;
            if (content == null)
            {
                UpdateContent(message);
                return;
            }

            var entities = MessageServiceText.GetEntities(message, true);
            if (entities.Text != null)
            {
                content.SetText(message.ClientService, entities.Text, entities.Entities);
                AutomationProperties.SetName(this, entities.Text);
            }

            UpdateContent(message);
            UpdateMessageInteractionInfo(message);
        }

        /// <summary>
        /// Called when the container goes back on the recycle queue: releases what the
        /// control holds, so a service message off screen doesn't keep its whole view model
        /// (and the inlines its text built) alive.
        ///
        /// Overrides must also reset any template state <see cref="UpdateContent"/> only
        /// sets conditionally — the same container comes back for another message of the
        /// same type, and whatever isn't reset is inherited by it.
        /// </summary>
        public virtual void Recycle()
        {
            if (FindName("Text") is FormattedTextBlock content)
            {
                content.Clear();
            }

            _message = null;
        }

        public void UpdateMessageTopic()
        {
            if (_message is not MessageViewModel message)
            {
                return;
            }

            var title = FindName("TitleLabel") as TextBlock;
            var photo = FindName("Photo") as ProfilePicture;
            var iconRoot = FindName("IconRoot") as Grid;
            var iconPath = FindName("IconPath") as Path;
            var iconText = FindName("IconText") as TextBlock;
            var typeIcon = FindName("TypeIcon") as IdentityIcon;

            if (message.ClientService.TryGetForumTopic(message.ChatId, message.TopicId, out ForumTopic topic))
            {
                title.Text = topic.Info.Name;
                photo.Source = null;

                if (topic.Info.IsGeneral || topic.Info.Icon.CustomEmojiId != 0)
                {
                    typeIcon.SetStatus(message.ClientService, topic.Info.Icon);
                    iconRoot.Visibility = Visibility.Collapsed;
                }
                else
                {
                    typeIcon.ClearStatus();
                    iconRoot.Visibility = Visibility.Visible;

                    var brush = ForumTopicCell.GetIconGradient(topic.Info.Icon);

                    iconPath.Fill = brush;
                    iconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                    iconText.Text = InitialNameStringConverter.Convert(topic.Info.Name);
                }
            }
            else if (message.ClientService.TryGetDirectMessagesChatTopic(message.ChatId, message.TopicId, out DirectMessagesChatTopic directMessagesChatTopic))
            {
                title.Text = message.ClientService.GetTitle(directMessagesChatTopic.SenderId);
                photo.Source = ProfilePictureSource.MessageSender(message.ClientService, directMessagesChatTopic.SenderId);

                typeIcon.ClearStatus();
                iconRoot.Visibility = Visibility.Collapsed;
            }

            AutomationProperties.SetName(this, title.Text);
        }

        protected virtual void UpdateContent(MessageViewModel message)
        {
            if (message.Content is MessageHeaderAccountInfo)
            {
                if (message.Chat.ActionBar is ChatActionBarReportAddBlock reportAddBlock && reportAddBlock.AccountInfo != null)
                {
                    if (message.ClientService.TryGetUser(message.Chat, out User user) && message.ClientService.TryGetUserFull(user.Id, out UserFullInfo fullInfo))
                    {
                        var info = FindName("AccountInfo") as ChatAccountInfo;
                        info.Update(message.ClientService, user, fullInfo, reportAddBlock.AccountInfo);
                    }
                }
            }
            else if (message.Content is MessageHeaderMessageTopic)
            {
                UpdateMessageTopic();
            }
            else if (message.Content is MessageChatChangePhoto chatChangePhoto)
            {
                var segments = FindName("Segments") as ActiveStoriesSegments;
                var photo = segments.Content as ProfilePicture;
                var view = FindName("View") as Border;

                Width = 216;

                segments.Visibility = Visibility.Visible;
                view.Visibility = Visibility.Visible;

                segments.SetChat(null, null, 120);
                photo.Source = ProfilePictureSource.ChatPhoto(message.ClientService, message.Chat, chatChangePhoto.Photo, true);

                if (view.Child is TextBlock label)
                {
                    label.Text = chatChangePhoto.Photo.Animation != null
                        ? Strings.ViewVideoAction
                        : Strings.ViewPhotoAction;
                }
            }
            else if (message.Content is MessageSuggestProfilePhoto suggestProfilePhoto)
            {
                var segments = FindName("Segments") as ActiveStoriesSegments;
                var photo = segments.Content as ProfilePicture;
                var view = FindName("View") as Border;

                Width = 216;

                segments.Visibility = Visibility.Visible;
                view.Visibility = Visibility.Visible;

                // TODO: Here it should probably be the user, but it's not critical
                segments.SetChat(null, null, 120);
                photo.Source = ProfilePictureSource.ChatPhoto(message.ClientService, message.Chat, suggestProfilePhoto.Photo, true);

                if (view.Child is TextBlock label)
                {
                    label.Text = suggestProfilePhoto.Photo.Animation != null
                        ? Strings.ViewVideoAction
                        : Strings.ViewPhotoAction;
                }
            }
            else if (message.Content is MessageAsyncStory story)
            {
                var segments = FindName("Segments") as ActiveStoriesSegments;
                var photo = segments.Content as ProfilePicture;
                var view = FindName("View") as Border;

                if (story.State == MessageStoryState.Expired)
                {
                    Width = double.NaN;

                    segments.Visibility = Visibility.Collapsed;
                    view.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Width = 216;

                    segments.Visibility = Visibility.Visible;
                    view.Visibility = Visibility.Visible;

                    if (message.ClientService.TryGetUser(message.SenderId, out User user) && message.ClientService.TryGetActiveStoriesFromUser(user.Id, out ChatActiveStories activeStories))
                    {
                        segments.UpdateSegments(120, story.Story?.PrivacySettings is StoryPrivacySettingsCloseFriends, activeStories.MaxReadStoryId < story.StoryId);
                    }
                    else
                    {
                        segments.UpdateSegments(120, story.Story?.PrivacySettings is StoryPrivacySettingsCloseFriends, false);
                    }

                    if (story.Story == null)
                    {
                        photo.Source = ProfilePictureSource.Chat(message.ClientService, message.Chat);
                    }
                    else
                    {
                        photo.Source = ProfilePictureSource.Story(message.ClientService, story.Story);
                    }

                    if (view.Child is TextBlock label)
                    {
                        label.Text = Strings.StoryMentionedAction;
                    }
                }
            }
        }

        public void UpdateMessageInteractionInfo(MessageViewModel message)
        {
            UpdateMessageReactions(message, false);
        }

        public void UpdateMessageReactions(MessageViewModel message, bool animate)
        {
            // TODO: Name
            var reactions = GetTemplateChild("Reactions") as ReactionsPanel;
            reactions?.UpdateMessageReactions(message, animate);
        }
    }

    public partial class DashedLine : Path
    {
        private readonly LineGeometry _geometry;

        public DashedLine()
        {
            _geometry = new LineGeometry();
            Data = _geometry;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            return new Size(size.Width, 2);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _geometry.StartPoint = new Windows.Foundation.Point(0, 1);
            _geometry.EndPoint = new Windows.Foundation.Point(finalSize.Width, 1);

            var size = base.ArrangeOverride(finalSize);
            return new Size(size.Width, 2);
        }
    }
}
