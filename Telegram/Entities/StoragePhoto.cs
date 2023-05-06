//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace Telegram.Entities
{
    public class StoragePhoto : StorageMedia
    {
        private readonly uint _width;
        private readonly uint _height;

        public StoragePhoto(StorageFile file, BasicProperties basic, uint width, uint height)
            : base(file, basic)
        {
            _width = width;
            _height = height;
        }

        public override uint Width => _width;
        public override uint Height => _height;

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

                    if (bitmap.PixelWidth >= 20 * bitmap.PixelHeight || bitmap.PixelHeight >= 20 * bitmap.PixelWidth)
                    {
                        return null;
                    }

                    if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                    {
                        return new StoragePhoto(file, basic, bitmap.PixelWidth, bitmap.PixelHeight);
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
