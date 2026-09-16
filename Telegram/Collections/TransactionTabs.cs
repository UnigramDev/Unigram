//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;

namespace Telegram.Collections
{
    public readonly struct TransactionPage<T>
    {
        public TransactionPage(IReadOnlyList<T> items, string nextOffset)
        {
            Items = items;
            NextOffset = nextOffset ?? string.Empty;
        }

        public IReadOnlyList<T> Items { get; }

        public string NextOffset { get; }
    }

    /// <summary>
    /// The All / Incoming / Outgoing tabs of a transaction list, each paging on a cursor of its
    /// own. Switching tabs swaps which collection the list binds to, so nothing already loaded is
    /// thrown away and nothing is fetched twice.
    /// </summary>
    public partial class TransactionTabs<T> where T : class
    {
        public delegate Task<TransactionPage<T>> PageLoader(TransactionDirection direction, string offset, int limit);

        private const int PageSize = 20;

        private readonly PageLoader _load;
        private readonly Func<T, string> _getId;
        private readonly Func<T, bool> _isIncoming;
        private readonly Func<uint, Task<IncrementalLoadResult?>> _prologue;
        private readonly Action _changed;

        private readonly Tab[] _tabs;

        private bool _seeded;

        /// <param name="prologue">
        /// Run by the All tab before its own request, for a list that has something else to drain
        /// through the same collection. Returning a result short-circuits the load.
        /// </param>
        public TransactionTabs(PageLoader load, Func<T, string> getId, Func<T, bool> isIncoming, Action changed, Func<uint, Task<IncrementalLoadResult?>> prologue = null)
        {
            _load = load;
            _getId = getId;
            _isIncoming = isIncoming;
            _changed = changed;
            _prologue = prologue;

            _tabs = new[]
            {
                new Tab(this, null),
                new Tab(this, new TransactionDirectionIncoming()),
                new Tab(this, new TransactionDirectionOutgoing())
            };

            Items = new IncrementalCollectionView<T, IncrementalCollection<T>>(_tabs[0].Items);
        }

        /// <summary>
        /// One instance for the life of the view model. The list binds to this and the tabs swap
        /// underneath it, so the ListView is never handed a different ItemsSource: the view patches
        /// itself item by item and the containers are recycled in place rather than rebuilt.
        /// </summary>
        public IncrementalCollectionView<T, IncrementalCollection<T>> Items { get; }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    Items.SetSource(_tabs[value].Items);
                }
            }
        }

        // Latched on purpose. The tab strip is bound to this, and binding it to the selected
        // collection's count instead is what made the strip disappear and come back on a switch.
        public bool HasTransactions { get; private set; }

        // Only ever from the All tab's first page. A later page starts further down the stream, so
        // what it holds is no longer a prefix of anything.
        private void Seed(IReadOnlyList<T> page)
        {
            if (_seeded)
            {
                return;
            }

            _seeded = true;

            var incoming = new List<T>();
            var outgoing = new List<T>();

            for (int i = 0; i < page.Count; i++)
            {
                (_isIncoming(page[i]) ? incoming : outgoing).Add(page[i]);
            }

            _tabs[1].Seed(incoming);
            _tabs[2].Seed(outgoing);
        }

        private partial class Tab : IIncrementalCollectionOwner
        {
            private readonly TransactionTabs<T> _owner;
            private readonly TransactionDirection _direction;

            private string _nextOffset = string.Empty;

            // Rows lifted out of the All tab so this one has something to show the moment it is
            // selected. Direction is an inbound/outbound flag on the same server query, so the
            // split is a true prefix of what this tab would fetch - but the cursor that would
            // follow it belongs to the query that produced it and cannot be carried across, so
            // the first real load still starts from the beginning and merges onto the seed.
            private bool _isSeed;

            // Set before the first await, not after: a tab that has begun loading must never be
            // seeded underneath the request in flight.
            private bool _loaded;

            public Tab(TransactionTabs<T> owner, TransactionDirection direction)
            {
                _owner = owner;
                _direction = direction;

                Items = new IncrementalCollection<T>(this);
            }

            public IncrementalCollection<T> Items { get; }

            public void Seed(IReadOnlyList<T> items)
            {
                if (_loaded || Items.Count > 0 || items.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < items.Count; i++)
                {
                    Items.Add(items[i]);
                }

                _isSeed = true;
            }

            public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
            {
                if (_direction == null && _owner._prologue != null)
                {
                    var handled = await _owner._prologue(count);
                    if (handled.HasValue)
                    {
                        return handled.Value;
                    }
                }

                _loaded = true;

                var page = await _owner._load(_direction, _nextOffset, PageSize);
                if (page.Items == null)
                {
                    return new IncrementalLoadResult(0, false);
                }

                var added = _isSeed
                    ? Merge(page.Items)
                    : Append(page.Items);

                _isSeed = false;
                _nextOffset = page.NextOffset;

                if (Items.Count > 0 && !_owner.HasTransactions)
                {
                    _owner.HasTransactions = true;
                    _owner._changed();
                }

                if (_direction == null)
                {
                    _owner.Seed(page.Items);
                }

                return new IncrementalLoadResult(added, page.NextOffset.Length > 0);
            }

            private uint Append(IReadOnlyList<T> page)
            {
                // One notification per item: a ListView does not take a multi-item Add.
                for (int i = 0; i < page.Count; i++)
                {
                    Items.Add(page[i]);
                }

                return (uint)page.Count;
            }

            // The seed is a prefix of this page, so the rows already on screen stay where they are
            // and only the tail is new. When it isn't - a zero amount is neither inbound nor
            // outbound, so the local split can disagree with the server - the page wins outright.
            private uint Merge(IReadOnlyList<T> page)
            {
                var seeded = Items.Count;

                if (seeded <= page.Count)
                {
                    var matches = true;

                    for (int i = 0; i < seeded && matches; i++)
                    {
                        matches = _owner._getId(Items[i]) == _owner._getId(page[i]);
                    }

                    if (matches)
                    {
                        for (int i = seeded; i < page.Count; i++)
                        {
                            Items.Add(page[i]);
                        }

                        return (uint)(page.Count - seeded);
                    }
                }

                Items.ReplaceWith(page);
                return (uint)page.Count;
            }
        }
    }
}
