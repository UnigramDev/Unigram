//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Views.Popups
{
    public sealed partial class ZoomableMediaPopup : Grid
    {
        private ApplicationView _applicationView;

        private long _fileToken;
        private long _thumbnailToken;

        private object _lastItem;

        public ZoomableMediaPopup()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _applicationView = ApplicationView.GetForCurrentView();
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;

            OnVisibleBoundsChanged(_applicationView, null);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _lastItem = null;

            if (_applicationView != null)
            {
                _applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            }
        }

        public Action<int> DownloadFile { get; set; }

        public Func<int> SessionId { get; set; }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            if (sender == null)
            {
                return;
            }

            if (/*BackgroundElement != null &&*/ Window.Current?.Bounds is Rect bounds && sender.VisibleBounds != bounds)
            {
                Margin = new Thickness(sender.VisibleBounds.X - bounds.Left, sender.VisibleBounds.Y - bounds.Top, bounds.Width - (sender.VisibleBounds.Right - bounds.Left), bounds.Height - (sender.VisibleBounds.Bottom - bounds.Top));
            }
            else
            {
                Margin = new Thickness();
            }
        }

        public void SetSticker(Sticker sticker)
        {
            _lastItem = sticker;

            Title.Text = sticker.Emoji;
            Aspect.MaxWidth = 180;
            Aspect.MaxHeight = 180;
            Aspect.Constraint = sticker;

            if (sticker.Thumbnail != null)
            {
                UpdateThumbnail(sticker.Thumbnail.File, true);
            }

            UpdateFile(sticker, sticker.StickerValue, true);
        }

        public void SetAnimation(Animation animation)
        {
            _lastItem = animation;

            Title.Text = string.Empty;
            Aspect.MaxWidth = 320;
            Aspect.MaxHeight = 420;
            Aspect.Constraint = animation;

            if (animation.Thumbnail != null && animation.Thumbnail.Format is ThumbnailFormatJpeg)
            {
                UpdateThumbnail(animation.Thumbnail.File, true);
            }

            UpdateFile(animation, animation.AnimationValue, true);
        }

        private void UpdateFile(object target, File file)
        {
            if (_lastItem is Sticker sticker && file.Local.IsDownloadingCompleted)
            {
                if (sticker.StickerValue.Id == file.Id)
                {
                    UpdateFile(sticker, file, false);
                }
                else if (sticker.Thumbnail?.File.Id == file.Id)
                {
                    UpdateThumbnail(file, false);
                }
            }
            else if (_lastItem is Animation animation && file.Local.IsDownloadingCompleted)
            {
                if (animation.AnimationValue.Id == file.Id)
                {
                    UpdateFile(animation, file, false);
                }
            }
        }

        private void UpdateFile(Sticker sticker, File file, bool download)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                UpdateManager.Unsubscribe(this, ref _fileToken, true);

                if (sticker.Format is StickerFormatTgs)
                {
                    Thumbnail.Opacity = 0;
                    Texture.Source = null;
                    Container.Child = new AnimatedImage
                    {
                        AutoPlay = true,
                        FrameSize = new Size(180, 180),
                        DecodeFrameType = DecodePixelType.Logical,
                        IsCachingEnabled = true,
                        Source = new LocalFileSource(file)
                    };
                }
                else if (sticker.Format is StickerFormatWebm)
                {
                    Thumbnail.Opacity = 0;
                    Texture.Source = null;
                    Container.Child = new AnimatedImage
                    {
                        AutoPlay = true,
                        FrameSize = new Size(0, 0),
                        DecodeFrameType = DecodePixelType.Physical,
                        IsCachingEnabled = false,
                        Source = new LocalFileSource(file)
                    };
                }
                else
                {
                    Thumbnail.Opacity = 0;
                    Texture.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
                    Container.Child = new Border();
                }
            }
            else
            {
                Thumbnail.Opacity = 1;
                Texture.Source = null;
                Container.Child = new Border();

                UpdateManager.Subscribe(this, SessionId(), file, ref _fileToken, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && download)
                {
                    DownloadFile?.Invoke(file.Id);
                }
            }
        }

        private void UpdateFile(Animation animation, File file, bool download)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                UpdateManager.Unsubscribe(this, ref _fileToken, true);

                Thumbnail.Opacity = 0;
                Texture.Source = null;
                Container.Child = new AnimatedImage
                {
                    AutoPlay = true,
                    FrameSize = new Size(0, 0),
                    DecodeFrameType = DecodePixelType.Physical,
                    IsCachingEnabled = false,
                    Source = new LocalFileSource(file)
                };
            }
            else
            {
                Thumbnail.Opacity = 1;
                Texture.Source = null;
                Container.Child = new Border();

                UpdateManager.Subscribe(this, SessionId(), file, ref _fileToken, UpdateFile, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && download)
                {
                    DownloadFile?.Invoke(file.Id);
                }
            }
        }

        private void UpdateThumbnail(object target, File file)
        {
            UpdateThumbnail(file, false);
        }

        private void UpdateThumbnail(File file, bool download)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                Thumbnail.Source = PlaceholderHelper.GetWebPFrame(file.Local.Path);
            }
            else
            {
                Thumbnail.Source = null;

                UpdateManager.Subscribe(this, SessionId(), file, ref _thumbnailToken, UpdateThumbnail, true);

                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && download)
                {
                    DownloadFile?.Invoke(file.Id);
                }
            }
        }
    }
}
