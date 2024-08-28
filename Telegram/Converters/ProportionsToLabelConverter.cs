//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Data;
using System;
using Telegram.Controls;

namespace Telegram.Converters
{
    public partial class ProportionsToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Convert((BitmapProportions)value);
        }

        public static string Convert(BitmapProportions proportions)
        {
            switch (proportions)
            {
                case BitmapProportions.Custom:
                    return "Custom";
                case BitmapProportions.Original:
                    return Strings.CropOriginal;
                case BitmapProportions.Square:
                    return Strings.CropSquare;
                // Portrait
                case BitmapProportions.TwoOverThree:
                    return "2:3";
                case BitmapProportions.ThreeOverFive:
                    return "3:5";
                case BitmapProportions.ThreeOverFour:
                    return "3:4";
                case BitmapProportions.FourOverFive:
                    return "4:5";
                case BitmapProportions.FiveOverSeven:
                    return "5:7";
                case BitmapProportions.NineOverSixteen:
                    return "9:16";
                // Landscape
                case BitmapProportions.ThreeOverTwo:
                    return "3:2";
                case BitmapProportions.FiveOverThree:
                    return "5:3";
                case BitmapProportions.FourOverThree:
                    return "4:3";
                case BitmapProportions.FiveOverFour:
                    return "5:4";
                case BitmapProportions.SevenOverFive:
                    return "7:5";
                case BitmapProportions.SixteenOverNine:
                    return "16:9";
                default:
                    return proportions.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
