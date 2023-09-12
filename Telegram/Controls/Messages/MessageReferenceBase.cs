//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
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
                GetMessageTemplate(embedded.EditingMessage.ClientService, embedded.EditingMessage, Strings.Edit, true);
            }
            else if (embedded.ReplyToMessage != null)
            {
                MessageId = embedded.ReplyToMessage.Id;
                GetMessageTemplate(embedded.ReplyToMessage.ClientService, embedded.ReplyToMessage, null, true);
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

            // TODO: chat type

            if (message.ReplyToState == MessageReplyToState.Hidden || message.ReplyTo == null)
            {
                Visibility = Visibility.Collapsed;
            }
            else if (message.ReplyToItem is MessageViewModel replyToMessage)
            {
                GetMessageTemplate(message.ClientService, replyToMessage, null, outgoing);
            }
            else if (message.ReplyToItem is Story replyToStory)
            {
                GetStoryTemplate(message.ClientService, replyToStory, null, outgoing);
            }
            else if (message.ReplyToState == MessageReplyToState.Loading)
            {
                SetLoadingTemplate(message.ClientService, null, null, outgoing);
            }
            else if (message.ReplyToState == MessageReplyToState.Deleted)
            {
                SetEmptyTemplate(message.ClientService, message.ReplyTo);
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
                SetLoadingTemplate(message?.ClientService, null, title, true);
            }
            else
            {
                MessageId = message.Id;
                GetMessageTemplate(message.ClientService, message, title, true);
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

        private bool GetMessageTemplate(IClientService clientService, MessageViewModel message, string title, bool outgoing)
        {
            switch (message.Content)
            {
                case MessageText text:
                    return SetTextTemplate(clientService, message, text, title, outgoing);
                case MessageAnimatedEmoji animatedEmoji:
                    return SetAnimatedEmojiTemplate(clientService, message, animatedEmoji, title, outgoing);
                case MessageAnimation animation:
                    return SetAnimationTemplate(clientService, message, animation, title, outgoing);
                case MessageAudio audio:
                    return SetAudioTemplate(clientService, message, audio, title, outgoing);
                case MessageCall call:
                    return SetCallTemplate(clientService, message, call, title, outgoing);
                case MessageContact contact:
                    return SetContactTemplate(clientService, message, contact, title, outgoing);
                case MessageDice dice:
                    return SetDiceTemplate(clientService, message, dice, title, outgoing);
                case MessageDocument document:
                    return SetDocumentTemplate(clientService, message, document, title, outgoing);
                case MessageGame game:
                    return SetGameTemplate(clientService, message, game, title, outgoing);
                case MessageInvoice invoice:
                    return SetInvoiceTemplate(clientService, message, invoice, title, outgoing);
                case MessageLocation location:
                    return SetLocationTemplate(clientService, message, location, title, outgoing);
                case MessagePhoto photo:
                    return SetPhotoTemplate(clientService, message, photo, title, outgoing);
                case MessagePoll poll:
                    return SetPollTemplate(clientService, message, poll, title, outgoing);
                case MessageSticker sticker:
                    return SetStickerTemplate(clientService, message, sticker, title, outgoing);
                case MessageStory story:
                    return SetStoryTemplate(clientService, message, story, title, outgoing);
                case MessageUnsupported:
                    return SetUnsupportedTemplate(clientService, message, title, outgoing);
                case MessageVenue venue:
                    return SetVenueTemplate(clientService, message, venue, title, outgoing);
                case MessageVideo video:
                    return SetVideoTemplate(clientService, message, video, title, outgoing);
                case MessageVideoNote videoNote:
                    return SetVideoNoteTemplate(clientService, message, videoNote, title, outgoing);
                case MessageVoiceNote voiceNote:
                    return SetVoiceNoteTemplate(clientService, message, voiceNote, title, outgoing);
                default:
                    return SetServiceTextTemplate(clientService, message, title, outgoing);
            }
        }

        private void GetStoryTemplate(IClientService clientService, Story story, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(clientService,
                outgoing ? null : new MessageSenderChat(story.SenderChatId),
                GetFromLabel(clientService, story, title),
                Strings.Story,
                null);

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

        private bool SetTextTemplate(IClientService clientService, MessageViewModel message, MessageText text, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                string.Empty,
                text.Text);

            return true;
        }

        private bool SetDiceTemplate(IClientService clientService, MessageViewModel message, MessageDice dice, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                dice.Emoji,
                null);

            return true;
        }

        private bool SetPhotoTemplate(IClientService clientService, MessageViewModel message, MessagePhoto photo, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            // 🖼

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.AttachPhoto,
                photo.Caption);

            if (message.SelfDestructType is not null)
            {
                HideThumbnail();
            }
            else
            {
                UpdateThumbnail(clientService, photo.Photo.GetSmall(), photo.Photo.Minithumbnail);
            }

            return true;
        }

        private bool SetInvoiceTemplate(IClientService clientService, MessageViewModel message, MessageInvoice invoice, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            var caption = message.GetCaption();
            if (caption != null && !string.IsNullOrEmpty(caption.Text))
            {
                SetText(clientService,
                    outgoing ? null : message.SenderId,
                    GetFromLabel(clientService, message, title),
                    null,
                    caption);
            }
            else
            {
                SetText(clientService,
                    outgoing ? null : message.SenderId,
                    GetFromLabel(clientService, message, title),
                    invoice.Title,
                    null);
            }

            return true;
        }

        private bool SetLocationTemplate(IClientService clientService, MessageViewModel message, MessageLocation location, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                location.LivePeriod > 0 ? Strings.AttachLiveLocation : Strings.AttachLocation,
                null);

            return true;
        }

        private bool SetVenueTemplate(IClientService clientService, MessageViewModel message, MessageVenue venue, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.AttachLocation,
                new FormattedText(venue.Venue.Title, null));

            return true;
        }

        private bool SetCallTemplate(IClientService clientService, MessageViewModel message, MessageCall call, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                call.ToOutcomeText(message.IsOutgoing),
                null);

            return true;
        }

        private bool SetGameTemplate(IClientService clientService, MessageViewModel message, MessageGame game, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                $"\uD83C\uDFAE {game.Game.Title}",
                null);

            UpdateThumbnail(clientService, game.Game.Photo?.GetSmall(), game.Game.Photo?.Minithumbnail);

            return true;
        }

        private bool SetContactTemplate(IClientService clientService, MessageViewModel message, MessageContact contact, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.AttachContact,
                null);

            return true;
        }

        private bool SetAudioTemplate(IClientService clientService, MessageViewModel message, MessageAudio audio, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

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
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                service,
                audio.Caption);

            return true;
        }

        private bool SetPollTemplate(IClientService clientService, MessageViewModel message, MessagePoll poll, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                $"\uD83D\uDCCA {poll.Poll.Question.Replace('\n', ' ')}",
                null);

            return true;
        }

        private bool SetVoiceNoteTemplate(IClientService clientService, MessageViewModel message, MessageVoiceNote voiceNote, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.AttachAudio,
                voiceNote.Caption);

            return true;
        }

        private bool SetVideoTemplate(IClientService clientService, MessageViewModel message, MessageVideo video, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.AttachVideo,
                video.Caption);

            if (message.SelfDestructType is not null)
            {
                HideThumbnail();
            }
            else
            {
                UpdateThumbnail(clientService, video.Video.Thumbnail, video.Video.Minithumbnail);
            }

            return true;
        }

        private bool SetVideoNoteTemplate(IClientService clientService, MessageViewModel message, MessageVideoNote videoNote, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.AttachRound,
                null);

            UpdateThumbnail(clientService, videoNote.VideoNote.Thumbnail, videoNote.VideoNote.Minithumbnail, new CornerRadius(18));

            return true;
        }

        private bool SetAnimatedEmojiTemplate(IClientService clientService, MessageViewModel message, MessageAnimatedEmoji animatedEmoji, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            if (animatedEmoji.AnimatedEmoji?.Sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                SetText(clientService,
                    outgoing ? null : message.SenderId,
                    GetFromLabel(clientService, message, title),
                    string.Empty,
                    new FormattedText(animatedEmoji.Emoji, new[]
                    {
                        new TextEntity(0, animatedEmoji.Emoji.Length, new TextEntityTypeCustomEmoji(customEmoji.CustomEmojiId))
                    }));
            }
            else
            {
                SetText(clientService,
                    outgoing ? null : message.SenderId,
                    GetFromLabel(clientService, message, title),
                    animatedEmoji.Emoji,
                    null);
            }

            HideThumbnail();

            return true;
        }

        private bool SetAnimationTemplate(IClientService clientService, MessageViewModel message, MessageAnimation animation, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.AttachGif,
                animation.Caption);

            UpdateThumbnail(clientService, animation.Animation.Thumbnail, animation.Animation.Minithumbnail);

            return true;
        }

        private bool SetStickerTemplate(IClientService clientService, MessageViewModel message, MessageSticker sticker, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                string.IsNullOrEmpty(sticker.Sticker.Emoji) ? Strings.AttachSticker : $"{sticker.Sticker.Emoji} {Strings.AttachSticker}",
                null);

            return true;
        }

        private bool SetStoryTemplate(IClientService clientService, MessageViewModel message, MessageStory story, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.Story,
                null);

            return true;
        }

        private bool SetDocumentTemplate(IClientService clientService, MessageViewModel message, MessageDocument document, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                document.Document.FileName,
                document.Caption);

            return true;
        }

        private bool SetServiceTextTemplate(IClientService clientService, MessageViewModel message, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                MessageService.GetText(message),
                null);

            return true;
        }

        private bool SetLoadingTemplate(IClientService clientService, MessageViewModel message, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(null,
                outgoing ? null : message?.SenderId,
                title,
                Strings.Loading,
                null);

            return true;
        }

        private bool SetEmptyTemplate(IClientService clientService, MessageReplyTo replyTo)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            if (replyTo is MessageReplyToStory replyToStory)
            {
                if (clientService.TryGetChat(replyToStory.StorySenderChatId, out Chat chat))
                {
                    SetText(null,
                        null,
                        chat.Title,
                        Icons.ExpiredStory + "\u00A0" + Strings.ExpiredStory,
                        null);
                }
                else
                {
                    SetText(null,
                        null,
                        null,
                        Icons.ExpiredStory + "\u00A0" + Strings.ExpiredStory,
                        null);
                }
            }
            else
            {
                SetText(null,
                    null,
                    null,
                    Strings.DeletedMessage,
                    null);
            }

            return true;
        }

        private bool SetUnsupportedTemplate(IClientService clientService, MessageViewModel message, string title, bool outgoing)
        {
            Visibility = Visibility.Visible;

            HideThumbnail();

            SetText(clientService,
                outgoing ? null : message.SenderId,
                GetFromLabel(clientService, message, title),
                Strings.UnsupportedAttachment,
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
        protected abstract void SetText(IClientService clientService, MessageSender sender, string title, string service, FormattedText text);

        #endregion

        private string GetFromLabel(IClientService clientService, MessageViewModel message, string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            var forwardedTitle = clientService.GetTitle(message.ForwardInfo);
            if (forwardedTitle != null)
            {
                return forwardedTitle;
            }
            else if (clientService.TryGetChat(message.SenderId, out Chat senderChat))
            {
                return clientService.GetTitle(senderChat);
            }
            if (clientService.TryGetUser(message.SenderId, out User user))
            {
                return user.FullName();
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
