using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.Converters;
using Windows.Globalization.DateTimeFormatting;

namespace Unigram.Common
{
    public class LastSeenHelper
    {
        public static Tuple<string, DateTime> GetLastSeen(TLUser user)
        {
            if (user.Id == 777000)
            {
                return Tuple.Create("Service notifications", DateTime.Now);
            }
            if (user.IsBot)
            {
                // TODO: messages access
                return Tuple.Create("Bot", DateTime.Now);
            }
            if (user.IsSelf)
            {
                return Tuple.Create("Chat with yourself", DateTime.Now);
            }

            if (user.HasStatus && user.Status != null)
            {
                switch (user.Status.TypeId)
                {
                    case TLType.UserStatusOffline:
                        {
                            var status = user.Status as TLUserStatusOffline;
                            var seen = TLUtils.ToDateTime(status.WasOnline);
                            var now = DateTime.Now;
                            var time = (now.Date == seen.Date) ? ((now - seen).Hours < 1 ? ((now - seen).Minutes < 1 ? "moments ago" : (now - seen).Minutes.ToString() + ((now - seen).Minutes.ToString() == "1" ? " minute ago" : " minutes ago")) : ((now - seen).Hours.ToString()) + (((now - seen).Hours.ToString()) == "1" ? (" hour ago") : (" hours ago"))) : now.Date - seen.Date == new TimeSpan(24, 0, 0) ? "yesterday " + BindConvert.Current.ShortTime.Format(seen) : BindConvert.Current.ShortDate.Format(seen);

                            return Tuple.Create($"Last seen {time}", seen);
                        }
                    case TLType.UserStatusOnline:
                        return Tuple.Create("online", DateTime.Now);
                    case TLType.UserStatusRecently:
                        return Tuple.Create("Last seen recently", DateTime.Now.AddYears(-2));
                    case TLType.UserStatusLastWeek:
                        return Tuple.Create("Last seen within a week", DateTime.Now.AddYears(-3));
                    case TLType.UserStatusLastMonth:
                        return Tuple.Create("Last seen within a month", DateTime.Now.AddYears(-4));
                    case TLType.UserStatusEmpty:
                    default:
                        return Tuple.Create("Last seen a long time ago", DateTime.Now.AddYears(-5));
                }
            }

            if (user.IsBot)
            {
                // TODO
            }

            // Debugger.Break();
            return Tuple.Create("Last seen a long time ago", DateTime.Now.AddYears(-30));
        }

        public static string GetLastSeenLabel(TLUser user)
        {
            if (user.Id == 777000)
            {
                return "Service notifications";
            }
            if (user.IsBot)
            {
                // TODO: messages access
                return "Bot";
            }
            if (user.IsSelf)
            {
                return "Chat with yourself";
            }

            if (user.HasStatus && user.Status != null)
            {
                switch (user.Status.TypeId)
                {
                    case TLType.UserStatusOffline:
                        {
                            var status = user.Status as TLUserStatusOffline;
                            var seen = TLUtils.ToDateTime(status.WasOnline);
                            var now = DateTime.Now;
                            var time = (now.Date == seen.Date) ? ((now - seen).Hours < 1 ? ((now - seen).Minutes < 1 ? "moments ago" : (now - seen).Minutes.ToString() + ((now - seen).Minutes.ToString() == "1" ? " minute ago" : " minutes ago")) : ((now - seen).Hours.ToString()) + (((now - seen).Hours.ToString()) == "1" ? (" hour ago") : (" hours ago"))) : now.Date - seen.Date == new TimeSpan(24, 0, 0) ? "yesterday " + BindConvert.Current.ShortTime.Format(seen) : BindConvert.Current.ShortDate.Format(seen);

                            return $"Last seen {time}";
                        }
                    case TLType.UserStatusOnline:
                        return "online";
                    case TLType.UserStatusRecently:
                        return "Last seen recently";
                    case TLType.UserStatusLastWeek:
                        return "Last seen within a week";
                    case TLType.UserStatusLastMonth:
                        return "Last seen within a month";
                    case TLType.UserStatusEmpty:
                    default:
                        return "Last seen a long time ago";
                }
            }

            // Debugger.Break();
            return "Last seen a long time ago";
        }

        public static string GetLastSeenTime(TLUser user)
        {
            if (user.Id == 777000)
            {
                return "Service notifications";
            }
            if (user.IsBot)
            {
                // TODO: messages access
                return "Bot";
            }
            if (user.IsSelf)
            {
                return "Chat with yourself";
            }

            if (!user.IsSelf && user.HasStatus && user.Status != null && user.Status.TypeId == TLType.UserStatusOffline)
            {
                var status = user.Status as TLUserStatusOffline;
                var seen = TLUtils.ToDateTime(status.WasOnline);
                var now = DateTime.Now;
                var time = ((now.Date == seen.Date) ? "today at " : (((now.Date - seen.Date) == new TimeSpan(1, 0, 0, 0)) ? "yesterday at " : BindConvert.Current.ShortDate.Format(seen) + " ")) + BindConvert.Current.ShortTime.Format(seen);
                return $"Last seen {time}";
            }
            else
            {
                return GetLastSeenLabel(user);
            }
        }
    }
}
