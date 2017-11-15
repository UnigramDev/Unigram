using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unigram.Common;
using Windows.ApplicationModel.Resources;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class StringPluralConverter : IValueConverter
    {
        private readonly ResourceLoader _loader;

        public StringPluralConverter()
        {
            _loader = ResourceLoader.GetForViewIndependentUse("Android");
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (parameter is string format)
            {
                // TODO: declesion

                return string.Format(_loader.GetString(LocaleHelper.Pluralize(format, System.Convert.ToInt32(value))), value);
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
