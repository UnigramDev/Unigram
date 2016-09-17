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
        public static Tuple<string, int> getLastSeen(TLUser User)
        {
            string status="";
            int lastSeenEpoch=0;
            var sOfflineCheck = User.Status as TLUserStatusOffline;
            var sOnlineCheck = User.Status as TLUserStatusOnline;
            var sRecentlyCheck = User.Status as TLUserStatusRecently;
            var sMonthCheck = User.Status as TLUserStatusLastMonth;
            var sWeekCheck = User.Status as TLUserStatusLastWeek;
            if (sOfflineCheck != null)
            {
                var seen = TLUtils.ToDateTime(sOfflineCheck.WasOnline);
                var now = DateTime.Now;
                string t;
                //if date=date, show hours else show full string
                //if hours=hours, show minutes else show "hours ago"
                t = (now.Date == seen.Date) ? ((now - seen).Hours < 1 ? ((now - seen).Minutes < 1 ? "moments ago" : (now - seen).Minutes.ToString() + ((now - seen).Minutes.ToString()=="1"? " minute ago" :" minutes ago")) : ((now - seen).Hours.ToString()) + (((now - seen).Hours.ToString())=="1"? (" hour ago"): (" hours ago"))) : now.Date - seen.Date == new TimeSpan(24, 0, 0) ? "yesterday " + new DateTimeFormatter("shorttime").Format(seen) : new DateTimeFormatter("shortdate").Format(seen) + " " +new DateTimeFormatter("shorttime").Format(seen);
                status = "Last seen " + t;
                lastSeenEpoch = sOfflineCheck.WasOnline;
            }

            if (sOnlineCheck != null)
            {
                status = "Online";
                lastSeenEpoch = int.MaxValue;
            }

            if (sRecentlyCheck != null)
            {
                status = "Last seen recently";
                lastSeenEpoch = 3;
            }
            if (sWeekCheck != null)
            {
                status = "Last seen within a week";
                lastSeenEpoch = 2;
            }
            if (sMonthCheck != null)
            {
                status = "Last seen within a month"; ;
                lastSeenEpoch = 1;
            }
            if (status == "")
            {
                status = "Last seen long time ago";
                lastSeenEpoch = 0;
            }
            return Tuple.Create(status, lastSeenEpoch);
        }
    }
}
