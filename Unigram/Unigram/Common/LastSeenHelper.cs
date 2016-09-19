using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Globalization.DateTimeFormatting;

namespace Unigram.Common
{
    public  class LastSeenHelper
    {
        public static string GetLastSeen(TLUser User)
        {
            switch (User.Status.TypeId)
            {
                case TLType.UserStatusOffline:
                    {
                        var status = User.Status as TLUserStatusOffline;
                        var seen = TLUtils.ToDateTime(status.WasOnline);
                        var now = DateTime.Now;
                        var time = (now.Date == seen.Date) ? ((now - seen).Hours < 1 ? ((now - seen).Minutes < 1 ? "moments ago" : (now - seen).Minutes.ToString() + ((now - seen).Minutes.ToString() == "1" ? " minute ago" : " minutes ago")) : ((now - seen).Hours.ToString()) + (((now - seen).Hours.ToString()) == "1" ? (" hour ago") : (" hours ago"))) : now.Date - seen.Date == new TimeSpan(24, 0, 0) ? "yesterday " + new DateTimeFormatter("shorttime").Format(seen) : new DateTimeFormatter("shortdate").Format(seen) + " " + new DateTimeFormatter("shorttime").Format(seen);
                        return $"Last seen {time}";

                    }
                case TLType.UserStatusOnline:
                    return "Online";
                case TLType.UserStatusRecently:
                    return "Last seen recently";
                case TLType.UserStatusLastWeek:
                    return "Last seen within a week";
                case TLType.UserStatusLastMonth:
                    return "Last seen within a month";
                case TLType.UserStatusEmpty:
                default:
                    return "Last seen long time ago"; 
            }
        }
    }
}
