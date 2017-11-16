using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Controls;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class ProportionsToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return Convert((ImageCroppingProportions)value);
        }

        public static string Convert(ImageCroppingProportions proportions)
        {
            switch (proportions)
            {
                case ImageCroppingProportions.Custom:
                    return "Custom";
                case ImageCroppingProportions.Original:
                    return Strings.Android.CropOriginal;
                case ImageCroppingProportions.Square:
                    return Strings.Android.CropSquare;
                // Portrait
                case ImageCroppingProportions.TwoOverThree:
                    return "2:3";
                case ImageCroppingProportions.ThreeOverFive:
                    return "3:5";
                case ImageCroppingProportions.ThreeOverFour:
                    return "3:4";
                case ImageCroppingProportions.FourOverFive:
                    return "4:5";
                case ImageCroppingProportions.FiveOverSeven:
                    return "5:7";
                case ImageCroppingProportions.NineOverSixteen:
                    return "9:16";
                // Landscape
                case ImageCroppingProportions.ThreeOverTwo:
                    return "3:2";
                case ImageCroppingProportions.FiveOverThree:
                    return "5:3";
                case ImageCroppingProportions.FourOverThree:
                    return "4:3";
                case ImageCroppingProportions.FiveOverFour:
                    return "5:4";
                case ImageCroppingProportions.SevenOverFive:
                    return "7:5";
                case ImageCroppingProportions.SixteenOverNine:
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
