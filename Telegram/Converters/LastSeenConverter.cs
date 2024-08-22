//
// Copyright Fela Ameghino & Contributors 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Td.Api;
using Microsoft.UI.Xaml.Data;

namespace Telegram.Converters
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

        public static string GetLabel(User user, bool details)
        {
            if (user == null)
            {
                return string.Empty;
            }

            if (IsServiceUser(user))
            {
                return Strings.ServiceNotifications;
            }
            else if (IsSupportUser(user))
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
                return FormatDateOnline(offline.WasOnline);
            }
            else if (user.Status is UserStatusOnline online)
            {
                if (online.Expires > DateTime.Now.ToTimestamp() / 1000)
                {
                    return Strings.Online;
                }
                else
                {
                    return FormatDateOnline(online.Expires);
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

        private static string FormatDateOnline(long date)
        {
            try
            {
                var rightNow = DateTime.Now;
                int day = rightNow.DayOfYear;
                int year = rightNow.Year;

                var online = Formatter.ToLocalTime(date);
                int dateDay = online.DayOfYear;
                int dateYear = online.Year;

                if (dateDay == day && year == dateYear)
                {
                    return string.Format(Strings.LastSeenFormatted, string.Format(Strings.TodayAtFormatted, Formatter.ShortTime.Format(online)));
                }
                else if (dateDay + 1 == day && year == dateYear)
                {
                    return string.Format(Strings.LastSeenFormatted, string.Format(Strings.YesterdayAtFormatted, Formatter.ShortTime.Format(online)));
                }
                else if (Math.Abs(DateTime.Now.ToTimestamp() / 1000 - date) < 31536000000L)
                {
                    string format = string.Format(Strings.formatDateAtTime, online.ToString(Strings.formatterMonth), Formatter.ShortTime.Format(online));
                    return string.Format(Strings.LastSeenDateFormatted, format);
                }
                else
                {
                    string format = string.Format(Strings.formatDateAtTime, online.ToString(Strings.formatterYear), Formatter.ShortTime.Format(online));
                    return string.Format(Strings.LastSeenDateFormatted, format);
                }
            }
            catch (Exception)
            {
                //FileLog.e(e);
            }

            return "LOC_ERR";
        }

        public static bool IsServiceUser(User user)
        {
            return user.Id == 777000;
        }

        public static bool IsSupportUser(User user)
        {
            return user != null && (user.IsSupport || user.Id / 1000 == 777 || user.Id == 333000 ||
                    user.Id == 4240000 || user.Id == 4240000 || user.Id == 4244000 ||
                    user.Id == 4245000 || user.Id == 4246000 || user.Id == 410000 ||
                    user.Id == 420000 || user.Id == 431000 || user.Id == 431415000 ||
                    user.Id == 434000 || user.Id == 4243000 || user.Id == 439000 ||
                    user.Id == 449000 || user.Id == 450000 || user.Id == 452000 ||
                    user.Id == 454000 || user.Id == 4254000 || user.Id == 455000 ||
                    user.Id == 460000 || user.Id == 470000 || user.Id == 479000 ||
                    user.Id == 796000 || user.Id == 482000 || user.Id == 490000 ||
                    user.Id == 496000 || user.Id == 497000 || user.Id == 498000 ||
                    user.Id == 4298000);
        }
    }
}
