using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Common;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class LastSeenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var user = value as TLUser;
            if (user != null)
            {
                return GetLabel(user, parameter == null);
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public static int GetIndex(TLUser user)
        {
            if (user.IsBot)
            {
                // Last
                return user.IsBotChatHistory ? 1 : 0;
            }

            if (user.HasStatus && user.Status != null)
            {
                switch (user.Status)
                {
                    case TLUserStatusOffline offline:
                        return offline.WasOnline;
                    case TLUserStatusOnline online:
                        return int.MaxValue;
                    case TLUserStatusRecently recently:
                        // recently
                        // Before within a week
                        return 5;
                    case TLUserStatusLastWeek lastWeek:
                        // within a week
                        // Before within a month
                        return 4;
                    case TLUserStatusLastMonth lastMonth:
                        // within a month
                        // Before long time ago
                        return 3;
                    case TLUserStatusEmpty empty:
                    default:
                        // long time ago
                        // Before bots
                        return 2;
                }
            }

            // long time ago
            // Before bots
            return 2;
        }

        public static string GetLabel(TLUser user, bool details)
        {
            if (user.Id == 777000)
            {
                return Strings.Resources.ServiceNotificationsLabel;
            }
            else if (user.IsBot)
            {
                if (details)
                {
                    return Strings.Resources.GenericBotStatus;
                }

                return user.IsBotChatHistory ? Strings.Resources.BotStatusReadsGroupHistory : Strings.Resources.BotStatusDoesNotReadGroupHistory;
            }
            else if (user.IsSelf && details)
            {
                return Strings.Resources.CloudStorageChatStatus;
            }

            if (user.HasStatus && user.Status != null)
            {
                switch (user.Status)
                {
                    case TLUserStatusOffline offline:
                        var now = DateTime.Now;
                        var seen = TLUtils.ToDateTime(offline.WasOnline);
                        var time = string.Empty;
                        if (details)
                        {
                            time = ((now.Date == seen.Date) ? Strings.Resources.LastSeenToday : (((now.Date - seen.Date) == new TimeSpan(1, 0, 0, 0)) ? Strings.Resources.LastSeenYesterdayAt : BindConvert.Current.ShortDate.Format(seen) + " ")) + BindConvert.Current.ShortTime.Format(seen);
                        }
                        else
                        {
                            time = (now.Date == seen.Date) ? ((now - seen).Hours < 1 ? ((now - seen).Minutes < 1 ? Strings.Resources.LastSeenMomentsAgo : (now - seen).Minutes.ToString() + ((now - seen).Minutes.ToString() == "1" ? Strings.Resources.LastSeenMinutesSingular : Strings.Resources.LastSeenMinutesPlural)) : ((now - seen).Hours.ToString()) + (((now - seen).Hours.ToString()) == "1" ? (Strings.Resources.LastSeenHoursSingular) : (Strings.Resources.LastSeenHoursPlural))) : now.Date - seen.Date == new TimeSpan(24, 0, 0) ? Strings.Resources.LastSeenYesterday + BindConvert.Current.ShortTime.Format(seen) : BindConvert.Current.ShortDate.Format(seen);
                        }

                        return string.Format("{0} {1}", Strings.Resources.LastSeen, time);
                    case TLUserStatusOnline online:
                        return Strings.Resources.UserOnline;
                    case TLUserStatusRecently recently:
                        return Strings.Resources.LastSeenRecently;
                    case TLUserStatusLastWeek lastWeek:
                        return Strings.Resources.LastSeenWithinWeek;
                    case TLUserStatusLastMonth lastMonth:
                        return Strings.Resources.LastSeenWithinMonth;
                    case TLUserStatusEmpty empty:
                    default:
                        return Strings.Resources.LastSeenLongTimeAgo;
                }
            }

            // Debugger.Break();
            return "Last seen a long time ago";
        }
    }
}
