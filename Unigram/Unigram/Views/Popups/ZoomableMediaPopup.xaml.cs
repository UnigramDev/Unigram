//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class ZoomableMediaPopup : Grid
    {
        private string _fileToken;
        private string _thumbnailToken;

        private object _lastItem;

        public ZoomableMediaPopup()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _lastItem = null;
        }

        public Action<int> DownloadFile { get; set; }

        public Func<int> SessionId { get; set; }

        public void SetSticker(Sticker sticker)
        {
            _lastItem = sticker;

            Title.Text = sticker.Emoji;
            Aspect.MaxWidth = 200;
            Aspect.MaxHeight = 200;
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
                if (sticker.Format is StickerFormatTgs)
                {
                    Thumbnail.Opacity = 0;
                    Texture.Source = null;
                    Container.Child = new LottieView { Source = UriEx.ToLocal(file.Local.Path) };
                }
                else if (sticker.Format is StickerFormatWebm)
                {
                    Thumbnail.Opacity = 0;
                    Texture.Source = null;
                    Container.Child = new AnimationView { Source = new LocalVideoSource(file) };
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
                Thumbnail.Opacity = 0;
                Texture.Source = null;
                Container.Child = new AnimationView { Source = new LocalVideoSource(file) };
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
