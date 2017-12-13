using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Devices.Geolocation;
using Windows.Storage.FileProperties;

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

        public static bool Equals(this string input, params string[] check)
        {
            foreach (var str in check)
            {
                if (input.Equals(str))
                {
                    return true;
                }
            }

            return false;
        }

        public static TLInputGeoPointBase ToInputGeoPoint(this Geoposition position)
        {
            return new TLInputGeoPoint { Lat = position.Coordinate.Point.Position.Latitude, Long = position.Coordinate.Point.Position.Longitude };
        }



        public static uint GetHeight(this ImageProperties props)
        {
            return props.Height;
            return props.Orientation == PhotoOrientation.Rotate180 ? props.Height : props.Width;
        }

        public static uint GetWidth(this ImageProperties props)
        {
            return props.Width;
            return props.Orientation == PhotoOrientation.Rotate180 ? props.Width : props.Height;
        }



        public static uint GetHeight(this VideoProperties props)
        {
            return props.Orientation == VideoOrientation.Rotate180 || props.Orientation == VideoOrientation.Normal ? props.Height : props.Width;
        }

        public static uint GetWidth(this VideoProperties props)
        {
            return props.Orientation == VideoOrientation.Rotate180 || props.Orientation == VideoOrientation.Normal ? props.Width : props.Height;
        }
    }
}
