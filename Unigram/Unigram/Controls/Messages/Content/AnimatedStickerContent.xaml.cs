using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Unigram.Controls.Messages.Content
{
    public sealed partial class AnimatedStickerContent : HyperlinkButton, IContentWithFile
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private CompositionAnimation _thumbnailShimmer;

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

                var sound = message.ProtoService.GetEmojiSound(sticker.Emoji);
                if (sound != null && sound.Local.CanBeDownloaded && !sound.Local.IsDownloadingActive)
                {
                    message.ProtoService.DownloadFile(sound.Id, 1);
                }
            }
            else
            {
                Width = Player.Width = 200;
                Height = Player.Height = 200;
                Player.ColorReplacements = null;
            }

            if (sticker.Contours.Count > 0 && !sticker.StickerValue.Local.IsDownloadingCompleted)
            {
                UpdateThumbnail(message, sticker.Contours);
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

            if (sticker.StickerValue.Id != file.Id)
            {
                return;
            }

            if (file.Local.IsDownloadingCompleted)
            {
                Player.IsLoopingEnabled = message.Content is MessageSticker && SettingsService.Current.Stickers.IsLoopingEnabled;
                Player.Source = new Uri("file:///" + file.Local.Path);
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                message.ProtoService.DownloadFile(file.Id, 1);
            }
        }

        private void UpdateThumbnail(MessageViewModel message, IList<ClosedVectorPath> contours)
        {
            if (ApiInfo.CanUseDirectComposition)
            {
                _thumbnailShimmer = CompositionPathParser.ParseThumbnail(contours, Player.Width, out ShapeVisual visual);
                ElementCompositionPreview.SetElementChildVisual(Player, visual);
            }
        }

        private void Player_FirstFrameRendered(object sender, EventArgs e)
        {
            _thumbnailShimmer = null;
            ElementCompositionPreview.SetElementChildVisual(Player, null);
        }

        public bool IsValid(MessageContent content, bool primary)
        {
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

            if (_message.Content is MessageText)
            {
                var started = Player.Play();
                if (started)
                {
                    var sound = _message.ProtoService.GetEmojiSound(sticker.Emoji);
                    if (sound != null && sound.Local.IsDownloadingCompleted)
                    {
                        SoundEffects.Play(sound);
                    }
                }
            }
            else
            {
                _message.Delegate.OpenSticker(sticker);
            }
        }
    }
}
