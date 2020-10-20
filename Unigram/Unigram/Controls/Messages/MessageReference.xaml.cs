using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages
{
    public sealed partial class MessageReference : HyperlinkButton
    {
        public MessageReference()
        {
            InitializeComponent();
        }

        public long MessageId { get; private set; }

        #region Message

        public object Message
        {
            get { return (object)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(object), typeof(MessageReference), new PropertyMetadata(null, OnMessageChanged));

        private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MessageReference)d).OnMessageChanged(e.NewValue as MessageComposerHeader);
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

                if (ThumbRoot != null)
                    ThumbRoot.Visibility = Visibility.Collapsed;

                TitleLabel.Text = embedded.WebPagePreview.SiteName;
                ServiceLabel.Text = string.Empty;

                if (!string.IsNullOrEmpty(embedded.WebPagePreview.Title))
                {
                    MessageLabel.Text = embedded.WebPagePreview.Title;
                }
                else if (!string.IsNullOrEmpty(embedded.WebPagePreview.Author))
                {
                    MessageLabel.Text = embedded.WebPagePreview.Author;
                }
                else
                {
                    MessageLabel.Text = embedded.WebPagePreview.Url;
                }
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
            TitleLabel.Text = sender;
            ServiceLabel.Text = string.Empty;
            MessageLabel.Text = message;
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
            if (photoSize.Photo.Local.IsDownloadingCompleted)
            {
                double ratioX = (double)36 / photoSize.Width;
                double ratioY = (double)36 / photoSize.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(photoSize.Width * ratio);
                var height = (int)(photoSize.Height * ratio);

                ThumbImage.ImageSource = new BitmapImage(new Uri("file:///" + photoSize.Photo.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
            }
            else
            {
                ThumbImage.ImageSource = null;

                if (photoSize.Photo.Local.CanBeDownloaded && !photoSize.Photo.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(photoSize.Photo.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail)
        {
            if (thumbnail.File.Local.IsDownloadingCompleted && thumbnail.Format is ThumbnailFormatJpeg)
            {
                double ratioX = (double)36 / thumbnail.Width;
                double ratioY = (double)36 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                ThumbImage.ImageSource = new BitmapImage(new Uri("file:///" + thumbnail.File.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical };
            }
            else
            {
                ThumbImage.ImageSource = null;

                if (thumbnail.File.Local.CanBeDownloaded && !thumbnail.File.Local.IsDownloadingActive)
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
                case MessageUnsupported unsupported:
                    return SetUnsupportedMediaTemplate(message, title);
                case MessageVenue venue:
                    return SetVenueTemplate(message, venue, title);
                case MessageVideo video:
                    return SetVideoTemplate(message, video, title);
                case MessageVideoNote videoNote:
                    return SetVideoNoteTemplate(message, videoNote, title);
                case MessageVoiceNote voiceNote:
                    return SetVoiceNoteTemplate(message, voiceNote, title);

                case MessageBasicGroupChatCreate basicGroupChatCreate:
                case MessageChatAddMembers chatAddMembers:
                case MessageChatChangePhoto chatChangePhoto:
                case MessageChatChangeTitle chatChangeTitle:
                case MessageChatDeleteMember chatDeleteMember:
                case MessageChatDeletePhoto chatDeletePhoto:
                case MessageChatJoinByLink chatJoinByLink:
                case MessageChatSetTtl chatSetTtl:
                case MessageChatUpgradeFrom chatUpgradeFrom:
                case MessageChatUpgradeTo chatUpgradeTo:
                case MessageContactRegistered contactRegistered:
                case MessageCustomServiceAction customServiceAction:
                case MessageGameScore gameScore:
                case MessagePaymentSuccessful paymentSuccessful:
                case MessagePinMessage pinMessage:
                case MessageScreenshotTaken screenshotTaken:
                case MessageSupergroupChatCreate supergroupChatCreate:
                    return SetServiceTextTemplate(message, title);
                case MessageExpiredPhoto expiredPhoto:
                case MessageExpiredVideo expiredVideo:
                    return SetServiceTextTemplate(message, title);
            }

            Visibility = Visibility.Collapsed;
            return false;

            //var message = obj as TLMessage;
            //if (message != null)
            //{
            //    if (!string.IsNullOrEmpty(message.Message) && (message.Media == null || message.Media is TLMessageMediaEmpty))
            //    {
            //        return SetTextTemplate(message, Title);
            //    }

            //    var media = message.Media;
            //    if (media != null)
            //    {
            //        switch (media.TypeId)
            //        {
            //            case TLType.MessageMediaPhoto:
            //                return SetPhotoTemplate(message, Title);
            //            case TLType.MessageMediaGeo:
            //                return SetGeoTemplate(message, Title);
            //            case TLType.MessageMediaGeoLive:
            //                return SetGeoLiveTemplate(message, Title);
            //            case TLType.MessageMediaVenue:
            //                return SetVenueTemplate(message, Title);
            //            case TLType.MessageMediaContact:
            //                return SetContactTemplate(message, Title);
            //            case TLType.MessageMediaGame:
            //                return SetGameTemplate(message, Title);
            //            case TLType.MessageMediaEmpty:
            //                return SetUnsupportedTemplate(message, Title);
            //            case TLType.MessageMediaWebPage:
            //                return SetWebPageTemplate(message, Title);
            //            case TLType.MessageMediaDocument:
            //                if (message.IsSticker())
            //                {
            //                    return SetStickerTemplate(message, Title);
            //                }
            //                else if (message.IsGif())
            //                {
            //                    return SetGifTemplate(message, Title);
            //                }
            //                else if (message.IsVoice())
            //                {
            //                    return SetVoiceMessageTemplate(message, Title);
            //                }
            //                else if (message.IsVideo())
            //                {
            //                    return SetVideoTemplate(message, Title);
            //                }
            //                else if (message.IsRoundVideo())
            //                {
            //                    return SetRoundVideoTemplate(message, Title);
            //                }
            //                else if (message.IsAudio())
            //                {
            //                    return SetAudioTemplate(message, Title);
            //                }

            //                return SetDocumentTemplate(message, Title);
            //            case TLType.MessageMediaUnsupported:
            //                return SetUnsupportedMediaTemplate(message, Title);
            //        }
            //    }
            //}

            //var serviceMessage = obj as TLMessageService;
            //if (serviceMessage != null)
            //{
            //    var action = serviceMessage.Action;
            //    if (action is TLMessageActionChatEditPhoto)
            //    {
            //        return SetServicePhotoTemplate(serviceMessage, Title);
            //    }

            //    return SetServiceTextTemplate(serviceMessage, Title);
            //}
            //else
            //{
            //    var emptyMessage = obj as TLMessageEmpty;
            //    if (emptyMessage != null)
            //    {
            //        return SetEmptyTemplate(emptyMessage, Title);
            //    }

            //    return SetUnsupportedTemplate(message, Title);
            //}
        }

        private bool SetTextTemplate(MessageViewModel message, MessageText text, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = string.Empty;
            MessageLabel.Text = text.Text.Text.Replace("\r\n", "\n").Replace('\n', ' ');

            return true;
        }

        private bool SetDiceTemplate(MessageViewModel message, MessageDice dice, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = string.Empty;
            MessageLabel.Text = dice.Emoji;

            return true;
        }

        private bool SetPhotoTemplate(MessageViewModel message, MessagePhoto photo, string title)
        {
            Visibility = Visibility.Visible;

            // 🖼

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = Strings.Resources.AttachPhoto;
            MessageLabel.Text = string.Empty;

            if (message.Ttl > 0)
            {
                if (ThumbRoot != null)
                    ThumbRoot.Visibility = Visibility.Collapsed;
            }
            else
            {
                FindName(nameof(ThumbRoot));
                if (ThumbRoot != null)
                    ThumbRoot.Visibility = Visibility.Visible;

                ThumbRoot.CornerRadius = ThumbEllipse.CornerRadius = default(CornerRadius);
                //ThumbImage.ImageSource = (ImageSource)DefaultPhotoConverter.Convert(photoMedia, true);

                var small = photo.Photo.GetSmall();
                if (small != null)
                {
                    UpdateThumbnail(message, small);
                }
            }

            if (photo.Caption != null && !string.IsNullOrWhiteSpace(photo.Caption.Text))
            {
                ServiceLabel.Text += ", ";
                MessageLabel.Text += photo.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' ');
            }

            return true;
        }

        private bool SetInvoiceTemplate(MessageViewModel message, MessageInvoice invoice, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = invoice.Title;
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetLocationTemplate(MessageViewModel message, MessageLocation location, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation;
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetVenueTemplate(MessageViewModel message, MessageVenue venue, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = Strings.Resources.AttachLocation + ", " + venue.Venue.Title.Replace("\r\n", "\n").Replace('\n', ' ');
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetCallTemplate(MessageViewModel message, MessageCall call, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            var outgoing = message.IsOutgoing;
            var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = call.ToOutcomeText(message.IsOutgoing);
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetGameTemplate(MessageViewModel message, MessageGame game, string title)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = $"\uD83C\uDFAE {game.Game.Title}";
            MessageLabel.Text = string.Empty;

            ThumbRoot.CornerRadius = ThumbEllipse.CornerRadius = default(CornerRadius);

            var thumbnail = game.Game.Photo?.GetSmall();
            if (thumbnail != null)
            {
                UpdateThumbnail(message, thumbnail);
            }

            return true;
        }

        private bool SetContactTemplate(MessageViewModel message, MessageContact contact, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = Strings.Resources.AttachContact;
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetAudioTemplate(MessageViewModel message, MessageAudio audio, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            //ServiceLabel.Text = Strings.Resources.AttachMusic;
            MessageLabel.Text = string.Empty;


            var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
            var audioTitle = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

            if (performer == null || audioTitle == null)
            {
                ServiceLabel.Text = Strings.Resources.AttachMusic;
            }
            else
            {
                ServiceLabel.Text = $"\uD83C\uDFB5 {performer} - {audioTitle}";
            }

            if (audio.Caption != null && !string.IsNullOrWhiteSpace(audio.Caption.Text))
            {
                ServiceLabel.Text += ", ";
                MessageLabel.Text += audio.Caption.Text.Replace('\n', ' ');
            }

            return true;
        }

        private bool SetPollTemplate(MessageViewModel message, MessagePoll poll, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = $"\uD83D\uDCCA {poll.Poll.Question.Replace("\r\n", "\n").Replace('\n', ' ')}";
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetVoiceNoteTemplate(MessageViewModel message, MessageVoiceNote voiceNote, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = Strings.Resources.AttachAudio;
            MessageLabel.Text = string.Empty;

            if (voiceNote.Caption != null && !string.IsNullOrWhiteSpace(voiceNote.Caption.Text))
            {
                ServiceLabel.Text += ", ";
                MessageLabel.Text += voiceNote.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' ');
            }

            return true;
        }

        private bool SetWebPageTemplate(MessageViewModel message, MessageText text, string title)
        {
            //var webPageMedia = message.Media as TLMessageMediaWebPage;
            //if (webPageMedia != null)
            //{
            //    var webPage = webPageMedia.WebPage as TLWebPage;
            //    if (webPage != null && webPage.Photo != null && webPage.Type != null)
            //    {
            //        Visibility = Visibility.Visible;

            //        FindName(nameof(ThumbRoot));
            //        if (ThumbRoot != null)
            //            ThumbRoot.Visibility = Visibility.Visible;

            //        TitleLabel.Text = GetFromLabel(message, title);
            //        ServiceLabel.Text = string.Empty;
            //        MessageLabel.Text = message.Message.Replace("\r\n", "\n").Replace('\n', ' ');

            //        ThumbRoot.CornerRadius = ThumbEllipse.CornerRadius = default(CornerRadius);
            //        ThumbImage.ImageSource = (ImageSource)DefaultPhotoConverter.Convert(webPage.Photo, true);
            //    }
            //    else
            //    {
            //        return SetTextTemplate(message, title);
            //    }
            //}

            return true;
        }

        private bool SetVideoTemplate(MessageViewModel message, MessageVideo video, string title)
        {
            Visibility = Visibility.Visible;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = Strings.Resources.AttachVideo;
            MessageLabel.Text = string.Empty;

            if (message.Ttl > 0)
            {
                if (ThumbRoot != null)
                    ThumbRoot.Visibility = Visibility.Collapsed;
            }
            else
            {
                FindName(nameof(ThumbRoot));
                if (ThumbRoot != null)
                    ThumbRoot.Visibility = Visibility.Visible;

                ThumbRoot.CornerRadius = ThumbEllipse.CornerRadius = default(CornerRadius);

                if (video.Video.Thumbnail != null)
                {
                    UpdateThumbnail(message, video.Video.Thumbnail);
                }
            }

            if (video.Caption != null && !string.IsNullOrWhiteSpace(video.Caption.Text))
            {
                ServiceLabel.Text += ", ";
                MessageLabel.Text += video.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' ');
            }

            return true;
        }

        private bool SetVideoNoteTemplate(MessageViewModel message, MessageVideoNote videoNote, string title)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = Strings.Resources.AttachRound;
            MessageLabel.Text = string.Empty;

            ThumbRoot.CornerRadius = ThumbEllipse.CornerRadius = new CornerRadius(18);

            if (videoNote.VideoNote.Thumbnail != null)
            {
                UpdateThumbnail(message, videoNote.VideoNote.Thumbnail);
            }

            return true;
        }

        private bool SetAnimationTemplate(MessageViewModel message, MessageAnimation animation, string title)
        {
            Visibility = Visibility.Visible;

            FindName(nameof(ThumbRoot));
            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Visible;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = Strings.Resources.AttachGif;
            MessageLabel.Text = string.Empty;

            if (animation.Caption != null && !string.IsNullOrWhiteSpace(animation.Caption.Text))
            {
                ServiceLabel.Text += ", ";
                MessageLabel.Text += animation.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' ');
            }

            ThumbRoot.CornerRadius = ThumbEllipse.CornerRadius = default(CornerRadius);

            if (animation.Animation.Thumbnail != null)
            {
                UpdateThumbnail(message, animation.Animation.Thumbnail);
            }

            return true;
        }

        private bool SetStickerTemplate(MessageViewModel message, MessageSticker sticker, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = string.IsNullOrEmpty(sticker.Sticker.Emoji) ? Strings.Resources.AttachSticker : $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}";
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetDocumentTemplate(MessageViewModel message, MessageDocument document, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = document.Document.FileName;
            MessageLabel.Text = string.Empty;

            if (document.Caption != null && !string.IsNullOrWhiteSpace(document.Caption.Text))
            {
                ServiceLabel.Text += ", ";
                MessageLabel.Text += document.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' ');
            }

            return true;

            //var documentMedia = message.Media as TLMessageMediaDocument;
            //if (documentMedia != null)
            //{
            //    var document = documentMedia.Document as TLDocument;
            //    if (document != null)
            //    {
            //        var photoSize = document.Thumb as TLPhotoSize;
            //        var photoCachedSize = document.Thumb as TLPhotoCachedSize;
            //        if (photoCachedSize != null || photoSize != null)
            //        {
            //            Visibility = Visibility.Visible;

            //            FindName(nameof(ThumbRoot));
            //            if (ThumbRoot != null)
            //                ThumbRoot.Visibility = Visibility.Visible;

            //            ThumbRoot.CornerRadius = ThumbEllipse.CornerRadius = default(CornerRadius);
            //            ThumbImage.ImageSource = (ImageSource)DefaultPhotoConverter.Convert(documentMedia.Document, true);
            //        }
            //        else
            //        {
            //            Visibility = Visibility.Visible;

            //            if (ThumbRoot != null)
            //                ThumbRoot.Visibility = Visibility.Collapsed;
            //        }

            //        TitleLabel.Text = GetFromLabel(message, title);
            //        ServiceLabel.Text = document.FileName;
            //        MessageLabel.Text = string.Empty;

            //        if (!string.IsNullOrWhiteSpace(documentMedia.Caption))
            //        {
            //            ServiceLabel.Text += ", ";
            //            MessageLabel.Text += documentMedia.Caption.Replace("\r\n", "\n").Replace('\n', ' ');
            //        }
            //    }
            //}
            return true;
        }

        private bool SetServiceTextTemplate(MessageViewModel message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = MessageService.GetText(message);
            MessageLabel.Text = string.Empty;

            return true;
        }

        private bool SetServicePhotoTemplate(MessageViewModel message, string title)
        {
            //Visibility = Visibility.Visible;

            //FindName(nameof(ThumbRoot));
            //if (ThumbRoot != null)
            //    ThumbRoot.Visibility = Visibility.Visible;

            //TitleLabel.Text = GetFromLabel(message, title);
            //ServiceLabel.Text = string.Empty;
            //MessageLabel.Text = LegacyServiceHelper.Convert(message);

            //var action = message.Action as TLMessageActionChatEditPhoto;
            //if (action != null)
            //{
            //    ThumbRoot.CornerRadius = ThumbEllipse.CornerRadius = default(CornerRadius);
            //    ThumbImage.ImageSource = (ImageSource)DefaultPhotoConverter.Convert(action.Photo, true);
            //}

            return true;
        }

        private bool SetLoadingTemplate(MessageViewModel message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = string.Empty;
            ServiceLabel.Text = Strings.Resources.Loading;
            MessageLabel.Text = string.Empty;
            return true;
        }

        private bool SetEmptyTemplate(Message message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = string.Empty;
            ServiceLabel.Text = message == null ? Strings.Additional.DeletedMessage : string.Empty;
            MessageLabel.Text = string.Empty;
            return true;
        }

        private bool SetUnsupportedMediaTemplate(MessageViewModel message, string title)
        {
            Visibility = Visibility.Visible;

            if (ThumbRoot != null)
                ThumbRoot.Visibility = Visibility.Collapsed;

            TitleLabel.Text = GetFromLabel(message, title);
            ServiceLabel.Text = Strings.Resources.UnsupportedAttachment;
            MessageLabel.Text = string.Empty;

            return true;
        }

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

            string GetCaption(string caption)
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
                return (location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation);
            }
            else if (message.Content is MessageVenue vanue)
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

                case MessageDice dice:
                    return dice.Emoji;
            }

            return string.Empty;
        }
    }
}
