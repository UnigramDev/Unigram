//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Diagnostics.CodeAnalysis;
using Windows.Data.Json;

namespace Telegram.Stub
{
    public static class Extensions
    {
        public static bool TryGet<T>(this IDictionary<string, object> dict, string key, [NotNullWhen(true)] out T? value)
        {
            if (dict.TryGetValue(key, out object? tryGetValue) && tryGetValue is T tryGet)
            {
                value = tryGet;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        public static int GetNamedInt32(this JsonObject obj, string key, int defaultValue)
        {
            return (int)obj.GetNamedNumber(key, defaultValue);
        }
    }
}
