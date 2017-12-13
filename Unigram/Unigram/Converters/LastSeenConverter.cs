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
            if (user == null)
            {
                return null;
            }

            if (user.Id == 777000)
            {
                return Strings.Android.ServiceNotifications;
            }
            else if (user.IsBot)
            {
                if (details)
                {
                    return Strings.Android.Bot;
                }

                return user.IsBotChatHistory ? Strings.Android.BotStatusRead : Strings.Android.BotStatusCantRead;
            }
            else if (user.IsSelf && details)
            {
                return Strings.Android.ChatYourSelf;
            }

            if (user.Status is TLUserStatusOffline offline)
            {
                return FormatDateOnline(offline.WasOnline);
            }
            else if (user.Status is TLUserStatusOnline online)
            {
                if (online.Expires > Utils.CurrentTimestamp / 1000)
                {
                    return Strings.Android.Online;
                }
                else
                {
                    return FormatDateOnline(online.Expires);
                }
            }
            else if (user.Status is TLUserStatusRecently recently)
            {
                return Strings.Android.Lately;
            }
            else if (user.Status is TLUserStatusLastWeek lastWeek)
            {
                return Strings.Android.WithinAWeek;
            }
            else if (user.Status is TLUserStatusLastMonth lastMonth)
            {
                return Strings.Android.WithinAMonth;
            }
            else
            {
                return Strings.Android.ALongTimeAgo;
            }
        }

        private static String FormatDateOnline(long date)
        {
            try
            {
                var rightNow = DateTime.Now;
                int day = rightNow.DayOfYear;
                int year = rightNow.Year;

                var online = Utils.UnixTimestampToDateTime(date);
                int dateDay = online.DayOfYear;
                int dateYear = online.Year;

                if (dateDay == day && year == dateYear)
                {
                    return string.Format("{0} {1} {2}", Strings.Android.LastSeen, Strings.Android.TodayAt, BindConvert.Current.ShortTime.Format(online));
                }
                else if (dateDay + 1 == day && year == dateYear)
                {
                    return string.Format("{0} {1} {2}", Strings.Android.LastSeen, Strings.Android.YesterdayAt, BindConvert.Current.ShortTime.Format(online));
                }
                else if (Math.Abs(Utils.CurrentTimestamp / 1000 - date) < 31536000000L)
                {
                    string format = string.Format(Strings.Android.FormatDateAtTime, online.ToString(Strings.Android.FormatterMonth), BindConvert.Current.ShortTime.Format(online));
                    return string.Format("{0} {1}", Strings.Android.LastSeenDate, format);
                }
                else
                {
                    string format = string.Format(Strings.Android.FormatDateAtTime, online.ToString(Strings.Android.FormatterYear), BindConvert.Current.ShortTime.Format(online));
                    return string.Format("{0} {1}", Strings.Android.LastSeenDate, format);
                }
            }
            catch (Exception e)
            {
                //FileLog.e(e);
            }

            return "LOC_ERR";
        }
    }
}
