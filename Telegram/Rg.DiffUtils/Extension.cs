using System.Collections.Generic;

namespace Rg.DiffUtils
{
    public static class Extension
    {
        internal static void Fill<T>(this T[] originalArray, T value)
        {
            for (int i = 0; i < originalArray.Length; i++)
                originalArray[i] = value;
        }

        public static DiffEqualityComparer<T> ToDiffEqualityComparer<T>(this IEqualityComparer<T> comparer)
        {
            return new DiffEqualityComparer<T>(comparer);
        }

        public static DiffHandler<T> ToDiffHandler<T>(this IEqualityComparer<T> comparer, DiffHandler<T>.UpdateItemDelegate updateHandler = null)
        {
            return new DiffHandler<T>(comparer, updateHandler);
        }

        public static DiffHandler<T> ToDiffHandler<T>(this IDiffEqualityComparer<T> comparer, DiffHandler<T>.UpdateItemDelegate updateHandler = null)
        {
            return new DiffHandler<T>(comparer, updateHandler);
        }
    }
}
