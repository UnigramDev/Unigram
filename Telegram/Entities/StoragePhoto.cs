//
// Copyright Fela Ameghino & Contributors 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Telegram.Entities
{
    public partial class StoragePhoto : StorageMedia
    {
        private readonly int _width;
        private readonly int _height;

        private StoragePhoto(StorageFile file, ulong fileSize, uint width, uint height)
            : base(file, fileSize)
        {
            _width = (int)width;
            _height = (int)height;
        }

        public override int Width => _width;
        public override int Height => _height;

        public static async Task<StoragePhoto> CreateAsync(StorageFile file, ulong fileSize)
        {
            try
            {
                using (var source = await file.OpenReadAsync())
                {
                    var bitmap = await BitmapDecoder.CreateAsync(source);
                    if (bitmap.PixelWidth >= 20 * bitmap.PixelHeight || bitmap.PixelHeight >= 20 * bitmap.PixelWidth)
                    {
                        return null;
                    }

                    if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                    {
                        return new StoragePhoto(file, fileSize, bitmap.PixelWidth, bitmap.PixelHeight);
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
