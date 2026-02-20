//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class PreviewContent : Control, IContent
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        private PaidMediaPreview _paidMedia;

        private ThumbnailController _thumbnailController;

        public PreviewContent(MessageViewModel message, PaidMediaPreview paidMedia)
        {
            _message = message;
            _paidMedia = paidMedia;

            DefaultStyleKey = typeof(PreviewContent);
        }

        #region InitializeComponent

        private AspectView LayoutRoot;
        private ImageBrush ThumbnailTexture;
        private AnimatedImage Particles;
        private Border Overlay;
        private TextBlock Subtitle;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as AspectView;
            ThumbnailTexture = LayoutRoot.Background as ImageBrush;
            Particles = GetTemplateChild(nameof(Particles)) as AnimatedImage;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

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

            var preview = GetContent(message);
            if (preview == null || !_templateApplied)
            {
                return;
            }

            LayoutRoot.Constraint = _paidMedia;

            Particles.Source = new ParticlesImageSource();

            if (preview.Duration > 0)
            {
                Subtitle.Text = preview.GetDuration();
                Overlay.Visibility = Visibility.Visible;
            }
            else
            {
                Overlay.Visibility = Visibility.Collapsed;
            }

            UpdateThumbnail(message, preview.Minithumbnail);
        }

        private void UpdateThumbnail(MessageViewModel message, Minithumbnail minithumbnail)
        {
            _thumbnailController ??= new ThumbnailController(ThumbnailTexture);

            if (minithumbnail != null)
            {
                _thumbnailController.Blur(minithumbnail.Data, 3, HashCode.Combine(message.ChatId, message.Id));
            }
            else
            {
                _thumbnailController.Recycle();
            }
        }

        public void Recycle()
        {
            _message = null;
            _thumbnailController?.Recycle();
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            if (content is MessageInvoice invoice && invoice.PaidMedia is PaidMediaPreview)
            {
                return true;
            }

            return false;
        }

        private PaidMediaPreview GetContent(MessageViewModel message)
        {
            if (message?.Delegate == null)
            {
                return null;
            }

            return _paidMedia;
        }
    }
}
