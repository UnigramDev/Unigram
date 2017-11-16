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
            var participant = value as TLChannelParticipantBase;
            switch (value)
            {
                case TLChannelParticipantCreator creator:
                    return Strings.Android.ChannelCreator;
                case TLChannelParticipantAdmin admin:
                    return string.Format(Strings.Android.EditAdminPromotedBy, admin.PromotedByUser.FullName);
                case TLChannelParticipantBanned banned:
                    return string.Format(Strings.Android.UserRestrictionsBy, banned.KickedByUser.FullName);
                default:
                    return LastSeenConverter.GetLabel(participant.User, false);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
