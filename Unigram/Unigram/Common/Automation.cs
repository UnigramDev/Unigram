using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
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

        public static string GetSummary(Message message)
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
    }
}
