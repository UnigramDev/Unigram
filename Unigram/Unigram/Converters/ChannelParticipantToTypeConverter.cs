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
                case TLChannelParticipantAdmin admin:
                    return string.Format("Promoted by {0}", admin.PromotedByUser.FullName);
                case TLChannelParticipantBanned banned:
                    return string.Format("Restricted by {0}", banned.KickedByUser.FullName);
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
