using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class ChannelJoinedToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TLChannel channel)
            {
                if (parameter != null)
                {
                    return !channel.IsLeft && !channel.HasBannedRights ? Visibility.Collapsed : Visibility.Visible;
                }

                return !channel.IsLeft && !channel.HasBannedRights ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
