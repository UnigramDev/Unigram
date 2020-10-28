using System;
using System.Runtime.CompilerServices;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Messages
{
    public abstract class MessageReferenceBase : HyperlinkButton
    {
        public MessageReferenceBase()
        {
        }

        public long MessageId { get; private set; }

        #region Message

        public object Message
        {
            get { return (object)GetValue(MessageProperty); }
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

                SetTitle(embedded.WebPagePreview.SiteName);
                SetService(string.Empty);

                if (!string.IsNullOrEmpty(embedded.WebPagePreview.Title))
                {
                    SetMessage(embedded.WebPagePreview.Title);
                }
                else if (!string.IsNullOrEmpty(embedded.WebPagePreview.Author))
                {
                    SetMessage(embedded.WebPagePreview.Author);
                }
                else
                {
                    SetMessage(embedded.WebPagePreview.Url);
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
            SetTitle(sender);
            SetService(string.Empty);
            SetMessage(message);
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

                SetThumbnail(new BitmapImage(new Uri("file:///" + photoSize.Photo.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical });
            }
            else
            {
                SetThumbnail(null);

                if (photoSize != null && photoSize.Photo.Local.CanBeDownloaded && !photoSize.Photo.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(photoSize.Photo.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail)
        {
            if (thumbnail != null && thumbnail.File.Local.IsDownloadingCompleted && thumbnail.Format is ThumbnailFormatJpeg)
            {
                double ratioX = (double)36 / thumbnail.Width;
                double ratioY = (double)36 / thumbnail.Height;
                double ratio = Math.Max(ratioX, ratioY);

                var width = (int)(thumbnail.Width * ratio);
                var height = (int)(thumbnail.Height * ratio);

                SetThumbnail(new BitmapImage(new Uri("file:///" + thumbnail.File.Local.Path)) { DecodePixelWidth = width, DecodePixelHeight = height, DecodePixelType = DecodePixelType.Logical });
            }
            else
            {
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
        }

        private bool SetTextTemplate(MessageViewModel message, MessageText text, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(string.Empty);
            SetMessage(text.Text.Text.Replace("\r\n", "\n").Replace('\n', ' '));

            return true;
        }

        private bool SetDiceTemplate(MessageViewModel message, MessageDice dice, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(string.Empty);
            SetMessage(dice.Emoji);

            return true;
        }

        private bool SetPhotoTemplate(MessageViewModel message, MessagePhoto photo, string title)
        {
            Visibility = Visibility.Visible;

            // 🖼

            SetTitle(GetFromLabel(message, title));
            SetService(Strings.Resources.AttachPhoto);
            SetMessage(string.Empty);

            if (message.Ttl > 0)
            {
                HideThumbnail();
            }
            else
            {
                ShowThumbnail();

                var small = photo.Photo.GetSmall();
                if (small != null)
                {
                    UpdateThumbnail(message, small);
                }
            }

            if (photo.Caption != null && !string.IsNullOrWhiteSpace(photo.Caption.Text))
            {
                AppendService(", ");
                AppendMessage(photo.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            }

            return true;
        }

        private bool SetInvoiceTemplate(MessageViewModel message, MessageInvoice invoice, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(invoice.Title);
            SetMessage(string.Empty);

            return true;
        }

        private bool SetLocationTemplate(MessageViewModel message, MessageLocation location, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation);
            SetMessage(string.Empty);

            return true;
        }

        private bool SetVenueTemplate(MessageViewModel message, MessageVenue venue, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(Strings.Resources.AttachLocation + ", " + venue.Venue.Title.Replace("\r\n", "\n").Replace('\n', ' '));
            SetMessage(string.Empty);

            return true;
        }

        private bool SetCallTemplate(MessageViewModel message, MessageCall call, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            var outgoing = message.IsOutgoing;
            var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

            SetTitle(GetFromLabel(message, title));
            SetService(call.ToOutcomeText(message.IsOutgoing));
            SetMessage(string.Empty);

            return true;
        }

        private bool SetGameTemplate(MessageViewModel message, MessageGame game, string title)
        {
            Visibility = Visibility.Visible;

            ShowThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService($"\uD83C\uDFAE {game.Game.Title}");
            SetMessage(string.Empty);

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

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(Strings.Resources.AttachContact);
            SetMessage(string.Empty);

            return true;
        }

        private bool SetAudioTemplate(MessageViewModel message, MessageAudio audio, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            //SetService(Strings.Resources.AttachMusic;
            SetMessage(string.Empty);


            var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
            var audioTitle = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

            if (performer == null || audioTitle == null)
            {
                SetService(Strings.Resources.AttachMusic);
            }
            else
            {
                SetService($"\uD83C\uDFB5 {performer} - {audioTitle}");
            }

            if (audio.Caption != null && !string.IsNullOrWhiteSpace(audio.Caption.Text))
            {
                AppendService(", ");
                AppendMessage(audio.Caption.Text.Replace('\n', ' '));
            }

            return true;
        }

        private bool SetPollTemplate(MessageViewModel message, MessagePoll poll, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService($"\uD83D\uDCCA {poll.Poll.Question.Replace("\r\n", "\n").Replace('\n', ' ')}");
            SetMessage(string.Empty);

            return true;
        }

        private bool SetVoiceNoteTemplate(MessageViewModel message, MessageVoiceNote voiceNote, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(Strings.Resources.AttachAudio);
            SetMessage(string.Empty);

            if (voiceNote.Caption != null && !string.IsNullOrWhiteSpace(voiceNote.Caption.Text))
            {
                AppendService(", ");
                AppendMessage(voiceNote.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
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

            //        SetTitle(GetFromLabel(message, title);
            //        SetService(string.Empty;
            //        SetMessage(message.Message.Replace("\r\n", "\n").Replace('\n', ' ');

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

            SetTitle(GetFromLabel(message, title));
            SetService(Strings.Resources.AttachVideo);
            SetMessage(string.Empty);

            if (message.Ttl > 0)
            {
                HideThumbnail();
            }
            else
            {
                ShowThumbnail();

                if (video.Video.Thumbnail != null)
                {
                    UpdateThumbnail(message, video.Video.Thumbnail);
                }
            }

            if (video.Caption != null && !string.IsNullOrWhiteSpace(video.Caption.Text))
            {
                AppendService(", ");
                AppendMessage(video.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            }

            return true;
        }

        private bool SetVideoNoteTemplate(MessageViewModel message, MessageVideoNote videoNote, string title)
        {
            Visibility = Visibility.Visible;

            ShowThumbnail(new CornerRadius(18));

            SetTitle(GetFromLabel(message, title));
            SetService(Strings.Resources.AttachRound);
            SetMessage(string.Empty);

            if (videoNote.VideoNote.Thumbnail != null)
            {
                UpdateThumbnail(message, videoNote.VideoNote.Thumbnail);
            }

            return true;
        }

        private bool SetAnimationTemplate(MessageViewModel message, MessageAnimation animation, string title)
        {
            Visibility = Visibility.Visible;

            ShowThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(Strings.Resources.AttachGif);
            SetMessage(string.Empty);

            if (animation.Caption != null && !string.IsNullOrWhiteSpace(animation.Caption.Text))
            {
                AppendService(", ");
                AppendMessage(animation.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            }

            if (animation.Animation.Thumbnail != null)
            {
                UpdateThumbnail(message, animation.Animation.Thumbnail);
            }

            return true;
        }

        private bool SetStickerTemplate(MessageViewModel message, MessageSticker sticker, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(string.IsNullOrEmpty(sticker.Sticker.Emoji) ? Strings.Resources.AttachSticker : $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}");
            SetMessage(string.Empty);

            return true;
        }

        private bool SetDocumentTemplate(MessageViewModel message, MessageDocument document, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(document.Document.FileName);
            SetMessage(string.Empty);

            if (document.Caption != null && !string.IsNullOrWhiteSpace(document.Caption.Text))
            {
                AppendService(", ");
                AppendMessage(document.Caption.Text.Replace("\r\n", "\n").Replace('\n', ' '));
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

            //        SetTitle(GetFromLabel(message, title);
            //        SetService(document.FileName;
            //        SetMessage(string.Empty;

            //        if (!string.IsNullOrWhiteSpace(documentMedia.Caption))
            //        {
            //            AppendService(", ";
            //            AppendMessage(documentMedia.Caption.Replace("\r\n", "\n").Replace('\n', ' ');
            //        }
            //    }
            //}
            return true;
        }

        private bool SetServiceTextTemplate(MessageViewModel message, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(MessageService.GetText(message));
            SetMessage(string.Empty);

            return true;
        }

        private bool SetServicePhotoTemplate(MessageViewModel message, string title)
        {
            //Visibility = Visibility.Visible;

            //FindName(nameof(ThumbRoot));
            //if (ThumbRoot != null)
            //    ThumbRoot.Visibility = Visibility.Visible;

            //SetTitle(GetFromLabel(message, title);
            //SetService(string.Empty;
            //SetMessage(LegacyServiceHelper.Convert(message);

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

            HideThumbnail();

            SetTitle(string.Empty);
            SetService(Strings.Resources.Loading);
            SetMessage(string.Empty);
            return true;
        }

        private bool SetEmptyTemplate(Message message, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(string.Empty);
            SetService(message == null ? Strings.Additional.DeletedMessage : string.Empty);
            SetMessage(string.Empty);
            return true;
        }

        private bool SetUnsupportedMediaTemplate(MessageViewModel message, string title)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetTitle(GetFromLabel(message, title));
            SetService(Strings.Resources.UnsupportedAttachment);
            SetMessage(string.Empty);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetThumbnail(ImageSource value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void HideThumbnail();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void ShowThumbnail(CornerRadius radius = default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetTitle(string value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetService(string value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetMessage(string value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void AppendService(string value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void AppendMessage(string value);

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
}
