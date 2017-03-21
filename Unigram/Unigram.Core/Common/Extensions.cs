using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Core.Common
{
    public static class Extensions
    {
        public static void PutRange<TKey, TItem>(this IDictionary<TKey, TItem> list, IDictionary<TKey, TItem> source)
        {
            foreach (var item in source)
            {
                list[item.Key] = item.Value;
            }
        }
    }
}
