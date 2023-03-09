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
using Telegram.Services;
using Telegram.Td.Api;
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
    public sealed class ForumTopicCell : Control, IMultipleElement, IPlayerView
    {
        private bool _selected;

        private ForumTopic _topic;
        private Chat _chat;

        private IClientService _clientService;

        private Visual _onlineBadge;
        private bool _onlineCall;

        // Used only to prevent garbage collection
        private CompositionAnimation _size1;
        private CompositionAnimation _size2;
        private CompositionAnimation _offset1;
        private CompositionAnimation _offset2;
        private CompositionAnimation _offset3;

        private MessageTicksState _ticksState;

        public ForumTopicCell()
        {
            DefaultStyleKey = typeof(ForumTopicCell);

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
        private IdentityIcon TypeIcon;
        private TextBlock TitleLabel;
        private FontIcon MutedIcon;
        private FontIcon StateIcon;
        private TextBlock TimeLabel;
        private Border MinithumbnailPanel;
        private TextBlock BriefInfo;
        private ChatActionIndicator ChatActionIndicator;
        private TextBlock TypingLabel;
        private Border PinnedIcon;
        private Border UnreadMentionsBadge;
        private InfoBadge UnreadBadge;
        private Rectangle DropVisual;
        private TextBlock FailedLabel;
        private TextBlock UnreadMentionsLabel;
        private Run FromLabel;
        private Run DraftLabel;
        private Span BriefLabel;
        private Image Minithumbnail;
        private Ellipse SelectionOutline;
        private ProfilePicture Photo;

        // Lazy loaded
        private CustomEmojiCanvas CustomEmoji;

        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            PhotoPanel = GetTemplateChild(nameof(PhotoPanel)) as Grid;
            TypeIcon = GetTemplateChild(nameof(TypeIcon)) as IdentityIcon;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            MutedIcon = GetTemplateChild(nameof(MutedIcon)) as FontIcon;
            StateIcon = GetTemplateChild(nameof(StateIcon)) as FontIcon;
            TimeLabel = GetTemplateChild(nameof(TimeLabel)) as TextBlock;
            MinithumbnailPanel = GetTemplateChild(nameof(MinithumbnailPanel)) as Border;
            BriefInfo = GetTemplateChild(nameof(BriefInfo)) as TextBlock;
            ChatActionIndicator = GetTemplateChild(nameof(ChatActionIndicator)) as ChatActionIndicator;
            TypingLabel = GetTemplateChild(nameof(TypingLabel)) as TextBlock;
            PinnedIcon = GetTemplateChild(nameof(PinnedIcon)) as Border;
            UnreadMentionsBadge = GetTemplateChild(nameof(UnreadMentionsBadge)) as Border;
            UnreadBadge = GetTemplateChild(nameof(UnreadBadge)) as InfoBadge;
            DropVisual = GetTemplateChild(nameof(DropVisual)) as Rectangle;
            FailedLabel = GetTemplateChild(nameof(FailedLabel)) as TextBlock;
            UnreadMentionsLabel = GetTemplateChild(nameof(UnreadMentionsLabel)) as TextBlock;
            FromLabel = GetTemplateChild(nameof(FromLabel)) as Run;
            DraftLabel = GetTemplateChild(nameof(DraftLabel)) as Run;
            BriefLabel = GetTemplateChild(nameof(BriefLabel)) as Span;
            Minithumbnail = GetTemplateChild(nameof(Minithumbnail)) as Image;
            SelectionOutline = GetTemplateChild(nameof(SelectionOutline)) as Ellipse;
            Photo = GetTemplateChild(nameof(Photo)) as ProfilePicture;

            BriefInfo.SizeChanged += OnSizeChanged;

            var tooltip = new ToolTip();
            tooltip.Opened += ToolTip_Opened;

            ToolTipService.SetToolTip(BriefInfo, tooltip);

            _selectionPhoto = ElementCompositionPreview.GetElementVisual(Photo);
            _selectionOutline = ElementCompositionPreview.GetElementVisual(SelectionOutline);
            _selectionPhoto.CenterPoint = new Vector3(24);
            _selectionOutline.CenterPoint = new Vector3(24);
            _selectionOutline.Opacity = 0;

            _templateApplied = true;

            if (_topic != null)
            {
                UpdateForumTopic(_clientService, _topic, _chat);
            }
        }

        #endregion

        public void UpdateForumTopic(IClientService clientService, ForumTopic topic, Chat chat)
        {
            _clientService = clientService;

            Update(topic, chat);
        }

        public string GetAutomationName()
        {
            if (_clientService == null)
            {
                return null;
            }

            if (_topic != null && _chat != null)
            {
                return UpdateAutomation(_clientService, _topic, _chat, _topic.LastMessage);
            }

            return null;
        }

        private string UpdateAutomation(IClientService clientService, ForumTopic topic, Chat chat, Message message)
        {
            var builder = new StringBuilder();

            {
                //if (topic.Type is ForumTopicTypeSupergroup super && super.IsChannel)
                //{
                //    builder.Append(Strings.Resources.AccDescrChannel);
                //}
                //else
                //{
                //    builder.Append(Strings.Resources.AccDescrGroup);
                //}

                builder.Append(", ");
                builder.Append(topic.Info.Name);
                builder.Append(", ");
            }

            if (topic.UnreadCount > 0)
            {
                builder.Append(Locale.Declension("NewMessages", topic.UnreadCount));
                builder.Append(", ");
            }

            if (topic.UnreadMentionCount > 0)
            {
                builder.Append(Locale.Declension("AccDescrMentionCount", topic.UnreadMentionCount));
                builder.Append(", ");
            }

            if (message == null)
            {
                //AutomationProperties.SetName(this, builder.ToString());
                return builder.ToString();
            }

            //if (!message.IsOutgoing && message.SenderUserId != 0 && !message.IsService())
            if (ShowFrom(clientService, topic, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsOutgoing)
                {
                    //if (!(topic.Type is ForumTopicTypePrivate priv && priv.UserId == fromUser?.Id) && !message.IsChannelPost)
                    {
                        builder.Append(Strings.Resources.FromYou);
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

        public void UpdateForumTopicLastMessage(ForumTopic topic)
        {
            if (topic == null || !_templateApplied)
            {
                return;
            }

            DraftLabel.Text = UpdateDraftLabel(topic);
            FromLabel.Text = UpdateFromLabel(topic);
            TimeLabel.Text = UpdateTimeLabel(topic);
            StateIcon.Glyph = UpdateStateIcon(topic.LastReadOutboxMessageId, topic, topic.DraftMessage, topic.LastMessage, topic.LastMessage?.SendingState);

            UpdateBriefLabel(UpdateBriefLabel(topic));
            UpdateMinithumbnail(topic, topic.DraftMessage == null ? topic.LastMessage : null);
        }

        public void UpdateForumTopicReadInbox(ForumTopic topic)
        {
            if (!_templateApplied)
            {
                return;
            }

            PinnedIcon.Visibility = topic.UnreadCount == 0 && topic.IsPinned ? Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Visibility = topic.UnreadCount > 0 ? topic.UnreadMentionCount == 1 && topic.UnreadCount == 1 ? Visibility.Collapsed : Visibility.Visible : Visibility.Collapsed;
            UnreadBadge.Value = topic.UnreadCount;

            //UpdateAutomation(_clientService, topic, topic.LastMessage);
        }

        //public void UpdateForumTopicReadOutbox(ForumTopic topic)
        //{
        //    if (!_templateApplied)
        //    {
        //        return;
        //    }

        //    StateIcon.Glyph = UpdateStateIcon(topic.LastReadOutboxMessageId, topic, topic.DraftMessage, topic.LastMessage, topic.LastMessage?.SendingState);
        //}

        //public void UpdateForumTopicIsMarkedAsUnread(ForumTopic topic)
        //{

        //}

        public void UpdateForumTopicUnreadMentionCount(ForumTopic topic)
        {
            if (!_templateApplied)
            {
                return;
            }

            UpdateForumTopicReadInbox(topic);
            UnreadMentionsBadge.Visibility = topic.UnreadMentionCount > 0 || topic.UnreadReactionCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            UnreadMentionsLabel.Text = topic.UnreadMentionCount > 0 ? Icons.Mention16 : Icons.HeartFilled12;
        }

        public void UpdateNotificationSettings(ForumTopic topic)
        {
            if (!_templateApplied)
            {
                return;
            }

            var muted = _clientService.Notifications.GetMutedFor(_chat, topic) > 0;
            VisualStateManager.GoToState(this, muted ? "Muted" : "Unmuted", false);
            MutedIcon.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
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
            if (!_templateApplied)
            {
                return;
            }

            TitleLabel.Text = topic.Info.Name;
        }

        public void UpdateForumTopicIcon(ForumTopic topic)
        {
            if (!_templateApplied)
            {
                return;
            }

            //Photo.SetForumTopic(_clientService, topic, 48);
            TypeIcon.SetStatus(_clientService, topic.Info.Icon);
        }

        public void UpdateForumTopicActions(ForumTopic topic, IDictionary<MessageSender, ChatAction> actions)
        {
            if (!_templateApplied)
            {
                return;
            }

            if (actions != null && actions.Count > 0)
            {
                TypingLabel.Text = InputChatActionManager.GetTypingString(null, actions, _clientService.GetUser, _clientService.GetChat, out ChatAction commonAction);
                ChatActionIndicator.UpdateAction(commonAction);
                ChatActionIndicator.Visibility = Visibility.Visible;
                TypingLabel.Visibility = Visibility.Visible;
                BriefInfo.Visibility = Visibility.Collapsed;
                Minithumbnail.Visibility = Visibility.Collapsed;
            }
            else
            {
                ChatActionIndicator.Visibility = Visibility.Collapsed;
                ChatActionIndicator.UpdateAction(null);
                TypingLabel.Visibility = Visibility.Collapsed;
                BriefInfo.Visibility = Visibility.Visible;
                Minithumbnail.Visibility = Visibility.Visible;
            }
        }

        //private void UpdateForumTopicType(ForumTopic topic)
        //{
        //    var type = UpdateType(topic);
        //    TypeIcon.Text = type ?? string.Empty;
        //    TypeIcon.Visibility = type == null ? Visibility.Collapsed : Visibility.Visible;

        //    Identity.SetStatus(_clientService, topic);
        //}

        private void Update(ForumTopic topic, Chat chat)
        {
            _topic = topic;
            _chat = chat;

            Tag = topic;

            if (!_templateApplied)
            {
                return;
            }

            //UpdateViewState(topic, ForumTopicFilterMode.None, false, false);

            UpdateForumTopicName(topic);
            UpdateForumTopicIcon(topic);

            UpdateForumTopicLastMessage(topic);
            //UpdateForumTopicReadInbox(topic);
            UpdateForumTopicUnreadMentionCount(topic);
            UpdateNotificationSettings(topic);
            UpdateForumTopicActions(topic, _clientService.GetChatActions(chat.Id, topic.Info.MessageThreadId));
        }

        #endregion

        public void UpdateViewState(ForumTopic topic, bool compact)
        {
            VisualStateManager.GoToState(this, compact ? "Compact" : "Expanded", false);
        }

        private void UpdateMinithumbnail(ForumTopic topic, Message message)
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
                BriefInfo.LayoutUpdated -= OnLayoutUpdated;

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
                if (rect.X + 20 > BriefInfo.ActualWidth && BriefInfo.IsTextTrimmed)
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
                BriefInfo.LayoutUpdated -= OnLayoutUpdated;

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
                        BriefLabel.Inlines.Add(new Run { Text = clean.Substring(entity.Offset, entity.Length), FontFamily = App.Current.Resources["SpoilerFontFamily"] as FontFamily });

                        emoji.Add(customEmoji.CustomEmojiId);
                        shift += 2;

                        previous = entity.Offset + entity.Length;
                    }
                }

                if (clean.Length > previous)
                {
                    BriefLabel.Inlines.Add(new Run { Text = clean.Substring(previous) });
                }

                BriefInfo.LayoutUpdated -= OnLayoutUpdated;

                if (emoji.Count > 0)
                {
                    CustomEmoji ??= GetTemplateChild(nameof(CustomEmoji)) as CustomEmojiCanvas;
                    CustomEmoji.UpdateEntities(_clientService, emoji);

                    if (_playing)
                    {
                        CustomEmoji.Play();
                    }

                    _ignoreLayoutUpdated = false;
                    BriefInfo.LayoutUpdated += OnLayoutUpdated;
                }
                else if (CustomEmoji != null)
                {
                    XamlMarkupHelper.UnloadObject(CustomEmoji);
                    CustomEmoji = null;
                }

            }
            else if (CustomEmoji != null)
            {
                BriefInfo.LayoutUpdated -= OnLayoutUpdated;

                XamlMarkupHelper.UnloadObject(CustomEmoji);
                CustomEmoji = null;
            }
        }


        private FormattedText UpdateBriefLabel(ForumTopic topic)
        {
            var topMessage = topic.LastMessage;
            if (topMessage != null)
            {
                return UpdateBriefLabel(topic, topMessage, true, true);
            }

            return new FormattedText(string.Empty, new TextEntity[0]);
        }

        private FormattedText UpdateBriefLabel(ForumTopic topic, Message value, bool showContent, bool draft)
        {
            //if (ViewModel.DraftMessage is DraftMessage draft && !string.IsNullOrWhiteSpace(draft.InputMessageText.ToString()))
            //{
            //    return draft.Message;
            //}

            if (topic.DraftMessage != null && draft)
            {
                switch (topic.DraftMessage.InputMessageText)
                {
                    case InputMessageText text:
                        return text.Text;
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
                return new FormattedText(Strings.Resources.Message, new TextEntity[0]);
            }

            return value.Content switch
            {
                MessageAnimation animation => animation.Caption,
                MessageAudio audio => audio.Caption,
                MessageDocument document => document.Caption,
                MessagePhoto photo => photo.Caption,
                MessageVideo video => video.Caption,
                MessageVoiceNote voiceNote => voiceNote.Caption,
                MessageText text => text.Text,
                MessageAnimatedEmoji animatedEmoji => new FormattedText(animatedEmoji.Emoji, new TextEntity[0]),
                MessageDice dice => new FormattedText(dice.Emoji, new TextEntity[0]),
                MessageInvoice invoice => invoice.ExtendedMedia switch
                {
                    MessageExtendedMediaPreview preview => preview.Caption,
                    MessageExtendedMediaPhoto photo => photo.Caption,
                    MessageExtendedMediaVideo video => video.Caption,
                    MessageExtendedMediaUnsupported unsupported => unsupported.Caption,
                    _ => new FormattedText(string.Empty, new TextEntity[0])
                },
                _ => new FormattedText(string.Empty, new TextEntity[0]),
            };
        }

        private string UpdateDraftLabel(ForumTopic topic)
        {
            if (topic.DraftMessage != null)
            {
                switch (topic.DraftMessage.InputMessageText)
                {
                    case InputMessageText:
                        return string.Format("{0}: ", Strings.Resources.Draft);
                }
            }

            return string.Empty;
        }

        private string UpdateFromLabel(ForumTopic topic)
        {
            if (topic.DraftMessage != null)
            {
                switch (topic.DraftMessage.InputMessageText)
                {
                    case InputMessageText:
                        return string.Empty;
                }
            }

            var message = topic.LastMessage;
            if (message == null)
            {
                return string.Empty;
            }

            return UpdateFromLabel(topic, message);
        }

        private string UpdateFromLabel(ForumTopic topic, Message message)
        {
            if (message.IsService())
            {
                return MessageService.GetText(new ViewModels.MessageViewModel(_clientService, null, null, message));
            }

            var format = "{0}: ";
            var result = string.Empty;

            if (ShowFrom(_clientService, topic, message, out User fromUser, out Chat fromChat))
            {
                if (message.IsSaved(_clientService.Options.MyId))
                {
                    result = string.Format(format, _clientService.GetTitle(message.ForwardInfo));
                }
                else if (message.IsOutgoing)
                {
                    result = string.Format(format, Strings.Resources.FromYou);
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
                    else if (fromUser.Type is UserTypeDeleted)
                    {
                        result = string.Format(format, Strings.Resources.HiddenName);
                    }
                    else
                    {
                        result = string.Format(format, fromUser.Id);
                    }
                }
                //else if (fromChat != null && fromChat.Id != topic.Id)
                //{
                //    result = string.Format(format, fromChat.Title);
                //}
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
                if (invoice.ExtendedMedia != null && invoice.HasCaption())
                {
                    return result;
                }

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

        private bool ShowFrom(IClientService clientService, ForumTopic topic, Message message, out User senderUser, out Chat senderChat)
        {
            if (message.IsService())
            {
                senderUser = null;
                senderChat = null;
                return false;
            }

            senderUser = null;
            senderChat = null;
            return clientService.TryGetUser(message.SenderId, out senderUser)
                || clientService.TryGetChat(message.SenderId, out senderChat);
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

        private string UpdateTimeLabel(ForumTopic topic)
        {
            var lastMessage = topic.LastMessage;
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
            //var topic = _topic;
            //if (topic == null)
            //{
            //    return;
            //}

            //try
            //{
            //    if (_clientService.CanPostMessages(topic) && e.DataView.AvailableFormats.Count > 0)
            //    {
            //        if (DropVisual == null)
            //        {
            //            FindName(nameof(DropVisual));
            //        }

            //        DropVisual.Visibility = Visibility.Visible;
            //        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            //    }
            //    else
            //    {
            //        if (DropVisual != null)
            //        {
            //            DropVisual.Visibility = Visibility.Collapsed;
            //        }

            //        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            //    }
            //}
            //catch
            //{
            //    if (DropVisual != null)
            //    {
            //        DropVisual.Visibility = Visibility.Collapsed;
            //    }
            //}

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
            //if (DropVisual != null)
            //{
            //    DropVisual.Visibility = Visibility.Collapsed;
            //}

            //try
            //{
            //    if (e.DataView.AvailableFormats.Count == 0)
            //    {
            //        return;
            //    }

            //    var topic = _topic;
            //    if (topic == null)
            //    {
            //        return;
            //    }

            //    var service = WindowContext.Current.NavigationServices.GetByFrameId($"Main{_clientService.SessionId}") as NavigationService;
            //    if (service != null)
            //    {
            //        App.DataPackages[topic.Id] = e.DataView;
            //        service.NavigateToForumTopic(topic);
            //    }
            //}
            //catch { }

            base.OnDrop(e);
        }

        #region SelectionStroke

        private long _selectionStrokeToken;

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
            DependencyProperty.Register("Stroke", typeof(Brush), typeof(ForumTopicCell), new PropertyMetadata(null, OnStrokeChanged));

        private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ForumTopicCell)d).OnStrokeChanged(e.NewValue as SolidColorBrush, e.OldValue as SolidColorBrush);
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

    }

    public class ForumTopicCellPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var PhotoPanel = Children[0];

            var TypeIcon = Children[1];
            var TitleLabel = Children[2];
            var MutedIcon = Children[3];
            var StateIcon = Children[4];
            var TimeLabel = Children[5];

            var MinithumbnailPanel = Children[6];
            var BriefInfo = Children[7];

            var shift = 0;
            var CustomEmoji = default(CustomEmojiCanvas);

            if (Children[8] is CustomEmojiCanvas)
            {
                shift++;
                CustomEmoji = Children[8] as CustomEmojiCanvas;
            }

            var ForumTopicActionIndicator = Children[8 + shift];
            var TypingLabel = Children[9 + shift];
            var PinnedIcon = Children[10 + shift];
            var UnreadMentionsBadge = Children[11 + shift];
            var UnreadBadge = Children[12 + shift];

            PhotoPanel.Measure(availableSize);

            TimeLabel.Measure(availableSize);
            StateIcon.Measure(availableSize);
            TypeIcon.Measure(availableSize);
            MutedIcon.Measure(availableSize);

            var line1Left = /*8 + PhotoPanel.DesiredSize.Width +*/ 8 + TypeIcon.DesiredSize.Width;
            var line1Right = availableSize.Width - 12 - TimeLabel.DesiredSize.Width - StateIcon.DesiredSize.Width;

            var titleWidth = Math.Max(0, line1Right - (line1Left + MutedIcon.DesiredSize.Width));

            TitleLabel.Measure(new Size(titleWidth, availableSize.Height));



            MinithumbnailPanel.Measure(availableSize);
            ForumTopicActionIndicator.Measure(availableSize);
            PinnedIcon.Measure(availableSize);
            UnreadBadge.Measure(availableSize);
            UnreadMentionsBadge.Measure(availableSize);

            var line2RightPadding = Math.Max(PinnedIcon.DesiredSize.Width, UnreadBadge.DesiredSize.Width);

            var line2Left = /*8 + PhotoPanel.DesiredSize.Width +*/ 8 + MinithumbnailPanel.DesiredSize.Width;
            var line2Right = availableSize.Width - 8 - line2RightPadding - UnreadMentionsBadge.DesiredSize.Width;

            var briefWidth = Math.Max(0, line2Right - line2Left);

            BriefInfo.Measure(new Size(briefWidth, availableSize.Height));
            CustomEmoji?.Measure(new Size(briefWidth, availableSize.Height));
            TypingLabel.Measure(new Size(briefWidth + MinithumbnailPanel.DesiredSize.Width, availableSize.Height));

            if (Children.Count > 13)
            {
                Children[13].Measure(availableSize);
            }

            return base.MeasureOverride(availableSize);

            return new Size(availableSize.Width, PhotoPanel.DesiredSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var PhotoPanel = Children[0];

            var TypeIcon = Children[1];
            var TitleLabel = Children[2];
            var MutedIcon = Children[3];
            var StateIcon = Children[4];
            var TimeLabel = Children[5];

            var MinithumbnailPanel = Children[6];
            var BriefInfo = Children[7];

            var shift = 0;
            var CustomEmoji = default(CustomEmojiCanvas);

            if (Children[8] is CustomEmojiCanvas)
            {
                shift++;
                CustomEmoji = Children[8] as CustomEmojiCanvas;
            }

            var ForumTopicActionIndicator = Children[8 + shift];
            var TypingLabel = Children[9 + shift];
            var PinnedIcon = Children[10 + shift];
            var UnreadMentionsBadge = Children[11 + shift];
            var UnreadBadge = Children[12 + shift];

            var rect = new Rect();
            var min = /*8 + PhotoPanel.DesiredSize.Width +*/ 8;

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
            rect.Y = 14;
            rect.Width = TypeIcon.DesiredSize.Width;
            rect.Height = TypeIcon.DesiredSize.Height;
            TypeIcon.Arrange(rect);

            var line1Left = min + TypeIcon.DesiredSize.Width;
            var line1Right = finalSize.Width - 8 - TimeLabel.DesiredSize.Width - StateIcon.DesiredSize.Width;

            double titleWidth;
            if (line1Left + TitleLabel.DesiredSize.Width + MutedIcon.DesiredSize.Width > line1Right)
            {
                titleWidth = Math.Max(0, line1Right - (line1Left + MutedIcon.DesiredSize.Width));
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
            rect.Y = 14;
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

            var line2RightPadding = Math.Max(PinnedIcon.DesiredSize.Width, UnreadBadge.DesiredSize.Width);

            var line2Left = min + MinithumbnailPanel.DesiredSize.Width;
            var line2Right = finalSize.Width - 8 - line2RightPadding - UnreadMentionsBadge.DesiredSize.Width;

            var briefWidth = Math.Max(0, line2Right - line2Left);

            rect.X = min + MinithumbnailPanel.DesiredSize.Width;
            rect.Y = 34;
            rect.Width = briefWidth;
            rect.Height = BriefInfo.DesiredSize.Height;
            BriefInfo.Arrange(rect);

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
            rect.Width = ForumTopicActionIndicator.DesiredSize.Width;
            rect.Height = ForumTopicActionIndicator.DesiredSize.Height;
            ForumTopicActionIndicator.Arrange(rect);

            line2Left = min + ForumTopicActionIndicator.DesiredSize.Width;
            line2Right = finalSize.Width - 8 - line2RightPadding - UnreadMentionsBadge.DesiredSize.Width;

            var typingLabel = Math.Max(0, line2Right - line2Left);

            rect.X = min + ForumTopicActionIndicator.DesiredSize.Width;
            rect.Y = 34;
            rect.Width = typingLabel;
            rect.Height = TypingLabel.DesiredSize.Height;
            TypingLabel.Arrange(rect);

            if (Children.Count > 13)
            {
                Children[13].Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            }

            return finalSize;
        }
    }
}
