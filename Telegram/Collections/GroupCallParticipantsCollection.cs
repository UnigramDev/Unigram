//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Threading.Tasks;
using Telegram.Services;
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.System;

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
    public partial class GroupCallParticipantsCollection
        : WindowedCollection<GroupCallParticipant, MessageSender, string, VoipGroupCallParticipantChangedEventArgs>
    {
        private readonly VoipGroupCallParticipants _participants;

        private bool _disposed;

        public GroupCallParticipantsCollection(VoipGroupCallParticipants participants, DispatcherQueue dispatcherQueue)
            : base(handler => dispatcherQueue.TryEnqueue(handler), new MessageSenderEqualityComparer())
        {
            _participants = participants;
            _participants.Changed += OnChanged;

            _ = LoadMoreItemsAsync(0);
        }

        public override void Dispose()
        {
            _disposed = true;

            base.Dispose();
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
            // Armed rather than tested: every load before this one answered "no more",
            // because nothing could be paged in until the call was joined. Joining is what
            // makes that untrue.
            HasMoreItems = true;

            _ = LoadMoreItemsAsync(0);
        }

        private void OnChanged(VoipGroupCallParticipants sender, VoipGroupCallParticipantChangedEventArgs args)
        {
            Enqueue(args);
        }

        protected override MessageSender GetKey(GroupCallParticipant item)
        {
            return item.ParticipantId;
        }

        protected override GroupCallParticipant GetItem(VoipGroupCallParticipantChangedEventArgs args)
        {
            return args.Participant;
        }

        protected override string GetOrder(VoipGroupCallParticipantChangedEventArgs args)
        {
            return args.Order;
        }

        protected override bool IsPlaced(string order)
        {
            return order.Length > 0;
        }

        protected override int Compare(string order, MessageSender participantId, string otherOrder, MessageSender otherParticipantId)
        {
            return VoipGroupCallParticipants.Compare(order, participantId, otherOrder, otherParticipantId);
        }

        protected override void OnApplied(VoipGroupCallParticipantChangedEventArgs args)
        {
            ParticipantChanged?.Invoke(this, args);
        }

        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var slice = await _participants.GetParticipantsAsync(Count, 20);

            // The window can have closed while the page was in flight.
            if (_disposed)
            {
                return default;
            }

            foreach (var participant in slice.Participants)
            {
                // An update can have inserted it already while the page was in flight,
                // and can have moved it since: place it where it belongs either way.
                var next = NextIndexOf(participant, participant.ParticipantId, participant.Order, out int prev);
                if (next == prev)
                {
                    continue;
                }

                if (prev >= 0)
                {
                    RemoveAt(prev);
                }
                else
                {
                    totalCount++;
                }

                SetOrder(participant.ParticipantId, participant.Order);
                Insert(Math.Min(Count, next), participant);
            }

            // Passed rather than read back: the collection is told only once this returns.
            UpdateWindow(slice.HasMore);

            return new IncrementalLoadResult(totalCount, slice.HasMore);
        }
    }
}
