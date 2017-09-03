using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Mvvm;
using Unigram.Core.Helpers;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Core.Models
{
    public class StorageMedia : BindableBase
    {
        public StorageMedia(StorageFile file)
        {
            File = file;
        }

        public StorageFile File { get; private set; }

        protected BitmapImage _thumbnail;
        public BitmapImage Thumbnail
        {
            get
            {
                if (_thumbnail == null)
                    LoadThumbnail();

                return _thumbnail;
            }
        }

        protected string _caption;
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

        protected int? _ttlSeconds;
        public int? TTLSeconds
        {
            get
            {
                return _ttlSeconds;
            }
            set
            {
                Set(ref _ttlSeconds, value);
            }
        }

        protected bool _isSelected;
        public bool IsSelected
        {
            get
            {
                return _isSelected;
            }
            set
            {
                Set(ref _isSelected, value);
            }
        }

        public bool IsPhoto => this is StoragePhoto;
        public bool IsVideo => this is StorageVideo;

        private async void LoadThumbnail()
        {
            try
            {
                if (!File.Attributes.HasFlag(FileAttributes.Temporary))
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
            }
            catch { }
        }

        public virtual StorageMedia Clone()
        {
            var item = new StorageMedia(File);
            item._thumbnail = _thumbnail;

            return item;
        }
    }
}
