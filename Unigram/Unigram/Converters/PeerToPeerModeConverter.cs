using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class PeerToPeerModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch (System.Convert.ToInt32(value))
            {
                case 0:
                default:
                    return Strings.Android.LastSeenEverybody;
                case 1:
                    return Strings.Android.LastSeenContacts;
                case 2:
                    return Strings.Android.LastSeenNobody;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
