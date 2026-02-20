//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Native;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Common
{
    public class ThumbnailController
    {
        private readonly ImageBrush _brush;
        private readonly double _maxWidth;
        private readonly double _maxHeight;

        private long _hashCode;

        private ImageSource _source;
        private SoftwareBitmap _bitmap;
        private int _generation;

        public ThumbnailController(ImageBrush brush, double maxWidth = 0, double maxHeight = 0)
        {
            _brush = brush;
            _maxWidth = maxWidth;
            _maxHeight = maxHeight;
        }

        public async void Blur(string path, float amount, long hashCode = 0)
        {
            var generation = ++_generation;

            try
            {
                if (_hashCode != hashCode)
                {
                    Recycle(generation, hashCode);
                }

                var bitmap = await Task.Run(() => PlaceholderHelper.Background.DrawBlurred(path, amount));

                if (_generation != generation)
                {
                    bitmap.Dispose();
                    return;
                }

                if (_source is not SoftwareBitmapSource bitmapSource)
                {
                    bitmapSource = new SoftwareBitmapSource();
                }

                await bitmapSource.SetBitmapAsync(bitmap);

                if (_generation != generation)
                {
                    return;
                }

                _source = bitmapSource;
                _bitmap = bitmap;

                if (_brush.ImageSource != bitmapSource)
                {
                    _brush.ImageSource = bitmapSource;
                }
            }
            catch { }
        }

        public async void Blur(IList<byte> bytes, float amount, long hashCode = 0)
        {
            var generation = ++_generation;

            try
            {
                if (_hashCode != hashCode)
                {
                    Recycle(generation, hashCode);
                }

                var bitmap = await Task.Run(() => PlaceholderHelper.Background.DrawBlurred(bytes, amount));

                if (_generation != generation)
                {
                    bitmap.Dispose();
                    return;
                }

                if (_source is not SoftwareBitmapSource bitmapSource)
                {
                    bitmapSource = new SoftwareBitmapSource();
                }

                await bitmapSource.SetBitmapAsync(bitmap);

                if (_generation != generation)
                {
                    return;
                }

                _source = bitmapSource;
                _bitmap = bitmap;

                if (_brush.ImageSource != bitmapSource)
                {
                    _brush.ImageSource = bitmapSource;
                }
            }
            catch { }
        }

        public async void Bitmap(string path, int width = 0, int height = 0, long hashCode = 0)
        {
            var generation = ++_generation;

            try
            {
                if (_hashCode != hashCode)
                {
                    Recycle(generation, hashCode);
                }

                if (_source is not BitmapImage bitmapSource)
                {
                    bitmapSource = new BitmapImage
                    {
                        DecodePixelType = DecodePixelType.Logical
                    };
                }

                // TODO: implement
                bitmapSource.DecodePixelWidth = width;
                bitmapSource.DecodePixelHeight = height;

                var file = await StorageFile.GetFileFromPathAsync(path);
                using (var stream = await file.OpenReadAsync())
                {
                    if (_generation != generation)
                    {
                        return;
                    }

                    await bitmapSource.SetSourceAsync(stream);
                }

                if (_generation != generation)
                {
                    return;
                }

                _source = bitmapSource;

                if (_brush.ImageSource != bitmapSource)
                {
                    _brush.ImageSource = bitmapSource;
                }
            }
            catch { }
        }

        public async void Bitmap(IList<byte> bytes, int width = 0, int height = 0, long hashCode = 0)
        {
            var generation = ++_generation;

            try
            {
                if (_hashCode != hashCode)
                {
                    Recycle(generation, hashCode);
                }

                if (_source is not BitmapImage bitmapSource)
                {
                    bitmapSource = new BitmapImage
                    {
                        DecodePixelType = DecodePixelType.Logical
                    };
                }

                // TODO: implement
                bitmapSource.DecodePixelWidth = width;
                bitmapSource.DecodePixelHeight = height;

                using (var stream = new InMemoryRandomAccessStream())
                {
                    PlaceholderImageHelper.WriteBytes(bytes, stream);

                    await bitmapSource.SetSourceAsync(stream);
                }

                if (_generation != generation)
                {
                    return;
                }

                _source = bitmapSource;

                if (_brush.ImageSource != bitmapSource)
                {
                    _brush.ImageSource = bitmapSource;
                }
            }
            catch { }
        }

        public void Recycle()
        {
            Recycle(0, 0);
        }

        private void Recycle(int generation, long hashCode)
        {
            _brush.ImageSource = null;

            if (_source is SoftwareBitmapSource software)
            {
                software.Dispose();
            }

            _source = null;

            _bitmap?.Dispose();
            _bitmap = null;

            _generation = generation;
            _hashCode = hashCode;
        }
    }
}
