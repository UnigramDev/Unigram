//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Common
{
    public static class UriEx
    {
        public static BitmapImage ToBitmap(string path, int width = 0, int height = 0)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return new BitmapImage(ToLocal(path))
            {
                // TODO: experiment
                //CreateOptions = BitmapCreateOptions.IgnoreImageCache,
                DecodePixelWidth = width,
                DecodePixelHeight = height,
                DecodePixelType = width > 0 || height > 0
                    ? DecodePixelType.Logical
                    : DecodePixelType.Logical
            };
        }

        public static Uri ToLocal(string path)
        {
            return new Uri(path);
        }
    }
}
