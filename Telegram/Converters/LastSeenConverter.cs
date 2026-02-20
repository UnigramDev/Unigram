//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Td.Api;
using Windows.UI.Xaml.Data;

namespace Telegram.Converters
{
    public partial class LastSeenConverter : IValueConverter
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
                case UserStatusOnline:
                    return int.MaxValue;
                case UserStatusRecently:
                    // recently
                    // Before within a week
                    return 5;
                case UserStatusLastWeek:
                    // within a week
                    // Before within a month
                    return 4;
                case UserStatusLastMonth:
                    // within a month
                    // Before long time ago
                    return 3;
                case UserStatusEmpty:
                default:
                    // long time ago
                    // Before bots
                    return 2;
            }
        }

        public static string GetLabel(User user, bool details, bool relative = false)
        {
            if (user == null)
            {
                return string.Empty;
            }

            if (user.Id == 777000)
            {
                return Strings.ServiceNotifications;
            }
            else if (user.IsSupport)
            {
                return Strings.SupportStatus;
            }
            else if (user.Type is UserTypeBot bot)
            {
                if (details)
                {
                    return bot.ActiveUserCount > 0 ? Locale.Declension(Strings.R.BotDAU, bot.ActiveUserCount) : Strings.Bot;
                }

                return bot.CanReadAllGroupMessages ? Strings.BotStatusRead : Strings.BotStatusCantRead;
            }
            //else if (clientService.IsUserSavedMessages(user))
            //{
            //    return Strings.ChatYourSelf;
            //}
            //else if (user.IsSelf && details)
            //{
            //    return Strings.ChatYourSelf;
            //}

            if (user.Status is UserStatusOffline offline)
            {
                return FormatDateOnline(offline.WasOnline, relative);
            }
            else if (user.Status is UserStatusOnline online)
            {
                if (online.Expires > DateTime.Now.ToTimestamp() / 1000)
                {
                    return Strings.Online;
                }
                else
                {
                    return FormatDateOnline(online.Expires, relative);
                }
            }
            else if (user.Status is UserStatusRecently)
            {
                return Strings.Lately;
            }
            else if (user.Status is UserStatusLastWeek)
            {
                return Strings.WithinAWeek;
            }
            else if (user.Status is UserStatusLastMonth)
            {
                return Strings.WithinAMonth;
            }
            else
            {
                return Strings.ALongTimeAgo;
            }
        }

        private static string FormatDateOnline(long till, bool relative)
        {
            try
            {
                var rightNow = DateTime.Now;
                var now = rightNow.ToTimestamp();

                int day = rightNow.DayOfYear;
                int year = rightNow.Year;

                var online = Formatter.ToLocalTime(till);
                int dateDay = online.DayOfYear;
                int dateYear = online.Year;

                if (relative)
                {
                    var minutes = (now - till) / 60;
                    if (minutes < 1)
                    {
                        return Strings.LastSeenNow;
                    }
                    else if (minutes < 60)
                    {
                        return Locale.Declension(Strings.R.LastSeenMinutes, minutes);
                    }
                }

                if (dateDay == day && year == dateYear)
                {
                    return string.Format(Strings.LastSeenFormatted, string.Format(Strings.TodayAtFormatted, Formatter.Time(online)));
                }
                else if (dateDay + 1 == day && year == dateYear)
                {
                    return string.Format(Strings.LastSeenFormatted, string.Format(Strings.YesterdayAtFormatted, Formatter.Time(online)));
                }
                else if (Math.Abs(DateTime.Now.ToTimestamp() / 1000 - till) < 31536000000L)
                {
                    string format = string.Format(Strings.formatDateAtTime, online.ToString(Strings.formatterMonth), Formatter.Time(online));
                    return string.Format(Strings.LastSeenDateFormatted, format);
                }
                else
                {
                    string format = string.Format(Strings.formatDateAtTime, online.ToString(Strings.formatterYear), Formatter.Time(online));
                    return string.Format(Strings.LastSeenDateFormatted, format);
                }
            }
            catch (Exception)
            {
                //FileLog.e(e);
            }

            return "LOC_ERR";
        }

        public static double OnlinePhraseChange(UserStatus status, DateTime now)
        {
            return Math.Clamp(OnlinePhraseChangeInSeconds(status, now.ToTimestamp()), 0, 86400);
        }

        public static double OnlinePhraseChangeInSeconds(UserStatus status, int now)
        {
            var till = status switch
            {
                UserStatusOnline online => online.Expires,
                UserStatusOffline offline => offline.WasOnline,
                _ => -1
            };

            if (till < 0)
            {
                return till;
            }

            if (till > now)
            {
                return till - now;
            }

            var minutes = (now - till) / 60;
            if (minutes < 60)
            {
                return (minutes + 1) * 60 - (now - till);
            }

            return -1;
        }
    }
}
