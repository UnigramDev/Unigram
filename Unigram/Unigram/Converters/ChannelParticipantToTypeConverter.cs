using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class ChannelParticipantToTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch (value)
            {
                case TLChannelParticipantCreator creator:
                    return "Creator";
                case TLChannelParticipantModerator moderator:
                case TLChannelParticipantEditor editor:
                    return "Admin";
                case TLChannelParticipant participant:
                default:
                    return "User";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
