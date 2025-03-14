//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System;
using System.ComponentModel;
using System.Linq;
using Telegram.Common;

namespace Telegram.Collection
{
    // TODO: This is not the best solution ever, but I am lazy
    public partial class BatchedObservableCollection<T> : DiffObservableCollection<T>
    {
        private readonly int _headSize;

        public BatchedObservableCollection(int headSize, IDiffHandler<T> diffHandler, DiffOptions options)
            : base(diffHandler, options)
        {
            _headSize = headSize;
            Head = new DiffObservableCollection<T>(diffHandler, options);
        }

        public DiffObservableCollection<T> Head { get; }

        public int RemainingCount => Count - Head.Count;

        public override void ReplaceDiff(DiffResult<T> diffResult, IDiffHandler<T> diffHandler)
        {
            base.ReplaceDiff(diffResult, diffHandler);
            SynchronizeHead();
        }

        public void SynchronizeHead()
        {
            if (Head.Empty())
            {
                var bufferSize = Math.Max(_headSize, Head.Count);
                Head.AddRange(this.Take(bufferSize));
            }
            else
            {
                var bufferSize = Math.Max(_headSize, Head.Count);
                Head.ReplaceDiff(this.Take(bufferSize));
            }

            OnPropertyChanged(new PropertyChangedEventArgs("RemainingCount"));
        }

        public void Load()
        {
            if (Head.Empty())
            {
                Head.AddRange(this);
            }
            else
            {
                Head.ReplaceDiff(this);
            }

            OnPropertyChanged(new PropertyChangedEventArgs("RemainingCount"));
        }
    }
}
