using LinqToVisualTree;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Native;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages
{
    public abstract class MessageReferenceBase : HyperlinkButton
    {
        protected MessageViewModel _messageReply;

        protected MessageViewModel _message;
        protected bool _loading;
        protected string _title;

        protected bool _templateApplied;

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
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(object), typeof(MessageReferenceBase), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MessageReferenceBase)d).OnMessageChanged(e.NewValue as MessageComposerHeader);
        }

        protected void OnMessageChanged(MessageComposerHeader embedded)
        {
            if (embedded == null || !_templateApplied)
            {
                return;
            }

            if (embedded.WebPagePreview != null && !embedded.WebPageDisabled)
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

                SetText(null,
                    null,
                    embedded.WebPagePreview.SiteName,
                    string.Empty,
                    new FormattedText { Text = message });
            }
            else if (embedded.EditingMessage != null)
            {
                MessageId = embedded.EditingMessage.Id;
                GetMessageTemplate(embedded.EditingMessage, Strings.Resources.Edit, true);
            }
            else if (embedded.ReplyToMessage != null)
            {
                MessageId = embedded.ReplyToMessage.Id;
                GetMessageTemplate(embedded.ReplyToMessage, null, true);
            }
        }

        #endregion

        public void Mockup(string sender, string message)
        {
            SetText(null, null, sender, string.Empty, new FormattedText { Text = message });
        }

        public void UpdateMessageReply(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                _messageReply = message;
                return;
            }

            var outgoing = message.IsOutgoing && !message.IsChannelPost;
            outgoing &= !message.IsChannelPost;
            
            // TODO: chat type

            if (message.ReplyToMessageState == ReplyToMessageState.Hidden || message.ReplyToMessageId == 0)
            {
                Visibility = Visibility.Collapsed;
            }
            else if (message.ReplyToMessage != null)
            {
                GetMessageTemplate(message.ReplyToMessage, null, outgoing);
            }
            else if (message.ReplyToMessageState == ReplyToMessageState.Loading)
            {
                SetLoadingTemplate(null, null, outgoing);
            }
            else if (message.ReplyToMessageState == ReplyToMessageState.Deleted)
            {
                SetEmptyTemplate();
            }
        }

        public void UpdateMessage(MessageViewModel message, bool loading, string title)
        {
            if (!_templateApplied)
            {
                _message = message;
                _loading = loading;
                _title = title;
                return;
            }

            if (loading)
            {
                SetLoadingTemplate(null, title, true);
            }
            else
            {
                MessageId = message.Id;
                GetMessageTemplate(message, title, true);
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            // TODO: maybe something better...
            UpdateMessageReply(message);
        }

        private void UpdateThumbnail(MessageViewModel message, PhotoSize photoSize, Minithumbnail minithumbnail)
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
                UpdateThumbnail(message, minithumbnail);

                if (photoSize != null && photoSize.Photo.Local.CanBeDownloaded && !photoSize.Photo.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(photoSize.Photo.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, Minithumbnail minithumbnail, CornerRadius radius = default)
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
                UpdateThumbnail(message, minithumbnail);

                if (thumbnail != null && thumbnail.File.Local.CanBeDownloaded && !thumbnail.File.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(thumbnail.File.Id, 1);
                }
            }
        }


        private void UpdateThumbnail(MessageViewModel message, Minithumbnail thumbnail, CornerRadius radius = default)
        {
            if (thumbnail != null)
            {
                double ratioX = (double)36 / thumbnail.Width;
                double ratioY = (double)36 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                var bitmap = new BitmapImage { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };

                using (var stream = new InMemoryRandomAccessStream())
                {
                    PlaceholderImageHelper.Current.WriteBytes(thumbnail.Data, stream);
                    bitmap.SetSource(stream);
                }

                ShowThumbnail(radius);
                SetThumbnail(bitmap);
            }
            else
            {
                HideThumbnail();
                SetThumbnail(null);
            }
        }

        #region Reply

        private bool GetMessageTemplate(MessageViewModel message, string title, bool outgoing)
        {
            switch (message.Content)
            {
                case MessageText text:
                    return SetTextTemplate(message, text, title, outgoing);
                case MessageAnimatedEmoji animatedEmoji:
                    return SetAnimatedEmojiTemplate(message, animatedEmoji, title, outgoing);
                case MessageAnimation animation:
                    return SetAnimationTemplate(message, animation, title, outgoing);
                case MessageAudio audio:
                    return SetAudioTemplate(message, audio, title, outgoing);
                case MessageCall call:
                    return SetCallTemplate(message, call, title, outgoing);
                case MessageContact contact:
                    return SetContactTemplate(message, contact, title, outgoing);
                case MessageDice dice:
                    return SetDiceTemplate(message, dice, title, outgoing);
                case MessageDocument document:
                    return SetDocumentTemplate(message, document, title, outgoing);
                case MessageGame game:
                    return SetGameTemplate(message, game, title, outgoing);
                case MessageInvoice invoice:
                    return SetInvoiceTemplate(message, invoice, title, outgoing);
                case MessageLocation location:
                    return SetLocationTemplate(message, location, title, outgoing);
                case MessagePhoto photo:
                    return SetPhotoTemplate(message, photo, title, outgoing);
                case MessagePoll poll:
                    return SetPollTemplate(message, poll, title, outgoing);
                case MessageSticker sticker:
                    return SetStickerTemplate(message, sticker, title, outgoing);
                case MessageUnsupported:
                    return SetUnsupportedMediaTemplate(message, title, outgoing);
                case MessageVenue venue:
                    return SetVenueTemplate(message, venue, title, outgoing);
                case MessageVideo video:
                    return SetVideoTemplate(message, video, title, outgoing);
                case MessageVideoNote videoNote:
                    return SetVideoNoteTemplate(message, videoNote, title, outgoing);
                case MessageVoiceNote voiceNote:
                    return SetVoiceNoteTemplate(message, voiceNote, title, outgoing);

                case MessageBasicGroupChatCreate:
                case MessageChatAddMembers:
                case MessageChatChangePhoto:
                case MessageChatChangeTitle:
                case MessageChatSetTheme:
                case MessageChatDeleteMember:
                case MessageChatDeletePhoto:
                case MessageChatJoinByLink:
                case MessageChatJoinByRequest:
                case MessageChatSetTtl:
                case MessageChatUpgradeFrom:
                case MessageChatUpgradeTo:
                case MessageContactRegistered:
                case MessageCustomServiceAction:
                case MessageGameScore:
                case MessageGiftedPremium:
                case MessageInviteVideoChatParticipants:
                case MessageProximityAlertTriggered:
                case MessagePassportDataSent:
                case MessagePaymentSuccessful:
                case MessagePinMessage:
                case MessageScreenshotTaken:
                case MessageSupergroupChatCreate:
                case MessageVideoChatEnded:
                case MessageVideoChatScheduled:
                case MessageVideoChatStarted:
                case MessageWebsiteConnected:
                case MessageWebAppDataSent:
                    return SetServiceTextTemplate(message, title, outgoing);
                case MessageExpiredPhoto:
                case MessageExpiredVideo:
                    return SetServiceTextTemplate(message, title, outgoing);
            }

            Visibility = Visibility.Collapsed;
            return false;
        }

        private bool SetTextTemplate(MessageViewModel message, MessageText text, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                string.Empty,
                text.Text);

            return true;
        }

        private bool SetDiceTemplate(MessageViewModel message, MessageDice dice, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                dice.Emoji,
                null);

            return true;
        }

        private bool SetPhotoTemplate(MessageViewModel message, MessagePhoto photo, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            // 🖼

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                Strings.Resources.AttachPhoto,
                photo.Caption);

            if (message.Ttl > 0)
            {
                HideThumbnail();
            }
            else
            {
                UpdateThumbnail(message, photo.Photo.GetSmall(), photo.Photo.Minithumbnail);
            }

            return true;
        }

        private bool SetInvoiceTemplate(MessageViewModel message, MessageInvoice invoice, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            var caption = message.GetCaption();
            if (caption != null)
            {
                SetText(message.ClientService,
                    outgoing ? null : message.SenderId,
                    GetFromLabel(message, title),
                    null,
                    caption);
            }
            else
            {
                SetText(message.ClientService,
                    outgoing ? null : message.SenderId,
                    GetFromLabel(message, title),
                    invoice.Title,
                    null);
            }

            return true;
        }

        private bool SetLocationTemplate(MessageViewModel message, MessageLocation location, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation,
                null);

            return true;
        }

        private bool SetVenueTemplate(MessageViewModel message, MessageVenue venue, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                Strings.Resources.AttachLocation,
                new FormattedText(venue.Venue.Title, null));

            return true;
        }

        private bool SetCallTemplate(MessageViewModel message, MessageCall call, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                call.ToOutcomeText(message.IsOutgoing),
                null);

            return true;
        }

        private bool SetGameTemplate(MessageViewModel message, MessageGame game, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                $"\uD83C\uDFAE {game.Game.Title}",
                null);

            UpdateThumbnail(message, game.Game.Photo?.GetSmall(), game.Game.Photo?.Minithumbnail);

            return true;
        }

        private bool SetContactTemplate(MessageViewModel message, MessageContact contact, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                Strings.Resources.AttachContact,
                null);

            return true;
        }

        private bool SetAudioTemplate(MessageViewModel message, MessageAudio audio, string title, bool outgoing)
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

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                service,
                audio.Caption);

            return true;
        }

        private bool SetPollTemplate(MessageViewModel message, MessagePoll poll, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                $"\uD83D\uDCCA {poll.Poll.Question.Replace('\n', ' ')}",
                null);

            return true;
        }

        private bool SetVoiceNoteTemplate(MessageViewModel message, MessageVoiceNote voiceNote, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                Strings.Resources.AttachAudio,
                voiceNote.Caption);

            return true;
        }

        private bool SetVideoTemplate(MessageViewModel message, MessageVideo video, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                Strings.Resources.AttachVideo,
                video.Caption);

            if (message.Ttl > 0)
            {
                HideThumbnail();
            }
            else
            {
                UpdateThumbnail(message, video.Video.Thumbnail, video.Video.Minithumbnail);
            }

            return true;
        }

        private bool SetVideoNoteTemplate(MessageViewModel message, MessageVideoNote videoNote, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                Strings.Resources.AttachRound,
                null);

            UpdateThumbnail(message, videoNote.VideoNote.Thumbnail, videoNote.VideoNote.Minithumbnail, new CornerRadius(18));

            return true;
        }

        private bool SetAnimatedEmojiTemplate(MessageViewModel message, MessageAnimatedEmoji animatedEmoji, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                animatedEmoji.Emoji,
                null);

            HideThumbnail();

            return true;
        }

        private bool SetAnimationTemplate(MessageViewModel message, MessageAnimation animation, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                Strings.Resources.AttachGif,
                animation.Caption);

            UpdateThumbnail(message, animation.Animation.Thumbnail, animation.Animation.Minithumbnail);

            return true;
        }

        private bool SetStickerTemplate(MessageViewModel message, MessageSticker sticker, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                string.IsNullOrEmpty(sticker.Sticker.Emoji) ? Strings.Resources.AttachSticker : $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}",
                null);

            return true;
        }

        private bool SetDocumentTemplate(MessageViewModel message, MessageDocument document, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                document.Document.FileName,
                document.Caption);

            return true;
        }

        private bool SetServiceTextTemplate(MessageViewModel message, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                MessageService.GetText(message),
                null);

            return true;
        }

        private bool SetLoadingTemplate(MessageViewModel message, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(null,
                outgoing ? null : message?.SenderId,
                title,
                Strings.Resources.Loading,
                null);

            return true;
        }

        private bool SetEmptyTemplate()
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(null,
                null,
                null,
                Strings.Resources.lng_deleted_message,
                null);

            return true;
        }

        private bool SetUnsupportedMediaTemplate(MessageViewModel message, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(message.ClientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(message, title),
                Strings.Resources.UnsupportedAttachment,
                null);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetThumbnail(ImageSource value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void HideThumbnail();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void ShowThumbnail(CornerRadius radius = default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetText(IClientService clientService, MessageSender sender, string title, string service, FormattedText message);

        #endregion

        private string GetFromLabel(MessageViewModel message, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            var forwardedTitle = message.ClientService.GetTitle(message.ForwardInfo);
            if (forwardedTitle != null)
            {
                return forwardedTitle;
            }
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                return message.ClientService.GetTitle(senderChat);
            }
            if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                return user.FullName();
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
