using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Telegram.Api.TL
{
    public class TLVector : TLObject
    {

    }

    public class TLVectorEmpty
    {
    }

    public class TLVector<T> : TLVector, IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable
    {
        private List<T> _items;

        public TLVector()
        {
            _items = new List<T>();
        }
        public TLVector(TLBinaryReader from, bool fromCache)
        {
            _items = new List<T>();
            Read(from, fromCache);
        }
        public TLVector(IEnumerable<T> source)
        {
            _items = new List<T>(source);
        }
        public TLVector(int capacity)
        {
            _items = new List<T>(capacity);
        }

        public override TLType TypeId { get { return TLType.Vector; } }

        public override void Read(TLBinaryReader from, bool fromCache)
        {
            //var type2 = (TLType)from.ReadUInt32();
            var count = from.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                _items.Add(TLFactory.Read<T>(from, fromCache));
            }
        }

        public override void Write(TLBinaryWriter to, bool toCache)
        {
            to.Write(0x1CB5C415);
            to.Write((uint)_items.Count());

            foreach (var item in _items)
            {
                TLFactory.Write(to, item, toCache);
            }
        }

        #region Enumeration
        public T this[int index]
        {
            get { return _items[index]; }
            set { _items[index] = value; }
        }

        public int Count
        {
            get { return _items.Count; }
        }

        public bool IsReadOnly { get { return false; } }

        public void Add(T item)
        {
            _items.Add(item);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public bool Contains(T item)
        {
            return _items.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _items.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _items.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            _items.Insert(index, item);
        }

        public bool Remove(T item)
        {
            return _items.Remove(item);
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _items.GetEnumerator();
        }
        #endregion
    }
}
