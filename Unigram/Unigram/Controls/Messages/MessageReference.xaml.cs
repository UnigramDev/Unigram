using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public sealed class MessageReference : MessageReferenceBase, IPlayerView
    {
        private bool _ignoreLayoutUpdated = true;

        public MessageReference()
        {
            DefaultStyleKey = typeof(MessageReference);
        }

        #region InitializeComponent

        private Grid LayoutRoot;
        private TextBlock Label;
        private Run TitleLabel;
        private Run ServiceLabel;
        private Span MessageLabel;

        // Lazy loaded
        private Border ThumbRoot;
        private Border ThumbEllipse;
        private ImageBrush ThumbImage;

        private CustomEmojiCanvas CustomEmoji;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            Label = GetTemplateChild(nameof(Label)) as TextBlock;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            ServiceLabel = GetTemplateChild(nameof(ServiceLabel)) as Run;
            MessageLabel = GetTemplateChild(nameof(MessageLabel)) as Span;

            Label.LayoutUpdated += OnLayoutUpdated;

            _templateApplied = true;

            if (_light)
            {
                VisualStateManager.GoToState(this, "LightState", false);
            }

            if (_messageReply != null)
            {
                UpdateMessageReply(_messageReply);
            }
            else if (_message != null)
            {
                UpdateMessage(_message, _loading, _title);
            }
            else if (Message != null)
            {
                OnMessageChanged(Message as MessageComposerHeader);
            }
        }

        #endregion

        private readonly List<EmojiPosition> _positions = new();

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
                Label.LayoutUpdated -= OnLayoutUpdated;

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
                var pointer = MessageLabel.ContentStart.GetPositionAtOffset(item.X, LogicalDirection.Forward);
                if (pointer == null)
                {
                    continue;
                }

                var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
                if (rect.X + 20 > Label.ActualWidth && Label.IsTextTrimmed)
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
                Label.LayoutUpdated -= OnLayoutUpdated;

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

        private bool _light;
        private bool _tinted;

        public void ToLightState()
        {
            _light = true;
            VisualStateManager.GoToState(this, "LightState", false);
        }

        public void ToNormalState()
        {
            _light = false;
            VisualStateManager.GoToState(this, _tinted ? "TintedState" : "NormalState", false);
        }

        #region Overrides

        private static readonly CornerRadius _defaultRadius = new(2);

        protected override void HideThumbnail()
        {
            if (ThumbRoot != null)
            {
                ThumbRoot.Visibility = Visibility.Collapsed;
            }
        }

        protected override void ShowThumbnail(CornerRadius radius = default)
        {
            if (ThumbRoot == null)
            {
                ThumbRoot = GetTemplateChild(nameof(ThumbRoot)) as Border;
                ThumbEllipse = GetTemplateChild(nameof(ThumbEllipse)) as Border;
                ThumbImage = GetTemplateChild(nameof(ThumbImage)) as ImageBrush;
            }

            ThumbRoot.Visibility = Visibility.Visible;
            ThumbRoot.CornerRadius =
                ThumbEllipse.CornerRadius = radius == default ? _defaultRadius : radius;
        }

        protected override void SetThumbnail(ImageSource value)
        {
            if (ThumbImage != null)
            {
                ThumbImage.ImageSource = value;
            }
        }

        protected override void SetText(MessageViewModel message, MessageSender sender, string title, string service, FormattedText text)
        {
            if (TitleLabel != null)
            {
                TitleLabel.Text = title ?? string.Empty;
                ServiceLabel.Text = service ?? string.Empty;

                if (!string.IsNullOrEmpty(text?.Text) && !string.IsNullOrEmpty(service))
                {
                    ServiceLabel.Text += ", ";
                }

                if (_light || sender is null)
                {
                    ClearValue(BorderBrushProperty);
                    TitleLabel.ClearValue(TextElement.ForegroundProperty);

                    _tinted = false;
                    VisualStateManager.GoToState(this, _light ? "LightState" : "NormalState", false);
                }
                else
                {
                    _tinted = true;
                    VisualStateManager.GoToState(this, "TintedState", false);

                    if (sender is MessageSenderUser user)
                    {
                        BorderBrush = PlaceholderHelper.GetBrush(user.UserId);
                        TitleLabel.Foreground = PlaceholderHelper.GetBrush(user.UserId);
                    }
                    else if (sender is MessageSenderChat chat)
                    {
                        BorderBrush = PlaceholderHelper.GetBrush(chat.ChatId);
                        TitleLabel.Foreground = PlaceholderHelper.GetBrush(chat.ChatId);
                    }
                }

                _positions.Clear();
                MessageLabel.Inlines.Clear();

                if (text != null)
                {
                    var clean = text.ReplaceSpoilers();
                    var previous = 0;

                    var emoji = new HashSet<long>();
                    var shift = 0;

                    if (text.Entities != null)
                    {
                        foreach (var entity in text.Entities)
                        {
                            if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                            {
                                continue;
                            }

                            if (entity.Offset > previous)
                            {
                                MessageLabel.Inlines.Add(new Run { Text = clean.Substring(previous, entity.Offset - previous) });
                                shift += 2;
                            }

                            _positions.Add(new EmojiPosition { X = shift + entity.Offset + 1, CustomEmojiId = customEmoji.CustomEmojiId });
                            MessageLabel.Inlines.Add(new Run { Text = clean.Substring(entity.Offset, entity.Length), FontFamily = App.Current.Resources["SpoilerFontFamily"] as FontFamily });

                            emoji.Add(customEmoji.CustomEmojiId);
                            shift += 2;

                            previous = entity.Offset + entity.Length;
                        }
                    }

                    if (clean.Length > previous)
                    {
                        MessageLabel.Inlines.Add(new Run { Text = clean.Substring(previous) });
                    }

                    Label.LayoutUpdated -= OnLayoutUpdated;

                    if (emoji.Count > 0)
                    {
                        CustomEmoji ??= GetTemplateChild(nameof(CustomEmoji)) as CustomEmojiCanvas;
                        CustomEmoji.UpdateEntities(message.ClientService, emoji);

                        if (_playing)
                        {
                            CustomEmoji.Play();
                        }

                        _ignoreLayoutUpdated = false;
                        Label.LayoutUpdated += OnLayoutUpdated;
                    }
                    else if (CustomEmoji != null)
                    {
                        XamlMarkupHelper.UnloadObject(CustomEmoji);
                        CustomEmoji = null;
                    }

                }
                else if (CustomEmoji != null)
                {
                    Label.LayoutUpdated -= OnLayoutUpdated;

                    XamlMarkupHelper.UnloadObject(CustomEmoji);
                    CustomEmoji = null;
                }
            }
        }

        #endregion

        public static string GetServicePart(Message message)
        {
            if (message.Content is MessageGame gameMedia)
            {
                return "\uD83C\uDFAE " + gameMedia.Game.Title;
            }
            if (message.Content is MessageExpiredVideo)
            {
                return Strings.Resources.AttachVideoExpired;
            }
            else if (message.Content is MessageExpiredPhoto)
            {
                return Strings.Resources.AttachPhotoExpired;
            }
            else if (message.Content is MessageVideoNote)
            {
                return Strings.Resources.AttachRound;
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return Strings.Resources.AttachSticker;
                }

                return $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}";
            }

            static string GetCaption(string caption)
            {
                return string.IsNullOrEmpty(caption) ? string.Empty : ", ";
            }

            if (message.Content is MessageVoiceNote voiceNote)
            {
                return Strings.Resources.AttachAudio + GetCaption(voiceNote.Caption.Text);
            }
            else if (message.Content is MessageVideo video)
            {
                return (video.IsSecret ? Strings.Resources.AttachDestructingVideo : Strings.Resources.AttachVideo) + GetCaption(video.Caption.Text);
            }
            else if (message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                return animatedEmoji.Emoji;
            }
            else if (message.Content is MessageAnimation animation)
            {
                return Strings.Resources.AttachGif + GetCaption(animation.Caption.Text);
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null || title == null)
                {
                    return Strings.Resources.AttachMusic + GetCaption(audio.Caption.Text);
                }
                else
                {
                    return $"\uD83C\uDFB5 {performer} - {title}" + GetCaption(audio.Caption.Text);
                }
            }
            else if (message.Content is MessageDocument document)
            {
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    return Strings.Resources.AttachDocument + GetCaption(document.Caption.Text);
                }

                return document.Document.FileName + GetCaption(document.Caption.Text);
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return invoice.Title;
            }
            else if (message.Content is MessageContact)
            {
                return Strings.Resources.AttachContact;
            }
            else if (message.Content is MessageLocation location)
            {
                return location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation;
            }
            else if (message.Content is MessageVenue)
            {
                return Strings.Resources.AttachLocation;
            }
            else if (message.Content is MessagePhoto photo)
            {
                return (photo.IsSecret ? Strings.Resources.AttachDestructingPhoto : Strings.Resources.AttachPhoto) + GetCaption(photo.Caption.Text);
            }
            else if (message.Content is MessagePoll poll)
            {
                return $"\uD83D\uDCCA {poll.Poll.Question}";
            }
            else if (message.Content is MessageCall call)
            {
                return call.ToOutcomeText(message.IsOutgoing);
            }
            else if (message.Content is MessageUnsupported)
            {
                return Strings.Resources.UnsupportedAttachment;
            }

            return string.Empty;
        }

        public static string GetTextPart(Message value)
        {
            switch (value.Content)
            {
                case MessageAnimation animation:
                    return animation.Caption.Text.Replace('\n', ' ');
                case MessageAudio audio:
                    return audio.Caption.Text.Replace('\n', ' ');
                case MessageDocument document:
                    return document.Caption.Text.Replace('\n', ' ');
                case MessagePhoto photo:
                    return photo.Caption.Text.Replace('\n', ' ');
                case MessageVideo video:
                    return video.Caption.Text.Replace('\n', ' ');
                case MessageVoiceNote voiceNote:
                    return voiceNote.Caption.Text.Replace('\n', ' ');

                case MessageText text:
                    return text.Text.Text.Replace('\n', ' ');

                case MessageAnimatedEmoji animatedEmoji:
                    return animatedEmoji.Emoji;

                case MessageDice dice:
                    return dice.Emoji;
            }

            return string.Empty;
        }

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

        public double ContentWidth { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (ContentWidth > 0 && ContentWidth <= availableSize.Width)
            {
                LayoutRoot.Measure(new Size(Math.Max(144, ContentWidth), availableSize.Height));
                return LayoutRoot.DesiredSize;
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (ContentWidth > 0 && ContentWidth <= finalSize.Width)
            {
                LayoutRoot.Arrange(new Rect(0, 0, finalSize.Width, LayoutRoot.DesiredSize.Height));
                return new Size(finalSize.Width, LayoutRoot.DesiredSize.Height);
            }

            return base.ArrangeOverride(finalSize);
        }
    }
}
