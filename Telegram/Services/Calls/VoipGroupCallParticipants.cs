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

namespace Telegram.Services
{
    public partial class VoipGroupCallParticipants
    {
        private readonly IClientService _clientService;
        private readonly int _groupCallId;

        private readonly Dictionary<MessageSender, GroupCallParticipant> _participantsCache = new(new MessageSenderEqualityComparer());

        private readonly SortedSet<OrderedParticipant> _participants = new();
        private bool _haveFullParticipants;

        public VoipGroupCallParticipants(IClientService clientService, int groupCallId)
        {
            _clientService = clientService;
            _groupCallId = groupCallId;
        }

        private void SetParticipantOrder(GroupCallParticipant participant, string order)
        {
            lock (_participants)
            {
                _participants.Remove(new OrderedParticipant(participant.ParticipantId, participant.Order));

                participant.Order = order;

                if (order.Length > 0)
                {
                    _participants.Add(new OrderedParticipant(participant.ParticipantId, order));
                }
            }
        }

        public Task<IList<GroupCallParticipant>> GetParticipantsAsync(int offset, int limit)
        {
            return GetParticipantsAsyncImpl(offset, limit, false);
        }

        public async Task<IList<GroupCallParticipant>> GetParticipantsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            var count = offset + limit;

            // How many participants are still to be loaded, 0 when the cache can answer on
            // its own. Decided under the lock, acted on outside it: awaiting is not allowed
            // in there.
            int missing;

            lock (_participants)
            {
                var sorted = _participants;

                missing = !_haveFullParticipants && count > sorted.Count && !reentrancy
                    ? count - sorted.Count
                    : 0;

                if (missing == 0)
                {
                    // Have enough chats in the chat list to answer request
                    var result = new GroupCallParticipant[Math.Max(0, Math.Min(limit, sorted.Count - offset))];
                    var pos = 0;

                    using (var iter = sorted.GetEnumerator())
                    {
                        int max = Math.Min(count, sorted.Count);

                        for (int i = 0; i < max; i++)
                        {
                            iter.MoveNext();

                            if (i >= offset)
                            {
                                if (_participantsCache.TryGetValue(iter.Current.ParticipantId, out var topic))
                                {
                                    result[pos++] = topic;
                                }
                                else
                                {
                                    pos++;
                                }
                            }
                        }
                    }

                    return result;
                }
            }

            var response = await _clientService.SendAsync(new LoadGroupCallParticipants(_groupCallId, missing));
            if (response is Ok or Error)
            {
                if (response is Error error)
                {
                    if (error.Code == 404)
                    {
                        _haveFullParticipants = true;
                    }
                    else
                    {
                        return null;
                    }
                }

                // Chats have already been received through updates, let's retry request
                return await GetParticipantsAsyncImpl(offset, limit, true);
            }

            return null;
        }

        private readonly struct OrderedParticipant : IComparable<OrderedParticipant>
        {
            public readonly MessageSender ParticipantId;
            public readonly string Order;

            public OrderedParticipant(MessageSender participantId, string order)
            {
                ParticipantId = participantId;
                Order = order;
            }

            public int CompareTo(OrderedParticipant o)
            {
                if (Order != o.Order)
                {
                    return o.Order.CompareTo(Order);
                }

                if (ParticipantId != o.ParticipantId)
                {
                    return o.ParticipantId.ComparaTo(ParticipantId) < 0 ? -1 : 1;
                }

                return 0;
            }

            public override bool Equals(object obj)
            {
                OrderedParticipant o = (OrderedParticipant)obj;
                return ParticipantId.AreTheSame(o.ParticipantId) && Order == o.Order;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ParticipantId, Order);
            }
        }
    }
}
