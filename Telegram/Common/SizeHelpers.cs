//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Windows.Foundation;
using Windows.Graphics.Display;

namespace Unigram.Common
{
    public static class SizeHelpers
    {
        public static double GetHypotenuse(this Size rect)
        {
            return Math.Sqrt(Math.Pow(rect.Width, 2) + Math.Pow(rect.Height, 2));
        }

        public static Size Scale(this Size size, double scaleFactor)
        {
            Size scaledSize = new Size();
            var h = size.Height;
            scaledSize.Height = double.IsInfinity(h) ? h : h * scaleFactor;
            var w = size.Width;
            scaledSize.Width = double.IsInfinity(w) ? w : w * scaleFactor;
            return scaledSize;
        }

        public static double GetDiagonalFromWidth(double width, double aspectRatio)
        {
            return Math.Sqrt(Math.Pow(width, 2) + Math.Pow(width * aspectRatio, 2));
        }

        public static Size MakeSize(double width, double aspectRatio)
        {
            return new Size(width, width * aspectRatio);
        }

        public static Size Round(this Size normalSize)
        {
            return new Size((int)normalSize.Width, (int)normalSize.Height);
        }

        public static int LogicalPixels(int pixels)
        {
            return (int)(pixels * (DisplayInformation.GetForCurrentView().LogicalDpi / 96.0f));
        }

        public static Size LogicalPixels(this Size size)
        {
            var dpi = DisplayInformation.GetForCurrentView().LogicalDpi / 96.0f;
            return new Size(size.Width * dpi, size.Height * dpi);
        }
    }
}
