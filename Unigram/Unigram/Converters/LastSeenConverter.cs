using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Windows.UI.Xaml.Data;

namespace Unigram.Converters
{
    public class LastSeenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is User user)
            {
                return GetLabel(user, parameter == null);
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }

        public static int GetIndex(User user)
        {
            if (user.Type is UserTypeBot bot)
            {
                // Last
                return bot.CanReadAllGroupMessages ? 1 : 0;
            }

            switch (user.Status)
            {
                case UserStatusOffline offline:
                    return offline.WasOnline;
                case UserStatusOnline online:
                    return int.MaxValue;
                case UserStatusRecently recently:
                    // recently
                    // Before within a week
                    return 5;
                case UserStatusLastWeek lastWeek:
                    // within a week
                    // Before within a month
                    return 4;
                case UserStatusLastMonth lastMonth:
                    // within a month
                    // Before long time ago
                    return 3;
                case UserStatusEmpty empty:
                default:
                    // long time ago
                    // Before bots
                    return 2;
            }
        }

        public static string GetLabel(User user, bool details)
        {
            if (user == null)
            {
                return null;
            }

            if (user.Id == 777000)
            {
                return Strings.Resources.ServiceNotifications;
            }
            else if (user.Type is UserTypeBot bot)
            {
                if (details)
                {
                    return Strings.Resources.Bot;
                }

                return bot.CanReadAllGroupMessages ? Strings.Resources.BotStatusRead : Strings.Resources.BotStatusCantRead;
            }
            //else if (user.IsSelf && details)
            //{
            //    return Strings.Resources.ChatYourSelf;
            //}

            if (user.Status is UserStatusOffline offline)
            {
                return FormatDateOnline(offline.WasOnline);
            }
            else if (user.Status is UserStatusOnline online)
            {
                if (online.Expires > DateTime.Now.ToTimestamp() / 1000)
                {
                    return Strings.Resources.Online;
                }
                else
                {
                    return FormatDateOnline(online.Expires);
                }
            }
            else if (user.Status is UserStatusRecently recently)
            {
                return Strings.Resources.Lately;
            }
            else if (user.Status is UserStatusLastWeek lastWeek)
            {
                return Strings.Resources.WithinAWeek;
            }
            else if (user.Status is UserStatusLastMonth lastMonth)
            {
                return Strings.Resources.WithinAMonth;
            }
            else
            {
                return Strings.Resources.ALongTimeAgo;
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
                    return string.Format("{0} {1} {2}", Strings.Resources.LastSeen, Strings.Resources.TodayAt, BindConvert.Current.ShortTime.Format(online));
                }
                else if (dateDay + 1 == day && year == dateYear)
                {
                    return string.Format("{0} {1} {2}", Strings.Resources.LastSeen, Strings.Resources.YesterdayAt, BindConvert.Current.ShortTime.Format(online));
                }
                else if (Math.Abs(DateTime.Now.ToTimestamp() / 1000 - date) < 31536000000L)
                {
                    string format = string.Format(Strings.Resources.FormatDateAtTime, online.ToString(Strings.Resources.FormatterMonth), BindConvert.Current.ShortTime.Format(online));
                    return string.Format("{0} {1}", Strings.Resources.LastSeenDate, format);
                }
                else
                {
                    string format = string.Format(Strings.Resources.FormatDateAtTime, online.ToString(Strings.Resources.FormatterYear), BindConvert.Current.ShortTime.Format(online));
                    return string.Format("{0} {1}", Strings.Resources.LastSeenDate, format);
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
