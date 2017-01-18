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
    public class EditToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var replyInfo = value as ReplyInfo;
            if (replyInfo != null)
            {
                value = replyInfo.Reply;
            }

            var container = value as TLMessagesContainter;
            if (container != null)
            {
                if (container.EditMessage != null)
                {
                    return parameter != null ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            return parameter == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
