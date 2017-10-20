using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Mvvm;
using Unigram.Core.Helpers;
using Windows.Foundation;
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

        private Rect? _cropRectangle;
        public Rect? CropRectangle
        {
            get { return _cropRectangle; }
            set { Set(ref _cropRectangle, value); }
        }

        private int _zoomFactor;
        public int ZoomFactor
        {
            get { return _zoomFactor; }
            set { Set(ref _zoomFactor, value); }
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
