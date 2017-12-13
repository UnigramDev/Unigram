using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class ChannelToSummaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TLChannel channel)
            {
                if (channel.HasUsername && channel.HasParticipantsCount)
                {
                    return string.Format("{0}, {1}", channel.Username, LocaleHelper.Declension(channel.IsMegaGroup ? "Members" : "Subscribers", channel.ParticipantsCount ?? 0));
                }
                else if (channel.HasUsername)
                {
                    return channel.Username;
                }
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
