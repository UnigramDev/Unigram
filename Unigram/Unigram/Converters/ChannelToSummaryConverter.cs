using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class ChannelToSummaryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is TLChannel channel)
            {
                var result = channel.IsMegaGroup ? "Group" : "Channel";
                if (channel.HasUsername && channel.HasParticipantsCount)
                {
                    return string.Format("{0}, {1}, {2} members", channel.Username, channel.IsMegaGroup ? "group" : "channel", channel.ParticipantsCount);
                }
                else if (channel.HasUsername)
                {
                    return string.Format("{0}, {1}", channel.Username, channel.IsMegaGroup ? "group" : "channel");
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
