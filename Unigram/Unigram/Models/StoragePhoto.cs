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

namespace Unigram.Models
{
    public class StoragePhoto : StorageMedia
    {
        public StoragePhoto(StorageFile file)
            : base(file)
        {
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

        private bool _applyCrop;
        public bool ApplyCrop
        {
            get
            {
                return _applyCrop;
            }
            set
            {
                Set(ref _applyCrop, value);

                if (File == null)
                {
                    return;
                }

                if (!_applyCrop)
                {
                    LoadPreview();
                }
                else if (_applyCrop && CropRectangle.HasValue)
                {
                    LoadCroppedPreview();
                }
            }
        }

        private async void LoadPreview()
        {
            _preview = await ImageHelper.GetPreviewBitmapAsync(File);
            RaisePropertyChanged(() => Preview);
        }

        private async void LoadCroppedPreview()
        {
            if (!CropRectangle.HasValue)
            {
                return;
            }

            _preview = await ImageHelper.CropAndPreviewAsync(File, CropRectangle.Value);
            RaisePropertyChanged(() => Preview);
        }

        public Task<StorageFile> GetFileAsync()
        {
            if (IsCropped)
            {
                return ImageHelper.CropAsync(File, CropRectangle.Value);
            }

            return Task.FromResult(File);
        }

        public override StorageMedia Clone()
        {
            var item = new StoragePhoto(File);
            item._thumbnail = _thumbnail;
            item._preview = _preview;
            item._cropRectangle = _cropRectangle;

            return item;
        }
    }
}
