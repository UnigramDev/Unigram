using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Telegram.Td.Api;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.System;

namespace Unigram.Collections
{
    public class GroupCallParticipantsCollection : ObservableCollection<GroupCallParticipant>, IDelegable<IGroupCallDelegate>, IHandle<UpdateGroupCallParticipant>
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private readonly int _groupCallId;

        public IGroupCallDelegate Delegate { get; set; }

        public GroupCallParticipantsCollection(IProtoService protoService, IEventAggregator aggregator, int groupCallId)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _groupCallId = groupCallId;

            _aggregator.Subscribe(this);
        }

        public void Load()
        {
            _protoService.Send(new LoadGroupCallParticipants(_groupCallId, 100));
        }

        public void Handle(UpdateGroupCallParticipant update)
        {
            if (_groupCallId == update.GroupCallId)
            {
                if (update.Participant.Order > 0)
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
                    var already = this.FirstOrDefault(x => x.UserId == update.Participant.UserId);
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
                if (item.UserId == participant.UserId)
                {
                    prev = i;
                    update = item;
                    //continue;
                }

                if (index == int.MaxValue && (participant.Order > item.Order || participant.Order == item.Order && participant.UserId >= item.UserId))
                {
                    index = next == prev ? -1 : next;
                }

                next++;
            }

            if (update != null)
            {
                update.CanUnmuteSelf = participant.CanUnmuteSelf;
                update.IsMuted = participant.IsMuted;
                update.IsSpeaking = participant.IsSpeaking;
                update.Order = participant.Order;
                update.Source = participant.Source;
                update.UserId = participant.UserId;
            }

            return index < int.MaxValue ? index : Count;
        }

        private ConcurrentDictionary<DispatcherQueue, NotifyCollectionChangedEventHandler> _handlers = new ConcurrentDictionary<DispatcherQueue, NotifyCollectionChangedEventHandler>();

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
