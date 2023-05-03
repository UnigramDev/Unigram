//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Messages.Content
{
    public sealed class StickerContent : ImageView, IContent, IContentWithMask
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private string _fileToken;

        private CompositionAnimation _thumbnailShimmer;

        public StickerContent(MessageViewModel message)
        {
            DefaultStyleKey = typeof(StickerContent);

            Click += Button_Click;

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

            if (message.Content is MessageAnimatedEmoji)
            {
                MaxWidth = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
                MaxHeight = 180 * message.ClientService.Config.GetNamedNumber("emojies_animated_zoom", 0.625f);
            }
            else
            {
                MaxWidth = 180;
                MaxHeight = 180;
            }

            Constraint = sticker;
            UpdateFile(message, sticker.StickerValue);
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_message, file);
        }

        private async void UpdateFile(MessageViewModel message, File file)
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
                Source = await PlaceholderHelper.GetWebPFrameAsync(file.Local.Path, 180);
                ElementCompositionPreview.SetElementChildVisual(this, null);

                UpdateManager.Unsubscribe(this, ref _fileToken);
            }
            else
            {
                Source = null;
                UpdateThumbnail(message, sticker);

                UpdateManager.Subscribe(this, message, sticker.StickerValue, ref _fileToken, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    message.ClientService.DownloadFile(file.Id, 1);
                }
            }
        }

        private void UpdateThumbnail(MessageViewModel message, Sticker sticker)
        {
            _thumbnailShimmer = CompositionPathParser.ParseThumbnail(sticker, out ShapeVisual visual);
            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        public void Recycle()
        {
            if (_fileToken != null)
            {
                UpdateManager.Unsubscribe(this);
            }

            _fileToken = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageSticker sticker)
            {
                return sticker.Sticker.Format is StickerFormatWebp;
            }
            else if (content is MessageText text && text.WebPage != null && !primary)
            {
                return text.WebPage.Sticker != null && text.WebPage.Sticker.Format is StickerFormatWebp;
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

        public CompositionBrush GetAlphaMask()
        {
            if (Holder is Image image)
            {
                return image.GetAlphaMask();
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

            _message.Delegate.OpenSticker(sticker);
        }
    }
}
