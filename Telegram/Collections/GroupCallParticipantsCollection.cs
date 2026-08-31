//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    /// <summary>
    /// A window over <see cref="VoipGroupCallParticipants"/>, owned by one view and
    /// touched only on its dispatcher.
    /// </summary>
    /// <remarks>
    /// It holds the top of the call, in order, down to the last participant paged in;
    /// anyone below that is left to paging. <see cref="ParticipantChanged"/> is raised
    /// for every update all the same, in window or not, since the video grid covers the
    /// whole call rather than the visible list.
    /// </remarks>
    public partial class GroupCallParticipantsCollection : ObservableCollection<GroupCallParticipant>, ISupportIncrementalLoading
    {
        private readonly VoipGroupCallParticipants _participants;
        private readonly DispatcherQueue _dispatcherQueue;

        // Updates arrive on TDLib's update thread and are drained on the dispatcher.
        // Queued rather than captured one closure at a time: a busy call updates
        // constantly, and this also coalesces a burst into a single drain.
        private readonly ConcurrentQueue<VoipGroupCallParticipantChangedEventArgs> _pending = new();
        private readonly DispatcherQueueHandler _drain;

        // The last participant in the window, or none while the window is still open at
        // the bottom. Recomputed after every mutation rather than tracked, so that a
        // participant leaving the boundary can't leave it pointing at a stale order.
        private string _lastOrder = string.Empty;
        private MessageSender _lastParticipantId;

        private bool _hasMoreItems = true;

        public GroupCallParticipantsCollection(VoipGroupCallParticipants participants, DispatcherQueue dispatcherQueue)
        {
            _participants = participants;
            _dispatcherQueue = dispatcherQueue;
            _drain = Drain;

            _participants.Changed += OnChanged;

            _ = LoadMoreItemsAsync();
        }

        public void Dispose()
        {
            _participants.Changed -= OnChanged;
        }

        /// <summary>
        /// Raised on the dispatcher for every participant update, whether or not it moved
        /// a row.
        /// </summary>
        public event TypedEventHandler<GroupCallParticipantsCollection, VoipGroupCallParticipantChangedEventArgs> ParticipantChanged;

        /// <summary>
        /// Asks for another page. Joining is the one moment worth calling this on: until
        /// then TDLib has nothing to load and the list is fed by updates alone.
        /// </summary>
        public void Load()
        {
            if (_hasMoreItems)
            {
                _ = LoadMoreItemsAsync();
            }
        }

        private void OnChanged(VoipGroupCallParticipants sender, VoipGroupCallParticipantChangedEventArgs args)
        {
            _pending.Enqueue(args);
            _dispatcherQueue.TryEnqueue(_drain);
        }

        private void Drain()
        {
            while (_pending.TryDequeue(out var args))
            {
                Apply(args);
            }
        }

        private void Apply(VoipGroupCallParticipantChangedEventArgs args)
        {
            var participant = args.Participant;

            if (args.Order.Length > 0 && IsWithinWindow(args.Order, participant.ParticipantId))
            {
                var next = NextIndexOf(participant, args.Order, out int prev);
                if (next != prev)
                {
                    if (prev >= 0)
                    {
                        RemoveAt(prev);
                    }

                    Insert(Math.Min(Count, next), participant);
                    UpdateWindow();
                }
            }
            else
            {
                var prev = IndexOf(participant);
                if (prev >= 0)
                {
                    RemoveAt(prev);
                    UpdateWindow();
                }
            }

            ParticipantChanged?.Invoke(this, args);
        }

        private bool IsWithinWindow(string order, MessageSender participantId)
        {
            // Nothing left to page in means the window is the whole call: a participant
            // sinking to the bottom must stay in the list, since nothing would bring it
            // back.
            return !_hasMoreItems
                || _lastParticipantId == null
                || VoipGroupCallParticipants.Compare(order, participantId, _lastOrder, _lastParticipantId) >= 0;
        }

        private void UpdateWindow()
        {
            if (_hasMoreItems && Count > 0)
            {
                var last = this[Count - 1];

                _lastOrder = last.Order;
                _lastParticipantId = last.ParticipantId;
            }
            else
            {
                _lastOrder = string.Empty;
                _lastParticipantId = null;
            }
        }

        /// <summary>
        /// Where the participant belongs, counted over the list without it, so that
        /// <paramref name="prev"/> and the result can be compared directly: equal means
        /// it is already in place and nothing has to move.
        /// </summary>
        private int NextIndexOf(GroupCallParticipant participant, string order, out int prev)
        {
            prev = -1;

            var next = 0;
            var index = -1;

            for (int i = 0; i < Count; i++)
            {
                var item = this[i];
                if (item == participant)
                {
                    prev = i;
                    continue;
                }

                if (index < 0 && VoipGroupCallParticipants.Compare(order, participant.ParticipantId, item.Order, item.ParticipantId) >= 0)
                {
                    index = next;
                }

                next++;
            }

            return index < 0 ? next : index;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return IncrementalLoading.Run(token => LoadMoreItemsAsync());
        }

        private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
        {
            var totalCount = 0u;

            var slice = await _participants.GetParticipantsAsync(Count, 20);

            foreach (var participant in slice.Participants)
            {
                // An update can have inserted it already while the page was in flight,
                // and can have moved it since: place it where it belongs either way.
                var next = NextIndexOf(participant, participant.Order, out int prev);
                if (next != prev)
                {
                    if (prev >= 0)
                    {
                        RemoveAt(prev);
                    }
                    else
                    {
                        totalCount++;
                    }

                    Insert(Math.Min(Count, next), participant);
                }
            }

            _hasMoreItems = slice.HasMore;
            UpdateWindow();

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems => _hasMoreItems;
    }
}
