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

        // Never handed out to be mutated in place: every request builds its own source and
        // swaps it in, so a request that loses the race owns everything it disposes, and one
        // that finishes late cannot write over what is already on screen.
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

                // Re-checked inside the work item: it can sit in the pool while the control is
                // rebound, and DrawBlurred then holds the one Direct2D device that placeholder,
                // SVG and pattern rendering also share.
                var bitmap = await Task.Run(() =>
                {
                    if (_generation != generation)
                    {
                        return null;
                    }

                    return Direct2D.Shared.DrawBlurred(path, amount);
                });

                // Null for a skipped work item, and also when DrawBlurred could not open the
                // file: TDLib can delete a cached thumbnail between the check and the read.
                if (bitmap == null || _generation != generation)
                {
                    bitmap?.Dispose();
                    return;
                }

                var bitmapSource = new SoftwareBitmapSource();
                await bitmapSource.SetBitmapAsync(bitmap);

                if (_generation != generation)
                {
                    bitmapSource.Dispose();
                    bitmap.Dispose();
                    return;
                }

                SetSource(bitmapSource, bitmap);
            }
            catch { }
        }

        public async void Blur(byte[] bytes, float amount, long hashCode = 0)
        {
            var generation = ++_generation;

            try
            {
                if (_hashCode != hashCode)
                {
                    Recycle(generation, hashCode);
                }

                // Re-checked inside the work item: it can sit in the pool while the control is
                // rebound, and DrawBlurred then holds the one Direct2D device that placeholder,
                // SVG and pattern rendering also share.
                var bitmap = await Task.Run(() =>
                {
                    if (_generation != generation)
                    {
                        return null;
                    }

                    return Direct2D.Shared.DrawBlurred(bytes, amount);
                });

                // Null for a skipped work item, and also when the decode failed.
                if (bitmap == null || _generation != generation)
                {
                    bitmap?.Dispose();
                    return;
                }

                var bitmapSource = new SoftwareBitmapSource();
                await bitmapSource.SetBitmapAsync(bitmap);

                if (_generation != generation)
                {
                    bitmapSource.Dispose();
                    bitmap.Dispose();
                    return;
                }

                SetSource(bitmapSource, bitmap);
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

                var bitmapSource = new BitmapImage
                {
                    DecodePixelType = DecodePixelType.Logical
                };

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

                SetSource(bitmapSource);
            }
            catch { }
        }

        public async void Bitmap(byte[] bytes, int width = 0, int height = 0, long hashCode = 0)
        {
            var generation = ++_generation;

            try
            {
                if (_hashCode != hashCode)
                {
                    Recycle(generation, hashCode);
                }

                var bitmapSource = new BitmapImage
                {
                    DecodePixelType = DecodePixelType.Logical
                };

                // TODO: implement
                bitmapSource.DecodePixelWidth = width;
                bitmapSource.DecodePixelHeight = height;

                using (var stream = new InMemoryRandomAccessStream())
                {
                    Direct2DDevice.WriteBytes(bytes, stream);

                    await bitmapSource.SetSourceAsync(stream);
                }

                if (_generation != generation)
                {
                    return;
                }

                SetSource(bitmapSource);
            }
            catch { }
        }

        public void Recycle()
        {
            Recycle(++_generation, 0);
        }

        private void Recycle(int generation, long hashCode)
        {
            SetSource(null);

            _generation = generation;
            _hashCode = hashCode;
        }

        // Releases whatever the previous request left behind, once the brush has stopped
        // pointing at it.
        private void SetSource(ImageSource source, SoftwareBitmap bitmap = null)
        {
            var previousSource = _source;
            var previousBitmap = _bitmap;

            _source = source;
            _bitmap = bitmap;

            if (_brush.ImageSource != source)
            {
                _brush.ImageSource = source;
            }

            // Detached from the brush first, so nothing renders from a disposed source.
            if (previousSource != source && previousSource is SoftwareBitmapSource software)
            {
                software.Dispose();
            }

            if (previousBitmap != bitmap)
            {
                previousBitmap?.Dispose();
            }
        }
    }
}
