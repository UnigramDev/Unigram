using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public sealed class MessageReference : MessageReferenceBase
    {
        public MessageReference()
        {
            DefaultStyleKey = typeof(MessageReference);
        }

        #region InitializeComponent

        private TextBlock Label;
        private Run TitleLabel;
        private Run ServiceLabel;
        private Run MessageLabel;

        // Lazy loaded
        private Border ThumbRoot;
        private Border ThumbEllipse;
        private ImageBrush ThumbImage;

        protected override void OnApplyTemplate()
        {
            Label = GetTemplateChild(nameof(Label)) as TextBlock;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            ServiceLabel = GetTemplateChild(nameof(ServiceLabel)) as Run;
            MessageLabel = GetTemplateChild(nameof(MessageLabel)) as Run;

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

        public void ToLightState()
        {
            _light = true;
            VisualStateManager.GoToState(this, "LightState", false);
        }

        public void ToNormalState()
        {
            _light = false;
            VisualStateManager.GoToState(this, "NormalState", false);
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

        protected override void SetText(MessageSender sender, string title, string service, FormattedText message)
        {
            if (TitleLabel != null)
            {
                TitleLabel.Text = title ?? string.Empty;
                ServiceLabel.Text = service ?? string.Empty;
                MessageLabel.Text = message?.ReplaceSpoilers() ?? string.Empty;

                if (!string.IsNullOrEmpty(message?.Text) && !string.IsNullOrEmpty(service))
                {
                    ServiceLabel.Text += ", ";
                }

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
                //else
                //{
                //    ClearValue(BorderBrushProperty);
                //    TitleLabel.ClearValue(TextElement.ForegroundProperty);
                //}
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
    }
}
