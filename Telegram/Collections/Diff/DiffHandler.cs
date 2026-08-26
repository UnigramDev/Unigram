//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
// Based on Rg.DiffUtils, Copyright (c) 2021 Kirill Lyubimov. See DiffUtil.cs for the
// MIT notice covering this folder.
//

using System.Collections.Generic;

namespace Telegram.Collections
{
    public interface IDiffEqualityComparer<in T>
    {
        bool CompareItems(T oldItem, T newItem);
    }

    public interface IDiffHandler<T> : IDiffEqualityComparer<T>
    {
        void UpdateItem(T oldItem, T newItem);
    }

    public partial class DiffEqualityComparer<T> : IDiffEqualityComparer<T>
    {
        private readonly ComparerItemsDelegate _comparer;

        public delegate bool ComparerItemsDelegate(T x, T y);

        // Cached: the parameterless collection constructors reach for this, and it never
        // carries state.
        public static DiffEqualityComparer<T> Default { get; } = new(EqualityComparer<T>.Default);

        public DiffEqualityComparer(ComparerItemsDelegate comparer)
        {
            _comparer = comparer;
        }

        public DiffEqualityComparer(IEqualityComparer<T> comparer)
        {
            _comparer = comparer.Equals;
        }

        public virtual bool CompareItems(T oldItem, T newItem)
        {
            return _comparer(oldItem, newItem);
        }
    }

    public partial class DiffHandler<T> : IDiffHandler<T>
    {
        private readonly IDiffEqualityComparer<T> _comparer;
        private readonly UpdateItemDelegate _updateHandler;

        public delegate void UpdateItemDelegate(T oldItem, T newItem);

        public static DiffHandler<T> Default { get; } = new(DiffEqualityComparer<T>.Default);

        public DiffHandler(IDiffEqualityComparer<T> comparer, UpdateItemDelegate updateHandler = null)
        {
            _comparer = comparer;
            _updateHandler = updateHandler;
        }

        public DiffHandler(IEqualityComparer<T> comparer, UpdateItemDelegate updateHandler = null)
        {
            _comparer = new DiffEqualityComparer<T>(comparer);
            _updateHandler = updateHandler;
        }

        public DiffHandler(DiffEqualityComparer<T>.ComparerItemsDelegate comparer, UpdateItemDelegate updateHandler = null)
        {
            _comparer = new DiffEqualityComparer<T>(comparer);
            _updateHandler = updateHandler;
        }

        public virtual bool CompareItems(T oldItem, T newItem)
        {
            return _comparer.CompareItems(oldItem, newItem);
        }

        public virtual void UpdateItem(T oldItem, T newItem)
        {
            _updateHandler?.Invoke(oldItem, newItem);
        }
    }
}
