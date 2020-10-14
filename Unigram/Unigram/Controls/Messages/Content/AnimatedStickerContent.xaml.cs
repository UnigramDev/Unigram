using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Td;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class AnimatedStickerContent : HyperlinkButton, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public AnimatedStickerContent(MessageViewModel message)
        {
            InitializeComponent();
            UpdateMessage(message);
        }

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var sticker = GetContent(message);
            if (sticker == null)
            {
                return;
            }

            if (message.Content is MessageText text)
            {
                Width = Player.Width = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
                Height = Player.Height = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
                Player.ColorReplacements = Emoji.GetColorReplacements(text.Text.Text);
            }
            else if (message.Content is MessageDice)
            {
                Width = Player.Width = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
                Height = Player.Height = 200 * message.ProtoService.Config.GetNamedNumber("emojies_animated_zoom", 0.625);
                Player.ColorReplacements = null;
            }
            else
            {
                Width = Player.Width = 200;
                Height = Player.Height = 200;
                Player.ColorReplacements = null;
            }

            if (sticker.Thumbnail != null && !sticker.StickerValue.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker.Thumbnail.File);
            }

            UpdateFile(message, sticker.StickerValue);
        }

        public void UpdateMessageContentOpened(MessageViewModel message) { }

        public void UpdateFile(MessageViewModel message, File file)
        {
            var sticker = GetContent(message);
            if (sticker == null)
            {
                return;
            }

            if (sticker.Thumbnail != null && sticker.Thumbnail.File.Id == file.Id)
            {
                UpdateThumbnail(message, file);
                return;
            }
            else if (sticker.StickerValue.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                Player.IsLoopingEnabled = (message.Content is MessageDice dice && dice.Value == 0) || (message.Content is MessageSticker && SettingsService.Current.Stickers.IsLoopingEnabled);
                Player.IsCachingEnabled = !(message.Content is MessageDice dies && !message.GeneratedContentUnread);
                Player.Source = new Uri("file:///" + file.Local.Path);

                if (message.IsOutgoing &&
                    message.GeneratedContentUnread &&
                    message.Content is MessageDice dais &&
                    dais.FinalStateSticker != null &&
                    dais.SuccessAnimationFrameNumber != 0)
                {
                    Player.IndexChanged += OnIndexChanged;
                }
                else
                {
                    Player.IndexChanged -= OnIndexChanged;
                }
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        private void OnIndexChanged(object sender, int e)
        {
            if (_message?.Content is MessageDice dice && dice.SuccessAnimationFrameNumber == e)
            {
                _message.Delegate.Aggregator.Publish(new UpdateConfetti());
                Player.IndexChanged -= OnIndexChanged;
            }
        }

        private void UpdateThumbnail(MessageViewModel message, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Player.Thumbnail = PlaceholderHelper.GetWebPFrame(file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            // We can't recycle it as we must destroy CanvasAnimatedControl on Unload.
            if (Player.IsUnloaded)
            {
                return false;
            }

            if (content is MessageSticker sticker)
            {
                return sticker.Sticker.IsAnimated;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && text.WebPage.Sticker.IsAnimated;
            }

            return false;
        }

        private Sticker GetContent(MessageViewModel message)
        {
            var content = message.GeneratedContent ?? message.Content;
            if (content is MessageSticker sticker)
            {
                return sticker.Sticker;
            }
            else if (content is MessageText text && text.WebPage != null)
            {
                return text.WebPage.Sticker;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var sticker = GetContent(_message);
            if (sticker == null)
            {
                return;
            }

            if (_message.Content is MessageDice dice)
            {
                string text;
                switch (dice.Emoji)
                {
                    case "\uD83C\uDFB2":
                        text = Strings.Resources.DiceInfo2;
                        break;
                    case "\uD83C\uDFAF":
                        text = Strings.Resources.DartInfo;
                        break;
                    default:
                        text = string.Format(Strings.Resources.DiceEmojiInfo, dice.Emoji);
                        break;
                }

                var formatted = Client.Execute(new ParseMarkdown(new FormattedText(text, new TextEntity[0]))) as FormattedText;
                Window.Current.ShowTeachingTip(this, formatted, _message.IsOutgoing && !_message.IsChannelPost ? TeachingTipPlacementMode.TopLeft : TeachingTipPlacementMode.TopRight);
            }
            else if (_message.Content is MessageText)
            {
                Player.Play();
            }
            else
            {
                _message.Delegate.OpenSticker(sticker);
            }
        }
    }
}
