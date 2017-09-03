using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class CompressionToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double)
            {
                value = (int)(double)value;
            }

            switch ((int)value)
            {
                case 0:
                    return parameter == null ? "\uE901" : "240p";
                case 1:
                    return parameter == null ? "\uE902" : "360p";
                case 2:
                    return parameter == null ? "\uE903" : "480p";
                case 3:
                    return parameter == null ? "\uE904" : "720p";
                case 4:
                default:
                    return parameter == null ? "\uE905" : "1080p";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
