using System;
using Unigram.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class ProportionsToLabelConverter : IValueConverter
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
                    return Strings.Resources.CropOriginal;
                case BitmapProportions.Square:
                    return Strings.Resources.CropSquare;
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
