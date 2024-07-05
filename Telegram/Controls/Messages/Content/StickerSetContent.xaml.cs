//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls.Messages.Content
{
    public sealed class StickerSetContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public StickerSetContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(StickerSetContent);
        }

        #region InitializeComponent

        private AspectView LayoutRoot;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            LayoutRoot.Constraint = message;

            var linkPreview = GetContent(message);
            if (linkPreview?.Type is not LinkPreviewTypeStickerSet stickerSet || !_templateApplied)
            {
                return;
            }

            LayoutRoot.Children.Clear();
            LayoutRoot.ColumnDefinitions.Clear();
            LayoutRoot.RowDefinitions.Clear();

            if (stickerSet.Stickers.Count > 1)
            {
                LayoutRoot.ColumnDefinitions.Add(new ColumnDefinition());
                LayoutRoot.ColumnDefinitions.Add(new ColumnDefinition());

                LayoutRoot.RowDefinitions.Add(new RowDefinition());
                LayoutRoot.RowDefinitions.Add(new RowDefinition());
            }

            for (int i = 0; i < stickerSet.Stickers.Count; i++)
            {
                var size = stickerSet.Stickers.Count > 1 ? 20 : 44;
                var animated = new AnimatedImage
                {
                    Width = size,
                    Height = size,
                    FrameSize = new Size(size, size),
                    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    Source = new DelayedFileSource(message.ClientService, stickerSet.Stickers[i]),
                    AutoPlay = false,
                    IsViewportAware = true
                };

                Grid.SetRow(animated, i / 2);
                Grid.SetColumn(animated, i % 2);

                LayoutRoot.Children.Add(animated);
            }
        }

        public void Recycle()
        {
            _message = null;
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageText text && text.LinkPreview != null && !primary)
            {
                return text.LinkPreview.Type is LinkPreviewTypeStickerSet;
            }

            return false;
        }

        private LinkPreview GetContent(MessageViewModel message)
        {
            var content = message?.GeneratedContent ?? message?.Content;
            if (content is MessageText text && text.LinkPreview?.Type is LinkPreviewTypeStickerSet)
            {
                return text.LinkPreview;
            }

            return null;
        }
    }
}
