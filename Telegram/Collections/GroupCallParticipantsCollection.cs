//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Services;
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml.Data;

namespace Telegram.Collections
{
    public partial class GroupCallParticipantsCollection : ObservableCollection<GroupCallParticipant>
        , ISupportIncrementalLoading
        , IDelegable<IGroupCallDelegate>
    {
        private readonly IClientService _clientService;

        private readonly Dictionary<int, GroupCallParticipant> _audioSources = new();

        private readonly VoipGroupCall _call;

        private bool _loadedAllParticipants;
        private int _participantCount;

        public IGroupCallDelegate Delegate { get; set; }

        public GroupCallParticipantsCollection(VoipGroupCall call)
        {
            _clientService = call.ClientService;

            _call = call;
            _call.PropertyChanged += OnPropertyChanged;

            _loadedAllParticipants = call.LoadedAllParticipants;
            _participantCount = call.ParticipantCount;
        }

        public void Load()
        {
            _clientService.Send(new LoadGroupCallParticipants(_call.Id, 100));
        }

        public void Dispose()
        {
            _call.PropertyChanged -= OnPropertyChanged;
            Delegate = null;
        }

        public bool TryGetFromAudioSourceId(int audioSourceId, out GroupCallParticipant participant)
        {
            return _audioSources.TryGetValue(audioSourceId, out participant);
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_call.LoadedAllParticipants && _call.LoadedAllParticipants != _loadedAllParticipants)
            {
                Load();
            }
            else if (_call.ParticipantCount == Items.Count && _call.ParticipantCount < _participantCount)
            {
                Load();
            }

            _loadedAllParticipants = _call.LoadedAllParticipants;
            _participantCount = _call.ParticipantCount;

            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Count"));
        }

        public void Update(GroupCallParticipant participant)
        {
            TryEnqueue(() =>
            {
                if (participant.Order.Length > 0)
                {
                    var nextIndex = NextIndexOf(participant, out var updated, out int prevIndex);
                    if (nextIndex >= 0)
                    {
                        if (prevIndex >= 0)
                        {
                            RemoveAt(prevIndex);
                        }

                        _audioSources[participant.IsCurrentUser ? 0 : participant.AudioSourceId] = participant;
                        Insert(Math.Min(Count, nextIndex), participant);
                    }
                    else if (updated != null)
                    {
                        Delegate?.UpdateGroupCallParticipant(updated);
                    }
                }
                else
                {
                    var already = this.FirstOrDefault(x => x.ParticipantId.AreTheSame(participant.ParticipantId));
                    if (already != null)
                    {
                        if (already.HasVideoInfo())
                        {
                            Delegate?.VideoInfoRemoved(already, already.ScreenSharingVideoInfo?.EndpointId, already.VideoInfo?.EndpointId);
                        }

                        _audioSources.Remove(participant.IsCurrentUser ? 0 : participant.AudioSourceId);
                        Remove(already);
                    }
                }
            });
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
                if (item.AreTheSame(participant))
                {
                    prev = i;
                    update = item;
                    //continue;
                }

                var order = participant.Order.CompareTo(item.Order);
                var compare = participant.IsCurrentUser && item.IsCurrentUser ? 0 : participant.ParticipantId.ComparaTo(item.ParticipantId);

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

                var response = await _clientService.SendAsync(new LoadGroupCallParticipants(_call.Id, 100));
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

        public bool HasMoreItems => !_call.LoadedAllParticipants;
    }
}
