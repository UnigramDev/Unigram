using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        public static string RegexReplace(this string input, string pattern, string replacement)
        {
            return Regex.Replace(input, pattern, replacement);
        }
    }
}
