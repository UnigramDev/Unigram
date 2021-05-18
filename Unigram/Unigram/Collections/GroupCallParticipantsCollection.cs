using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml.Data;

namespace Unigram.Collections
{
    public class GroupCallParticipantsCollection : ObservableCollection<GroupCallParticipant>, IDelegable<IGroupCallDelegate>, IHandle<UpdateGroupCall>, IHandle<UpdateGroupCallParticipant>, ISupportIncrementalLoading
    {
        private readonly IProtoService _protoService;
        private readonly IEventAggregator _aggregator;

        private GroupCall _groupCall;

        public IGroupCallDelegate Delegate { get; set; }

        public GroupCallParticipantsCollection(IProtoService protoService, IEventAggregator aggregator, GroupCall groupCall)
        {
            _protoService = protoService;
            _aggregator = aggregator;

            _groupCall = groupCall;

            _aggregator.Subscribe(this);
        }

        public void Load()
        {
            _protoService.Send(new LoadGroupCallParticipants(_groupCall.Id, 100));
        }

        public void Dispose()
        {
            _aggregator.Unsubscribe(this);
            _handlers.Clear();
        }

        public void Handle(UpdateGroupCall update)
        {
            if (_groupCall.Id == update.GroupCall.Id)
            {
                if (_groupCall.LoadedAllParticipants && _groupCall.LoadedAllParticipants != update.GroupCall.LoadedAllParticipants)
                {
                    Load();
                }
                else if (_groupCall.ParticipantCount == Items.Count && _groupCall.ParticipantCount < update.GroupCall.ParticipantCount)
                {
                    Load();
                }

                _groupCall = update.GroupCall;
                OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Count"));
            }
        }

        public void Handle(UpdateGroupCallParticipant update)
        {
            if (_groupCall.Id == update.GroupCallId)
            {
                if (update.Participant.Order.Length > 0)
                {
                    var nextIndex = NextIndexOf(update.Participant, out var updated, out int prevIndex, out bool videoChanged);
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

                    if (videoChanged)
                    {
                        Delegate?.UpdateGroupCallParticipants();
                    }
                }
                else
                {
                    var already = this.FirstOrDefault(x => x.ParticipantId.IsEqual(update.Participant.ParticipantId));
                    if (already != null)
                    {
                        Remove(already);
                        Delegate?.UpdateGroupCallParticipants();
                    }
                }
            }
        }

        private int NextIndexOf(GroupCallParticipant participant, out GroupCallParticipant update, out int prev, out bool videoChanged)
        {
            update = null;
            videoChanged = false;

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
                videoChanged = update.ScreenSharingVideoInfo?.EndpointId != participant.ScreenSharingVideoInfo?.EndpointId
                    || update.VideoInfo?.EndpointId != participant.VideoInfo?.EndpointId;

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
                update.ScreenSharingVideoInfo = participant.ScreenSharingVideoInfo;
                update.VideoInfo = participant.VideoInfo;
                update.AudioSourceId = participant.AudioSourceId;
                update.ParticipantId = participant.ParticipantId;
            }
            else
            {
                var nextIndex = index < int.MaxValue ? index : Count;
                videoChanged = nextIndex >= 0;
            }

            return index < int.MaxValue ? index : Count;
        }


        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                count = (uint)Count;

                var response = await _protoService.SendAsync(new LoadGroupCallParticipants(_groupCall.Id, 100));
                if (response is Ok)
                {
                    count = (uint)Count - count;
                }
                else
                {
                    count = 0;
                }

                return new LoadMoreItemsResult { Count = count };
            });
        }

        public bool HasMoreItems => !_groupCall.LoadedAllParticipants;

        #region Dispatcher queue

        private readonly ConcurrentDictionary<DispatcherQueue, NotifyCollectionChangedEventHandler> _handlers
            = new ConcurrentDictionary<DispatcherQueue, NotifyCollectionChangedEventHandler>();

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            foreach (var dispatcher in _handlers)
            {
                dispatcher.Key.TryEnqueue(() =>
                {
                    try
                    {
                        dispatcher.Value?.Invoke(this, e);
                    }
                    catch { }
                });
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

        #endregion
    }
}
