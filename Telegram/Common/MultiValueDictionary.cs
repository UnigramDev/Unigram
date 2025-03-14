//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Collections.Generic;

namespace Telegram.Common
{
    public partial class MultiValueDictionary<TKey, TValue> : Dictionary<TKey, IList<TValue>>
    {
        public void Add(TKey key, TValue value)
        {
            if (TryGetValue(key, out var values))
            {
                values.Add(value);
            }
            else
            {
                Add(key, new List<TValue>
                {
                    value
                });
            }
        }

        public void Remove(TKey key, TValue value)
        {
            if (TryGetValue(key, out var values))
            {
                values.Remove(value);

                if (values.Count == 0)
                {
                    Remove(key);
                }
            }
        }
    }
}
