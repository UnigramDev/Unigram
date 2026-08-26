//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Telegram.Common;

namespace Telegram.Collections
{
    // TODO: This is not the best solution ever, but I am lazy
    public partial class BatchedObservableCollection<T> : DiffObservableCollection<T>
    {
        private readonly int _headSize;

        public BatchedObservableCollection(int headSize, IDiffHandler<T> diffHandler, bool detectMoves = true)
            : base(diffHandler, detectMoves)
        {
            _headSize = headSize;
            Head = new DiffObservableCollection<T>(diffHandler, detectMoves);
        }

        public DiffObservableCollection<T> Head { get; }

        public int RemainingCount => Count - Head.Count;

        public override void ReplaceDiff(IEnumerable<T> seq, IDiffHandler<T> diffHandler, bool detectMoves)
        {
            base.ReplaceDiff(seq, diffHandler, detectMoves);
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
