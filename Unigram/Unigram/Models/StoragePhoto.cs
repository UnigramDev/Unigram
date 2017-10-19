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

        private StorageFile _croppedFile;
        public StorageFile CroppedFile
        {
            get { return _croppedFile; }
            set
            {
                _croppedFile = value;
                if (_croppedFile != null)
                {
                    LoadCroppedPreview();
                }
                else
                {
                    LoadPreview();
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
            _preview = await ImageHelper.GetPreviewBitmapAsync(CroppedFile);
            RaisePropertyChanged(() => Preview);
        }

        public override StorageMedia Clone()
        {
            var item = new StoragePhoto(File);
            item._thumbnail = _thumbnail;
            item._preview = _preview;
            item._croppedFile = _croppedFile;

            return item;
        }
    }
}
