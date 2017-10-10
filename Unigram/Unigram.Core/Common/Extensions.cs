using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Devices.Geolocation;

namespace Unigram.Core.Common
{
    public static class Extensions
    {
        public static bool IsEmpty<T>(this ICollection<T> items)
        {
            return items.Count == 0;
        }

        public static void PutRange<TKey, TItem>(this IDictionary<TKey, TItem> list, IDictionary<TKey, TItem> source)
        {
            foreach (var item in source)
            {
                list[item.Key] = item.Value;
            }
        }

        public static string RegexReplace(this string input, string pattern, string replacement)
        {
            return Regex.Replace(input, pattern, replacement);
        }

        public static TLInputGeoPointBase ToInputGeoPoint(this Geoposition position)
        {
            return new TLInputGeoPoint { Lat = position.Coordinate.Point.Position.Latitude, Long = position.Coordinate.Point.Position.Longitude };
        }
    }
}
