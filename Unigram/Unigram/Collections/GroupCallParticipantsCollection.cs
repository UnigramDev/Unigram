using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.System;

namespace Unigram.Collections
{
    public class GroupCallParticipantsCollection : ObservableCollection<GroupCallParticipant>, IDelegable<IGroupCallDelegate>, IHandle<UpdateGroupCall>, IHandle<UpdateGroupCallParticipant>
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private readonly int _groupCallId;
        private bool _loadedAllParticipants;

        public IGroupCallDelegate Delegate { get; set; }

        public GroupCallParticipantsCollection(IProtoService protoService, IEventAggregator aggregator, GroupCall groupCall)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _groupCallId = groupCall.Id;
            _loadedAllParticipants = groupCall.LoadedAllParticipants;

            _aggregator.Subscribe(this);
        }

        public void Load()
        {
            _protoService.Send(new LoadGroupCallParticipants(_groupCallId, 100));
        }

        public void Dispose()
        {
            _aggregator.Unsubscribe(this);
            _handlers.Clear();
        }

        public void Handle(UpdateGroupCall update)
        {
            if (_groupCallId == update.GroupCall.Id)
            {
                if (_loadedAllParticipants && _loadedAllParticipants != update.GroupCall.LoadedAllParticipants)
                {
                    Load();
                }

                _loadedAllParticipants = update.GroupCall.LoadedAllParticipants;
            }
        }

        public void Handle(UpdateGroupCallParticipant update)
        {
            if (_groupCallId == update.GroupCallId)
            {
                if (update.Participant.Order.Length > 0)
                {
                    var nextIndex = NextIndexOf(update.Participant, out var updated, out int prevIndex);
                    if (nextIndex >= 0)
                    {
                        if (prevIndex >= 0)
                        {
                            RemoveAt(prevIndex);
                        }

                        Insert(Math.Min(Count, nextIndex), update.Participant);
                    }
                    else if (updated != null)
                    {
                        Delegate?.UpdateGroupCallParticipant(updated);
                    }
                }
                else
                {
                    var already = this.FirstOrDefault(x => x.ParticipantId.IsEqual(update.Participant.ParticipantId));
                    if (already != null)
                    {
                        Remove(already);
                    }
                }
            }
        }

        private int NextIndexOf(GroupCallParticipant participant, out GroupCallParticipant update, out int prev)
        {
            update = null;

            prev = -1;
            var next = 0;
            var index = int.MaxValue;

            for (int i = 0; i < Count; i++)
            {
                var item = this[i];
                if (item.ParticipantId.IsEqual(participant.ParticipantId))
                {
                    prev = i;
                    update = item;
                    //continue;
                }

                var order = participant.Order.CompareTo(item.Order);
                var compare = participant.ParticipantId.ComparaTo(item.ParticipantId);

                if (index == int.MaxValue && (order > 0 || participant.Order == item.Order && compare >= 0))
                {
                    index = next == prev ? -1 : next;
                }

                next++;
            }

            if (update != null)
            {
                update.CanUnmuteSelf = participant.CanUnmuteSelf;
                update.CanBeMutedForAllUsers = participant.CanBeMutedForAllUsers;
                update.CanBeMutedForCurrentUser = participant.CanBeMutedForCurrentUser;
                update.CanBeUnmutedForAllUsers = participant.CanBeUnmutedForAllUsers;
                update.CanBeUnmutedForCurrentUser = participant.CanBeUnmutedForCurrentUser;
                update.IsMutedForAllUsers = participant.IsMutedForAllUsers;
                update.IsMutedForCurrentUser = participant.IsMutedForCurrentUser;
                update.IsCurrentUser = participant.IsCurrentUser;
                update.IsSpeaking = participant.IsSpeaking;
                update.IsHandRaised = participant.IsHandRaised;
                update.VolumeLevel = participant.VolumeLevel;
                update.Bio = participant.Bio;
                update.Order = participant.Order;
                update.Source = participant.Source;
                update.ParticipantId = participant.ParticipantId;
            }

            return index < int.MaxValue ? index : Count;
        }

        private readonly ConcurrentDictionary<DispatcherQueue, NotifyCollectionChangedEventHandler> _handlers
            = new ConcurrentDictionary<DispatcherQueue, NotifyCollectionChangedEventHandler>();

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            foreach (var dispatcher in _handlers)
            {
                dispatcher.Key.TryEnqueue(() => dispatcher.Value?.Invoke(this, e));
            }
        }

        public override event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                if (dispatcher == null)
                {
                    return;
                }

                if (_handlers.TryGetValue(dispatcher, out NotifyCollectionChangedEventHandler handlers))
                {
                    handlers += value;
                }
                else
                {
                    _handlers[dispatcher] = value;
                }
            }
            remove
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread();
                if (dispatcher == null)
                {
                    return;
                }

                if (_handlers.TryGetValue(dispatcher, out NotifyCollectionChangedEventHandler handlers))
                {
                    handlers -= value;
                }
            }
        }
    }
}
