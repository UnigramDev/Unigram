using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Mvvm;
using Unigram.Common;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Entities
{
    public class StoragePhoto : StorageMedia
    {
        private BasicProperties _basic;

        public StoragePhoto(StorageFile file, BasicProperties basic, ImageProperties props)
            : base(file, basic)
        {
            _fullRectangle = new Rect(0, 0, props.GetWidth(), props.GetHeight());
            _basic = basic;

            Properties = props;
        }

        public override uint Width => Properties.GetWidth();
        public override uint Height => Properties.GetHeight();

        //private bool? _isAnimatable;
        //public override bool IsAnimatable
        //{
        //    get
        //    {
        //        if (_isAnimatable == null)
        //        {
        //            try
        //            {
        //                using (var archive = ZipFile.OpenRead(File.Path))
        //                {
        //                    _isAnimatable = true;
        //                }
        //            }
        //            catch
        //            {
        //                _isAnimatable = false;
        //            }
        //        }

        //        return _isAnimatable ?? false;
        //    }
        //}

        public ImageProperties Properties { get; private set; }

        public new static async Task<StoragePhoto> CreateAsync(StorageFile file, bool selected)
        {
            try
            {
                if (!file.IsAvailable)
                {
                    return null;
                }

                var basic = await file.GetBasicPropertiesAsync();
                var image = await file.Properties.GetImagePropertiesAsync();

                if (image.Width > 0 && image.Height > 0)
                {
                    return new StoragePhoto(file, basic, image) { IsSelected = selected };
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public override StorageMedia Clone()
        {
            var item = new StoragePhoto(File, _basic, Properties);
            item._thumbnail = _thumbnail;
            item._preview = _preview;
            item._cropRectangle = _cropRectangle;

            return item;
        }
    }
}
