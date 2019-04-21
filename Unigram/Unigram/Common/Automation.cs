using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls.Messages;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;

namespace Unigram.Common
{
    public static class Automation
    {
        public static void SetToolTip(DependencyObject element, string text)
        {
            AutomationProperties.SetName(element, text);
            ToolTipService.SetToolTip(element, text);
        }

        public static string GetSummary2(Message message)
        {
            var builder = new StringBuilder();

            if (message.Content is MessagePhoto photo)
            {
                builder.Append($"{Strings.Resources.AttachPhoto}");

                if (photo.Caption != null && !string.IsNullOrEmpty(photo.Caption.Text))
                {
                    builder.Append($". {photo.Caption.Text}");
                }
            }
            else if (message.Content is MessageVoiceNote voiceNote)
            {
                builder.Append($"{Strings.Resources.AttachAudio}");

                if (voiceNote.Caption != null && !string.IsNullOrEmpty(voiceNote.Caption.Text))
                {
                    builder.Append($". {voiceNote.Caption.Text}");
                }
            }
            else if (message.Content is MessageVideo video)
            {
                builder.Append($"{Strings.Resources.AttachVideo}");

                if (video.Caption != null && !string.IsNullOrEmpty(video.Caption.Text))
                {
                    builder.Append($". {video.Caption.Text}");
                }
            }
            else if (message.Content is MessageVideoNote)
            {
                builder.Append($"{Strings.Resources.AttachRound}");
            }
            else if (message.Content is MessageAnimation animation)
            {
                builder.Append($". {Strings.Resources.AttachGif}");

                if (animation.Caption != null && !string.IsNullOrEmpty(animation.Caption.Text))
                {
                    builder.Append($". {animation.Caption.Text}");
                }
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (!string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    builder.Append($"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}");
                }
                else
                {
                    builder.Append($"{Strings.Resources.AttachSticker}");
                }
            }
            else if (message.Content is MessageAudio audio)
            {
                builder.Append($"{Strings.Resources.AttachMusic}");

                if (audio.Caption != null && !string.IsNullOrEmpty(audio.Caption.Text))
                {
                    builder.Append($". {audio.Caption.Text}");
                }
            }
            else if (message.Content is MessageLocation location)
            {
                builder.Append($"{Strings.Resources.AttachLocation}");
            }
            else if (message.Content is MessageVenue venue)
            {
                builder.Append($"{Strings.Resources.AttachLocation}");
                builder.Append(venue.Venue.Title);
                builder.Append(venue.Venue.Address);
            }
            else if (message.Content is MessageContact contact)
            {
                builder.Append($"{Strings.Resources.AttachContact}");
                builder.Append(contact.Contact.GetFullName());
                builder.Append(PhoneNumber.Format(contact.Contact.PhoneNumber));
            }
            else if (message.Content is MessagePoll poll)
            {
                builder.Append($"{Strings.Resources.Poll}. ");
                builder.Append($"{poll.Poll.Question}");
            }
            else if (message.Content is MessageCall call)
            {
                var outgoing = message.IsOutgoing;
                var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

                builder.Append(missed ? (outgoing ? Strings.Resources.CallMessageOutgoingMissed : Strings.Resources.CallMessageIncomingMissed) : (outgoing ? Strings.Resources.CallMessageOutgoing : Strings.Resources.CallMessageIncoming));
            }
            else if (message.Content is MessageText text)
            {
                builder.Append(text.Text.Text);
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

        public static string GetSummary(IProtoService protoService, Message message)
        {
            if (message.IsService())
            {
                return MessageService.GetText(new ViewModels.MessageViewModel(protoService, null, null, message)) + ", ";
            }

            if (message.Content is MessageText text)
            {
                return text.Text.Text + ", ";
            }
            if (message.Content is MessageGame gameMedia)
            {
                return Strings.Resources.AttachGame + ", " + gameMedia.Game.Title + ", ";
            }
            if (message.Content is MessageExpiredVideo)
            {
                return Strings.Resources.AttachVideoExpired + ", ";
            }
            else if (message.Content is MessageExpiredPhoto)
            {
                return Strings.Resources.AttachPhotoExpired + ", ";
            }
            else if (message.Content is MessageVideoNote)
            {
                return Strings.Resources.AttachRound + ", ";
            }
            else if (message.Content is MessageSticker sticker)
            {
                if (string.IsNullOrEmpty(sticker.Sticker.Emoji))
                {
                    return Strings.Resources.AttachSticker + ", ";
                }

                return $"{sticker.Sticker.Emoji} {Strings.Resources.AttachSticker}" + ", ";
            }

            string GetCaption(string caption)
            {
                return string.IsNullOrEmpty(caption) ? string.Empty : ", " + caption;
            }

            if (message.Content is MessageVoiceNote voiceNote)
            {
                return Strings.Resources.AttachAudio + GetCaption(voiceNote.Caption.Text) + ", ";
            }
            else if (message.Content is MessageVideo video)
            {
                return (video.IsSecret ? Strings.Resources.AttachDestructingVideo : Strings.Resources.AttachVideo) + GetCaption(video.Caption.Text) + ", ";
            }
            else if (message.Content is MessageAnimation animation)
            {
                return Strings.Resources.AttachGif + GetCaption(animation.Caption.Text) + ", ";
            }
            else if (message.Content is MessageAudio audio)
            {
                var performer = string.IsNullOrEmpty(audio.Audio.Performer) ? null : audio.Audio.Performer;
                var title = string.IsNullOrEmpty(audio.Audio.Title) ? null : audio.Audio.Title;

                if (performer == null && title == null)
                {
                    return Strings.Resources.AttachMusic + GetCaption(audio.Caption.Text) + ", ";
                }
                else
                {
                    return $"{performer ?? Strings.Resources.AudioUnknownArtist} - {title ?? Strings.Resources.AudioUnknownTitle}" + GetCaption(audio.Caption.Text) + ", ";
                }
            }
            else if (message.Content is MessageDocument document)
            {
                if (string.IsNullOrEmpty(document.Document.FileName))
                {
                    return Strings.Resources.AttachDocument + GetCaption(document.Caption.Text) + ", ";
                }

                return document.Document.FileName + GetCaption(document.Caption.Text) + ", ";
            }
            else if (message.Content is MessageInvoice invoice)
            {
                return Strings.Resources.PaymentInvoice + ", " + invoice.Title + ", ";
            }
            else if (message.Content is MessageContact)
            {
                return Strings.Resources.AttachContact + ", ";
            }
            else if (message.Content is MessageLocation location)
            {
                return (location.LivePeriod > 0 ? Strings.Resources.AttachLiveLocation : Strings.Resources.AttachLocation) + ", ";
            }
            else if (message.Content is MessageVenue vanue)
            {
                return Strings.Resources.AttachLocation + ", ";
            }
            else if (message.Content is MessagePhoto photo)
            {
                return (photo.IsSecret ? Strings.Resources.AttachDestructingPhoto : Strings.Resources.AttachPhoto) + GetCaption(photo.Caption.Text) + ", ";
            }
            else if (message.Content is MessagePoll poll)
            {
                return Strings.Resources.Poll + ", " + poll.Poll.Question + ", ";
            }
            else if (message.Content is MessageCall call)
            {
                var outgoing = message.IsOutgoing;
                var missed = call.DiscardReason is CallDiscardReasonMissed || call.DiscardReason is CallDiscardReasonDeclined;

                return (missed ? (outgoing ? Strings.Resources.CallMessageOutgoingMissed : Strings.Resources.CallMessageIncomingMissed) : (outgoing ? Strings.Resources.CallMessageOutgoing : Strings.Resources.CallMessageIncoming)) + ", ";
            }
            else if (message.Content is MessageUnsupported)
            {
                return Strings.Resources.UnsupportedAttachment + ", ";
            }

            return null;
        }
    }
}
