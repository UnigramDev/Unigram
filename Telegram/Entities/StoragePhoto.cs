//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Telegram.Entities
{
    public class StoragePhoto : StorageMedia
    {
        private readonly BasicProperties _basic;

        public StoragePhoto(StorageFile file, BasicProperties basic, ImageProperties props)
            : base(file, basic)
        {
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

        public static new async Task<StoragePhoto> CreateAsync(StorageFile file)
        {
            try
            {
                if (!file.IsAvailable)
                {
                    return null;
                }

                using (var source = await file.OpenReadAsync())
                {
                    var bitmap = await BitmapDecoder.CreateAsync(source);

                    var basic = await file.GetBasicPropertiesAsync();
                    var image = await file.Properties.GetImagePropertiesAsync();

                    if (bitmap.PixelWidth >= 20 * bitmap.PixelHeight || bitmap.PixelHeight >= 20 * bitmap.PixelWidth)
                    {
                        return null;
                    }

                    if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                    {
                        return new StoragePhoto(file, basic, image);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
