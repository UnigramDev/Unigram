using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Core.Common
{
    public class FunctionalComparer<T> : IComparer<T>
    {
        private readonly Func<T, T, int> comparer;

        public FunctionalComparer(Func<T, T, int> comparer)
        {
            this.comparer = comparer;
        }

        public static IComparer<T> Create(Func<T, T, int> comparer)
        {
            return new FunctionalComparer<T>(comparer);
        }

        public int Compare(T x, T y)
        {
            return comparer(x, y);
        }
    }

    public class FunctionalEqualityComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, T, bool> _equals;
        private readonly Func<T, int> _hash;

        public FunctionalEqualityComparer(Func<T, T, bool> equals, Func<T, int> hash)
        {
            _equals = equals;
            _hash = hash;
        }

        public bool Equals(T x, T y)
        {
            return _equals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return _hash(obj);
        }
    }
}
