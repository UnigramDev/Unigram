using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Resources;

namespace Unigram.Converters
{
    public class StringFormatConverter : IValueConverter
    {
        private readonly ResourceLoader _loader;

        public StringFormatConverter()
        {
            _loader = ResourceLoader.GetForViewIndependentUse("Android");
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is string format)
            {
                return string.Format(_loader.GetString(format), value);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
