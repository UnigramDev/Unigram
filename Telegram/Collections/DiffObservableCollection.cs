//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
// Based on Rg.DiffUtils, Copyright (c) 2021 Kirill Lyubimov. See Diff/DiffUtil.cs for the
// MIT notice covering it. The range operations it builds on are in RangeObservableCollection.
//

using System.Collections.Generic;

namespace Telegram.Collections
{
    public partial class DiffObservableCollection<T> : RangeObservableCollection<T>
    {
        public IDiffHandler<T> DefaultDiffHandler { get; }

        public bool DetectMoves { get; }

        public DiffObservableCollection()
            : this(DiffHandler<T>.Default, true)
        {
        }

        public DiffObservableCollection(IDiffHandler<T> diffHandler)
            : this(diffHandler, true)
        {
        }

        public DiffObservableCollection(IDiffHandler<T> diffHandler, bool detectMoves)
        {
            DefaultDiffHandler = diffHandler;
            DetectMoves = detectMoves;
        }

        public DiffObservableCollection(IEnumerable<T> collection)
            : this(collection, DiffHandler<T>.Default, true)
        {
        }

        public DiffObservableCollection(IEnumerable<T> collection, IDiffHandler<T> diffHandler)
            : this(collection, diffHandler, true)
        {
        }

        public DiffObservableCollection(IEnumerable<T> collection, IDiffHandler<T> diffHandler, bool detectMoves)
            : base(collection)
        {
            DefaultDiffHandler = diffHandler;
            DetectMoves = detectMoves;
        }

        public DiffObservableCollection(List<T> list)
            : this(list, DiffHandler<T>.Default, true)
        {
        }

        public DiffObservableCollection(List<T> list, IDiffHandler<T> diffHandler)
            : this(list, diffHandler, true)
        {
        }

        public DiffObservableCollection(List<T> list, IDiffHandler<T> diffHandler, bool detectMoves)
            : base(list)
        {
            DefaultDiffHandler = diffHandler;
            DetectMoves = detectMoves;
        }

        public void ReplaceDiff(IEnumerable<T> seq)
        {
            ReplaceDiff(seq, DefaultDiffHandler, DetectMoves);
        }

        public void ReplaceDiff(IEnumerable<T> seq, IDiffHandler<T> diffHandler)
        {
            ReplaceDiff(seq, diffHandler, DetectMoves);
        }

        public void ReplaceDiff(IEnumerable<T> seq, bool detectMoves)
        {
            ReplaceDiff(seq, DefaultDiffHandler, detectMoves);
        }

        public virtual void ReplaceDiff(IEnumerable<T> seq, IDiffHandler<T> diffHandler, bool detectMoves)
        {
            // Computed and applied in one pass, so there is no window in which the indices
            // the walk reports can go stale against this collection.
            var diff = new DiffCalculator<T>(this, seq, diffHandler, detectMoves);

            while (diff.Next())
            {
                switch (diff.State)
                {
                    case DiffState.Add:
                        Insert(diff.NewIndex, diff.NewValue);
                        break;
                    case DiffState.Remove:
                        RemoveAt(diff.OldIndex);
                        break;
                    case DiffState.Move:
                        UpdateItem(diff.OldValue, diff.NewValue, diff.NewSeqIndex, diffHandler);
                        Move(diff.OldIndex, diff.NewIndex);
                        break;
                    case DiffState.Unchanged:
                        UpdateItem(diff.OldValue, diff.NewValue, diff.NewSeqIndex, diffHandler);
                        break;
                }
            }
        }

        // Overridden where an item that survived the diff has to be reconciled with the one
        // that replaced it.
        protected virtual void UpdateItem(T oldValue, T newValue, int newSeqIndex, IDiffHandler<T> diffHandler)
        {
            diffHandler.UpdateItem(oldValue, newValue);
        }
    }
}
