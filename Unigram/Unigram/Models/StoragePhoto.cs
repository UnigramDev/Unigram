using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Mvvm;
using Unigram.Helpers;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Models
{
    public class StoragePhoto : BindableBase
    {
        public StoragePhoto(StorageFile file)
        {
            File = file;
        }

        public StorageFile File { get; private set; }

        private BitmapImage _thumbnail;
        public BitmapImage Thumbnail
        {
            get
            {
                if (_thumbnail == null)
                    LoadThumbnail();

                return _thumbnail;
            }
        }

        private ImageSource _preview;
        public ImageSource Preview
        {
            get
            {
                if (_preview == null)
                    LoadPreview();

                return _preview;
            }
        }

        private string _caption;
        public string Caption
        {
            get
            {
                return _caption;
            }
            set
            {
                Set(ref _caption, value);
            }
        }

        private async void LoadThumbnail()
        {
            using (var thumbnail = await File.GetThumbnailAsync(ThumbnailMode.ListView, 96, ThumbnailOptions.UseCurrentScale))
            {
                if (thumbnail != null)
                {
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(thumbnail);
                    _thumbnail = bitmapImage;
                }
            }

            RaisePropertyChanged(() => Thumbnail);
        }

        private async void LoadPreview()
        {
            _preview = await ImageHelper.GetPreviewBitmapAsync(File);
            RaisePropertyChanged(() => Preview);
        }
    }
}
