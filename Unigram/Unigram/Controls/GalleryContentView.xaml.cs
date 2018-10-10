using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.ViewModels;
using Unigram.ViewModels.Delegates;
using Unigram.ViewModels.Gallery;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls
{
    public sealed partial class GalleryContentView : Grid
    {
        private IGalleryDelegate _delegate;
        private GalleryContent _item;

        public GalleryContent Item => _item;
        public Grid Inner => Panel;

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

            //ScrollingHost.ChangeView(0, 0, 1, true);

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
                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Local.DownloadedSize / size;
                Button.Opacity = 1;
            }
            else if (file.Remote.IsUploadingActive)
            {
                Button.Glyph = "\uE10A";
                Button.Progress = (double)file.Remote.UploadedSize / size;
                Button.Opacity = 1;
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                Button.Glyph = "\uE118";
                Button.Progress = 0;
                Button.Opacity = 1;

                if (item.IsPhoto)
                {
                    item.ProtoService.Send(new DownloadFile(file.Id, 1));
                }
            }
            else
            {
                if (item.IsVideo)
                {
                    Button.Glyph = "\uE102";
                    Button.Progress = 1;
                    Button.Opacity = 1;
                }
                else if (item.IsPhoto)
                {
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
                item.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (_item == null)
            {
                return;
            }

            var file = _item.GetFile();
            if (file.Local.IsDownloadingActive)
            {
                _item.ProtoService.Send(new CancelDownloadFile(file.Id, false));
            }
            else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
            {
                _item.ProtoService.Send(new DownloadFile(file.Id, 1));
            }
            else
            {
                if (_item.IsVideo)
                {
                    _delegate?.OpenFile(_item, file);
                }

                _delegate?.OpenItem(_item);
            }
        }

        public void Reset()
        {
            //ScrollingHost.ChangeView(0, 0, 1, true);
        }

        //protected override Size MeasureOverride(Size availableSize)
        //{
        //    Panel.MaxWidth = availableSize.Width;
        //    Panel.MaxHeight = availableSize.Height;

        //    return base.MeasureOverride(availableSize);
        //}
    }
}
