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
using Windows.Foundation;

namespace Telegram.Services.Calls
{
    /// <summary>
    /// Every participant TDLib has told us about, in call order.
    /// </summary>
    /// <remarks>
    /// The authoritative copy, created with the call and kept for its whole life: it is
    /// never dropped on rejoin, and it holds everyone regardless of what any view has
    /// paged in. Views keep their own observable window over it, the way
    /// ChatListViewModel.ItemsCollection does over ClientService.
    /// </remarks>
    public partial class VoipGroupCallParticipants
    {
        private readonly VoipGroupCall _call;

        // Guards every field below. Update arrives on TDLib's update thread,
        // TryGetByAudioSource is called from tgcalls threads and GetParticipantsAsync
        // from a window's UI thread, so none of this can be lock-free.
        private readonly object _lock = new();

        private readonly Dictionary<MessageSender, GroupCallParticipant> _participants = new(new MessageSenderEqualityComparer());
        private readonly SortedSet<OrderedParticipant> _ordered = new();

        // Both of a participant's audio sources map to it: tgcalls resolves a screen
        // sharing ssrc through the same request as the microphone one. Source 0 means
        // "not connected over WebRTC" and is shared by everyone in that state, so it is
        // never a key.
        private readonly Dictionary<int, GroupCallParticipant> _audioSources = new();

        private bool _haveFullParticipants;

        public VoipGroupCallParticipants(VoipGroupCall call)
        {
            _call = call;
        }

        /// <summary>
        /// Raised on TDLib's update thread, once per participant update. Handlers marshal.
        /// </summary>
        public event TypedEventHandler<VoipGroupCallParticipants, VoipGroupCallParticipantChangedEventArgs> Changed;

        /// <summary>
        /// Merges an update into the participant this already holds, so that every view
        /// and every caller shares one instance per participant, and reports what
        /// changed. The merge overwrites the previous state, so the diff is the only
        /// record of it left.
        /// </summary>
        public VoipGroupCallParticipantChangedEventArgs Update(GroupCallParticipant participant)
        {
            VoipGroupCallParticipantChangedEventArgs args;

            lock (_lock)
            {
                args = UpdateImpl(participant);
            }

            // Never under the lock: handlers marshal to their own thread and one of them
            // can be paging through GetParticipantsAsync meanwhile.
            Changed?.Invoke(this, args);
            return args;
        }

        private VoipGroupCallParticipantChangedEventArgs UpdateImpl(GroupCallParticipant participant)
        {
            if (!_participants.TryGetValue(participant.ParticipantId, out var already))
            {
                if (participant.Order.Length == 0)
                {
                    // Never seen and already gone: nothing to add and nothing to forget.
                    return new VoipGroupCallParticipantChangedEventArgs(participant, string.Empty, null, null);
                }

                _participants[participant.ParticipantId] = participant;
                _ordered.Add(new OrderedParticipant(participant.ParticipantId, participant.Order));
                AddAudioSources(participant);

                return new VoipGroupCallParticipantChangedEventArgs(participant, participant.Order, null, VideoInfoOf(participant));
            }

            // Captured before the merge overwrites them: the endpoints that go away are
            // the ones the view has cells for.
            var previousScreenSharing = already.ScreenSharingVideoInfo?.EndpointId;
            var previousVideo = already.VideoInfo?.EndpointId;

            _ordered.Remove(new OrderedParticipant(already.ParticipantId, already.Order));
            RemoveAudioSources(already);

            Merge(already, participant);

            if (already.Order.Length > 0)
            {
                _ordered.Add(new OrderedParticipant(already.ParticipantId, already.Order));
                AddAudioSources(already);
            }
            else
            {
                // Leaving drops every endpoint, whether or not it changed.
                _participants.Remove(already.ParticipantId);

                return new VoipGroupCallParticipantChangedEventArgs(already, string.Empty,
                    previousScreenSharing == null && previousVideo == null ? null : new[] { previousScreenSharing, previousVideo }, null);
            }

            string[] removed = null;
            GroupCallParticipantVideoInfo[] added = null;

            // Slot 0 is screen sharing and slot 1 the camera, so that a view can tell the
            // two apart by position as well as by reference.
            if (previousScreenSharing != already.ScreenSharingVideoInfo?.EndpointId)
            {
                if (previousScreenSharing != null)
                {
                    removed ??= new string[2];
                    removed[0] = previousScreenSharing;
                }

                if (already.ScreenSharingVideoInfo != null)
                {
                    added ??= new GroupCallParticipantVideoInfo[2];
                    added[0] = already.ScreenSharingVideoInfo;
                }
            }

            if (previousVideo != already.VideoInfo?.EndpointId)
            {
                if (previousVideo != null)
                {
                    removed ??= new string[2];
                    removed[1] = previousVideo;
                }

                if (already.VideoInfo != null)
                {
                    added ??= new GroupCallParticipantVideoInfo[2];
                    added[1] = already.VideoInfo;
                }
            }

            return new VoipGroupCallParticipantChangedEventArgs(already, already.Order, removed, added);
        }

        private static GroupCallParticipantVideoInfo[] VideoInfoOf(GroupCallParticipant participant)
        {
            return participant.ScreenSharingVideoInfo != null || participant.VideoInfo != null
                ? new[] { participant.ScreenSharingVideoInfo, participant.VideoInfo }
                : null;
        }

        private static void Merge(GroupCallParticipant already, GroupCallParticipant participant)
        {
            already.CanUnmuteSelf = participant.CanUnmuteSelf;
            already.CanBeMutedForAllUsers = participant.CanBeMutedForAllUsers;
            already.CanBeMutedForCurrentUser = participant.CanBeMutedForCurrentUser;
            already.CanBeUnmutedForAllUsers = participant.CanBeUnmutedForAllUsers;
            already.CanBeUnmutedForCurrentUser = participant.CanBeUnmutedForCurrentUser;
            already.IsMutedForAllUsers = participant.IsMutedForAllUsers;
            already.IsMutedForCurrentUser = participant.IsMutedForCurrentUser;
            already.IsCurrentUser = participant.IsCurrentUser;
            already.IsSpeaking = participant.IsSpeaking;
            already.IsHandRaised = participant.IsHandRaised;
            already.VolumeLevel = participant.VolumeLevel;
            already.Bio = participant.Bio;
            already.Order = participant.Order;
            already.ScreenSharingVideoInfo = participant.ScreenSharingVideoInfo;
            already.VideoInfo = participant.VideoInfo;
            already.AudioSourceId = participant.AudioSourceId;
            already.ScreenSharingAudioSourceId = participant.ScreenSharingAudioSourceId;
            already.ParticipantId = participant.ParticipantId;
        }

        private void AddAudioSources(GroupCallParticipant participant)
        {
            if (participant.AudioSourceId != 0)
            {
                _audioSources[participant.AudioSourceId] = participant;
            }

            if (participant.ScreenSharingAudioSourceId != 0)
            {
                _audioSources[participant.ScreenSharingAudioSourceId] = participant;
            }
        }

        private void RemoveAudioSources(GroupCallParticipant participant)
        {
            // Only when it still maps here: a source can be handed to someone else, and
            // dropping it then would lose the mapping that has just replaced this one.
            if (participant.AudioSourceId != 0 && _audioSources.TryGetValue(participant.AudioSourceId, out var already) && already == participant)
            {
                _audioSources.Remove(participant.AudioSourceId);
            }

            if (participant.ScreenSharingAudioSourceId != 0 && _audioSources.TryGetValue(participant.ScreenSharingAudioSourceId, out already) && already == participant)
            {
                _audioSources.Remove(participant.ScreenSharingAudioSourceId);
            }
        }

        /// <summary>
        /// Resolves a WebRTC synchronization source. Called from tgcalls threads.
        /// </summary>
        public bool TryGetByAudioSource(int audioSourceId, out GroupCallParticipant participant)
        {
            lock (_lock)
            {
                return _audioSources.TryGetValue(audioSourceId, out participant);
            }
        }

        /// <summary>
        /// Every participant currently sharing video, whether or not a view has paged
        /// them in: the video grid covers the whole call, not the visible window.
        /// </summary>
        public IList<GroupCallParticipant> GetVideoParticipants()
        {
            lock (_lock)
            {
                var result = new List<GroupCallParticipant>();

                foreach (var participant in _participants.Values)
                {
                    if (participant.ScreenSharingVideoInfo != null || participant.VideoInfo != null)
                    {
                        result.Add(participant);
                    }
                }

                return result;
            }
        }

        public Task<VoipGroupCallParticipantsSlice> GetParticipantsAsync(int offset, int limit)
        {
            return GetParticipantsAsyncImpl(offset, limit, false);
        }

        private async Task<VoipGroupCallParticipantsSlice> GetParticipantsAsyncImpl(int offset, int limit, bool reentrancy)
        {
            var count = offset + limit;

            // Read outside the lock, and once: it belongs to the call rather than to this,
            // and both the answer below and the request further down turn on it.
            var joinable = _call.Id != 0 && _call.IsJoined;

            // How many participants are still to be loaded, 0 when the cache can answer on
            // its own. Decided under the lock, acted on outside it: awaiting is not allowed
            // in there.
            int missing;

            lock (_lock)
            {
                _haveFullParticipants |= _call.LoadedAllParticipants;

                missing = count > _ordered.Count && !_haveFullParticipants && !reentrancy
                    ? count - _ordered.Count
                    : 0;

                if (missing == 0)
                {
                    // Have enough participants in the call to answer the request
                    var result = new GroupCallParticipant[Math.Max(0, Math.Min(limit, _ordered.Count - offset))];
                    var pos = 0;

                    using (var iter = _ordered.GetEnumerator())
                    {
                        int max = Math.Min(count, _ordered.Count);

                        for (int i = 0; i < max; i++)
                        {
                            iter.MoveNext();

                            if (i >= offset)
                            {
                                result[pos++] = _participants[iter.Current.ParticipantId];
                            }
                        }
                    }

                    // Nothing can be asked for until the call is joined, so the answer is
                    // "no more" however many participants it really has: a caller told
                    // otherwise would keep asking for a page that cannot arrive, and the
                    // ones it is owed reach it through updates meanwhile.
                    var hasMore = joinable && (!_haveFullParticipants || count < _ordered.Count);
                    return new VoipGroupCallParticipantsSlice(result, hasMore);
                }
            }

            // loadGroupCallParticipants errors out before the call is joined, so there is
            // nothing to ask for yet: the participants will arrive through updates.
            if (joinable)
            {
                var response = await _call.ClientService.SendAsync(new LoadGroupCallParticipants(_call.Id, missing));
                if (response is Error)
                {
                    // Nothing to load for this call, and asking again would only repeat it.
                    lock (_lock)
                    {
                        _haveFullParticipants = true;
                    }
                }
            }

            // The participants have already been received through updates, let's retry the
            // request.
            return await GetParticipantsAsyncImpl(offset, limit, true);
        }

        /// <summary>
        /// Ranks one participant against another: positive when the first comes first.
        /// </summary>
        public static int Compare(string order, MessageSender participantId, string otherOrder, MessageSender otherParticipantId)
        {
            // Order is a fixed width, zero padded ASCII string built by TDLib, so an
            // ordinal comparison is both exact and cheaper than the culture aware default.
            var compare = string.CompareOrdinal(order, otherOrder);
            if (compare != 0)
            {
                return compare;
            }

            return CompareIds(participantId, otherParticipantId);
        }

        private static int CompareIds(MessageSender x, MessageSender y)
        {
            var xUser = x as MessageSenderUser;
            var yUser = y as MessageSenderUser;

            if (xUser != null && yUser != null)
            {
                return xUser.UserId.CompareTo(yUser.UserId);
            }
            else if (xUser != null || yUser != null)
            {
                // MessageSender.ComparaTo has no answer across types and a SortedSet needs
                // a total order all the same, so users are ranked ahead of chats.
                return xUser != null ? 1 : -1;
            }

            return ((MessageSenderChat)x).ChatId.CompareTo(((MessageSenderChat)y).ChatId);
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
                // Arguments swapped rather than the result negated: the set iterates in
                // call order, so the participant that ranks first must sort first.
                return Compare(o.Order, o.ParticipantId, Order, ParticipantId);
            }
        }
    }
}
