//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;

namespace Telegram.Stub
{
    public static class Extensions
    {
        public static bool TryGet<T>(this IDictionary<string, object> dict, string key, out T value)
        {
            if (dict.TryGetValue(key, out object tryGetValue) && tryGetValue is T tryGet)
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
    }
}
