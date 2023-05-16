//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryContentView : AspectView
    {
        private IGalleryDelegate _delegate;
        private GalleryContent _item;

        public GalleryContent Item => _item;

        public Border Presenter => Panel;

        private long _fileToken;
        private long _thumbnailToken;

        private Stretch _lastStretch;

        public bool IsEnabled
        {
            get => Button.IsEnabled;
            set => Button.IsEnabled = value;
        }

        public GalleryContentView()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            Logger.Debug();

            if (_lastStretch == Stretch)
            {
                return;
            }

            _lastStretch = Stretch;

            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var anim = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            anim.InsertKeyFrame(0, new Vector3(prev / next, 1));
            anim.InsertKeyFrame(1, Vector3.One);

            var panel = ElementCompositionPreview.GetElementVisual(this);
            panel.CenterPoint = new Vector3(next.X / 2, next.Y / 2, 0);
            panel.StartAnimation("Scale", anim);

            var factor = Window.Current.Compositor.CreateExpressionAnimation("Vector3(1 / content.Scale.X, 1 / content.Scale.Y, 1)");
            factor.SetReferenceParameter("content", panel);

            var button = ElementCompositionPreview.GetElementVisual(Button);
            button.CenterPoint = new Vector3(Button.ActualSize.X / 2, Button.ActualSize.Y / 2, 0);
            button.StartAnimation("Scale", factor);
        }

        public void UpdateItem(IGalleryDelegate delegato, GalleryContent item)
        {
            _delegate = delegato;
            _item = item;

            Tag = item;

            Background = null;
            Texture.Source = null;

            //ScrollingHost.ChangeView(0, 0, 1, true);

            if (item == null)
            {
                return;
            }

            var file = item.GetFile();
            var thumbnail = item.GetThumbnail();

            if (item.IsVideoNote)
            {
                MaxWidth = 384;
                MaxHeight = 384;

                CornerRadius = new CornerRadius(384 / 2);
                Constraint = new Size(384, 384);
            }
            else
            {
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;

                CornerRadius = new CornerRadius(0);
                Constraint = item.Constraint;
            }

            UpdateManager.Subscribe(this, delegato.ClientService, file, ref _fileToken, UpdateFile);
            UpdateFile(item, file);

            if (thumbnail != null && (item.IsVideo || (item.IsPhoto && !file.Local.IsDownloadingCompleted)))
            {
                UpdateThumbnail(item, thumbnail, true);
            }
        }

        private void UpdateFile(object target, File file)
        {
            UpdateFile(_item, file);
        }

        private void UpdateFile(GalleryContent item, File file)
        {
            var reference = item?.GetFile();
            if (reference == null || reference.Id != file.Id)
            {
                return;
            }

            var size = Math.Max(file.Size, file.ExpectedSize);
            if (file.Local.IsDownloadingActive)
            {
                Button.SetGlyph(file.Id, MessageContentState.Downloading);
                Button.Progress = (double)file.Local.DownloadedSize / size;
                Button.Opacity = 1;
            }
            else if (file.Remote.IsUploadingActive)
            {
                Button.SetGlyph(file.Id, MessageContentState.Uploading);
                Button.Progress = (double)file.Remote.UploadedSize / size;
                Button.Opacity = 1;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.SetGlyph(file.Id, MessageContentState.Download);
                Button.Progress = 0;
                Button.Opacity = 1;

                if (item.IsPhoto)
                {
                    item.ClientService.DownloadFile(file.Id, 1);
                }
            }
            else
            {
                if (item.IsVideo)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Play);
                    Button.Progress = 1;
                    Button.Opacity = 1;
                }
                else if (item.IsPhoto)
                {
                    Button.SetGlyph(file.Id, MessageContentState.Photo);
                    Button.Opacity = 0;

                    Texture.Source = UriEx.ToBitmap(file.Local.Path, 0, 0);
                }
            }

            Canvas.SetZIndex(Button,
                Button.State == MessageContentState.Photo ? -1 : 0);
        }

        private void UpdateThumbnail(object target, File file)
        {
            UpdateThumbnail(_item, file, false);
        }

        private void UpdateThumbnail(GalleryContent item, File file, bool download)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                //Texture.Source = new BitmapImage(UriEx.GetLocal(file.Local.Path));
                Background = new ImageBrush { ImageSource = PlaceholderHelper.GetBlurred(file.Local.Path), Stretch = Stretch.UniformToFill };
            }
            else if (download)
            {
                if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                {
                    item.ClientService.DownloadFile(file.Id, 1);
                }

                UpdateManager.Subscribe(this, _delegate.ClientService, file, ref _thumbnailToken, UpdateThumbnail, true);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var item = _item;
            if (item == null)
            {
                return;
            }

            var file = item.GetFile();
            if (file == null)
            {
                return;
            }

            if (file.Local.IsDownloadingActive)
            {
                item.ClientService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (SettingsService.Current.IsStreamingEnabled && item.IsVideo && item.IsStreamable)
                {
                    _delegate?.OpenFile(item, file);
                }
                else
                {
                    item.ClientService.DownloadFile(file.Id, 32);
                }
            }
            else if (item.IsVideo)
            {
                _delegate?.OpenFile(item, file);
            }
        }
    }
}
