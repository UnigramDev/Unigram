//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Native;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Common
{
    public static class LottieContent
    {
        /// <summary>
        /// Renders the first frame of <paramref name="path"/> and crops it to the square around
        /// what it paints, which the returned rectangle gives as a fraction of the frame. A
        /// reaction's center animation paints the emoji smaller than its frame, and not quite in
        /// its middle, leaving room for its effect: cropped, it fills a box as a custom emoji
        /// does.
        /// </summary>
        /// <param name="size">The size to render the whole frame at, large enough for the part
        /// that is kept to stay sharp.</param>
        public static async Task<(ImageSource Source, Rect Content)> RenderAsync(string path, int size)
        {
            var dpi = WindowContext.Current.RasterizationScale;
            var pixels = (int)(size * dpi);

            if (pixels <= 0)
            {
                return (null, default);
            }

            var buffer = BufferSurface.Create((uint)(pixels * pixels * 4));

            var rendered = await Task.Run(() =>
            {
                var animation = LottieAnimation.LoadFromFile(path, pixels, pixels, false, null);
                if (animation != null)
                {
                    animation.RenderSync(buffer, 0);
                    animation.Dispose();
                    return true;
                }

                return false;
            });

            if (!rendered)
            {
                return (null, default);
            }

            // BGRA, so the alpha of each pixel is its fourth byte.
            var data = buffer.ToArray();

            int minX = pixels, minY = pixels, maxX = -1, maxY = -1;

            for (int y = 0; y < pixels; y++)
            {
                for (int x = 0; x < pixels; x++)
                {
                    if (data[(y * pixels + x) * 4 + 3] > 8)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (maxX < 0)
            {
                return (null, default);
            }

            var side = Math.Max(maxX - minX, maxY - minY) + 1;
            var left = (minX + maxX + 1 - side) / 2;
            var top = (minY + maxY + 1 - side) / 2;

            var output = new byte[side * side * 4];

            for (int y = 0; y < side; y++)
            {
                var source = top + y;
                if (source < 0 || source >= pixels)
                {
                    continue;
                }

                for (int x = 0; x < side; x++)
                {
                    var column = left + x;
                    if (column >= 0 && column < pixels)
                    {
                        Buffer.BlockCopy(data, (source * pixels + column) * 4, output, (y * side + x) * 4, 4);
                    }
                }
            }

            var bitmap = new WriteableBitmap(side, side);
            output.CopyTo(bitmap.PixelBuffer);
            bitmap.Invalidate();

            return (bitmap, new Rect((double)left / pixels, (double)top / pixels, (double)side / pixels, (double)side / pixels));
        }
    }
}
