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

        private async void LoadPreview()
        {
            _preview = await ImageHelper.GetPreviewBitmapAsync(File);
            RaisePropertyChanged(() => Preview);
        }

        public override StorageMedia Clone()
        {
            var item = new StoragePhoto(File);
            item._thumbnail = _thumbnail;
            item._preview = _preview;

            return item;
        }
    }

    public class StorageVideo : StorageMedia
    {
        public StorageVideo(StorageFile file)
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

        private async void LoadPreview()
        {
            //_preview = await ImageHelper.GetPreviewBitmapAsync(File);
            //RaisePropertyChanged(() => Preview);
        }

        public override StorageMedia Clone()
        {
            var item = new StorageVideo(File);
            item._thumbnail = _thumbnail;
            item._preview = _preview;

            return item;
        }
    }
}
