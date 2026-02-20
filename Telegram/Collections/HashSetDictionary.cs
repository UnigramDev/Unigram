//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;

namespace Telegram.Collections
{
    public partial class HashSetDictionary<TKey, TValue> : Dictionary<TKey, ISet<TValue>>
    {
        public bool Add(TKey key, TValue value)
        {
            if (TryGetValue(key, out var values))
            {
                return values.Add(value);
            }
            else
            {
                Add(key, new HashSet<TValue>
                {
                    value
                });

                return true;
            }
        }

        public bool Contains(TKey key, TValue value)
        {
            if (TryGetValue(key, out var values))
            {
                return values.Contains(value);
            }

            return false;
        }

        public bool Remove(TKey key, TValue value)
        {
            if (TryGetValue(key, out var values))
            {
                var removed = values.Remove(value);

                if (values.Count == 0)
                {
                    Remove(key);
                }

                return removed;
            }

            return false;
        }
    }
}
