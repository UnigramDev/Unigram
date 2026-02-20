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

        public static bool TryGetString(this JsonObject obj, string key, [NotNullWhen(true)] out string? value)
        {
            if (obj.TryGetValue(key, out IJsonValue valueBoxed))
            {
                if (valueBoxed.ValueType == JsonValueType.String)
                {
                    value = valueBoxed.GetString();
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetInt32(this JsonObject obj, string key, [NotNullWhen(true)] out int? value)
        {
            if (obj.TryGetValue(key, out IJsonValue valueBoxed))
            {
                if (valueBoxed.ValueType == JsonValueType.Number)
                {
                    value = (int)valueBoxed.GetNumber();
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetObject(this JsonObject obj, string key, [NotNullWhen(true)] out JsonObject? value)
        {
            if (obj.TryGetValue(key, out IJsonValue valueBoxed))
            {
                if (valueBoxed.ValueType == JsonValueType.Object)
                {
                    value = valueBoxed.GetObject();
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static bool TryGetArray(this JsonObject obj, string key, [NotNullWhen(true)] out JsonArray? value)
        {
            if (obj.TryGetValue(key, out IJsonValue valueBoxed))
            {
                if (valueBoxed.ValueType == JsonValueType.Array)
                {
                    value = valueBoxed.GetArray();
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
