//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Common;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public class GroupCallParticipantsCollection : ObservableCollection<GroupCallParticipant>
        , ISupportIncrementalLoading
        , IDelegable<IGroupCallDelegate>
    //, IHandle<UpdateGroupCall>
    //, IHandle<UpdateGroupCallParticipant>
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly Dictionary<int, GroupCallParticipant> _audioSources = new();

        private GroupCall _groupCall;

        public IGroupCallDelegate Delegate { get; set; }

        public GroupCallParticipantsCollection(IClientService clientService, IEventAggregator aggregator, GroupCall groupCall)
        {
            _clientService = clientService;
            _aggregator = aggregator;

            _groupCall = groupCall;

            _aggregator.Subscribe<UpdateGroupCall>(this, Handle)
                .Subscribe<UpdateGroupCallParticipant>(Handle);
        }

        public void Load()
        {
            _clientService.Send(new LoadGroupCallParticipants(_groupCall.Id, 100));
        }

        public void Dispose()
        {
            _aggregator.Unsubscribe(this);
            Delegate = null;
        }

        public bool TryGetFromAudioSourceId(int audioSourceId, out GroupCallParticipant participant)
        {
            return _audioSources.TryGetValue(audioSourceId, out participant);
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
                TryEnqueue(() =>
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

                            _audioSources[update.Participant.IsCurrentUser ? 0 : update.Participant.AudioSourceId] = update.Participant;
                            Insert(Math.Min(Count, nextIndex), update.Participant);
                        }
                        else if (updated != null)
                        {
                            Delegate?.UpdateGroupCallParticipant(updated);
                        }
                    }
                    else
                    {
                        var already = this.FirstOrDefault(x => x.ParticipantId.AreTheSame(update.Participant.ParticipantId));
                        if (already != null)
                        {
                            if (already.HasVideoInfo())
                            {
                                Delegate?.VideoInfoRemoved(already, already.ScreenSharingVideoInfo?.EndpointId, already.VideoInfo?.EndpointId);
                            }

                            _audioSources.Remove(update.Participant.IsCurrentUser ? 0 : update.Participant.AudioSourceId);
                            Remove(already);
                        }
                    }
                });
            }
        }

        private void TryEnqueue(DispatcherQueueHandler callback)
        {
            if (Delegate?.DispatcherQueue == null || Delegate.DispatcherQueue.HasThreadAccess)
            {
                callback();
            }
            else
            {
                Delegate.DispatcherQueue.TryEnqueue(callback);
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
                if (item.ParticipantId.AreTheSame(participant.ParticipantId))
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

            string[] removedVideoInfo = null;
            GroupCallParticipantVideoInfo[] addedVideoInfo = null;

            if (update != null)
            {
                if (update.ScreenSharingVideoInfo?.EndpointId != participant.ScreenSharingVideoInfo?.EndpointId)
                {
                    if (update.ScreenSharingVideoInfo?.EndpointId != null)
                    {
                        removedVideoInfo ??= new string[2];
                        removedVideoInfo[0] = update.ScreenSharingVideoInfo.EndpointId;
                    }

                    if (participant.ScreenSharingVideoInfo?.EndpointId != null)
                    {
                        addedVideoInfo ??= new GroupCallParticipantVideoInfo[2];
                        addedVideoInfo[0] = participant.ScreenSharingVideoInfo;
                    }
                }

                if (update.VideoInfo?.EndpointId != participant.VideoInfo?.EndpointId)
                {
                    if (update.VideoInfo?.EndpointId != null)
                    {
                        removedVideoInfo ??= new string[2];
                        removedVideoInfo[1] = update.VideoInfo.EndpointId;
                    }

                    if (participant.VideoInfo?.EndpointId != null)
                    {
                        addedVideoInfo ??= new GroupCallParticipantVideoInfo[2];
                        addedVideoInfo[1] = participant.VideoInfo;
                    }
                }

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
                update.ScreenSharingAudioSourceId = participant.ScreenSharingAudioSourceId;
                update.ParticipantId = participant.ParticipantId;
            }
            else if (index >= 0 && (participant.ScreenSharingVideoInfo != null || participant.VideoInfo != null))
            {
                addedVideoInfo = new[] { participant.ScreenSharingVideoInfo, participant.VideoInfo };
            }

            if (removedVideoInfo != null)
            {
                Delegate?.VideoInfoRemoved(participant, removedVideoInfo);
            }

            if (addedVideoInfo != null)
            {
                Delegate?.VideoInfoAdded(participant, addedVideoInfo);
            }

            return index < int.MaxValue ? index : Count;
        }

        public void LoadVideoInfo()
        {
            if (Delegate?.DispatcherQueue == null || Delegate.DispatcherQueue.HasThreadAccess)
            {
                foreach (var participant in this)
                {
                    if (participant.ScreenSharingVideoInfo != null || participant.VideoInfo != null)
                    {
                        Delegate?.VideoInfoAdded(participant, new[] { participant.ScreenSharingVideoInfo, participant.VideoInfo });
                    }
                }
            }
            else
            {
                Delegate.DispatcherQueue.TryEnqueue(LoadVideoInfo);
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async token =>
            {
                count = (uint)Count;

                var response = await _clientService.SendAsync(new LoadGroupCallParticipants(_groupCall.Id, 100));
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
    }
}
