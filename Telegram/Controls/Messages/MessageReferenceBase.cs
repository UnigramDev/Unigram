//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages
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

            if (embedded.LinkPreview != null && !embedded.LinkPreviewDisabled)
            {
                MessageId = 0;
                Visibility = Visibility.Visible;

                HideThumbnail();

                string message;
                if (!string.IsNullOrEmpty(embedded.LinkPreview.Title))
                {
                    message = embedded.LinkPreview.Title;
                }
                else if (embedded.LinkPreview.HasAuthor(out string author))
                {
                    message = author;
                }
                else
                {
                    message = embedded.LinkPreview.Url;
                }

                SetText(null,
                    true,
                    null,
                    embedded.LinkPreview.SiteName,
                    string.Empty,
                    new FormattedText { Text = message });
            }
            else if (embedded.EditingMessage != null)
            {
                MessageId = embedded.EditingMessage.Id;
                GetMessageTemplate(embedded.EditingMessage.ClientService, embedded.EditingMessage, null, false, Strings.Edit, true, false, false);
            }
            else if (embedded.ReplyToMessage != null)
            {
                MessageId = embedded.ReplyToMessage.Id;
                GetMessageTemplate(embedded.ReplyToMessage.ClientService, embedded.ReplyToMessage, embedded.ReplyToQuote?.Text, false, embedded.ReplyToQuote != null ? Strings.ReplyToQuote : Strings.ReplyTo, true, false, false);
            }
        }

        #endregion

        public void Mockup(string sender, string message)
        {
            SetText(null, true, null, sender, string.Empty, new FormattedText { Text = message });
        }

        public void UpdateMessageReply(MessageViewModel message)
        {
            if (!_templateApplied)
            {
                _messageReply = message;
                return;
            }

            var outgoing = message.IsOutgoing && !message.IsChannelPost;
            var content = message.GeneratedContent ?? message.Content;
            var light = content is MessageSticker
                or MessageDice
                or MessageVideoNote
                or MessageBigEmoji
                or MessageAnimatedEmoji;

            // TODO: chat type

            if (message.ReplyToState == MessageReplyToState.Hidden || message.ReplyTo == null)
            {
                Visibility = Visibility.Collapsed;
            }
            else if (message.ReplyToItem is MessageViewModel replyToMessage && message.ReplyTo is MessageReplyToMessage replyToMessage1)
            {
                Visibility = Visibility.Visible;
                GetMessageTemplate(message.ClientService, replyToMessage, replyToMessage1.Quote?.Text, replyToMessage1.Quote?.IsManual ?? false, null, outgoing, light, message.ForwardInfo != null);
            }
            else if (message.ReplyToItem is Story replyToStory)
            {
                Visibility = Visibility.Visible;
                GetStoryTemplate(message.ClientService, replyToStory, null, outgoing, light);
            }
            else if (message.ReplyToItem is MessageReplyToMessage replyToMessageInfo)
            {
                Visibility = Visibility.Visible;
                GetMessageTemplate(message.ClientService, replyToMessageInfo, null, outgoing, light);
            }
            else if (message.ReplyToState == MessageReplyToState.Loading)
            {
                Visibility = Visibility.Visible;
                SetLoadingTemplate(message.ClientService, null, null, outgoing, light);
            }
            else if (message.ReplyToState == MessageReplyToState.Deleted)
            {
                Visibility = Visibility.Visible;
                SetEmptyTemplate(message.ClientService, message.ReplyTo, light);
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
                SetLoadingTemplate(message?.ClientService, null, title, true, false);
            }
            else
            {
                MessageId = message.Id;
                GetMessageTemplate(message.ClientService, message, null, false, title, true, false, message.ForwardInfo != null);
            }
        }

        public void UpdateFile(MessageViewModel message, File file)
        {
            // TODO: maybe something better...
            UpdateMessageReply(message);
        }

        private void UpdateThumbnail(IClientService clientService, PhotoSize photoSize, Minithumbnail minithumbnail)
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
                UpdateThumbnail(minithumbnail);

                if (photoSize != null && photoSize.Photo.Local.CanBeDownloaded && !photoSize.Photo.Local.IsDownloadingActive)
                {
                    clientService.DownloadFile(photoSize.Photo.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(IClientService clientService, Thumbnail thumbnail, Minithumbnail minithumbnail, CornerRadius radius = default)
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
                UpdateThumbnail(minithumbnail);

                if (thumbnail != null && thumbnail.File.Local.CanBeDownloaded && !thumbnail.File.Local.IsDownloadingActive)
                {
                    clientService.DownloadFile(thumbnail.File.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(Minithumbnail thumbnail, CornerRadius radius = default)
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
                    try
                    {
                        PlaceholderImageHelper.WriteBytes(thumbnail.Data, stream);
                        bitmap.SetSource(stream);
                    }
                    catch
                    {
                        // Throws when the data is not a valid encoded image,
                        // not so frequent, but if it happens during ContainerContentChanging it crashes the app.
                    }
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

        private void GetMessageTemplate(IClientService clientService, MessageViewModel message, FormattedText text, bool quote, string title, bool outgoing, bool white, bool forward)
        {
            MessageSender sender;
            if (title == null)
            {
                title = GetFromLabel(clientService, message, forward, out sender);
            }
            else
            {
                title = string.Format(title, GetFromLabel(clientService, message, forward, out sender));
            }

            switch (message.Content)
            {
                case MessageText text1:
                    SetTextTemplate(clientService, sender, text1, text, quote, title, outgoing, white);
                    break;
                case MessageAnimatedEmoji animatedEmoji:
                    SetAnimatedEmojiTemplate(clientService, sender, animatedEmoji, title, outgoing, white);
                    break;
                case MessageAnimation animation:
                    SetAnimationTemplate(clientService, sender, text, quote, animation, title, outgoing, white);
                    break;
                case MessageAudio audio:
                    SetAudioTemplate(clientService, sender, text, quote, audio, title, outgoing, white);
                    break;
                case MessageCall call:
                    SetCallTemplate(clientService, sender, call, title, outgoing, white);
                    break;
                case MessageContact contact:
                    SetContactTemplate(clientService, sender, contact, title, outgoing, white);
                    break;
                case MessageDice dice:
                    SetDiceTemplate(clientService, sender, dice, title, outgoing, white);
                    break;
                case MessageDocument document:
                    SetDocumentTemplate(clientService, sender, text, quote, document, title, outgoing, white);
                    break;
                case MessageGame game:
                    SetGameTemplate(clientService, sender, game, title, outgoing, white);
                    break;
                case MessageInvoice invoice:
                    SetInvoiceTemplate(clientService, sender, invoice, title, outgoing, white);
                    break;
                case MessagePaidAlbum paidAlbum:
                    SetPaidMediaTemplate(clientService, sender, paidAlbum, title, outgoing, white);
                    break;
                case MessagePaidMedia paidMedia:
                    SetPaidMediaTemplate(clientService, sender, paidMedia, title, outgoing, white);
                    break;
                case MessageLocation location:
                    SetLocationTemplate(clientService, sender, location, title, outgoing, white);
                    break;
                case MessagePhoto photo:
                    SetPhotoTemplate(clientService, sender, text, quote, photo, title, outgoing, white, message.SelfDestructType is null);
                    break;
                case MessagePoll poll:
                    SetPollTemplate(clientService, sender, poll, title, outgoing, white);
                    break;
                case MessageSticker sticker:
                    SetStickerTemplate(clientService, sender, sticker, title, outgoing, white);
                    break;
                case MessageStory story:
                    SetStoryTemplate(clientService, sender, story, title, outgoing, white);
                    break;
                case MessageUnsupported:
                    SetUnsupportedTemplate(clientService, message, title, outgoing, white);
                    break;
                case MessageVenue venue:
                    SetVenueTemplate(clientService, sender, venue, title, outgoing, white);
                    break;
                case MessageVideo video:
                    SetVideoTemplate(clientService, sender, text, quote, video, title, outgoing, white, message.SelfDestructType is null);
                    break;
                case MessageVideoNote videoNote:
                    SetVideoNoteTemplate(clientService, sender, videoNote, title, outgoing, white);
                    break;
                case MessageVoiceNote voiceNote:
                    SetVoiceNoteTemplate(clientService, sender, text, quote, voiceNote, title, outgoing, white);
                    break;
                case MessagePremiumGiveaway premiumGiveaway:
                    SetPremiumGiveawayTemplate(clientService, sender, premiumGiveaway, title, outgoing, white);
                    break;
                default:
                    SetServiceTextTemplate(clientService, message, title, outgoing, white);
                    break;
            }
        }

        private void GetMessageTemplate(IClientService clientService, MessageReplyToMessage message, string title, bool outgoing, bool white)
        {
            title = GetFromLabel(clientService, message, title);

            MessageSender sender = message.Origin switch
            {
                MessageOriginUser originUser => new MessageSenderUser(originUser.SenderUserId),
                MessageOriginChat fromChat => new MessageSenderChat(fromChat.SenderChatId),
                MessageOriginChannel fromChannel => new MessageSenderChat(fromChannel.ChatId),
                _ => null
            };

            var quote = message.Quote?.Text;
            var manual = message.Quote?.IsManual ?? false;

            switch (message.Content)
            {
                case MessageAnimation animation:
                    SetAnimationTemplate(clientService, sender, quote, manual, animation, title, outgoing, white);
                    break;
                case MessageAudio audio:
                    SetAudioTemplate(clientService, sender, quote, manual, audio, title, outgoing, white);
                    break;
                case MessageContact contact:
                    SetContactTemplate(clientService, sender, contact, title, outgoing, white);
                    break;
                case MessageDice dice:
                    SetDiceTemplate(clientService, sender, dice, title, outgoing, white);
                    break;
                case MessageDocument document:
                    SetDocumentTemplate(clientService, sender, quote, manual, document, title, outgoing, white);
                    break;
                case MessageGame game:
                    SetGameTemplate(clientService, sender, game, title, outgoing, white);
                    break;
                case MessageInvoice invoice:
                    SetInvoiceTemplate(clientService, sender, invoice, title, outgoing, white);
                    break;
                case MessagePaidAlbum paidAlbum:
                    SetPaidMediaTemplate(clientService, sender, paidAlbum, title, outgoing, white);
                    break;
                case MessagePaidMedia paidMedia:
                    SetPaidMediaTemplate(clientService, sender, paidMedia, title, outgoing, white);
                    break;
                case MessageLocation location:
                    SetLocationTemplate(clientService, sender, location, title, outgoing, white);
                    break;
                case MessagePhoto photo:
                    SetPhotoTemplate(clientService, sender, quote, manual, photo, title, outgoing, white, true);
                    break;
                case MessagePoll poll:
                    SetPollTemplate(clientService, sender, poll, title, outgoing, white);
                    break;
                case MessageSticker sticker:
                    SetStickerTemplate(clientService, sender, sticker, title, outgoing, white);
                    break;
                case MessageStory story:
                    SetStoryTemplate(clientService, sender, story, title, outgoing, white);
                    break;
                case MessageVenue venue:
                    SetVenueTemplate(clientService, sender, venue, title, outgoing, white);
                    break;
                case MessageVideo video:
                    SetVideoTemplate(clientService, sender, quote, manual, video, title, outgoing, white, true);
                    break;
                case MessageVideoNote videoNote:
                    SetVideoNoteTemplate(clientService, sender, videoNote, title, outgoing, white);
                    break;
                case MessageVoiceNote voiceNote:
                    SetVoiceNoteTemplate(clientService, sender, quote, manual, voiceNote, title, outgoing, white);
                    break;
                default:
                    SetReplyToMessageTemplate(clientService, message, sender, title, outgoing, white);
                    break;
            }
        }

        private void SetReplyToMessageTemplate(IClientService clientService, MessageReplyToMessage message, MessageSender sender, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                string.Empty,
                message.Quote?.Text,
                message.Quote?.IsManual ?? false,
                white);
        }

        private void GetStoryTemplate(IClientService clientService, Story story, string title, bool outgoing, bool white)
        {
            SetText(clientService,
                outgoing,
                new MessageSenderChat(story.SenderChatId),
                GetFromLabel(clientService, story, title),
                Strings.Story,
                null,
                false,
                white);

            switch (story.Content)
            {
                case StoryContentPhoto photo:
                    UpdateThumbnail(clientService, photo.Photo.GetSmall(), photo.Photo.Minithumbnail);
                    break;
                case StoryContentVideo video:
                    UpdateThumbnail(clientService, video.Video.Thumbnail, video.Video.Minithumbnail);
                    break;
                case StoryContentUnsupported:
                default:
                    HideThumbnail();
                    break;
            }
        }

        private void SetTextTemplate(IClientService clientService, MessageSender sender, MessageText text, FormattedText quote, bool qoote, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                string.Empty,
                quote ?? text.Text,
                qoote,
                white);
        }

        private void SetDiceTemplate(IClientService clientService, MessageSender sender, MessageDice dice, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                dice.Emoji,
                null,
                false,
                white);
        }

        private void SetPhotoTemplate(IClientService clientService, MessageSender sender, FormattedText text, bool quote, MessagePhoto photo, string title, bool outgoing, bool white, bool thumbnail)
        {
            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.AttachPhoto,
                text ?? photo.Caption,
                quote,
                white);

            if (thumbnail)
            {
                UpdateThumbnail(clientService, photo.Photo.GetSmall(), photo.Photo.Minithumbnail);
            }
            else
            {
                HideThumbnail();
            }
        }

        private void SetInvoiceTemplate(IClientService clientService, MessageSender sender, MessageInvoice invoice, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            var caption = invoice.PaidMediaCaption;
            if (caption != null && !string.IsNullOrEmpty(caption.Text))
            {
                SetText(clientService,
                    outgoing,
                    sender,
                    title,
                    null,
                    caption,
                    false,
                    white);
            }
            else
            {
                SetText(clientService,
                    outgoing,
                    sender,
                    title,
                    invoice.ProductInfo.Title,
                    null,
                    false,
                    white);
            }
        }

        private void SetPaidMediaTemplate(IClientService clientService, MessageSender sender, MessagePaidMedia paidMedia, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            var caption = paidMedia.Caption;
            if (caption != null && !string.IsNullOrEmpty(caption.Text))
            {
                SetText(clientService,
                    outgoing,
                    sender,
                    title,
                    Icons.Premium,
                    caption,
                    false,
                    white);
            }
            else
            {
                string text;
                if (paidMedia.Media.All(x => x.IsPhoto()))
                {
                    text = Icons.Premium + "\u2004" + (paidMedia.Media.Count > 1 ? Locale.Declension(Strings.R.Photos, paidMedia.Media.Count) : Strings.AttachPhoto);
                }
                else if (paidMedia.Media.All(x => x.IsVideo()))
                {
                    text = Icons.Premium + "\u2004" + (paidMedia.Media.Count > 1 ? Locale.Declension(Strings.R.Videos, paidMedia.Media.Count) : Strings.AttachVideo);
                }
                else
                {
                    text = Icons.Premium + "\u2004" + Locale.Declension(Strings.R.Media, paidMedia.Media.Count);
                }

                SetText(clientService,
                    outgoing,
                    sender,
                    title,
                    text,
                    null,
                    false,
                    white);
            }
        }

        private void SetPaidMediaTemplate(IClientService clientService, MessageSender sender, MessagePaidAlbum paidMedia, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            var caption = paidMedia.Caption;
            if (caption != null && !string.IsNullOrEmpty(caption.Text))
            {
                SetText(clientService,
                    outgoing,
                    sender,
                    title,
                    Icons.Premium,
                    caption,
                    false,
                    white);
            }
            else
            {
                string text;
                if (paidMedia.Media.All(x => x.IsPhoto()))
                {
                    text = Icons.Premium + "\u2004" + (paidMedia.Media.Count > 1 ? Locale.Declension(Strings.R.Photos, paidMedia.Media.Count) : Strings.AttachPhoto);
                }
                else if (paidMedia.Media.All(x => x.IsVideo()))
                {
                    text = Icons.Premium + "\u2004" + (paidMedia.Media.Count > 1 ? Locale.Declension(Strings.R.Videos, paidMedia.Media.Count) : Strings.AttachVideo);
                }
                else
                {
                    text = Icons.Premium + "\u2004" + Locale.Declension(Strings.R.Media, paidMedia.Media.Count);
                }

                SetText(clientService,
                    outgoing,
                    sender,
                    title,
                    text,
                    null,
                    false,
                    white);
            }
        }

        private void SetLocationTemplate(IClientService clientService, MessageSender sender, MessageLocation location, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                location.LivePeriod > 0 ? Strings.AttachLiveLocation : Strings.AttachLocation,
                null,
                false,
                white);
        }

        private void SetVenueTemplate(IClientService clientService, MessageSender sender, MessageVenue venue, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.AttachLocation,
                new FormattedText(venue.Venue.Title, null),
                false,
                white);
        }

        private void SetCallTemplate(IClientService clientService, MessageSender sender, MessageCall call, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                call.ToOutcomeText(outgoing),
                null,
                false,
                white);
        }

        private void SetGameTemplate(IClientService clientService, MessageSender sender, MessageGame game, string title, bool outgoing, bool white)
        {
            SetText(clientService,
                outgoing,
                sender,
                title,
                $"\uD83C\uDFAE {game.Game.Title}",
                null,
                false,
                white);

            UpdateThumbnail(clientService, game.Game.Photo?.GetSmall(), game.Game.Photo?.Minithumbnail);
        }

        private void SetContactTemplate(IClientService clientService, MessageSender sender, MessageContact contact, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.AttachContact,
                null,
                false,
                white);
        }

        private void SetAudioTemplate(IClientService clientService, MessageSender sender, FormattedText text, bool quote, MessageAudio audio, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
            var audioTitle = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

            string service;
            if (performer == null || audioTitle == null)
            {
                service = Strings.AttachMusic;
            }
            else
            {
                service = $"\uD83C\uDFB5 {performer} - {audioTitle}";
            }

            SetText(clientService,
                outgoing,
                sender,
                title,
                service,
                text ?? audio.Caption,
                quote,
                white);
        }

        private void SetPollTemplate(IClientService clientService, MessageSender sender, MessagePoll poll, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                $"\uD83D\uDCCA",
                poll.Poll.Question,
                false,
                white);
        }

        private void SetVoiceNoteTemplate(IClientService clientService, MessageSender sender, FormattedText text, bool quote, MessageVoiceNote voiceNote, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.AttachAudio,
                text ?? voiceNote.Caption,
                quote,
                white);
        }

        private void SetPremiumGiveawayTemplate(IClientService clientService, MessageSender sender, MessagePremiumGiveaway premiumGiveaway, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.BoostingGiveaway,
                null,
                false,
                white);
        }

        private void SetVideoTemplate(IClientService clientService, MessageSender sender, FormattedText text, bool quote, MessageVideo video, string title, bool outgoing, bool white, bool thumbnail)
        {
            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.AttachVideo,
                text ?? video.Caption,
                quote,
                white);

            if (thumbnail)
            {
                UpdateThumbnail(clientService, video.Video.Thumbnail, video.Video.Minithumbnail);
            }
            else
            {
                HideThumbnail();
            }
        }

        private void SetVideoNoteTemplate(IClientService clientService, MessageSender sender, MessageVideoNote videoNote, string title, bool outgoing, bool white)
        {
            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.AttachRound,
                null,
                false,
                white);

            UpdateThumbnail(clientService, videoNote.VideoNote.Thumbnail, videoNote.VideoNote.Minithumbnail, new CornerRadius(18));
        }

        private void SetAnimatedEmojiTemplate(IClientService clientService, MessageSender sender, MessageAnimatedEmoji animatedEmoji, string title, bool outgoing, bool white)
        {
            if (animatedEmoji.AnimatedEmoji?.Sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                SetText(clientService,
                    outgoing,
                    sender,
                    title,
                    string.Empty,
                    new FormattedText(animatedEmoji.Emoji, new[]
                    {
                        new TextEntity(0, animatedEmoji.Emoji.Length, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId))
                    }),
                    false,
                    white);
            }
            else
            {
                SetText(clientService,
                    outgoing,
                    sender,
                    title,
                    animatedEmoji.Emoji,
                    null,
                    false,
                    white);
            }

            HideThumbnail();
        }

        private void SetAnimationTemplate(IClientService clientService, MessageSender sender, FormattedText text, bool quote, MessageAnimation animation, string title, bool outgoing, bool white)
        {
            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.AttachGif,
                text ?? animation.Caption,
                quote,
                white);

            UpdateThumbnail(clientService, animation.Animation.Thumbnail, animation.Animation.Minithumbnail);
        }

        private void SetStickerTemplate(IClientService clientService, MessageSender sender, MessageSticker sticker, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                string.IsNullOrEmpty(sticker.Sticker.Emoji) ? Strings.AttachSticker : $"{sticker.Sticker.Emoji} {Strings.AttachSticker}",
                null,
                false,
                white);
        }

        private void SetStoryTemplate(IClientService clientService, MessageSender sender, MessageStory story, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                Strings.Story,
                null,
                false,
                white);
        }

        private void SetDocumentTemplate(IClientService clientService, MessageSender sender, FormattedText text, bool quote, MessageDocument document, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                sender,
                title,
                document.Document.FileName,
                text ?? document.Caption,
                quote,
                white);
        }

        private void SetServiceTextTemplate(IClientService clientService, MessageViewModel message, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                message.SenderId,
                title,
                MessageService.GetText(message),
                null,
                false,
                white);
        }

        private void SetLoadingTemplate(IClientService clientService, MessageSender sender, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(null,
                outgoing,
                sender,
                title,
                Strings.Loading,
                null,
                false,
                white);
        }

        private void SetEmptyTemplate(IClientService clientService, MessageReplyTo replyTo, bool white)
        {
            HideThumbnail();

            if (replyTo is MessageReplyToStory replyToStory)
            {
                if (clientService.TryGetChat(replyToStory.StorySenderChatId, out Chat chat))
                {
                    SetText(null,
                        true,
                        null,
                        chat.Title,
                        Icons.ExpiredStory + "\u00A0" + Strings.ExpiredStory,
                        null,
                        false,
                        white);
                }
                else
                {
                    SetText(null,
                        true,
                        null,
                        null,
                        Icons.ExpiredStory + "\u00A0" + Strings.ExpiredStory,
                        null,
                        false,
                        white);
                }
            }
            else
            {
                SetText(null,
                    true,
                    null,
                    null,
                    Strings.DeletedMessage,
                    null,
                    false,
                    white);
            }
        }

        private void SetUnsupportedTemplate(IClientService clientService, MessageViewModel message, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(clientService,
                outgoing,
                message.SenderId,
                title,
                Strings.UnsupportedAttachment,
                null,
                false,
                white);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetThumbnail(ImageSource value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void HideThumbnail();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void ShowThumbnail(CornerRadius radius = default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetText(IClientService clientService, bool outgoing, MessageSender sender, string title, string service, FormattedText text, bool quote = false, bool white = false);

        #endregion

        private string GetFromLabel(IClientService clientService, MessageViewModel message, bool forward, out MessageSender sender)
        {
            if (forward)
            {
                if (message.ForwardInfo?.Origin is MessageOriginUser fromUser && message.ClientService.TryGetUser(fromUser.SenderUserId, out User fromUserUser))
                {
                    sender = new MessageSenderUser(fromUser.SenderUserId);
                    return fromUserUser.FullName();
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChat fromChat && message.ClientService.TryGetChat(fromChat.SenderChatId, out Chat fromChatChat))
                {
                    sender = new MessageSenderChat(fromChat.SenderChatId);
                    return fromChatChat.Title;
                }
                else if (message.ForwardInfo?.Origin is MessageOriginChannel fromChannel && message.ClientService.TryGetChat(fromChannel.ChatId, out Chat fromChannelChat))
                {
                    sender = new MessageSenderChat(fromChannel.ChatId);
                    return fromChannelChat.Title;
                }
                else if (message.ForwardInfo?.Origin is MessageOriginHiddenUser fromHiddenUser)
                {
                    sender = null;
                    return fromHiddenUser.SenderName;
                }
                else if (message.ImportInfo != null)
                {
                    sender = null;
                    return message.ImportInfo.SenderName;
                }
            }
            
            if (clientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                sender = message.SenderId;
                return clientService.GetTitle(senderChat);
            }
            else if (clientService.TryGetUser(message.SenderId, out User user))
            {
                sender = message.SenderId;
                return user.FullName();
            }

            sender = null;
            return string.Empty;
        }

        private string GetFromLabel(IClientService clientService, MessageReplyToMessage message, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            if (message.Origin is MessageOriginUser fromUser)
            {
                var fullName = clientService.GetUser(fromUser.SenderUserId)?.FullName();

                if (clientService.TryGetChat(message.ChatId, out Chat senderChat))
                {
                    return fullName + Icons.Spacing + Icons.PeopleFilled16 + Icons.Spacing + senderChat.Title;
                }

                return Icons.PersonFilled16 + Icons.Spacing + fullName;
            }
            else if (message.Origin is MessageOriginChat fromChat)
            {
                return Icons.PeopleFilled16 + Icons.Spacing + clientService.GetTitle(fromChat.SenderChatId);
            }
            else if (message.Origin is MessageOriginChannel fromChannel)
            {
                return Icons.MegaphoneFilled16 + Icons.Spacing + clientService.GetTitle(fromChannel.ChatId);
            }
            else if (message.Origin is MessageOriginHiddenUser fromHiddenUser)
            {
                if (clientService.TryGetChat(message.ChatId, out Chat senderChat))
                {
                    return fromHiddenUser.SenderName + Icons.Spacing + Icons.PeopleFilled16 + Icons.Spacing + senderChat.Title;
                }

                return Icons.PersonFilled16 + Icons.Spacing + fromHiddenUser.SenderName;
            }

            return title ?? string.Empty;
        }

        private string GetFromLabel(IClientService clientService, Story story, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            if (clientService.TryGetUser(story.SenderChatId, out User user))
            {
                return user.FullName();
            }

            return title ?? string.Empty;
        }
    }
}
