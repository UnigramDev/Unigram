//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public readonly struct IncrementalLoadResult
    {
        public IncrementalLoadResult(uint count, bool hasMoreItems)
        {
            Count = count;
            HasMoreItems = hasMoreItems;
        }

        // Only a manual ListViewBase.LoadMoreItemsAsync caller ever reads this back: the automatic
        // edge trigger decides on HasMoreItems and the collection's own size alone. It is still how
        // the collection tells a load that made progress from one that did nothing.
        public uint Count { get; }

        public bool HasMoreItems { get; }
    }

    public interface IIncrementalCollectionOwner
    {
        Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count);
    }

    public partial class IncrementalCollection<T> : RangeObservableCollection<T>, IIncrementalCollection<T>, IIncrementalCollection, ICollectionWithTotalCount
    {
        // A load that adds nothing while still reporting more is an unbounded loop: the list asks
        // again the moment it completes, and forever, because the trigger is "the viewport is not
        // full". A server page filtered away on our side is legitimate, so allow a few in a row
        // before refusing to believe the owner.
        private const int EmptyLoadLimit = 3;

        private readonly IIncrementalCollectionOwner _owner;

        // The load in flight, if any. A second caller gets this task rather than a second load:
        // Restart re-arms HasMoreItems, so a manual reload and the arrange-triggered load that the
        // emptied list provokes would otherwise run against each other.
        private Task<LoadMoreItemsResult> _loading;

        // Bumped whenever the list stops paging what it was paging - Restart, or HasMoreItems turned
        // off. Work in flight compares it after its await: a load belonging to what came before must
        // not write HasMoreItems back over what replaced it.
        private int _version;

        private int _emptyLoads;
        private bool _hasMoreItems = true;

        public IncrementalCollection(IIncrementalCollectionOwner owner)
        {
            _owner = owner;
        }

        // For a collection that is its own loader, and so must override OnLoadMoreItemsAsync: the
        // base implementation has no owner to forward to.
        protected IncrementalCollection()
        {
        }

        protected IncrementalCollection(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return IncrementalLoading.Run(_ => LoadMoreItems(count));
        }

        private Task<LoadMoreItemsResult> LoadMoreItems(uint count)
        {
            if (_loading != null)
            {
                return _loading;
            }

            var loading = LoadMoreItemsImpl(count);

            // A load that completed synchronously has already run its finally, so storing it now
            // would leave every later caller waiting on a task that is never cleared.
            if (!loading.IsCompleted)
            {
                _loading = loading;
            }

            return loading;
        }

        private async Task<LoadMoreItemsResult> LoadMoreItemsImpl(uint count)
        {
            // Started first, and the version read after, so that an owner restarting as the opening
            // act of its own reload does not invalidate the load that restart belongs to.
            var loading = OnLoadMoreItemsAsync(count);
            var version = _version;

            try
            {
                var result = await loading;

                // Restarted while this was in flight: the list it loaded no longer exists, and
                // the reload that replaced it owns HasMoreItems now.
                if (version != _version)
                {
                    return default;
                }

                _hasMoreItems = result.HasMoreItems;

                if (result.Count > 0)
                {
                    _emptyLoads = 0;
                }
                else if (result.HasMoreItems && ++_emptyLoads >= EmptyLoadLimit)
                {
                    _hasMoreItems = false;
                    Logger.Error((_owner as object ?? this).GetType().Name + " reports more items but never adds any");
                }

                return new LoadMoreItemsResult { Count = result.Count };
            }
            catch (Exception ex)
            {
                // A load that throws must not leave the list asking, or it asks forever and never
                // gets anything.
                if (version == _version)
                {
                    _hasMoreItems = false;
                }

                Logger.Error(ex);
                return default;
            }
            finally
            {
                if (version == _version)
                {
                    _loading = null;
                }
            }
        }

        protected virtual Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            return _owner.LoadMoreItemsAsync(count);
        }

        // Settable because some lists fetch their first page outside LoadMoreItemsAsync - the search
        // cascade does - and the collection has no other way to learn that there is now a page to
        // continue from, or that a new query left nothing to continue from.
        public bool HasMoreItems
        {
            get => _hasMoreItems;
            set
            {
                if (value)
                {
                    // Arming can happen from inside a load - the cascade sets this as it pages - so
                    // it must not invalidate the load it is being set from.
                    _hasMoreItems = true;
                    _emptyLoads = 0;
                }
                else
                {
                    // Turning it off is a restart without the items: whatever the list was paging
                    // through is done with, so a load still in flight for it must not report back.
                    Invalidate(false);
                }
            }
        }

        // Emptying the list is not the same event as starting it over: ClearItems is also reached
        // through ReplaceWith and Clear(bool) on the base, which are content refreshes. Restart is
        // the one that means "begin again", so all of the reset state lives there.
        public virtual void Restart()
        {
            Invalidate(true);
            Clear();
        }

        // Drops the load in flight and the state it was accumulating, so that nothing it returns
        // afterwards is applied, and the next caller is not handed its task.
        private void Invalidate(bool hasMoreItems)
        {
            _version++;
            _loading = null;
            _emptyLoads = 0;
            _hasMoreItems = hasMoreItems;
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (_totalCount != value)
                {
                    _totalCount = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(TotalCount)));
                }
            }
        }
    }
}
