using System.Collections.Generic;

namespace Rg.DiffUtils
{
    class DiagonalComparer : IComparer<Diagonal>
    {
        public int Compare(Diagonal o1, Diagonal o2)
        {
            return o1.X - o2.X;
        }
    }
}
