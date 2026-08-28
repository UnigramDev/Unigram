//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// The root namespace rather than Telegram.Td, and deliberately: System.Numerics declares a
// Vector<T> of its own, and an enclosing namespace is searched before any using directive. Every
// file in the app is under Telegram, so this one wins with no using and no ambiguity - which it
// would not from Telegram.Td, where the 27 files that import System.Numerics and name a vector
// would each need CS0104 resolving by hand.
namespace Telegram
{
    /// <summary>
    /// The list every vector in td_api.tl is exposed as. Read-only through this type; see
    /// <see cref="MutableVector{T}"/> for the places that have to build one.
    /// </summary>
    /// <remarks>
    /// The point is not that TDLib objects must be immutable - it is that the parser materialises
    /// tens of thousands of vectors a minute and most of them are empty, so it wants to hand out a
    /// shared <see cref="Empty"/> instead of a List per empty vector. That is only safe if nothing
    /// writes into one, and the app is far too large to establish that by grep or by exercising it:
    /// the compiler has to be the thing that finds the sites. Hence a type with no mutating member
    /// rather than IList&lt;T&gt;, and hence <see cref="MutableVector{T}"/> for the few callers -
    /// building a request argument, mostly - that genuinely need to write.
    ///
    /// Not sealed, so <see cref="Count"/> cannot come from the array length: a mutable subclass
    /// keeps slack. That costs one int field (24 to 32 bytes an instance, parity with List) and one
    /// compare on the indexer, both still ahead of the interface dispatch this replaces.
    /// </remarks>
    [DebuggerDisplay("Count = {Count}")]
    [CollectionBuilder(typeof(Vector), nameof(Vector.Create))]
    // EXPERIMENT: IList<T> and the non-generic IList have moved down to MutableVector<T>, so that
    // passing an immutable vector where a mutable interface is expected stops compiling. Revert the
    // base list and move the two regions back if the WinRT side needs IBindableVector after all.
    public partial class Vector<T> : IReadOnlyList<T>
    {
        /// <summary>The instance every empty vector of this element type shares.</summary>
        public static readonly Vector<T> Empty = new Vector<T>(Array.Empty<T>(), 0);

        // Not readonly, and private protected: MutableVector grows the array and moves the count.
        private protected T[] _items;
        private protected int _count;

        private protected Vector(T[] items, int count)
        {
            _items = items;
            _count = count;
        }

        /// <summary>
        /// Takes a snapshot: a later edit to the source does not reach the vector. The implicit
        /// conversion from an array is the one path that does not copy, so the rule is that a
        /// constructor copies and a conversion wraps.
        /// </summary>
        public Vector(IEnumerable<T> source)
        {
            _items = Snapshot(source);
            _count = _items.Length;
        }

        public Vector(ReadOnlySpan<T> source)
        {
            _items = source.IsEmpty ? Array.Empty<T>() : source.ToArray();
            _count = _items.Length;
        }

        private static T[] Snapshot(IEnumerable<T> source)
        {
            // Already immutable and exactly sized, so its array can be shared rather than copied.
            if (source is Vector<T> vector && vector.GetType() == typeof(Vector<T>) && vector._count == vector._items.Length)
            {
                return vector._items;
            }

            if (source is ICollection<T> collection)
            {
                var count = collection.Count;
                if (count == 0)
                {
                    return Array.Empty<T>();
                }

                var items = new T[count];
                collection.CopyTo(items, 0);
                return items;
            }

            // No count to size an array from, so this pays for a List and then a copy out of it.
            return new List<T>(source).ToArray();
        }

        /// <summary>
        /// Wraps the array rather than copying it, so a caller that keeps its reference can still
        /// write through it. Every call site hands over an array it has just built, and copying
        /// would put an allocation on the construction of every request the app sends.
        /// </summary>
        public static implicit operator Vector<T>(T[] items)
        {
            // null maps to null, not to Empty: that is what the IList<T> this replaces did, and a
            // vector the schema marks as absent is not the same as one that arrived empty.
            if (items == null)
            {
                return null;
            }

            return items.Length == 0 ? Empty : new Vector<T>(items, items.Length);
        }

        public int Count => _count;

        public T this[int index]
        {
            get
            {
                // Against _count rather than the array, which a subclass may have grown past it.
                if ((uint)index >= (uint)_count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _items[index];
            }
        }

        public ReadOnlySpan<T> AsSpan()
        {
            return new ReadOnlySpan<T>(_items, 0, _count);
        }

        public int IndexOf(T item)
        {
            return Array.IndexOf(_items, item, 0, _count);
        }

        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(_items, 0, array, arrayIndex, _count);
        }

        public T[] ToArray()
        {
            if (_count == 0)
            {
                return Array.Empty<T>();
            }

            var items = new T[_count];
            Array.Copy(_items, 0, items, 0, _count);
            return items;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_items, _count);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(_items, _count);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(_items, _count);
        }

        /// <summary>
        /// Takes the array and the count at construction, so a MutableVector edited while it is
        /// being enumerated is read as it was rather than throwing the way List does.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly T[] _items;
            private readonly int _count;
            private int _index;

            internal Enumerator(T[] items, int count)
            {
                _items = items;
                _count = count;
                _index = -1;
            }

            public T Current => _items[_index];

            object IEnumerator.Current => _items[_index];

            public bool MoveNext()
            {
                // Never advances past the end, so a caller that keeps calling cannot overflow.
                if (_index + 1 < _count)
                {
                    _index++;
                    return true;
                }

                return false;
            }

            void IEnumerator.Reset()
            {
                _index = -1;
            }

            public void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// A <see cref="Vector{T}"/> that can be built and edited, for the callers that need to.
    /// </summary>
    /// <remarks>
    /// Assignable wherever a Vector&lt;T&gt; is taken, which is the point: a request argument built
    /// into a reused buffer costs no copy, and <see cref="Client.Send"/> serialises synchronously so
    /// the callee cannot observe a later edit.
    ///
    /// Writing MutableVector&lt;T&gt; in the source is the whole safety story. A Vector&lt;T&gt; has
    /// no mutating member, and casting an immutable one to this throws rather than corrupting it -
    /// so <see cref="Vector{T}.Empty"/> in particular can never be damaged.
    /// </remarks>
    [DebuggerDisplay("Count = {Count}")]
    public sealed class MutableVector<T> : Vector<T>, IList<T>, IList
    {
        public MutableVector()
            : base(Array.Empty<T>(), 0)
        {
        }

        public MutableVector(int capacity)
            : base(capacity == 0 ? Array.Empty<T>() : new T[capacity], 0)
        {
        }

        public MutableVector(IEnumerable<T> source)
            : base(Array.Empty<T>(), 0)
        {
            AddRange(source);
        }

        private MutableVector(T[] items, int count)
            : base(items, count)
        {
        }

        /// <summary>
        /// Wraps the array rather than copying it, as the conversion on <see cref="Vector{T}"/>
        /// does, so that a null vector can be coalesced against one.
        /// </summary>
        /// <remarks>
        /// The base conversion aliases safely because nothing can write through its result.
        /// This one can: <see cref="Set"/>, <see cref="RemoveAt"/> and <see cref="Clear"/>
        /// reach into the caller's array, and <see cref="Add"/> stops doing so only once it
        /// grows, since Array.Resize allocates. So hand it an array nothing else holds - a
        /// literal, or Array.Empty&lt;T&gt;(), which has no element any of those can reach.
        ///
        /// It cannot answer an empty array with a shared instance the way the base does:
        /// <see cref="Vector{T}.Empty"/> is the one thing that must never be written into.
        /// </remarks>
        public static implicit operator MutableVector<T>(T[] items)
        {
            // null maps to null, as it does on the base: a vector the schema marks as absent
            // is not the same as one that arrived empty.
            if (items == null)
            {
                return null;
            }

            return new MutableVector<T>(items, items.Length);
        }

        public int Capacity => _items.Length;

        public void Add(T item)
        {
            if (_count == _items.Length)
            {
                Grow(_count + 1);
            }

            _items[_count++] = item;
        }

        public void AddRange(IEnumerable<T> source)
        {
            if (source is ICollection<T> collection)
            {
                if (_count + collection.Count > _items.Length)
                {
                    Grow(_count + collection.Count);
                }

                collection.CopyTo(_items, _count);
                _count += collection.Count;
                return;
            }

            foreach (var item in source)
            {
                Add(item);
            }
        }

        public void Insert(int index, T item)
        {
            if ((uint)index > (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_count == _items.Length)
            {
                Grow(_count + 1);
            }

            if (index < _count)
            {
                Array.Copy(_items, index, _items, index + 1, _count - index);
            }

            _items[index] = item;
            _count++;
        }

        public bool Remove(T item)
        {
            var index = IndexOf(item);
            if (index < 0)
            {
                return false;
            }

            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _count--;

            if (index < _count)
            {
                Array.Copy(_items, index + 1, _items, index, _count - index);
            }

            // Cleared, or the slack keeps the last element alive for as long as the buffer does.
            _items[_count] = default;
        }

        /// <summary>
        /// Removes every element the predicate matches and returns how many went.
        /// </summary>
        /// <remarks>
        /// One pass, compacting survivors down as it goes, so removing n elements costs one
        /// move each rather than the tail shift <see cref="RemoveAt"/> would pay per element.
        /// </remarks>
        public int RemoveAll(Predicate<T> match)
        {
            var free = 0;
            while (free < _count && !match(_items[free]))
            {
                free++;
            }

            // Nothing matched, so nothing moves and the buffer is left alone.
            if (free == _count)
            {
                return 0;
            }

            var current = free + 1;
            while (current < _count)
            {
                while (current < _count && match(_items[current]))
                {
                    current++;
                }

                if (current < _count)
                {
                    _items[free++] = _items[current++];
                }
            }

            var removed = _count - free;

            // Cleared for the reason RemoveAt clears: the slack would hold on to what was
            // removed for as long as the buffer lives.
            Array.Clear(_items, free, removed);
            _count = free;

            return removed;
        }

        public void Set(int index, T item)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            _items[index] = item;
        }

        /// <summary>Keeps the buffer, so a reused one can be refilled without reallocating.</summary>
        public void Clear()
        {
            if (_count > 0)
            {
                Array.Clear(_items, 0, _count);
                _count = 0;
            }
        }

        /// <summary>
        /// An immutable snapshot, exactly sized. Copies unless the buffer happens to be full.
        /// </summary>
        public Vector<T> ToVector()
        {
            if (_count == 0)
            {
                return Empty;
            }

            return ToArray();
        }

        private void Grow(int capacity)
        {
            var length = _items.Length == 0 ? 4 : _items.Length * 2;
            if (length < capacity)
            {
                length = capacity;
            }

            Array.Resize(ref _items, length);
        }

        #region Interfaces

        // Count, IndexOf, Contains and CopyTo come from the base as public members, so only the
        // parts the base deliberately lacks are needed here: the setter and the mutators.

        bool ICollection<T>.IsReadOnly => false;

        T IList<T>.this[int index]
        {
            get => this[index];
            set => Set(index, value);
        }

        bool IList.IsFixedSize => false;

        bool IList.IsReadOnly => false;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        object IList.this[int index]
        {
            get => this[index];
            set => Set(index, (T)value);
        }

        int IList.IndexOf(object value)
        {
            return Array.IndexOf(_items, value, 0, _count);
        }

        bool IList.Contains(object value)
        {
            return Array.IndexOf(_items, value, 0, _count) >= 0;
        }

        void ICollection.CopyTo(Array array, int index)
        {
            Array.Copy(_items, 0, array, index, _count);
        }

        int IList.Add(object value)
        {
            Add((T)value);
            return _count - 1;
        }

        void IList.Insert(int index, object value) => Insert(index, (T)value);

        void IList.Remove(object value)
        {
            if (value is T item)
            {
                Remove(item);
            }
        }

        #endregion
    }

    public static class Vector
    {
        /// <summary>
        /// Backs the collection expression syntax, through the CollectionBuilder attribute on
        /// Vector&lt;T&gt;. An empty expression never materialises an array at all.
        /// </summary>
        public static Vector<T> Create<T>(ReadOnlySpan<T> values)
        {
            return values.IsEmpty ? Vector<T>.Empty : new Vector<T>(values);
        }

        /// <summary>
        /// The vector with <paramref name="item"/> appended. For editing an object that was just
        /// fetched and is about to be sent back - the shape Add had before, spelled as a rebuild.
        /// </summary>
        public static Vector<T> With<T>(this Vector<T> source, T item)
        {
            if (source == null || source.Count == 0)
            {
                return new[] { item };
            }

            var items = new T[source.Count + 1];
            source.CopyTo(items, 0);
            items[source.Count] = item;

            return items;
        }

        /// <summary>
        /// The vector with <paramref name="item"/> inserted at <paramref name="index"/>, matching
        /// what Insert did.
        /// </summary>
        public static Vector<T> With<T>(this Vector<T> source, int index, T item)
        {
            var count = source?.Count ?? 0;
            if ((uint)index > (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var items = new T[count + 1];
            items[index] = item;

            if (count > 0)
            {
                var span = source.AsSpan();
                span[..index].CopyTo(items);
                span[index..].CopyTo(items.AsSpan(index + 1));
            }

            return items;
        }

        /// <summary>
        /// The vector without the first <paramref name="item"/>, matching what Remove did. Absent,
        /// the vector itself comes back rather than a copy of it.
        /// </summary>
        public static Vector<T> Without<T>(this Vector<T> source, T item)
        {
            if (source == null)
            {
                return null;
            }

            var index = source.IndexOf(item);
            if (index < 0)
            {
                return source;
            }

            if (source.Count == 1)
            {
                return Vector<T>.Empty;
            }

            var items = new T[source.Count - 1];
            var span = source.AsSpan();

            span.Slice(0, index).CopyTo(items);
            span.Slice(index + 1).CopyTo(items.AsSpan(index));

            return items;
        }

        public static Vector<T> ToVector<T>(this IEnumerable<T> source)
        {
            // A MutableVector is a Vector<T>, but handing it back would alias a buffer the caller
            // can still edit, so only a genuinely immutable one is returned as is.
            if (source is Vector<T> vector && vector.GetType() == typeof(Vector<T>))
            {
                return vector;
            }

            // The constructor always allocates, so an empty source is answered before reaching it.
            if (source is ICollection<T> { Count: 0 })
            {
                return Vector<T>.Empty;
            }

            return new Vector<T>(source);
        }

        public static MutableVector<T> ToMutableVector<T>(this IEnumerable<T> source)
        {
            return new MutableVector<T>(source);
        }
    }
}
