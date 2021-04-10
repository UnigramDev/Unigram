using LinqToVisualTree;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls.Messages
{
    public abstract class MessageReferenceBase : HyperlinkButton
    {
        public MessageReferenceBase()
        {
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessageReferenceAutomationPeer(this);
        }

        public long MessageId { get; private set; }

        #region Message

        public object Message
        {
            get { return GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(object), typeof(MessageReferenceBase), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MessageReferenceBase)d).OnMessageChanged(e.NewValue as MessageComposerHeader);
        }

        private void OnMessageChanged(MessageComposerHeader embedded)
        {
            if (embedded == null)
            {
                return;
            }

            if (embedded.WebPagePreview != null)
            {
                MessageId = 0;
                Visibility = Visibility.Visible;

                HideThumbnail();

                string message;
                if (!string.IsNullOrEmpty(embedded.WebPagePreview.Title))
                {
                    message = embedded.WebPagePreview.Title;
                }
                else if (!string.IsNullOrEmpty(embedded.WebPagePreview.Author))
                {
                    message = embedded.WebPagePreview.Author;
                }
                else
                {
                    message = embedded.WebPagePreview.Url;
                }

                SetText(embedded.WebPagePreview.SiteName,
                    string.Empty,
                    message);
            }
            else if (embedded.EditingMessage != null)
            {
                MessageId = embedded.EditingMessage.Id;
                GetMessageTemplate(embedded.EditingMessage, Strings.Resources.Edit);
            }
            else if (embedded.ReplyToMessage != null)
            {
                MessageId = embedded.ReplyToMessage.Id;
                GetMessageTemplate(embedded.ReplyToMessage, null);
            }
        }

        #endregion

        public void Mockup(string sender, string message)
        {
            SetText(sender, string.Empty, message);
        }

        public void UpdateMessageReply(MessageViewModel message)
        {
            if (message.ReplyToMessageState == ReplyToMessageState.Hidden || message.ReplyToMessageId == 0)
            {
                Visibility = Visibility.Collapsed;
            }
            else if (message.ReplyToMessage != null)
            {
                GetMessageTemplate(message.ReplyToMessage, null);
            }
            else if (message.ReplyToMessageState == ReplyToMessageState.Loading)
            {
                SetLoadingTemplate(null, null);
            }
            else if (message.ReplyToMessageState == ReplyToMessageState.Deleted)
            {
                SetEmptyTemplate(null, null);
            }
        }

        public void UpdateMessage(MessageViewModel message, bool loading, string title)
        {
            if (loading)
            {
                SetLoadingTemplate(null, title);
            }
            else
            {
                MessageId = message.Id;
                GetMessageTemplate(message, title);
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            // TODO: maybe something better...
            UpdateMessageReply(message);
        }

        private void UpdateThumbnail(MessageViewModel message, PhotoSize photoSize)
        {
            if (photoSize != null && photoSize.Photo.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)36 / photoSize.Width;
                double ratioY = (double)36 / photoSize.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(photoSize.Width * ratio);
                var height = (int)(photoSize.Height * ratio);

                ShowThumbnail();
                SetThumbnail(UriEx.ToBitmap(photoSize.Photo.Local.Path, width, height));
            }
            else
            {
                HideThumbnail();
                SetThumbnail(null);

                if (photoSize != null && photoSize.Photo.Local.CanBeDownloaded && !photoSize.Photo.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(photoSize.Photo.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, CornerRadius radius = default)
        {
            if (thumbnail != null && thumbnail.File.Local.IsDownloadingCompleted && thumbnail.Format is ThumbnailFormatJpeg)
            {
                double ratioX = (double)36 / thumbnail.Width;
                double ratioY = (double)36 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                ShowThumbnail(radius);
                SetThumbnail(UriEx.ToBitmap(thumbnail.File.Local.Path, width, height));
            }
            else
            {
                HideThumbnail();
                SetThumbnail(null);

                if (thumbnail != null && thumbnail.File.Local.CanBeDownloaded && !thumbnail.File.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(thumbnail.File.Id, 1);
                }
            }
        }

        #region Reply

        private bool GetMessageTemplate(MessageViewModel message, string title)
        {
            switch (message.Content)
            {
                case MessageText text:
                    return SetTextTemplate(message, text, title);
                case MessageAnimation animation:
                    return SetAnimationTemplate(message, animation, title);
                case MessageAudio audio:
                    return SetAudioTemplate(message, audio, title);
                case MessageCall call:
                    return SetCallTemplate(message, call, title);
                case MessageContact contact:
                    return SetContactTemplate(message, contact, title);
                case MessageDice dice:
                    return SetDiceTemplate(message, dice, title);
                case MessageDocument document:
                    return SetDocumentTemplate(message, document, title);
                case MessageGame game:
                    return SetGameTemplate(message, game, title);
                case MessageInvoice invoice:
                    return SetInvoiceTemplate(message, invoice, title);
                case MessageLocation location:
                    return SetLocationTemplate(message, location, title);
                case MessagePhoto photo:
                    return SetPhotoTemplate(message, photo, title);
                case MessagePoll poll:
                    return SetPollTemplate(message, poll, title);
                case MessageSticker sticker:
                    return SetStickerTemplate(message, sticker, title);
                case MessageUnsupported:
                    return SetUnsupportedMediaTemplate(message, title);
                case MessageVenue venue:
                    return SetVenueTemplate(message, venue, title);
                case MessageVideo video:
                    return SetVideoTemplate(message, video, title);
                case MessageVideoNote videoNote:
                    return SetVideoNoteTemplate(message, videoNote, title);
                case MessageVoiceNote voiceNote:
                    return SetVoiceNoteTemplate(message, voiceNote, title);

                case MessageBasicGroupChatCreate:
                case MessageChatAddMembers:
                case MessageChatChangePhoto:
                case MessageChatChangeTitle:
                case MessageChatDeleteMember:
                case MessageChatDeletePhoto:
                case MessageChatJoinByLink:
                case MessageChatSetTtl:
                case MessageChatUpgradeFrom:
                case MessageChatUpgradeTo:
                case MessageContactRegistered:
                case MessageCustomServiceAction:
                case MessageGameScore:
                case MessageInviteVoiceChatParticipants:
                case MessageProximityAlertTriggered:
                case MessagePassportDataSent:
                case MessagePaymentSuccessful:
                case MessagePinMessage:
                case MessageScreenshotTaken:
                case MessageSupergroupChatCreate:
                case MessageVoiceChatEnded:
                case MessageVoiceChatScheduled:
                case MessageVoiceChatStarted:
                case MessageWebsiteConnected:
                    return SetServiceTextTemplate(message, title);
                case MessageExpiredPhoto:
                case MessageExpiredVideo:
                    return SetServiceTextTemplate(message, title);
            }

            Visibility = Visibility.Collapsed;
            return false;
        }

        private bool SetTextTemplate(MessageViewModel message, MessageText text, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                string.Empty,
                text.Text.Text.Replace("\r\n", "\n").Replace('\n', ' '));

            return true;
        }

        private bool SetDiceTemplate(MessageViewModel message, MessageDice dice, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                string.Empty,
                dice.Emoji);

            return true;
        }

        private bool SetPhotoTemplate(MessageViewModel message, MessagePhoto photo, string title)
        {
            Visibility = Visibility.Visible;

            // 🖼

            SetText(GetFromLabel(message, title),
                Strings.Resources.AttachPhoto,
                string.Empty);

            if (message.Ttl > 0)
            {
                HideThumbnail();
            }
            else
            {
                UpdateThumbnail(message, photo.Photo.GetSmall());
            }

            if (photo.Caption != null && !string.IsNullOrWhiteSpace(photo.Caption.Text))
            {
                AppendText(", ", photo.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            }

            return true;
        }

        private bool SetInvoiceTemplate(MessageViewModel message, MessageInvoice invoice, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                invoice.Title,
                string.Empty);

            return true;
        }

        private bool SetLocationTemplate(MessageViewModel message, MessageLocation location, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation,
                string.Empty);

            return true;
        }

        private bool SetVenueTemplate(MessageViewModel message, MessageVenue venue, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                Strings.Resources.AttachLocation + ", " + venue.Venue.Title.Replace("\r\n", "\n").Replace('\n', ' '),
                string.Empty);

            return true;
        }

        private bool SetCallTemplate(MessageViewModel message, MessageCall call, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                call.ToOutcomeText(message.IsOutgoing),
                string.Empty);

            return true;
        }

        private bool SetGameTemplate(MessageViewModel message, MessageGame game, string title)
        {
            Visibility = Visibility.Visible;

            SetText(GetFromLabel(message, title),
                $"\uD83C\uDFAE {game.Game.Title}",
                string.Empty);

            UpdateThumbnail(message, game.Game.Photo?.GetSmall());

            return true;
        }

        private bool SetContactTemplate(MessageViewModel message, MessageContact contact, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                Strings.Resources.AttachContact,
                string.Empty);

            return true;
        }

        private bool SetAudioTemplate(MessageViewModel message, MessageAudio audio, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
            var audioTitle = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

            string service;
            if (performer == null || audioTitle == null)
            {
                service = Strings.Resources.AttachMusic;
            }
            else
            {
                service = $"\uD83C\uDFB5 {performer} - {audioTitle}";
            }

            SetText(GetFromLabel(message, title),
                service,
                string.Empty);

            if (audio.Caption != null && !string.IsNullOrWhiteSpace(audio.Caption.Text))
            {
                AppendText(", ", audio.Caption.Text.Replace('\n', ' '));
            }

            return true;
        }

        private bool SetPollTemplate(MessageViewModel message, MessagePoll poll, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                $"\uD83D\uDCCA {poll.Poll.Question.Replace("\r\n", "\n").Replace('\n', ' ')}",
                string.Empty);

            return true;
        }

        private bool SetVoiceNoteTemplate(MessageViewModel message, MessageVoiceNote voiceNote, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                Strings.Resources.AttachAudio,
                string.Empty);

            if (voiceNote.Caption != null && !string.IsNullOrWhiteSpace(voiceNote.Caption.Text))
            {
                AppendText(", ", voiceNote.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            }

            return true;
        }

        private bool SetVideoTemplate(MessageViewModel message, MessageVideo video, string title)
        {
            Visibility = Visibility.Visible;

            SetText(GetFromLabel(message, title),
                Strings.Resources.AttachVideo,
                string.Empty);

            if (message.Ttl > 0)
            {
                HideThumbnail();
            }
            else
            {
                UpdateThumbnail(message, video.Video.Thumbnail);
            }

            if (video.Caption != null && !string.IsNullOrWhiteSpace(video.Caption.Text))
            {
                AppendText(", ", video.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            }

            return true;
        }

        private bool SetVideoNoteTemplate(MessageViewModel message, MessageVideoNote videoNote, string title)
        {
            Visibility = Visibility.Visible;

            SetText(GetFromLabel(message, title),
                Strings.Resources.AttachRound,
                string.Empty);

            UpdateThumbnail(message, videoNote.VideoNote.Thumbnail, new CornerRadius(18));

            return true;
        }

        private bool SetAnimationTemplate(MessageViewModel message, MessageAnimation animation, string title)
        {
            Visibility = Visibility.Visible;

            SetText(GetFromLabel(message, title),
                Strings.Resources.AttachGif,
                string.Empty);

            if (animation.Caption != null && !string.IsNullOrWhiteSpace(animation.Caption.Text))
            {
                AppendText(", ", animation.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            }

            UpdateThumbnail(message, animation.Animation.Thumbnail);

            return true;
        }

        private bool SetStickerTemplate(MessageViewModel message, MessageSticker sticker, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                string.IsNullOrEmpty(sticker.Sticker.Emoji) ? Strings.Resources.AttachSticker : $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}",
                string.Empty);

            return true;
        }

        private bool SetDocumentTemplate(MessageViewModel message, MessageDocument document, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                document.Document.FileName,
                string.Empty);

            if (document.Caption != null && !string.IsNullOrWhiteSpace(document.Caption.Text))
            {
                AppendText(", ", document.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            }

            return true;
        }

        private bool SetServiceTextTemplate(MessageViewModel message, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                MessageService.GetText(message),
                string.Empty);

            return true;
        }

        private bool SetLoadingTemplate(MessageViewModel message, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(string.Empty,
                Strings.Resources.Loading,
                string.Empty);

            return true;
        }

        private bool SetEmptyTemplate(Message message, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(string.Empty,
                message == null ? Strings.Additional.DeletedMessage : string.Empty,
                string.Empty);

            return true;
        }

        private bool SetUnsupportedMediaTemplate(MessageViewModel message, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(GetFromLabel(message, title),
                Strings.Resources.UnsupportedAttachment,
                string.Empty);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetThumbnail(ImageSource value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void HideThumbnail();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void ShowThumbnail(CornerRadius radius = default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetText(string title, string service, string message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void AppendText(string service, string message);

        #endregion

        private string GetFromLabel(MessageViewModel message, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            if (message.ProtoService.TryGetChat(message.Sender, out Chat senderChat))
            {
                return message.ProtoService.GetTitle(senderChat);
            }
            else if (message.IsSaved())
            {
                var forward = message.ProtoService.GetTitle(message.ForwardInfo);
                if (forward != null)
                {
                    return forward;
                }
            }

            if (message.ProtoService.TryGetUser(message.Sender, out User user))
            {
                return user.GetFullName();
            }

            return title ?? string.Empty;
        }
    }

    public class MessageReferenceAutomationPeer : FrameworkElementAutomationPeer
    {
        private readonly MessageReferenceBase _owner;

        public MessageReferenceAutomationPeer(MessageReferenceBase owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            var builder = new StringBuilder();
            var descendants = _owner.DescendantsAndSelf<TextBlock>();

            foreach (TextBlock child in descendants)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(child.Text);
            }

            return builder.Replace(Environment.NewLine, ": ").ToString();
        }
    }
}
