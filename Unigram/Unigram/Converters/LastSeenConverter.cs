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
                        return TLUtils.Now;
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
                return "Service notifications";
            }
            else if (user.IsBot)
            {
                if (details)
                {
                    return "Bot";
                }

                return user.IsBotChatHistory ? "Has access to messages" : "Has no access to messages";
            }
            else if (user.IsSelf && details)
            {
                return "Chat with yourself";
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
                            time = ((now.Date == seen.Date) ? "today at " : (((now.Date - seen.Date) == new TimeSpan(1, 0, 0, 0)) ? "yesterday at " : BindConvert.Current.ShortDate.Format(seen) + " ")) + BindConvert.Current.ShortTime.Format(seen);
                        }
                        else
                        {
                            time = (now.Date == seen.Date) ? ((now - seen).Hours < 1 ? ((now - seen).Minutes < 1 ? "moments ago" : (now - seen).Minutes.ToString() + ((now - seen).Minutes.ToString() == "1" ? " minute ago" : " minutes ago")) : ((now - seen).Hours.ToString()) + (((now - seen).Hours.ToString()) == "1" ? (" hour ago") : (" hours ago"))) : now.Date - seen.Date == new TimeSpan(24, 0, 0) ? "yesterday " + BindConvert.Current.ShortTime.Format(seen) : BindConvert.Current.ShortDate.Format(seen);
                        }

                        return string.Format("Last seen {0}", time);
                    case TLUserStatusOnline online:
                        return "online";
                    case TLUserStatusRecently recently:
                        return "Last seen recently";
                    case TLUserStatusLastWeek lastWeek:
                        return "Last seen within a week";
                    case TLUserStatusLastMonth lastMonth:
                        return "Last seen within a month";
                    case TLUserStatusEmpty empty:
                    default:
                        return "Last seen a long time ago";
                }
            }

            // Debugger.Break();
            return "Last seen a long time ago";
        }
    }
}
