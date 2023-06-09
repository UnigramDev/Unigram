//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public sealed class MessageReference : MessageReferenceBase
    {
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

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            Label = GetTemplateChild(nameof(Label)) as TextBlock;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            ServiceLabel = GetTemplateChild(nameof(ServiceLabel)) as Run;
            MessageLabel = GetTemplateChild(nameof(MessageLabel)) as Span;

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

        private bool _light;
        private bool _tinted;

        private string _currentState;

        public void ToLightState()
        {
            if (_currentState != "LightState")
            {
                _light = true;
                _currentState = "LightState";
                VisualStateManager.GoToState(this, "LightState", false);
            }
        }

        public void ToNormalState()
        {
            if (_currentState != (_tinted ? "TintedState" : "NormalState"))
            {
                _light = false;
                _currentState = _tinted ? "TintedState" : "NormalState";
                VisualStateManager.GoToState(this, _tinted ? "TintedState" : "NormalState", false);
            }
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
                        BorderBrush = PlaceholderImage.GetBrush(user.UserId);
                        TitleLabel.Foreground = PlaceholderImage.GetBrush(user.UserId);
                    }
                    else if (sender is MessageSenderChat chat)
                    {
                        BorderBrush = PlaceholderImage.GetBrush(chat.ChatId);
                        TitleLabel.Foreground = PlaceholderImage.GetBrush(chat.ChatId);
                    }
                }

                MessageLabel.Inlines.Clear();

                if (text != null)
                {
                    var clean = text.ReplaceSpoilers();
                    var previous = 0;

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
                            }

                            var player = new CustomEmojiIcon();
                            player.Source = new CustomEmojiFileSource(message.ClientService, customEmoji.CustomEmojiId);
                            player.Margin = new Thickness(0, -4, 0, -4);
                            player.IsHitTestVisible = false;

                            var inline = new InlineUIContainer();
                            inline.Child = player;

                            MessageLabel.Inlines.Add(inline);

                            previous = entity.Offset + entity.Length;
                        }
                    }

                    if (clean.Length > previous)
                    {
                        MessageLabel.Inlines.Add(new Run { Text = clean.Substring(previous) });
                    }
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
                return Strings.AttachVideoExpired;
            }
            else if (message.Content is MessageExpiredPhoto)
            {
                return Strings.AttachPhotoExpired;
            }
            else if (message.Content is MessageVideoNote)
            {
                return Strings.AttachRound;
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return Strings.AttachSticker;
                }

                return $"{sticker.Sticker.Emoji} {Strings.AttachSticker}";
            }

            static string GetCaption(string caption)
            {
                return string.IsNullOrEmpty(caption) ? string.Empty : ", ";
            }

            if (message.Content is MessageVoiceNote voiceNote)
            {
                return Strings.AttachAudio + GetCaption(voiceNote.Caption.Text);
            }
            else if (message.Content is MessageVideo video)
            {
                return (video.IsSecret ? Strings.AttachDestructingVideo : Strings.AttachVideo) + GetCaption(video.Caption.Text);
            }
            else if (message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                return animatedEmoji.Emoji;
            }
            else if (message.Content is MessageAnimation animation)
            {
                return Strings.AttachGif + GetCaption(animation.Caption.Text);
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null || title == null)
                {
                    return Strings.AttachMusic + GetCaption(audio.Caption.Text);
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
                    return Strings.AttachDocument + GetCaption(document.Caption.Text);
                }

                return document.Document.FileName + GetCaption(document.Caption.Text);
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return invoice.Title;
            }
            else if (message.Content is MessageContact)
            {
                return Strings.AttachContact;
            }
            else if (message.Content is MessageLocation location)
            {
                return location.LivePeriod > 0 ? Strings.AttachLiveLocation : Strings.AttachLocation;
            }
            else if (message.Content is MessageVenue)
            {
                return Strings.AttachLocation;
            }
            else if (message.Content is MessagePhoto photo)
            {
                return (photo.IsSecret ? Strings.AttachDestructingPhoto : Strings.AttachPhoto) + GetCaption(photo.Caption.Text);
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
                return Strings.UnsupportedAttachment;
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

        public double ContentWidth { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Logger.Debug();

            if (ContentWidth > 0 && ContentWidth <= availableSize.Width)
            {
                LayoutRoot.Measure(new Size(Math.Max(144, ContentWidth), availableSize.Height));
                return LayoutRoot.DesiredSize;
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Logger.Debug();

            if (ContentWidth > 0 && ContentWidth <= finalSize.Width)
            {
                LayoutRoot.Arrange(new Rect(0, 0, finalSize.Width, LayoutRoot.DesiredSize.Height));
                return new Size(finalSize.Width, LayoutRoot.DesiredSize.Height);
            }

            return base.ArrangeOverride(finalSize);
        }
    }
}
