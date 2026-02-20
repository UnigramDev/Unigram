//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public abstract class MessageReferenceBase : HyperlinkButton
    {
        protected MessageComposerHeader _composerHeader;

        protected MessageViewModel _messageReply;

        protected MessageViewModel _message;
        protected bool _loading;
        protected string _title;

        protected ThumbnailController _thumbnailController;

        protected bool _templateApplied;

        public MessageReferenceBase()
        {
        }

        public MessageViewModel Message { get; private set; }

        #region Message

        public void UpdateComposerHeader(MessageComposerHeader embedded)
        {
            _composerHeader = embedded;

            if (embedded == null || !_templateApplied)
            {
                return;
            }

            if (embedded.SuggestedPostInfo != null)
            {
                Message = null;
                GetSuggestedPostInfoTemplate(embedded.SuggestedPostInfo.Price, embedded.SuggestedPostInfo.SendDate);
            }
            else if (embedded.LinkPreview != null && !embedded.LinkPreviewDisabled)
            {
                Message = null;
                Visibility = Visibility.Visible;

                HideThumbnail();

                string message;
                if (!string.IsNullOrEmpty(embedded.LinkPreview.Title))
                {
                    message = embedded.LinkPreview.Title;
                }
                else if (!string.IsNullOrEmpty(embedded.LinkPreview.Author))
                {
                    message = embedded.LinkPreview.Author;
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
                    message.AsFormattedText());
            }
            else if (embedded.Editing != null)
            {
                Message = embedded.Editing.Message;
                GetMessageTemplate(embedded.Editing.Message, null, false, 0, Strings.Edit, true, false, false);
            }
            else if (embedded.ReplyTo != null)
            {
                Message = embedded.ReplyTo.Message;
                GetMessageTemplate(embedded.ReplyTo.Message, embedded.ReplyTo.Quote?.Text, false, embedded.ReplyTo.ChecklistTaskId, embedded.ReplyTo.Quote != null ? Strings.ReplyToQuote : Strings.ReplyTo, true, false, false);
            }
        }

        #endregion

        private void GetSuggestedPostInfoTemplate(SuggestedPostPrice price, int sendDate)
        {
            // 1F4C6	
            if (price == null && sendDate == 0)
            {
                SetText(null, false, null, Strings.SuggestAPostBelow, null, Strings.SuggestAPostBelowSubtitle.AsFormattedText());
            }
            else if (sendDate == 0)
            {
                if (price is SuggestedPostPriceStar priceStar)
                {
                    SetText(null, false, null, Strings.SuggestAPostBelow, null, string.Format(Strings.SuggestAPostBelowSubtitleStars.ReplaceStar(Icons.Premium), priceStar.StarCount).AsFormattedText());
                }
                else if (price is SuggestedPostPriceTon priceTon)
                {
                    SetText(null, false, null, Strings.SuggestAPostBelow, null, string.Format(Strings.SuggestAPostBelowSubtitleStars.ReplaceStar(Icons.Ton), priceTon.ToncoinCentCount / 100d).AsFormattedText());
                }
            }
            else if (price is SuggestedPostPriceStar priceStar)
            {
                SetText(null, false, null, Strings.SuggestAPostBelow, null, string.Format(Strings.SuggestAPostBelowSubtitleStarsAndTime.ReplaceStar(Icons.Premium), priceStar.StarCount, string.Format("\U0001F4C6 {0}", Formatter.DateAt(sendDate))).AsFormattedText());
            }
            else if (price is SuggestedPostPriceTon priceTon)
            {
                SetText(null, false, null, Strings.SuggestAPostBelow, null, string.Format(Strings.SuggestAPostBelowSubtitleStarsAndTime.ReplaceStar(Icons.Ton), priceTon.ToncoinCentCount / 100d, string.Format("\U0001F4C6 {0}", Formatter.DateAt(sendDate))).AsFormattedText());
            }
            else
            {
                SetText(null, false, null, Strings.SuggestAPostBelow, null, string.Format(Strings.SuggestAPostBelowSubtitleStarsAndTime.ReplaceStar(Icons.Premium), 0, string.Format("\U0001F4C6 {0}", Formatter.DateAt(sendDate))).AsFormattedText());
            }
        }

        public void Mockup(string sender, string message)
        {
            SetText(null, true, null, sender, string.Empty, message.AsFormattedText());
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
                or MessageStakeDice
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
                GetMessageTemplate(replyToMessage, replyToMessage1.Quote?.Text, replyToMessage1.Quote?.IsManual ?? false, replyToMessage1.ChecklistTaskId, null, outgoing, light, message.ForwardInfo != null);
            }
            else if (message.ReplyToItem is Story replyToStory)
            {
                Visibility = Visibility.Visible;
                GetStoryTemplate(message, replyToStory, null, outgoing, light);
            }
            else if (message.ReplyToItem is MessageReplyToMessage replyToMessageInfo)
            {
                Visibility = Visibility.Visible;
                GetMessageTemplate(message, replyToMessageInfo, null, outgoing, light);
            }
            else if (message.ReplyToState == MessageReplyToState.Loading)
            {
                Visibility = Visibility.Visible;
                SetLoadingTemplate(message, null, null, outgoing, light);
            }
            else if (message.ReplyToState == MessageReplyToState.Deleted)
            {
                Visibility = Visibility.Visible;
                SetEmptyTemplate(message, message.ReplyTo, light);
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
                Message = null;
                SetLoadingTemplate(message, null, title, true, false);
            }
            else
            {
                Message = message;
                GetMessageTemplate(message, null, false, 0, title, true, false, message.ForwardInfo != null);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, PhotoSize photoSize, Minithumbnail minithumbnail, bool hasSpoiler = false)
        {
            if (photoSize != null && photoSize.Photo.Local.IsDownloadingCompleted)
            {
                if (_thumbnailController == null)
                {
                    _thumbnailController = new ThumbnailController(ShowThumbnail());
                }
                else
                {
                    ShowThumbnail();
                }

                if (hasSpoiler)
                {
                    _thumbnailController.Blur(photoSize.Photo.Local.Path, 15, HashCode.Combine(message.ChatId, message.Id));
                }
                else
                {
                    double ratioX = (double)36 / photoSize.Width;
                    double ratioY = (double)36 / photoSize.Height;
                    double ratio = Math.Max(ratioX, ratioY);

                    var width = (int)(photoSize.Width * ratio);
                    var height = (int)(photoSize.Height * ratio);

                    _thumbnailController.Bitmap(photoSize.Photo.Local.Path, width, height, HashCode.Combine(message.ChatId, message.Id));
                }
            }
            else
            {
                UpdateThumbnail(minithumbnail, hasSpoiler, default, HashCode.Combine(message.ChatId, message.Id));

                if (photoSize != null && photoSize.Photo.Local.CanBeDownloaded && !photoSize.Photo.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(photoSize.Photo.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Thumbnail thumbnail, Minithumbnail minithumbnail, bool hasSpoiler = false, CornerRadius radius = default)
        {
            if (thumbnail != null && thumbnail.File.Local.IsDownloadingCompleted && thumbnail.Format is ThumbnailFormatJpeg)
            {
                if (_thumbnailController == null)
                {
                    _thumbnailController = new ThumbnailController(ShowThumbnail(radius));
                }
                else
                {
                    ShowThumbnail(radius);
                }

                if (hasSpoiler)
                {
                    _thumbnailController.Blur(thumbnail.File.Local.Path, 15, HashCode.Combine(message.ChatId, message.Id));
                }
                else
                {
                    double ratioX = (double)36 / thumbnail.Width;
                    double ratioY = (double)36 / thumbnail.Height;
                    double ratio = Math.Max(ratioX, ratioY);

                    var width = (int)(thumbnail.Width * ratio);
                    var height = (int)(thumbnail.Height * ratio);

                    _thumbnailController.Bitmap(thumbnail.File.Local.Path, width, height, HashCode.Combine(message.ChatId, message.Id));
                }
            }
            else
            {
                UpdateThumbnail(minithumbnail, hasSpoiler, radius, HashCode.Combine(message.ChatId, message.Id));

                if (thumbnail != null && thumbnail.File.Local.CanBeDownloaded && !thumbnail.File.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(thumbnail.File.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(Minithumbnail thumbnail, bool hasSpoiler, CornerRadius radius, int hashCode)
        {
            if (thumbnail != null)
            {
                if (_thumbnailController == null)
                {
                    _thumbnailController = new ThumbnailController(ShowThumbnail(radius));
                }
                else
                {
                    ShowThumbnail(radius);
                }

                if (hasSpoiler)
                {
                    _thumbnailController.Blur(thumbnail.Data, 15, hashCode);
                }
                else
                {
                    double ratioX = (double)36 / thumbnail.Width;
                    double ratioY = (double)36 / thumbnail.Height;
                    double ratio = Math.Max(ratioX, ratioY);

                    var width = (int)(thumbnail.Width * ratio);
                    var height = (int)(thumbnail.Height * ratio);

                    _thumbnailController.Bitmap(thumbnail.Data, width, height, hashCode);
                }
            }
            else
            {
                HideThumbnail();
            }
        }

        #region Reply

        private void GetMessageTemplate(MessageViewModel message, FormattedText quote, bool manual, int checklistTaskId, string title, bool outgoing, bool white, bool forward)
        {
            MessageSender sender;
            if (title == null)
            {
                title = GetFromLabel(message, forward, out sender);
            }
            else
            {
                title = string.Format(title, GetFromLabel(message, forward, out sender));
            }

            switch (message.Content)
            {
                case MessageText text1:
                    SetTextTemplate(message, sender, text1, quote, manual, title, outgoing, white);
                    break;
                case MessageAnimatedEmoji animatedEmoji:
                    SetAnimatedEmojiTemplate(message, sender, animatedEmoji, title, outgoing, white);
                    break;
                case MessageAnimation animation:
                    SetAnimationTemplate(message, sender, quote, manual, animation, title, outgoing, white);
                    break;
                case MessageAudio audio:
                    SetAudioTemplate(message, sender, quote, manual, audio, title, outgoing, white);
                    break;
                case MessageCall call:
                    SetCallTemplate(message, sender, call, title, outgoing, white);
                    break;
                case MessageGroupCall groupCall:
                    SetGroupCallTemplate(message, sender, groupCall, title, outgoing, white);
                    break;
                case MessageContact contact:
                    SetContactTemplate(message, sender, contact, title, outgoing, white);
                    break;
                case MessageDice dice:
                    SetDiceTemplate(message, sender, dice, title, outgoing, white);
                    break;
                case MessageStakeDice stakeDice:
                    SetStakeDiceTemplate(message, sender, stakeDice, title, outgoing, white);
                    break;
                case MessageDocument document:
                    SetDocumentTemplate(message, sender, quote, manual, document, title, outgoing, white);
                    break;
                case MessageGame game:
                    SetGameTemplate(message, sender, game, title, outgoing, white);
                    break;
                case MessageGiveaway giveaway:
                    SetGiveawayTemplate(message, sender, giveaway, title, outgoing, white);
                    break;
                case MessageGiveawayWinners giveawayWinners:
                    SetGiveawayWinnersTemplate(message, sender, giveawayWinners, title, outgoing, white);
                    break;
                case MessageInvoice invoice:
                    SetInvoiceTemplate(message, sender, invoice, title, outgoing, white);
                    break;
                case MessagePaidAlbum paidAlbum:
                    SetPaidMediaTemplate(message, sender, paidAlbum, title, outgoing, white);
                    break;
                case MessagePaidMedia paidMedia:
                    SetPaidMediaTemplate(message, sender, paidMedia, title, outgoing, white);
                    break;
                case MessageLocation location:
                    SetLocationTemplate(message, sender, location, title, outgoing, white);
                    break;
                case MessagePhoto photo:
                    SetPhotoTemplate(message, sender, quote, manual, photo, title, outgoing, white, message.SelfDestructType is null);
                    break;
                case MessagePoll poll:
                    SetPollTemplate(message, sender, poll, title, outgoing, white);
                    break;
                case MessageChecklist checklist:
                    SetChecklistTemplate(message, sender, checklist, checklistTaskId, title, outgoing, white);
                    break;
                case MessageSticker sticker:
                    SetStickerTemplate(message, sender, sticker, title, outgoing, white);
                    break;
                case MessageStory story:
                    SetStoryTemplate(message, sender, story, title, outgoing, white);
                    break;
                case MessageUnsupported:
                    SetUnsupportedTemplate(message, title, outgoing, white);
                    break;
                case MessageVenue venue:
                    SetVenueTemplate(message, sender, venue, title, outgoing, white);
                    break;
                case MessageVideo video:
                    SetVideoTemplate(message, sender, quote, manual, video, title, outgoing, white, message.SelfDestructType is null);
                    break;
                case MessageVideoNote videoNote:
                    SetVideoNoteTemplate(message, sender, videoNote, title, outgoing, white);
                    break;
                case MessageVoiceNote voiceNote:
                    SetVoiceNoteTemplate(message, sender, quote, manual, voiceNote, title, outgoing, white);
                    break;
                default:
                    SetServiceTextTemplate(message, title, outgoing, white);
                    break;
            }
        }

        private void GetMessageTemplate(MessageViewModel message, MessageReplyToMessage replyToMessage, string title, bool outgoing, bool white)
        {
            title = GetFromLabel(message, replyToMessage, title);

            MessageSender sender = replyToMessage.Origin switch
            {
                MessageOriginUser originUser => new MessageSenderUser(originUser.SenderUserId),
                MessageOriginChat fromChat => new MessageSenderChat(fromChat.SenderChatId),
                MessageOriginChannel fromChannel => new MessageSenderChat(fromChannel.ChatId),
                _ => null
            };

            var quote = replyToMessage.Quote?.Text;
            var manual = replyToMessage.Quote?.IsManual ?? false;

            switch (replyToMessage.Content)
            {
                case MessageAnimation animation:
                    SetAnimationTemplate(message, sender, quote, manual, animation, title, outgoing, white);
                    break;
                case MessageAudio audio:
                    SetAudioTemplate(message, sender, quote, manual, audio, title, outgoing, white);
                    break;
                case MessageContact contact:
                    SetContactTemplate(message, sender, contact, title, outgoing, white);
                    break;
                case MessageDice dice:
                    SetDiceTemplate(message, sender, dice, title, outgoing, white);
                    break;
                case MessageStakeDice stakeDice:
                    SetStakeDiceTemplate(message, sender, stakeDice, title, outgoing, white);
                    break;
                case MessageDocument document:
                    SetDocumentTemplate(message, sender, quote, manual, document, title, outgoing, white);
                    break;
                case MessageGame game:
                    SetGameTemplate(message, sender, game, title, outgoing, white);
                    break;
                case MessageInvoice invoice:
                    SetInvoiceTemplate(message, sender, invoice, title, outgoing, white);
                    break;
                case MessagePaidAlbum paidAlbum:
                    SetPaidMediaTemplate(message, sender, paidAlbum, title, outgoing, white);
                    break;
                case MessagePaidMedia paidMedia:
                    SetPaidMediaTemplate(message, sender, paidMedia, title, outgoing, white);
                    break;
                case MessageLocation location:
                    SetLocationTemplate(message, sender, location, title, outgoing, white);
                    break;
                case MessagePhoto photo:
                    SetPhotoTemplate(message, sender, quote, manual, photo, title, outgoing, white, true);
                    break;
                case MessagePoll poll:
                    SetPollTemplate(message, sender, poll, title, outgoing, white);
                    break;
                case MessageChecklist checklist:
                    SetChecklistTemplate(message, sender, checklist, replyToMessage.ChecklistTaskId, title, outgoing, white);
                    break;
                case MessageSticker sticker:
                    SetStickerTemplate(message, sender, sticker, title, outgoing, white);
                    break;
                case MessageStory story:
                    SetStoryTemplate(message, sender, story, title, outgoing, white);
                    break;
                case MessageVenue venue:
                    SetVenueTemplate(message, sender, venue, title, outgoing, white);
                    break;
                case MessageVideo video:
                    SetVideoTemplate(message, sender, quote, manual, video, title, outgoing, white, true);
                    break;
                case MessageVideoNote videoNote:
                    SetVideoNoteTemplate(message, sender, videoNote, title, outgoing, white);
                    break;
                case MessageVoiceNote voiceNote:
                    SetVoiceNoteTemplate(message, sender, quote, manual, voiceNote, title, outgoing, white);
                    break;
                default:
                    SetReplyToMessageTemplate(message, replyToMessage, sender, title, outgoing, white);
                    break;
            }
        }

        private void SetReplyToMessageTemplate(MessageViewModel message, MessageReplyToMessage replyToMessage, MessageSender sender, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                string.Empty,
                replyToMessage.Quote?.Text,
                replyToMessage.Quote?.IsManual ?? false,
                white);
        }

        private void GetStoryTemplate(MessageViewModel message, Story story, string title, bool outgoing, bool white)
        {
            SetText(null,
                outgoing,
                new MessageSenderChat(story.PosterChatId),
                GetFromLabel(message, story, title),
                Strings.Story,
                null,
                false,
                white);

            switch (story.Content)
            {
                case StoryContentPhoto photo:
                    UpdateThumbnail(message, photo.Photo.GetSmall(), photo.Photo.Minithumbnail);
                    break;
                case StoryContentVideo video:
                    UpdateThumbnail(message, video.Video.Thumbnail, video.Video.Minithumbnail);
                    break;
                case StoryContentUnsupported:
                default:
                    HideThumbnail();
                    break;
            }
        }

        private void SetTextTemplate(MessageViewModel message, MessageSender sender, MessageText text, FormattedText quote, bool qoote, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                string.Empty,
                quote,
                qoote,
                white);
        }

        private void SetDiceTemplate(MessageViewModel message, MessageSender sender, MessageDice dice, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                dice.Emoji,
                null,
                false,
                white);
        }

        private void SetStakeDiceTemplate(MessageViewModel message, MessageSender sender, MessageStakeDice stakeDice, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                Constants.StakeDice,
                null,
                false,
                white);
        }

        private void SetPhotoTemplate(MessageViewModel message, MessageSender sender, FormattedText quote, bool manual, MessagePhoto photo, string title, bool outgoing, bool white, bool thumbnail)
        {
            SetText(message,
                outgoing,
                sender,
                title,
                quote != null ? null : Strings.AttachPhoto,
                quote,
                manual,
                white);

            if (thumbnail)
            {
                UpdateThumbnail(message, photo.Photo.GetSmall(), photo.Photo.Minithumbnail, photo.HasSpoiler);
            }
            else
            {
                HideThumbnail();
            }
        }

        private void SetInvoiceTemplate(MessageViewModel message, MessageSender sender, MessageInvoice invoice, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            // TODO: caption?

            var caption = invoice.PaidMediaCaption;
            if (caption != null && !string.IsNullOrEmpty(caption.Text))
            {
                SetText(message,
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
                SetText(message,
                    outgoing,
                    sender,
                    title,
                    invoice.ProductInfo.Title,
                    null,
                    false,
                    white);
            }
        }

        private void SetPaidMediaTemplate(MessageViewModel message, MessageSender sender, MessagePaidMedia paidMedia, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            var caption = paidMedia.Caption;
            if (caption != null && !string.IsNullOrEmpty(caption.Text))
            {
                SetText(message,
                    outgoing,
                    sender,
                    title,
                    Icons.Premium,
                    null,
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

                SetText(message,
                    outgoing,
                    sender,
                    title,
                    text,
                    null,
                    false,
                    white);
            }
        }

        private void SetPaidMediaTemplate(MessageViewModel message, MessageSender sender, MessagePaidAlbum paidMedia, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            var caption = paidMedia.Caption;
            if (caption != null && !string.IsNullOrEmpty(caption.Text))
            {
                SetText(message,
                    outgoing,
                    sender,
                    title,
                    Icons.Premium,
                    null,
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

                SetText(message,
                    outgoing,
                    sender,
                    title,
                    text,
                    null,
                    false,
                    white);
            }
        }

        private void SetLocationTemplate(MessageViewModel message, MessageSender sender, MessageLocation location, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                location.LivePeriod > 0 ? Strings.AttachLiveLocation : Strings.AttachLocation,
                null,
                false,
                white);
        }

        private void SetVenueTemplate(MessageViewModel message, MessageSender sender, MessageVenue venue, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            // TODO: formatted text?
            SetText(message,
                outgoing,
                sender,
                title,
                Strings.AttachLocation,
                venue.Venue.Title.AsFormattedText(),
                false,
                white);
        }

        private void SetCallTemplate(MessageViewModel message, MessageSender sender, MessageCall call, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                call.ToOutcomeText(outgoing),
                null,
                false,
                white);
        }

        private void SetGroupCallTemplate(MessageViewModel message, MessageSender sender, MessageGroupCall call, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                call.ToOutcomeText(outgoing),
                null,
                false,
                white);
        }

        private void SetGameTemplate(MessageViewModel message, MessageSender sender, MessageGame game, string title, bool outgoing, bool white)
        {
            SetText(message,
                outgoing,
                sender,
                title,
                $"\uD83C\uDFAE {game.Game.Title}",
                null,
                false,
                white);

            UpdateThumbnail(message, game.Game.Photo.GetSmall(), game.Game.Photo?.Minithumbnail);
        }

        private void SetContactTemplate(MessageViewModel message, MessageSender sender, MessageContact contact, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                Strings.AttachContact,
                null,
                false,
                white);
        }

        private void SetAudioTemplate(MessageViewModel message, MessageSender sender, FormattedText quote, bool manual, MessageAudio audio, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                quote != null ? null : $"\uD83C\uDFB5 {audio.Audio.GetTitle()}",
                quote,
                manual,
                white);
        }

        private void SetPollTemplate(MessageViewModel message, MessageSender sender, MessagePoll poll, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                $"\uD83D\uDCCA",
                poll.Poll.Question,
                false,
                white);
        }

        private void SetChecklistTemplate(MessageViewModel message, MessageSender sender, MessageChecklist checklist, int checklistTaskId, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            var task = checklistTaskId > 0 ? checklist.List.Tasks.FirstOrDefault(x => x.Id == checklistTaskId) : null;
            if (task != null)
            {
                SetText(message,
                    outgoing,
                    sender,
                    title,
                    string.Empty,
                    TdExtensions.Concat(ClientEx.CustomEmoji(task.CompletionDate != 0 ? "\uEACF " : "\uEAD0 "), task.Text),
                    false,
                    white);
            }
            else
            {
                SetText(message,
                    outgoing,
                    sender,
                    title,
                    string.Empty,
                    ClientEx.Format("\u2611 {0}", checklist.List.Title),
                    false,
                    white);
            }
        }

        private void SetVoiceNoteTemplate(MessageViewModel message, MessageSender sender, FormattedText quote, bool manual, MessageVoiceNote voiceNote, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                quote != null ? null : Strings.AttachAudio,
                quote,
                manual,
                white);
        }

        private void SetGiveawayTemplate(MessageViewModel message, MessageSender sender, MessageGiveaway giveaway, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                Strings.BoostingGiveaway,
                null,
                false,
                white);
        }

        private void SetGiveawayWinnersTemplate(MessageViewModel message, MessageSender sender, MessageGiveawayWinners giveaway, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                Strings.BoostingGiveawayResults,
                null,
                false,
                white);
        }

        private void SetVideoTemplate(MessageViewModel message, MessageSender sender, FormattedText quote, bool manual, MessageVideo video, string title, bool outgoing, bool white, bool thumbnail)
        {
            SetText(message,
                outgoing,
                sender,
                title,
                quote != null ? null : Strings.AttachVideo,
                quote,
                manual,
                white);

            if (thumbnail)
            {
                if (video.Cover != null)
                {
                    UpdateThumbnail(message, video.Cover.GetSmall(), video.Cover.Minithumbnail, video.HasSpoiler);
                }
                else
                {
                    UpdateThumbnail(message, video.Video.Thumbnail, video.Video.Minithumbnail, video.HasSpoiler);
                }
            }
            else
            {
                HideThumbnail();
            }
        }

        private void SetVideoNoteTemplate(MessageViewModel message, MessageSender sender, MessageVideoNote videoNote, string title, bool outgoing, bool white)
        {
            SetText(message,
                outgoing,
                sender,
                title,
                Strings.AttachRound,
                null,
                false,
                white);

            UpdateThumbnail(message, videoNote.VideoNote.Thumbnail, videoNote.VideoNote.Minithumbnail, radius: new CornerRadius(18));
        }

        private void SetAnimatedEmojiTemplate(MessageViewModel message, MessageSender sender, MessageAnimatedEmoji animatedEmoji, string title, bool outgoing, bool white)
        {
            SetText(message,
                outgoing,
                sender,
                title,
                string.Empty,
                null,
                false,
                white);

            HideThumbnail();
        }

        private void SetAnimationTemplate(MessageViewModel message, MessageSender sender, FormattedText quote, bool manual, MessageAnimation animation, string title, bool outgoing, bool white)
        {
            SetText(message,
                outgoing,
                sender,
                title,
                quote != null ? null : Strings.AttachGif,
                quote,
                manual,
                white);

            UpdateThumbnail(message, animation.Animation.Thumbnail, animation.Animation.Minithumbnail, animation.HasSpoiler);
        }

        private void SetStickerTemplate(MessageViewModel message, MessageSender sender, MessageSticker sticker, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                string.IsNullOrEmpty(sticker.Sticker.Emoji) ? Strings.AttachSticker : $"{sticker.Sticker.Emoji} {Strings.AttachSticker}",
                null,
                false,
                white);
        }

        private void SetStoryTemplate(MessageViewModel message, MessageSender sender, MessageStory story, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                Strings.Story,
                null,
                false,
                white);
        }

        private void SetDocumentTemplate(MessageViewModel message, MessageSender sender, FormattedText quote, bool manual, MessageDocument document, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                sender,
                title,
                quote != null ? null : document.Document.FileName,
                quote,
                manual,
                white);
        }

        private void SetServiceTextTemplate(MessageViewModel message, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                message.SenderId,
                title,
                MessageService.GetText(message),
                null,
                false,
                white);
        }

        private void SetLoadingTemplate(MessageViewModel message, MessageSender sender, string title, bool outgoing, bool white)
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

        private void SetEmptyTemplate(MessageViewModel message, MessageReplyTo replyTo, bool white)
        {
            HideThumbnail();

            if (replyTo is MessageReplyToStory replyToStory)
            {
                if (message.ClientService.TryGetChat(replyToStory.StoryPosterChatId, out Chat chat))
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

        private void SetUnsupportedTemplate(MessageViewModel message, string title, bool outgoing, bool white)
        {
            HideThumbnail();

            SetText(message,
                outgoing,
                message.SenderId,
                title,
                Strings.UnsupportedAttachment,
                null,
                false,
                white);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void HideThumbnail();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract ImageBrush ShowThumbnail(CornerRadius radius = default);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract void SetText(MessageViewModel message, bool outgoing, MessageSender sender, string title, string service, FormattedText quote, bool manual = false, bool white = false);

        #endregion

        private string GetFromLabel(MessageViewModel message, bool forward, out MessageSender sender)
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

            if (message.ClientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                sender = message.SenderId;
                return message.ClientService.GetTitle(senderChat);
            }
            else if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                sender = message.SenderId;
                return user.FullName();
            }

            sender = null;
            return string.Empty;
        }

        private string GetFromLabel(MessageViewModel message, MessageReplyToMessage replyToMessage, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            if (replyToMessage.Origin is MessageOriginUser fromUser)
            {
                var fullName = message.ClientService.GetUser(fromUser.SenderUserId)?.FullName();

                if (message.ClientService.TryGetChat(replyToMessage.ChatId, out Chat senderChat))
                {
                    return fullName + Icons.Spacing + Icons.PeopleFilled16 + Icons.Spacing + senderChat.Title;
                }

                return Icons.PersonFilled16 + Icons.Spacing + fullName;
            }
            else if (replyToMessage.Origin is MessageOriginChat fromChat)
            {
                return Icons.PeopleFilled16 + Icons.Spacing + message.ClientService.GetTitle(fromChat.SenderChatId);
            }
            else if (replyToMessage.Origin is MessageOriginChannel fromChannel)
            {
                return Icons.MegaphoneFilled16 + Icons.Spacing + message.ClientService.GetTitle(fromChannel.ChatId);
            }
            else if (replyToMessage.Origin is MessageOriginHiddenUser fromHiddenUser)
            {
                if (message.ClientService.TryGetChat(replyToMessage.ChatId, out Chat senderChat))
                {
                    return fromHiddenUser.SenderName + Icons.Spacing + Icons.PeopleFilled16 + Icons.Spacing + senderChat.Title;
                }

                return Icons.PersonFilled16 + Icons.Spacing + fromHiddenUser.SenderName;
            }

            return title ?? string.Empty;
        }

        private string GetFromLabel(MessageViewModel message, Story story, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            if (message.ClientService.TryGetUser(story.PosterChatId, out User user))
            {
                return user.FullName();
            }

            return title ?? string.Empty;
        }
    }
}
