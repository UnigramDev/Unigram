//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

#if !NET9_0_OR_GREATER
using System;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// ConditionalWeakTable.AddOrUpdate arrived in .NET Core 3.0. The .NET Native target only
    /// has Add, which throws on an existing key, so call sites would otherwise have to spell out
    /// the remove-then-add themselves and read differently between the two flavours.
    /// </summary>
    public static class ConditionalWeakTableExtensions
    {
        public static void AddOrUpdate<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, TValue value)
            where TKey : class
            where TValue : class
        {
            table.Remove(key);
            table.Add(key, value);
        }

        public static TValue GetOrAdd<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, Func<TKey, TValue> valueFactory)
            where TKey : class
            where TValue : class
        {
            if (table.TryGetValue(key, out TValue value))
            {
                return value;
            }

            value = valueFactory(key);
            table.Add(key, value);

            return value;
        }

        public static TValue GetOrAdd<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, TValue value)
            where TKey : class
            where TValue : class
        {
            if (table.TryGetValue(key, out value))
            {
                return value;
            }

            table.Add(key, value);

            return value;
        }

        public static bool Remove<TKey, TValue>(this ConditionalWeakTable<TKey, TValue> table, TKey key, out TValue value)
            where TKey : class
            where TValue : class
        {
            if (table.TryGetValue(key, out value))
            {
                table.Remove(key);
                return true;
            }

            return false;
        }
    }
}
#endif
