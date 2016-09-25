using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Globalization.DateTimeFormatting;

namespace Unigram.Common
{
    public class LastSeenHelper
    {
        public static Tuple<string, DateTime> GetLastSeen(TLUser User)
        {
            if (User.HasStatus && User.Status != null)
            {
                switch (User.Status.TypeId)
                {
                    case TLType.UserStatusOffline:
                        {
                            var status = User.Status as TLUserStatusOffline;
                            var seen = TLUtils.ToDateTime(status.WasOnline);
                            var now = DateTime.Now;
                            var time = (now.Date == seen.Date) ? ((now - seen).Hours < 1 ? ((now - seen).Minutes < 1 ? "moments ago" : (now - seen).Minutes.ToString() + ((now - seen).Minutes.ToString() == "1" ? " minute ago" : " minutes ago")) : ((now - seen).Hours.ToString()) + (((now - seen).Hours.ToString()) == "1" ? (" hour ago") : (" hours ago"))) : now.Date - seen.Date == new TimeSpan(24, 0, 0) ? "yesterday " + new DateTimeFormatter("shorttime").Format(seen) : new DateTimeFormatter("shortdate").Format(seen) + " " + new DateTimeFormatter("shorttime").Format(seen);

                            return Tuple.Create($"Last seen {time}", seen); ;
                        }
                    case TLType.UserStatusOnline:
                        return Tuple.Create("Online", DateTime.Now);
                    case TLType.UserStatusRecently:
                        return Tuple.Create("Last seen recently", DateTime.Now.AddYears(-2));
                    case TLType.UserStatusLastWeek:
                        return Tuple.Create("Last seen within a week", DateTime.Now.AddYears(-3));
                    case TLType.UserStatusLastMonth:
                        return Tuple.Create("Last seen within a month", DateTime.Now.AddYears(-4));
                    case TLType.UserStatusEmpty:
                    default:
                        return Tuple.Create("Last seen long time ago", DateTime.Now.AddYears(-5));
                }
            }

           // Debugger.Break();
            return Tuple.Create("Undefinited", DateTime.Now.AddYears(-30));
        }
    }
}
