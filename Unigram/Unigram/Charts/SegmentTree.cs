using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unigram.Charts
{
    public class SegmentTree
    {

        private Node[] heap;
        private int[] array;
        private int size;

        public SegmentTree(int[] array)
        {
            this.array = array;
            if (array.Length < 30)
            {
                return;
            }
            //The max size of this array is about 2 * 2 ^ log2(n) + 1
            size = (int)(2 * Math.Pow(2.0, Math.Floor((Math.Log((double)array.Length) / Math.Log(2.0)) + 1)));
            heap = new Node[size];
            build(1, 0, array.Length);
        }


        private void build(int v, int from, int size)
        {
            heap[v] = new Node();
            heap[v].from = from;
            heap[v].to = from + size - 1;

            if (size == 1)
            {
                heap[v].sum = array[from];
                heap[v].max = array[from];
                heap[v].min = array[from];
            }
            else
            {
                //Build childs
                build(2 * v, from, size / 2);
                build(2 * v + 1, from + size / 2, size - size / 2);

                heap[v].sum = heap[2 * v].sum + heap[2 * v + 1].sum;
                //max = max of the children
                heap[v].max = Math.Max(heap[2 * v].max, heap[2 * v + 1].max);
                heap[v].min = Math.Min(heap[2 * v].min, heap[2 * v + 1].min);
            }
        }

        public int rMaxQ(int from, int to)
        {
            if (array.Length < 30)
            {
                int max = int.MinValue;
                if (from < 0) from = 0;
                if (to > array.Length - 1) to = array.Length - 1;
                for (int i = from; i <= to; i++)
                {
                    if (array[i] > max) max = array[i];
                }
                return max;
            }
            return rMaxQ(1, from, to);
        }

        private int rMaxQ(int v, int from, int to)
        {
            Node n = heap[v];
            //If you did a range update that contained this node, you can infer the Min value without going down the tree
            if (n.pendingVal != null && contains(n.from, n.to, from, to))
            {
                return n.pendingVal.Value;
            }

            if (contains(from, to, n.from, n.to))
            {
                return heap[v].max;
            }

            if (intersects(from, to, n.from, n.to))
            {
                propagate(v);
                int leftMin = rMaxQ(2 * v, from, to);
                int rightMin = rMaxQ(2 * v + 1, from, to);

                return Math.Max(leftMin, rightMin);
            }

            return 0;
        }

        public int rMinQ(int from, int to)
        {
            if (array.Length < 30)
            {
                int min = int.MaxValue;
                if (from < 0) from = 0;
                if (to > array.Length - 1) to = array.Length - 1;
                for (int i = from; i <= to; i++)
                {
                    if (array[i] < min) min = array[i];
                }
                return min;
            }
            return rMinQ(1, from, to);
        }

        private int rMinQ(int v, int from, int to)
        {
            Node n = heap[v];
            //If you did a range update that contained this node, you can infer the Min value without going down the tree
            if (n.pendingVal != null && contains(n.from, n.to, from, to))
            {
                return n.pendingVal.Value;
            }

            if (contains(from, to, n.from, n.to))
            {
                return heap[v].min;
            }

            if (intersects(from, to, n.from, n.to))
            {
                propagate(v);
                int leftMin = rMinQ(2 * v, from, to);
                int rightMin = rMinQ(2 * v + 1, from, to);

                return Math.Min(leftMin, rightMin);
            }

            return int.MaxValue;
        }

        private void propagate(int v)
        {
            Node n = heap[v];

            if (n.pendingVal != null)
            {
                change(heap[2 * v], n.pendingVal.Value);
                change(heap[2 * v + 1], n.pendingVal.Value);
                n.pendingVal = null;
            }
        }

        private void change(Node n, int value)
        {
            n.pendingVal = value;
            n.sum = n.size() * value;
            n.max = value;
            n.min = value;
            array[n.from] = value;

        }

        private bool contains(int from1, int to1, int from2, int to2)
        {
            return from2 >= from1 && to2 <= to1;
        }

        private bool intersects(int from1, int to1, int from2, int to2)
        {
            return from1 <= from2 && to1 >= from2   //  (.[..)..] or (.[...]..)
                    || from1 >= from2 && from1 <= to2; // [.(..]..) or [..(..)..
        }

        struct Node
        {
            public int sum;
            public int max;
            public int min;

            public int? pendingVal;
            public int from;
            public int to;

            public int size()
            {
                return to - from + 1;
            }
        }
    }
}
