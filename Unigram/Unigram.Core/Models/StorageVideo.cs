using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media;

namespace Unigram.Core.Models
{
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

        private bool _isMuted;
        public bool IsMuted
        {
            get
            {
                return _isMuted;
            }
            set
            {
                Set(ref _isMuted, value);
            }
        }

        private async void LoadPreview()
        {
            _preview = _thumbnail;
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
