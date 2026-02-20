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

namespace Telegram.Controls.Cells
{
    public sealed partial class SharedMediaCell : Control
    {
        private MessageWithOwner _message;
        public MessageWithOwner Message => _message;

        private long _thumbnailToken;

        private ThumbnailController _thumbnailController;

        public SharedMediaCell()
        {
            DefaultStyleKey = typeof(SharedMediaCell);
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
            ThumbnailTexture = GetTemplateChild(nameof(ThumbnailTexture)) as ImageBrush;
            Particles = GetTemplateChild(nameof(Particles)) as AnimatedImage;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message, true);
            }
            else if (_hidden)
            {
                ThumbnailTexture.Opacity = 0;
                Overlay.Opacity = 0;
            }
        }

        #endregion

        public void UpdateMessage(MessageWithOwner message, bool download)
        {
            _message = message;

            if (!_templateApplied)
            {
                return;
            }

            if (_hidden)
            {
                _hidden = false;
                ThumbnailTexture.Opacity = 1;
                Overlay.Opacity = 1;
            }

            UpdateManager.Unsubscribe(this, ref _thumbnailToken);

            if (message.Content is MessagePhoto photo)
            {
                var small = photo.Photo.GetThumbnail();
                UpdateThumbnail(message, small, photo.Photo.Minithumbnail, download, photo.HasSpoiler);

                Overlay.Visibility = Visibility.Collapsed;
            }
            else if (message.Content is MessageVideo video)
            {
                var thumbnail = video.Cover?.GetThumbnail();
                thumbnail ??= video.Video.Thumbnail;

                var minithumbnail = video.Cover?.Minithumbnail;
                minithumbnail ??= video.Video.Minithumbnail;

                UpdateThumbnail(message, thumbnail, minithumbnail, download, video.HasSpoiler);

                Overlay.Visibility = Visibility.Visible;
                Subtitle.Text = video.Video.GetDuration();
            }
            else if (message.Content is MessageAnimation animation)
            {
                UpdateThumbnail(message, animation.Animation.Thumbnail, animation.Animation.Minithumbnail, download, animation.HasSpoiler);

                Overlay.Visibility = Visibility.Collapsed;
            }
        }

        private bool _hidden;

        public void Hide()
        {
            if (_hidden)
            {
                return;
            }

            _hidden = true;

            if (!_templateApplied)
            {
                return;
            }

            _thumbnailController?.Recycle();

            ThumbnailTexture.Opacity = 0;
            Overlay.Opacity = 0;
        }

        private void UpdateThumbnail(object target, File file)
        {
            UpdateMessage(_message, false);
        }

        private void UpdateThumbnail(MessageWithOwner message, Thumbnail thumbnail, Minithumbnail minithumbnail, bool download, bool hasSpoiler)
        {
            _thumbnailController ??= new ThumbnailController(ThumbnailTexture);

            var file = thumbnail?.File;
            if (file != null && thumbnail.Format is ThumbnailFormatJpeg or ThumbnailFormatPng or ThumbnailFormatGif)
            {
                if (file.Local.IsDownloadingCompleted)
                {
                    if (hasSpoiler)
                    {
                        _thumbnailController.Blur(file.Local.Path, 15, HashCode.Combine(message.ChatId, message.Id));
                    }
                    else
                    {
                        _thumbnailController.Bitmap(file.Local.Path, hashCode: HashCode.Combine(message.ChatId, message.Id));
                    }
                }
                else
                {
                    if (download)
                    {
                        if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                        {
                            message.ClientService.DownloadFile(file.Id, 1);
                        }

                        UpdateManager.Subscribe(this, message, file, ref _thumbnailToken, UpdateThumbnail, true);
                    }

                    if (minithumbnail != null)
                    {
                        _thumbnailController.Blur(minithumbnail.Data, hasSpoiler ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
                    }
                    else
                    {
                        _thumbnailController.Recycle();
                    }
                }
            }
            else if (minithumbnail != null)
            {
                _thumbnailController.Blur(minithumbnail.Data, hasSpoiler ? 15 : 3, HashCode.Combine(message.ChatId, message.Id));
            }
            else
            {
                _thumbnailController.Recycle();
            }

            Particles.Source = hasSpoiler
                ? new ParticlesImageSource()
                : null;
        }
    }
}
