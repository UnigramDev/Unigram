using System;
using System.Collections.Generic;
using System.Linq;

namespace Rg.DiffUtils
{
    class BatchingStepsWrapper<T>
    {
        readonly DiffOptions _options;
        readonly List<DiffStep<T>> _steps;

        public IReadOnlyList<DiffStep<T>> Steps => _steps;

        public BatchingStepsWrapper(DiffOptions options)
        {
            _options = options;
            _steps = new List<DiffStep<T>>();
        }

        public void Insert(int index, DiffItem<T> item)
        {
            var lastStep = _steps.LastOrDefault();

            if(_options.AllowBatching && lastStep != null
                && lastStep.Status == DiffStatus.Add
                && index >= lastStep.NewStartIndex
                && index <= lastStep.NewStartIndex + lastStep.Items.Count)
            {
                lastStep.InsertItem(item);
                lastStep.NewStartIndex = Math.Min(index, lastStep.NewStartIndex);

                return;
            }

            _steps.Add(new DiffStep<T>(item)
            {
                Status = DiffStatus.Add,
                NewStartIndex = index
            });
        }

        public void Move(int fromPosition, int toPosition, DiffItem<T> item)
        {
            _steps.Add(new DiffStep<T>(item)
            {
                Status = DiffStatus.Move,
                OldStartIndex = fromPosition,
                NewStartIndex = toPosition
            });
        }

        public void Remove(int position, DiffItem<T> item)
        {
            var lastStep = _steps.LastOrDefault();

            if (_options.AllowBatching && lastStep != null
                && lastStep.Status == DiffStatus.Remove
                && lastStep.OldStartIndex >= position
                && lastStep.OldStartIndex <= position + 1)
            {
                lastStep.InsertItem(item);
                lastStep.OldStartIndex = position;

                return;
            }

            _steps.Add(new DiffStep<T>(item)
            {
                Status = DiffStatus.Remove,
                OldStartIndex = position
            });
        }
    }
}
