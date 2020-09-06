using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Messages.Content;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Gallery
{
    public sealed partial class GalleryContentView : Grid
    {
        private IGalleryDelegate _delegate;
        private GalleryContent _item;

        public GalleryContent Item => _item;
        public Grid Presenter => Panel;

        public GalleryContentView()
        {
            InitializeComponent();
        }

        public void UpdateItem(IGalleryDelegate delegato, GalleryContent item)
        {
            _delegate = delegato;
            _item = item;

            Tag = item;

            Panel.Background = null;
            Texture.Source = null;

            ScrollingHost.ChangeView(0, 0, 1, true);

            if (item == null)
            {
                return;
            }

            var data = item.GetFile();
            var thumb = item.GetThumbnail();

            Panel.Constraint = item.Constraint;
            Panel.InvalidateMeasure();

            if (thumb != null && (item.IsVideo || (item.IsPhoto && !data.Local.IsDownloadingCompleted)))
            {
                UpdateThumbnail(item, thumb);
            }

            UpdateFile(item, data);
        }

        public void UpdateFile(GalleryContent item, File file)
        {
            var data = item.GetFile();
            var thumb = item.GetThumbnail();

            if (thumb != null && thumb.Id != data.Id && thumb.Id == file.Id)
            {
                UpdateThumbnail(item, file);
                return;
            }
            else if (data == null || data.Id != file.Id)
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
                    item.ProtoService.DownloadFile(file.Id, 1);
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
                    Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                }
            }
        }

        private void UpdateThumbnail(GalleryContent item, File file)
        {
            if (file.Local.IsDownloadingCompleted)
            {
                //Texture.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                Panel.Background = new ImageBrush { ImageSource = PlaceholderHelper.GetBlurred(file.Local.Path), Stretch = Stretch.UniformToFill };
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
            {
                item.ProtoService.DownloadFile(file.Id, 1);
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
                item.ProtoService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                if (SettingsService.Current.IsStreamingEnabled && item.IsVideo && item.IsStreamable)
                {
                    _delegate?.OpenFile(item, file);
                }
                else
                {
                    item.ProtoService.DownloadFile(file.Id, 32);
                }
            }
            else
            {
                if (item.IsVideo)
                {
                    _delegate?.OpenFile(item, file);
                }

                _delegate?.OpenItem(item);
            }
        }

        public void Reset()
        {
            ScrollingHost.ChangeView(0, 0, 1, true);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Panel.MaxWidth = availableSize.Width;
            Panel.MaxHeight = availableSize.Height;

            return base.MeasureOverride(availableSize);
        }

        private void ScrollingHost_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            Panel.ManipulationMode = ScrollingHost.ZoomFactor == 1 ? ManipulationModes.TranslateY | ManipulationModes.TranslateRailsY | ManipulationModes.System : ManipulationModes.System;
        }
    }
}
