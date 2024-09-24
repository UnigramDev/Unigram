using System.Collections.Generic;
using System.Linq;

namespace Rg.DiffUtils
{
    public partial class DiffResult<T>
    {
        const int FLAG_MOVED = 1;
        const int FLAG_OFFSET = 1;

        readonly T[] _array1;
        readonly T[] _array2;
        readonly List<Diagonal> _diagonals;
        readonly int[] _oldItemStatuses;
        readonly int[] _newItemStatuses;
        readonly IDiffEqualityComparer<T> _comparer;

        readonly List<DiffItem<T>> _sameItems;
        readonly List<DiffItem<T>> _movedItems;
        readonly List<DiffItem<T>> _notMovedItems;
        readonly List<DiffItem<T>> _removedItems;
        readonly List<DiffItem<T>> _addedItems;

        public T[] OldSequence => _array1;
        public T[] NewSequence => _array2;

        public IReadOnlyList<DiffItem<T>> SameItems => _sameItems;
        public IReadOnlyList<DiffItem<T>> MovedItems => _movedItems;
        public IReadOnlyList<DiffItem<T>> NotMovedItems => _notMovedItems;
        public IReadOnlyList<DiffItem<T>> RemovedItems => _removedItems;
        public IReadOnlyList<DiffItem<T>> AddedItems => _addedItems;

        public IReadOnlyList<DiffStep<T>> Steps { get; private set; }

        public DiffOptions Options { get; }

        internal DiffResult(
            T[] array1, T[] array2,
            List<Diagonal> diagonals,
            int[] oldItemStatuses, int[] newItemStatuses,
            IDiffEqualityComparer<T> comparer, DiffOptions options)
        {
            oldItemStatuses.Fill(0);
            newItemStatuses.Fill(0);

            _array1 = array1;
            _array2 = array2;
            _diagonals = diagonals;
            _oldItemStatuses = oldItemStatuses;
            _newItemStatuses = newItemStatuses;
            Options = options;
            _comparer = comparer;

            _sameItems = new List<DiffItem<T>>();
            _movedItems = new List<DiffItem<T>>();
            _notMovedItems = new List<DiffItem<T>>();
            _removedItems = new List<DiffItem<T>>();
            _addedItems = new List<DiffItem<T>>();

            AddEdgeDiagonals();
            FindMatchingItems();

            Calculate();
        }

        void AddEdgeDiagonals()
        {
            var first = _diagonals.FirstOrDefault();

            if (first == null || first.X != 0 || first.Y != 0)
                _diagonals.Insert(0, new Diagonal(0, 0, 0));

            _diagonals.Add(new Diagonal(_array1.Length, _array2.Length, 0));
        }

        void FindMatchingItems()
        {
            if (Options.DetectMoves)
                FindMoveMatches();
        }

        void FindMoveMatches()
        {
            var posX = 0;

            foreach (var diagonal in _diagonals)
            {
                while (posX < diagonal.X)
                {
                    if (_oldItemStatuses[posX] == 0)
                        FindMatchingAddition(posX);

                    posX++;
                }

                posX = diagonal.EndX;
            }
        }

        void FindMatchingAddition(int posX)
        {
            var posY = 0;
            var diagonalsSize = _diagonals.Count;

            for (int i = 0; i < diagonalsSize; i++)
            {
                var diagonal = _diagonals[i];

                while (posY < diagonal.Y)
                {
                    if (_newItemStatuses[posY] == 0)
                    {
                        var matching = _comparer.CompareItems(_array1[posX], _array2[posY]);

                        if (matching)
                        {
                            _oldItemStatuses[posX] = (posY << FLAG_OFFSET) | FLAG_MOVED;
                            _newItemStatuses[posY] = (posX << FLAG_OFFSET) | FLAG_MOVED;

                            return;
                        }
                    }

                    posY++;
                }

                posY = diagonal.EndY;
            }
        }

        void Calculate()
        {
            var wrapper = new BatchingStepsWrapper<T>(Options);

            var currentListSize = _array1.Length;

            var postponedUpdates = new List<DiffPostponedUpdate>();

            int posX = _array1.Length;
            int posY = _array2.Length;

            for (int diagonalIndex = _diagonals.Count - 1; diagonalIndex >= 0; diagonalIndex--)
            {
                var diagonal = _diagonals[diagonalIndex];
                int endX = diagonal.EndX;
                int endY = diagonal.EndY;

                while (posX > endX)
                {
                    posX--;

                    var status = _oldItemStatuses[posX];

                    if ((status & FLAG_MOVED) != 0)
                    {
                        var newPos = status >> FLAG_OFFSET;

                        var postponedUpdate = GetPostponedUpdate(postponedUpdates, newPos, false);

                        if (postponedUpdate != null)
                        {
                            var updatedNewPos = currentListSize - postponedUpdate.CurrentPos - 1;
                            var diffItem = GetDiffItem(posX, newPos);

                            _sameItems.Add(diffItem);
                            _movedItems.Add(diffItem);
                            wrapper.Move(posX, updatedNewPos, diffItem);
                        }
                        else
                        {
                            postponedUpdates.Add(
                                new DiffPostponedUpdate(
                                    posX,
                                    currentListSize - posX - 1,
                                    true
                            ));
                        }
                    }
                    else
                    {
                        var diffItem = GetDiffItem(posX, -1);

                        _removedItems.Add(diffItem);
                        wrapper.Remove(posX, diffItem);

                        currentListSize--;
                    }
                }
                while (posY > endY)
                {
                    posY--;

                    var status = _newItemStatuses[posY];

                    if ((status & FLAG_MOVED) != 0)
                    {
                        var oldPos = status >> FLAG_OFFSET;
                        var postponedUpdate = GetPostponedUpdate(postponedUpdates, oldPos, true);

                        if (postponedUpdate == null)
                        {
                            postponedUpdates.Add(
                                new DiffPostponedUpdate(
                                    posY,
                                    currentListSize - posX,
                                    false
                            ));
                        }
                        else
                        {
                            var updatedOldPos = currentListSize - postponedUpdate.CurrentPos - 1;
                            var diffItem = GetDiffItem(oldPos, posY);

                            _sameItems.Add(diffItem);
                            _movedItems.Add(diffItem);
                            wrapper.Move(updatedOldPos, posX, diffItem);
                        }
                    }
                    else
                    {
                        var diffItem = GetDiffItem(-1, posY);

                        _addedItems.Add(diffItem);
                        wrapper.Insert(posX, diffItem);

                        currentListSize++;
                    }
                }

                posX = diagonal.X;
                posY = diagonal.Y;

                for (int i = 0; i < diagonal.Size; i++)
                {
                    var diffItem = GetDiffItem(posX, posY);

                    _sameItems.Add(diffItem);
                    _notMovedItems.Add(diffItem);

                    posX++;
                    posY++;
                }

                posX = diagonal.X;
                posY = diagonal.Y;
            }

            Steps = wrapper.Steps;
        }

        DiffPostponedUpdate GetPostponedUpdate(
            List<DiffPostponedUpdate> postponedUpdates,
            int posInList,
            bool removal)
        {
            DiffPostponedUpdate postponedUpdate = null;
            var enumerator = postponedUpdates.ToList().GetEnumerator();

            var index = 0;
            while (enumerator.MoveNext())
            {
                var update = enumerator.Current;

                if (update.PosInOwnerList == posInList
                    && update.Removal == removal)
                {
                    postponedUpdate = update;

                    postponedUpdates.RemoveAt(index);
                    break;
                }

                index++;
            }

            while (enumerator.MoveNext())
            {
                var update = enumerator.Current;

                if (removal)
                    update.CurrentPos--;
                else
                    update.CurrentPos++;

                index++;
            }

            return postponedUpdate;
        }

        DiffItem<T> GetDiffItem(int oldSeqIndex, int newSeqIndex)
        {
            T oldValue = default;
            T newValue = default;

            if (oldSeqIndex >= 0)
                oldValue = _array1[oldSeqIndex];

            if (newSeqIndex >= 0)
                newValue = _array2[newSeqIndex];

            return new DiffItem<T>(oldSeqIndex, newSeqIndex, oldValue, newValue);
        }
    }
}
