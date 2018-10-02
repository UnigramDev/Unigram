using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Mvvm;
using Unigram.Common;
using Unigram.Controls;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Entities
{
    public abstract class StorageMedia : BindableBase
    {
        public StorageMedia(StorageFile file, BasicProperties basic)
        {
            File = file;
            Basic = basic;
        }

        public StorageFile File { get; private set; }

        public BasicProperties Basic { get; private set; }

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

        protected ImageSource _bitmap;
        public ImageSource Bitmap
        {
            get
            {
                if (_bitmap == null)
                    Refresh();

                return _bitmap;
            }
        }

        protected ImageSource _preview;
        public ImageSource Preview
        {
            get
            {
                if (_preview == null)
                    Refresh();

                return _preview;
            }
        }

        protected FormattedText _caption;
        public FormattedText Caption
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

        protected int? _ttl;
        public int? Ttl
        {
            get
            {
                return _ttl;
            }
            set
            {
                Set(ref _ttl, value);
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

        protected bool _isForceFile;
        public bool IsForceFile
        {
            get
            {
                return _isForceFile;
            }
            set
            {
                Set(ref _isForceFile, value);
            }
        }

        public virtual uint Width { get; }
        public virtual uint Height { get; }

        public virtual bool IsAnimatable { get; }

        protected Rect? _fullRectangle;
        protected Rect? _cropRectangle;
        public Rect? CropRectangle
        {
            get
            {
                return _cropRectangle;
            }
            set
            {
                Set(ref _cropRectangle, value == _fullRectangle ? null : value);
            }
        }

        protected ImageCroppingProportions _cropProportions = ImageCroppingProportions.Custom;
        public ImageCroppingProportions CropProportions
        {
            get
            {
                return _cropProportions;
            }
            set
            {
                Set(ref _cropProportions, value);
            }
        }

        public bool IsPhoto => this is StoragePhoto;
        public bool IsVideo => this is StorageVideo;
        public bool IsCropped => CropRectangle.HasValue;

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

        public virtual async void Refresh()
        {
            if (_bitmap == null)
            {
                _bitmap = await ImageHelper.GetPreviewBitmapAsync(File);
            }

            if (_bitmap == null)
            {
                _bitmap = new BitmapImage();
            }

            if (CropRectangle.HasValue)
            {
                _preview = await ImageHelper.CropAndPreviewAsync(File, CropRectangle.Value);
            }
            else
            {
                _preview = _bitmap;
            }

            RaisePropertyChanged(() => Preview);
        }

        public abstract StorageMedia Clone();

        public virtual void Reset()
        {
            IsSelected = false;
            IsForceFile = false;
            Caption = null;
            CropRectangle = null;

            //_thumbnail = null;
            _preview = null;
        }

        public static async Task<StorageMedia> CreateAsync(StorageFile file, bool selected)
        {
            if (file.ContentType.Equals("video/mp4"))
            {
                return await StorageVideo.CreateAsync(file, selected);
            }
            else
            {
                return await StoragePhoto.CreateAsync(file, selected);
            }
        }
    }
}
