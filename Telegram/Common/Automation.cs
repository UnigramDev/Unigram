//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Linq;
using System.Text;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Telegram.Common
{
    public static class Automation
    {
        public static void SetToolTip(DependencyObject element, string text)
        {
            try
            {
                AutomationProperties.SetName(element, text ?? string.Empty);

                if (text == null)
                {
                    ToolTipService.SetToolTip(element, null);
                }
                else
                {
                    var tooltip = ToolTipService.GetToolTip(element) as ToolTip;
                    if (tooltip == null)
                    {
                        ToolTipService.SetToolTip(element, new ToolTip
                        {
                            Content = text
                        });
                    }
                    else
                    {
                        tooltip.Content = text;
                    }
                }
            }
            catch
            {
                // Seems to happen some times
            }
        }

        public static string GetSummary2(Message message)
        {
            var builder = new StringBuilder();

            if (message.Content is MessagePhoto photo)
            {
                builder.Append($"{Strings.AttachPhoto}");

                if (photo.Caption != null && !string.IsNullOrEmpty(photo.Caption.Text))
                {
                    builder.Append($". {photo.Caption.Text}");
                }
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                builder.Append($"{Strings.AttachAudio}");

                if (voiceNote.Caption != null && !string.IsNullOrEmpty(voiceNote.Caption.Text))
                {
                    builder.Append($". {voiceNote.Caption.Text}");
                }
            }
            else if (message.Content is MessageVideo video)
            {
                builder.Append($"{Strings.AttachVideo}");

                if (video.Caption != null && !string.IsNullOrEmpty(video.Caption.Text))
                {
                    builder.Append($". {video.Caption.Text}");
                }
            }
            else if (message.Content is MessageVideoNote)
            {
                builder.Append($"{Strings.AttachRound}");
            }
            else if (message.Content is MessageAnimation animation)
            {
                builder.Append($". {Strings.AttachGif}");

                if (animation.Caption != null && !string.IsNullOrEmpty(animation.Caption.Text))
                {
                    builder.Append($". {animation.Caption.Text}");
                }
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (!string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    builder.Append($"{sticker.Sticker.Emoji} {Strings.AttachSticker}");
                }
                else
                {
                    builder.Append($"{Strings.AttachSticker}");
                }
            }
            else if (message.Content is MessageAudio audio)
            {
                builder.Append($"{Strings.AttachMusic}");

                if (audio.Caption != null && !string.IsNullOrEmpty(audio.Caption.Text))
                {
                    builder.Append($". {audio.Caption.Text}");
                }
            }
            else if (message.Content is MessageLocation)
            {
                builder.Append($"{Strings.AttachLocation}");
            }
            else if (message.Content is MessageVenue venue)
            {
                builder.Append($"{Strings.AttachLocation}");
                builder.Append(venue.Venue.Title);
                builder.Append(venue.Venue.Address);
            }
            else if (message.Content is MessageContact contact)
            {
                builder.Append($"{Strings.AttachContact}");
                builder.Append(contact.Contact.GetFullName());
                builder.Append(PhoneNumber.Format(contact.Contact.PhoneNumber));
            }
            else if (message.Content is MessagePoll poll)
            {
                builder.Append($"{Strings.Poll}. ");
                builder.Append($"{poll.Poll.Question.Text}");
            }
            else if (message.Content is MessageCall call)
            {
                builder.Append(call.ToOutcomeText(message.IsOutgoing));
            }
            else if (message.Content is MessageText text)
            {
                builder.Append(text.Text.Text);
            }
            else if (message.Content is MessageDice dice)
            {
                builder.Append(dice.Emoji);
            }
            else if (message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                builder.Append(animatedEmoji.Emoji);
            }

            if (builder.Length > 0 && builder[builder.Length - 1] != '.')
            {
                builder.Append(". ");
            }
            else
            {
                builder.Append(" ");
            }

            return builder.ToString();
        }

        public static string GetSummaryWithName(MessageWithOwner message, bool details = false, bool addCaption = true)
        {
            var summary = GetSummary(message, details, addCaption);

            if (message.ClientService.TryGetUser(message.SenderId, out User user))
            {
                if (user.Id == message.ClientService.Options.MyId)
                {
                    return $"{Strings.FromYou}: {summary}";
                }

                return $"{user.FullName()}: {summary}";
            }
            //else if (message.IsChannelPost)
            //{
            //    return summary;
            //}
            else if (message.ClientService.TryGetChat(message.SenderId, out Chat chat))
            {
                return $"{chat.Title}: {summary}";
            }

            return summary;
        }

        public static string GetSummary(MessageWithOwner message, bool details = false, bool addCaption = true)
        {
            var altText = message.TranslatedText is MessageTranslateResultText text ? text.Text.Text : null;
            return GetSummary(message.ClientService, message.Get(), details, addCaption, altText);
        }

        public static string GetSummary(IClientService clientService, Message message, bool details = false, bool addCaption = true, string altText = null)
        {
            if (message.IsService() && clientService.TryGetChat(message.ChatId, out Chat chat))
            {
                return MessageService.GetText(new MessageViewModel(clientService, null, null, chat, message)) + ", ";
            }

            if (message.Content is MessageAlbum album)
            {
                if (album.IsMedia)
                {
                    var caption = string.Empty;
                    if (!string.IsNullOrEmpty(album.Caption.Text))
                    {
                        caption = album.Caption.Text + ", ";
                    }

                    var photos = album.Messages.Count(x => x.Content is MessagePhoto);
                    var videos = album.Messages.Count - photos;

                    if (album.Messages.Count > 0 && album.Messages[0].Content is MessageVideo)
                    {
                        if (photos > 0)
                        {
                            return Locale.Declension(Strings.R.Videos, videos) + ", " + Locale.Declension(Strings.R.Photos, photos) + ", " + caption;
                        }

                        return Locale.Declension(Strings.R.Videos, videos) + ", " + caption;
                    }

                    if (videos > 0)
                    {
                        return Locale.Declension(Strings.R.Photos, photos) + ", " + Locale.Declension(Strings.R.Videos, videos) + ", " + caption;
                    }

                    return Locale.Declension(Strings.R.Photos, photos) + ", " + caption;
                }
                else if (album.Messages.Count > 0 && album.Messages[0].Content is MessageAudio)
                {
                    return Locale.Declension(Strings.R.MusicFiles, album.Messages.Count) + ", ";
                }
                else
                {
                    return Locale.Declension(Strings.R.Files, album.Messages.Count) + ", ";
                }
            }
            else if (message.Content is MessagePaidAlbum paidAlbum)
            {
                var caption = string.Empty;
                if (!string.IsNullOrEmpty(paidAlbum.Caption.Text))
                {
                    caption = paidAlbum.Caption.Text + ", ";
                }

                var photos = paidAlbum.Media.Count(x => x.IsPhoto());
                var videos = paidAlbum.Media.Count - photos;

                if (paidAlbum.Media.Count > 0 && paidAlbum.Media[0].IsVideo())
                {
                    if (photos > 0)
                    {
                        return Locale.Declension(Strings.R.Videos, videos) + ", " + Locale.Declension(Strings.R.Photos, photos) + ", " + caption;
                    }

                    return Locale.Declension(Strings.R.Videos, videos) + ", " + caption;
                }

                if (videos > 0)
                {
                    return Locale.Declension(Strings.R.Photos, photos) + ", " + Locale.Declension(Strings.R.Videos, videos) + ", " + caption;
                }

                return Locale.Declension(Strings.R.Photos, photos) + ", " + caption;
            }
            else if (message.Content is MessagePaidMedia paidMedia)
            {
                var caption = string.Empty;
                if (!string.IsNullOrEmpty(paidMedia.Caption.Text))
                {
                    caption = paidMedia.Caption.Text + ", ";
                }

                var photos = paidMedia.Media.Count(x => x.IsPhoto());
                var videos = paidMedia.Media.Count - photos;

                if (paidMedia.Media.Count > 0 && paidMedia.Media[0].IsVideo())
                {
                    if (photos > 0)
                    {
                        return Locale.Declension(Strings.R.Videos, videos) + ", " + Locale.Declension(Strings.R.Photos, photos) + ", " + caption;
                    }

                    return Locale.Declension(Strings.R.Videos, videos) + ", " + caption;
                }

                if (videos > 0)
                {
                    return Locale.Declension(Strings.R.Photos, photos) + ", " + Locale.Declension(Strings.R.Videos, videos) + ", " + caption;
                }

                return Locale.Declension(Strings.R.Photos, photos) + ", " + caption;
            }
            else if (message.Content is MessageText text)
            {
                if (altText != null)
                {
                    return altText + ", ";
                }

                return text.Text.Text + ", ";
            }
            else if (message.Content is MessageDice dice)
            {
                return dice.Emoji + ", ";
            }
            else if (message.Content is MessageAnimatedEmoji animatedEmoji)
            {
                return animatedEmoji.Emoji + ", ";
            }
            else if (message.Content is MessageGame gameMedia)
            {
                return Strings.AttachGame + ", " + gameMedia.Game.Title + ", ";
            }
            else if (message.Content is MessageExpiredVideo)
            {
                return Strings.AttachVideoExpired + ", ";
            }
            else if (message.Content is MessageExpiredPhoto)
            {
                return Strings.AttachPhotoExpired + ", ";
            }
            else if (message.Content is MessageExpiredVideoNote)
            {
                return Strings.AttachRoundExpired + ", ";
            }
            else if (message.Content is MessageExpiredVoiceNote)
            {
                return Strings.AttachVoiceExpired + ", ";
            }
            else if (message.Content is MessageVideoNote videoNote)
            {
                var result = Strings.AttachRound + ", " + (videoNote.IsViewed ? "" : Strings.AccDescrMsgNotPlayed + ", ");

                if (details)
                {
                    result += videoNote.VideoNote.GetDuration() + ", ";
                }

                return result;
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return Strings.AttachSticker + ", ";
                }

                return $"{sticker.Sticker.Emoji} {Strings.AttachSticker}" + ", ";
            }

            string GetCaption(string caption)
            {
                return !addCaption || string.IsNullOrEmpty(altText ?? caption) ? string.Empty : ", " + (altText ?? caption);
            }

            if (message.Content is MessageVoiceNote voiceNote)
            {
                var result = Strings.AttachAudio + GetCaption(voiceNote.Caption.Text) + ", " + (voiceNote.IsListened ? "" : Strings.AccDescrMsgNotPlayed + ", ");

                if (details)
                {
                    result += voiceNote.VoiceNote.GetDuration() + ", ";

                    if (voiceNote.VoiceNote.SpeechRecognitionResult is SpeechRecognitionResultText recognitionResultText)
                    {
                        result += recognitionResultText.Text + ", ";
                    }
                    else if (voiceNote.VoiceNote.SpeechRecognitionResult is SpeechRecognitionResultError)
                    {
                        result += Strings.NoWordsRecognized + ", ";
                    }
                }

                return result;
            }
            else if (message.Content is MessageVideo video)
            {
                var result = (video.IsSecret ? Strings.AttachDestructingVideo : Strings.AttachVideo) + GetCaption(video.Caption.Text) + ", ";

                if (details)
                {
                    result += video.Video.GetDuration() + ", ";
                }

                if (details && !video.Video.VideoValue.Local.IsDownloadingCompleted)
                {
                    result += FileSizeConverter.Convert(video.Video.VideoValue.Size) + ", ";
                }

                return result;
            }
            else if (message.Content is MessageAnimation animation)
            {
                var result = Strings.AttachGif + GetCaption(animation.Caption.Text) + ", ";
                if (details)
                {
                    result += FileSizeConverter.Convert(animation.Animation.AnimationValue.Size) + ", ";
                }

                return result;
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                string result;
                if (performer == null && title == null)
                {
                    result = Strings.AttachMusic + ", " + audio.Audio.FileName + GetCaption(audio.Caption.Text) + ", ";
                }
                else
                {
                    result = Strings.AttachMusic + ", " + $"{performer ?? Strings.AudioUnknownArtist} - {title ?? Strings.AudioUnknownTitle}" + GetCaption(audio.Caption.Text) + ", ";
                }

                if (details)
                {
                    result += audio.Audio.GetDuration() + ", ";
                }

                if (details && !audio.Audio.AudioValue.Local.IsDownloadingCompleted)
                {
                    result += FileSizeConverter.Convert(audio.Audio.AudioValue.Size) + ", ";
                }

                return result;
            }
            else if (message.Content is MessageDocument document)
            {
                string result;
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    result = Strings.AttachDocument + GetCaption(document.Caption.Text) + ", ";
                }
                else
                {
                    result = document.Document.FileName + GetCaption(document.Caption.Text) + ", ";
                }

                if (details)
                {
                    result += FileSizeConverter.Convert(document.Document.DocumentValue.Size) + ", ";
                }

                return result;
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return Strings.PaymentInvoice + ", " + invoice.ProductInfo.Title + ", ";
            }
            else if (message.Content is MessageContact)
            {
                return Strings.AttachContact + ", ";
            }
            else if (message.Content is MessageLocation location)
            {
                return (location.LivePeriod > 0 ? Strings.AttachLiveLocation : Strings.AttachLocation) + ", ";
            }
            else if (message.Content is MessageVenue)
            {
                return Strings.AttachLocation + ", ";
            }
            else if (message.Content is MessagePhoto photo)
            {
                return (photo.IsSecret ? Strings.AttachDestructingPhoto : Strings.AttachPhoto) + GetCaption(photo.Caption.Text) + ", ";
            }
            else if (message.Content is MessagePoll poll)
            {
                if (details)
                {
                    string type = null;
                    if (poll.Poll.Type is PollTypeRegular)
                    {
                        type = poll.Poll.IsClosed ? Strings.FinalResults : poll.Poll.IsAnonymous ? Strings.AnonymousPoll : Strings.PublicPoll;
                    }
                    else if (poll.Poll.Type is PollTypeQuiz)
                    {
                        type = poll.Poll.IsClosed ? Strings.FinalResults : poll.Poll.IsAnonymous ? Strings.AnonymousQuizPoll : Strings.QuizPoll;
                    }

                    if (type != null)
                    {
                        return type + ", " + poll.Poll.Question.Text + ", ";
                    }
                }

                return Strings.Poll + ", " + poll.Poll.Question.Text + ", ";
            }
            else if (message.Content is MessageCall call)
            {
                return call.ToOutcomeText(message.IsOutgoing) + ", ";
            }
            else if (message.Content is MessageUnsupported)
            {
                return Strings.UnsupportedAttachment + ", ";
            }

            return null;
        }

        public static string GetDescription(IClientService clientService, Message message)
        {
            var chat = clientService.GetChat(message.ChatId);
            var content = message.Content;

            var sticker = content is MessageSticker;
            var light = sticker || content is MessageVideoNote;

            var title = string.Empty;

            if (!light && /*message.IsFirst &&*/ !message.IsOutgoing && !message.IsChannelPost && (chat.Type is ChatTypeBasicGroup || chat.Type is ChatTypeSupergroup))
            {
                if (clientService.TryGetUser(message.SenderId, out User senderUser))
                {
                    title = senderUser.FullName();
                }
                else if (clientService.TryGetChat(message.SenderId, out Chat senderChat))
                {
                    title = senderChat.Title;
                }
            }
            else if (!light && message.IsChannelPost && chat.Type is ChatTypeSupergroup)
            {
                title = clientService.GetTitle(chat);
            }
            else if (!light && /*message.IsFirst &&*/ message.IsSaved(clientService.Options.MyId))
            {
                title = clientService.GetTitle(message.ForwardInfo?.Origin, message.ImportInfo);
            }

            var builder = new StringBuilder();
            if (title?.Length > 0)
            {
                builder.AppendLine($"{title}. ");
            }

            //if (message.ReplyToMessage != null)
            //{
            //    var user = message.ClientService.GetUser(message.ReplyToMessage.SenderUserId);
            //    if (user != null)
            //    {
            //        builder.AppendLine($"{Strings.AccDescrReplying} {user.GetFullName()}. ");
            //    }
            //}

            builder.Append(GetSummary(clientService, message));

            var date = string.Format(Strings.TodayAtFormatted, Formatter.Time(message.Date));
            if (message.IsOutgoing)
            {
                builder.Append(string.Format(Strings.AccDescrSentDate, date));
            }
            else
            {
                builder.Append(string.Format(Strings.AccDescrReceivedDate, date));
            }

            builder.Append(". ");

            var maxId = 0L;
            if (chat != null)
            {
                maxId = chat.LastReadOutboxMessageId;
            }

            if (message.SendingState is MessageSendingStateFailed)
            {
            }
            else if (message.SendingState is MessageSendingStatePending)
            {
            }
            else if (message.Id <= maxId && message.IsOutgoing && !message.IsChannelPost)
            {
                builder.Append(Strings.AccDescrMsgRead);
            }
            else if (message.IsOutgoing && !message.IsChannelPost)
            {
                builder.Append(Strings.AccDescrMsgUnread);
            }

            builder.Append(".");

            return builder.ToString();
        }
    }
}
