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
    public enum DiffState
    {
        Add,
        Remove,
        Move,

        // The item survived the diff. Emitted inline rather than collected, which is the
        // order Android dispatches it in too: nothing here depends on a position, so it
        // does not matter that a later step still moves the row.
        Unchanged
    }

    /// <summary>
    /// Creates a <see cref="DiffCalculator{T}"/>, inferring the element type from the
    /// sequences: a constructor cannot infer it, and every call site knows it already.
    /// </summary>
    public static class DiffCalculator
    {
        public static DiffCalculator<T> Create<T>(IEnumerable<T> oldSequence, IEnumerable<T> newSequence, IDiffEqualityComparer<T> comparer, bool detectMoves = true)
        {
            return new DiffCalculator<T>(oldSequence, newSequence, comparer, detectMoves);
        }

        public static DiffCalculator<T> Create<T>(IEnumerable<T> oldSequence, IEnumerable<T> newSequence, DiffEqualityComparer<T>.ComparerItemsDelegate comparer, bool detectMoves = true)
        {
            return new DiffCalculator<T>(oldSequence, newSequence, comparer, detectMoves);
        }

        public static DiffCalculator<T> Create<T>(IEnumerable<T> oldSequence, IEnumerable<T> newSequence, bool detectMoves = true)
        {
            return new DiffCalculator<T>(oldSequence, newSequence, detectMoves);
        }
    }

    /// <summary>
    /// Walks the difference between two sequences one change at a time, in the order the
    /// changes have to be applied.
    /// </summary>
    /// <remarks>
    /// A reader rather than a list of changes: upstream materialized every step and every
    /// item as an object, which over a long list cost more than computing the diff. Nothing
    /// here allocates per change.
    /// <para>
    /// Both sequences are copied by the constructor and never read again, so the caller may
    /// mutate them afterwards - but the constructor itself has to run on the thread that
    /// owns them, and the indices it reports are only valid until something else changes
    /// the collection being updated.
    /// </para>
    /// <para>
    /// A ref struct so that it cannot be boxed, stored or captured: it carries the position
    /// of the walk, and a copy would advance on its own and report a diff that never
    /// happened. The cost is that it cannot be declared in an async method.
    /// </para>
    /// </remarks>
    public ref struct DiffCalculator<T>
    {
        private const int FlagMoved = 1;
        private const int FlagOffset = 1;

        private enum Phase
        {
            Removals,
            Additions,
            Unchanged,
            NextDiagonal,
            End
        }

        private readonly T[] _oldSequence;
        private readonly T[] _newSequence;
        private readonly List<Diagonal> _diagonals;
        private readonly int[] _oldItemStatuses;
        private readonly int[] _newItemStatuses;

        private List<PostponedUpdate> _postponed;

        private Phase _phase;
        private int _diagonalIndex;
        private int _posX;
        private int _posY;
        private int _endX;
        private int _endY;
        private int _diagonalX;
        private int _diagonalY;
        private int _unchangedX;
        private int _unchangedY;
        private int _unchangedRemaining;
        private int _currentListSize;

        public DiffState State { get; private set; }

        /// <summary>
        /// Where to apply the change. For a move this is neither sequence's index but the
        /// position the row currently sits at, which is what a list has to be told.
        /// </summary>
        public int OldIndex { get; private set; }

        public int NewIndex { get; private set; }

        /// <summary>
        /// Where the item sits in each sequence, or -1 where it is absent from one.
        /// </summary>
        public int OldSeqIndex { get; private set; }

        public int NewSeqIndex { get; private set; }

        public T OldValue => OldSeqIndex >= 0 ? _oldSequence[OldSeqIndex] : default;

        public T NewValue => NewSeqIndex >= 0 ? _newSequence[NewSeqIndex] : default;

        public DiffCalculator(IEnumerable<T> oldSequence, IEnumerable<T> newSequence, IDiffEqualityComparer<T> comparer, bool detectMoves)
        {
            DiffUtil.CalculateDiagonals(oldSequence, newSequence, comparer, out _oldSequence, out _newSequence, out _diagonals, out _oldItemStatuses, out _newItemStatuses);

            _postponed = null;

            State = default;
            OldIndex = -1;
            NewIndex = -1;
            OldSeqIndex = -1;
            NewSeqIndex = -1;

            _diagonalX = 0;
            _diagonalY = 0;
            _unchangedX = 0;
            _unchangedY = 0;
            _unchangedRemaining = 0;
            _endX = 0;
            _endY = 0;

            _currentListSize = _oldSequence.Length;
            _posX = _oldSequence.Length;
            _posY = _newSequence.Length;

            _diagonalIndex = _diagonals.Count;
            _phase = Phase.NextDiagonal;

            if (detectMoves)
            {
                FindMoveMatches(comparer);
            }
        }

        public DiffCalculator(IEnumerable<T> oldSequence, IEnumerable<T> newSequence, DiffEqualityComparer<T>.ComparerItemsDelegate comparer, bool detectMoves)
            : this(oldSequence, newSequence, new DiffEqualityComparer<T>(comparer), detectMoves)
        {
        }

        public DiffCalculator(IEnumerable<T> oldSequence, IEnumerable<T> newSequence, bool detectMoves)
            : this(oldSequence, newSequence, DiffEqualityComparer<T>.Default, detectMoves)
        {
        }

        public bool Next()
        {
            while (true)
            {
                switch (_phase)
                {
                    case Phase.Removals:
                        if (_posX > _endX)
                        {
                            if (TryRemoval())
                            {
                                return true;
                            }

                            continue;
                        }

                        _phase = Phase.Additions;
                        continue;

                    case Phase.Additions:
                        if (_posY > _endY)
                        {
                            if (TryAddition())
                            {
                                return true;
                            }

                            continue;
                        }

                        _phase = Phase.Unchanged;
                        continue;

                    case Phase.Unchanged:
                        if (_unchangedRemaining > 0)
                        {
                            _unchangedRemaining--;

                            Set(DiffState.Unchanged, -1, -1, _unchangedX, _unchangedY);

                            _unchangedX++;
                            _unchangedY++;
                            return true;
                        }

                        // Done with this diagonal: the walk drops back to where its matched
                        // run starts, which is where the next one down picks up.
                        _posX = _diagonalX;
                        _posY = _diagonalY;

                        _phase = Phase.NextDiagonal;
                        continue;

                    case Phase.NextDiagonal:
                        _diagonalIndex--;

                        if (_diagonalIndex < 0)
                        {
                            _phase = Phase.End;
                            continue;
                        }

                        var diagonal = _diagonals[_diagonalIndex];

                        _endX = diagonal.EndX;
                        _endY = diagonal.EndY;

                        // Both loops below run against the positions carried over from the
                        // diagonal above, and only then does the walk drop back to this one.
                        _diagonalX = diagonal.X;
                        _diagonalY = diagonal.Y;

                        _unchangedX = diagonal.X;
                        _unchangedY = diagonal.Y;
                        _unchangedRemaining = diagonal.Size;

                        _phase = Phase.Removals;
                        continue;

                    default:
                        return false;
                }
            }
        }

        private bool TryRemoval()
        {
            _posX--;

            var status = _oldItemStatuses[_posX];

            if ((status & FlagMoved) != 0)
            {
                var newPos = status >> FlagOffset;

                if (TryTakePostponed(newPos, false, out var postponed))
                {
                    Set(DiffState.Move, _posX, _currentListSize - postponed.CurrentPos - 1, _posX, newPos);
                    return true;
                }

                // The other half of the move has not come up yet, so the row stays where it
                // is until it does.
                Postpone(new PostponedUpdate(_posX, _currentListSize - _posX - 1, true));
                return false;
            }

            Set(DiffState.Remove, _posX, -1, _posX, -1);

            _currentListSize--;
            return true;
        }

        private bool TryAddition()
        {
            _posY--;

            var status = _newItemStatuses[_posY];

            if ((status & FlagMoved) != 0)
            {
                var oldPos = status >> FlagOffset;

                if (TryTakePostponed(oldPos, true, out var postponed))
                {
                    Set(DiffState.Move, _currentListSize - postponed.CurrentPos - 1, _posX, oldPos, _posY);
                    return true;
                }

                Postpone(new PostponedUpdate(_posY, _currentListSize - _posX, false));
                return false;
            }

            Set(DiffState.Add, -1, _posX, -1, _posY);

            _currentListSize++;
            return true;
        }

        private void Set(DiffState state, int oldIndex, int newIndex, int oldSeqIndex, int newSeqIndex)
        {
            State = state;
            OldIndex = oldIndex;
            NewIndex = newIndex;
            OldSeqIndex = oldSeqIndex;
            NewSeqIndex = newSeqIndex;
        }

        private void Postpone(PostponedUpdate update)
        {
            // Only ever touched when the two sequences share an item that moved, which most
            // diffs of a search result never do.
            _postponed ??= new List<PostponedUpdate>();
            _postponed.Add(update);
        }

        // Upstream copied the whole pending list on every moved item, only so that it could
        // keep iterating past a RemoveAt - which made move detection allocate O(moves^2).
        // Android indexes in place, and so does this.
        private bool TryTakePostponed(int posInList, bool removal, out PostponedUpdate result)
        {
            if (_postponed != null)
            {
                for (int index = 0; index < _postponed.Count; index++)
                {
                    var update = _postponed[index];

                    if (update.PosInOwnerList != posInList || update.Removal != removal)
                    {
                        continue;
                    }

                    _postponed.RemoveAt(index);

                    for (int i = index; i < _postponed.Count; i++)
                    {
                        var other = _postponed[i];
                        other.CurrentPos += removal ? -1 : 1;
                        _postponed[i] = other;
                    }

                    result = update;
                    return true;
                }
            }

            result = default;
            return false;
        }

        private void FindMoveMatches(IDiffEqualityComparer<T> comparer)
        {
            var posX = 0;

            foreach (var diagonal in _diagonals)
            {
                while (posX < diagonal.X)
                {
                    if (_oldItemStatuses[posX] == 0)
                    {
                        FindMatchingAddition(comparer, posX);
                    }

                    posX++;
                }

                posX = diagonal.EndX;
            }
        }

        private void FindMatchingAddition(IDiffEqualityComparer<T> comparer, int posX)
        {
            var posY = 0;

            for (int i = 0; i < _diagonals.Count; i++)
            {
                var diagonal = _diagonals[i];

                while (posY < diagonal.Y)
                {
                    if (_newItemStatuses[posY] == 0
                        && comparer.CompareItems(_oldSequence[posX], _newSequence[posY]))
                    {
                        _oldItemStatuses[posX] = (posY << FlagOffset) | FlagMoved;
                        _newItemStatuses[posY] = (posX << FlagOffset) | FlagMoved;

                        return;
                    }

                    posY++;
                }

                posY = diagonal.EndY;
            }
        }

        private struct PostponedUpdate
        {
            public int PosInOwnerList { get; }

            public int CurrentPos { get; set; }

            public bool Removal { get; }

            public PostponedUpdate(int posInOwnerList, int currentPos, bool removal)
            {
                PosInOwnerList = posInOwnerList;
                CurrentPos = currentPos;
                Removal = removal;
            }
        }
    }
}
