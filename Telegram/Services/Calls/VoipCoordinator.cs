//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Navigation.Services;
using Telegram.Services.Calls;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Telegram.Views.Calls.Popups;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services
{
    public partial class VoipCoordinator
    {
        private readonly object _activeLock = new();
        private VoipCallBase _activeCall;

        public VoipCallBase ActiveCall
        {
            get
            {
                lock (_activeLock)
                {
                    return _activeCall;
                }
            }
        }

        #region Private

        private async Task<bool> CheckActiveCallAsync(IClientService clientService, INavigationService navigation, object source)
        {
            VoipCallBase activeCall;
            lock (_activeLock)
            {
                activeCall = _activeCall;
            }

            if (activeCall != null)
            {
                if (activeCall is VoipCall privateCall && privateCall.ClientService.TryGetUser(privateCall.UserId, out User activeUser))
                {
                    string message;
                    string title;

                    if (source is User newUser && (newUser.Id != privateCall.UserId || privateCall.ClientService != clientService))
                    {
                        message = string.Format(Strings.VoipOngoingAlert, activeUser.FullName(), newUser.FullName());
                        title = Strings.VoipOngoingAlertTitle;
                    }
                    else if (source is Chat newChat)
                    {
                        message = string.Format(Strings.VoipOngoingAlert2, activeUser.FullName(), newChat.Title);
                        title = Strings.VoipOngoingAlertTitle;
                    }
                    else
                    {
                        activeCall.Show();
                        return true;
                    }

                    var confirm = await navigation.ShowPopupAsync(message, title, Strings.OK, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        privateCall.Discard();
                        return false;
                    }
                }
                else if (activeCall is VoipGroupCall groupCall && groupCall.Chat != null && groupCall.ClientService.TryGetChat(groupCall.Chat.Id, out Chat activeChat))
                {
                    string message;
                    string title;

                    // TODO: not the right string for conference calls

                    if (source is Chat newChat && (newChat.Id != activeChat.Id || groupCall.ClientService != clientService))
                    {
                        message = string.Format(Strings.VoipOngoingChatAlert, activeChat.Title, newChat.Title);
                        title = Strings.VoipOngoingChatAlertTitle;
                    }
                    else if (source is User newUser)
                    {
                        message = string.Format(Strings.VoipOngoingChatAlert2, activeChat.Title, newUser.FullName());
                        title = Strings.VoipOngoingChatAlertTitle;
                    }
                    else
                    {
                        activeCall.Show();
                        return true;
                    }

                    var confirm = await navigation.ShowPopupAsync(message, title, Strings.OK, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        groupCall.Discard();
                        return false;
                    }
                }
            }

            return false;
        }

        public void StartPrivateCall(IClientService clientService, INavigationService navigation, Chat chat, bool video)
        {
            if (chat == null)
            {
                return;
            }

            if (clientService.TryGetUser(chat, out User user))
            {
                StartPrivateCall(clientService, navigation, user, video);
            }
        }

        public async void StartPrivateCall(IClientService clientService, INavigationService navigation, User user, bool video)
        {
            if (MediaDevicePermissions.IsUnsupported(navigation.XamlRoot))
            {
                return;
            }

            if (user == null)
            {
                return;
            }

            var activeCall = await CheckActiveCallAsync(clientService, navigation, user);
            if (activeCall)
            {
                return;
            }

            var fullInfo = clientService.GetUserFull(user.Id);
            if (fullInfo != null && fullInfo.HasPrivateCalls)
            {
                await navigation.ShowPopupAsync(string.Format(Strings.CallNotAvailable, user.FirstName), Strings.VoipFailed, Strings.OK);
                return;
            }

            var permissions = await MediaDevicePermissions.CheckAccessAsync(navigation.XamlRoot, video ? MediaDeviceAccess.AudioAndVideo : MediaDeviceAccess.Audio);
            if (permissions == false)
            {
                return;
            }

            var protocol = VoipManager.Protocol.ToTd();

            var response = await clientService.SendAsync(new CreateCall(user.Id, protocol, video));
            if (response is Error error)
            {
                if (error.Code == 400 && error.Message.Equals("PARTICIPANT_VERSION_OUTDATED"))
                {
                    var message = video
                        ? Strings.VoipPeerVideoOutdated
                        : Strings.VoipPeerOutdated;
                    await navigation.ShowPopupAsync(string.Format(message, user.FirstName), Strings.AppName, Strings.OK);
                }
                else if (error.Code == 400 && error.Message.Equals("USER_PRIVACY_RESTRICTED"))
                {
                    await navigation.ShowPopupAsync(string.Format(Strings.CallNotAvailable, user.FullName()), Strings.AppName, Strings.OK);
                }
            }
        }

        #endregion

        #region Group

        public void JoinGroupCall(IClientService clientService, INavigationService navigation, InputGroupCall groupCall)
        {
            if (MediaDevicePermissions.IsUnsupported(navigation.XamlRoot))
            {
                return;
            }

            var xamlRoot = navigation.XamlRoot;

            ThreadPool.QueueUserWorkItem(state =>
            {
                var changed = false;

                lock (_activeLock)
                {
                    var settings = clientService.Session.Resolve<ISettingsService>();
                    var aggregator = clientService.Session.Resolve<IEventAggregator>();

                    _activeCall = new VoipGroupCall(clientService, settings, aggregator, xamlRoot, groupCall);
                    changed = false;
                }

                foreach (var aggregator in LifetimeService.Current.ResolveAll<IEventAggregator>())
                {
                    aggregator.Publish(new UpdateActiveCall());
                }

                //if (changed)
                //{
                //    Aggregator.Publish(new UpdateGroupCall(new GroupCall(groupCall.Id, groupCall.Title, groupCall.InviteLink, groupCall.ScheduledStartDate, groupCall.EnabledStartNotification, groupCall.IsActive, groupCall.IsVideoChat, groupCall.IsRtmpStream, true, false, groupCall.IsOwned, groupCall.CanBeManaged, groupCall.ParticipantCount, groupCall.HasHiddenListeners, groupCall.LoadedAllParticipants, groupCall.RecentSpeakers, groupCall.IsMyVideoEnabled, groupCall.IsMyVideoPaused, groupCall.CanEnableVideo, groupCall.MuteNewParticipants, groupCall.CanToggleMuteNewParticipants, groupCall.RecordDuration, groupCall.IsVideoRecorded, groupCall.Duration)));
                //}
            });
        }

        public void CreateGroupCall(IClientService clientService, INavigationService navigation, IList<long> userIds)
        {
            if (MediaDevicePermissions.IsUnsupported(navigation.XamlRoot))
            {
                return;
            }

            var xamlRoot = navigation.XamlRoot;

            ThreadPool.QueueUserWorkItem(state =>
            {
                var changed = false;

                lock (_activeLock)
                {
                    var settings = clientService.Session.Resolve<ISettingsService>();
                    var aggregator = clientService.Session.Resolve<IEventAggregator>();

                    _activeCall = new VoipGroupCall(clientService, settings, aggregator, xamlRoot, userIds);
                    changed = false;
                }

                foreach (var aggregator in LifetimeService.Current.ResolveAll<IEventAggregator>())
                {
                    aggregator.Publish(new UpdateActiveCall());
                }

                //if (changed)
                //{
                //    Aggregator.Publish(new UpdateGroupCall(new GroupCall(groupCall.Id, groupCall.Title, groupCall.InviteLink, groupCall.ScheduledStartDate, groupCall.EnabledStartNotification, groupCall.IsActive, groupCall.IsVideoChat, groupCall.IsRtmpStream, true, false, groupCall.IsOwned, groupCall.CanBeManaged, groupCall.ParticipantCount, groupCall.HasHiddenListeners, groupCall.LoadedAllParticipants, groupCall.RecentSpeakers, groupCall.IsMyVideoEnabled, groupCall.IsMyVideoPaused, groupCall.CanEnableVideo, groupCall.MuteNewParticipants, groupCall.CanToggleMuteNewParticipants, groupCall.RecordDuration, groupCall.IsVideoRecorded, groupCall.Duration)));
                //}
            });
        }

        public async void JoinGroupCall(IClientService clientService, INavigationService navigation, long chatId, string inviteHash)
        {
            if (MediaDevicePermissions.IsUnsupported(navigation.XamlRoot))
            {
                return;
            }

            var chat = clientService.GetChat(chatId);
            if (chat == null || chat.VideoChat.GroupCallId == 0)
            {
                return;
            }

            var activeCall = await CheckActiveCallAsync(clientService, navigation, chat);
            if (activeCall)
            {
                return;
            }

            var response = await clientService.SendAsync(new GetGroupCall(chat.VideoChat.GroupCallId));
            if (response is GroupCall groupCall)
            {
                await JoinAsyncInternal(clientService, navigation, chat, groupCall, null, inviteHash, false);
            }
        }

        public async void CreateGroupCall(IClientService clientService, INavigationService navigation, long chatId)
        {
            if (MediaDevicePermissions.IsUnsupported(navigation.XamlRoot))
            {
                return;
            }

            var chat = clientService.GetChat(chatId);
            if (chat == null || chat.VideoChat.GroupCallId != 0)
            {
                return;
            }

            var activeCall = await CheckActiveCallAsync(clientService, navigation, chat);
            if (activeCall)
            {
                return;
            }

            MessageSenders availableAliases;
            availableAliases = await clientService.SendAsync(new GetVideoChatAvailableParticipants(chatId)) as MessageSenders;
            availableAliases ??= new MessageSenders(0, Array.Empty<MessageSender>());

            var popup = new VideoChatAliasesPopup(clientService, chat, true, availableAliases.Senders);

            var confirm = await popup.ShowQueuedAsync(navigation.XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                var alias = popup.SelectedSender ?? new MessageSenderUser(clientService.Options.MyId);
                var startDate = 0;

                if (popup.IsScheduleSelected)
                {
                    var schedule = new ScheduleVideoChatPopup(chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel);

                    var again = await schedule.ShowQueuedAsync(navigation.XamlRoot);
                    if (again != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    startDate = schedule.Value.ToTimestamp();
                }
                else if (popup.IsStartWithSelected)
                {
                    var streams = new VideoChatStreamsPopup(clientService, chat.Id, true);

                    var again = await streams.ShowQueuedAsync(navigation.XamlRoot);
                    if (again != ContentDialogResult.Primary)
                    {
                        return;
                    }

                    if (streams.IsScheduleSelected)
                    {
                        var schedule = new ScheduleVideoChatPopup(true);

                        var oneMore = await schedule.ShowQueuedAsync(navigation.XamlRoot);
                        if (oneMore != ContentDialogResult.Primary)
                        {
                            return;
                        }

                        startDate = schedule.Value.ToTimestamp();
                    }
                }

                var response = await clientService.SendAsync(new CreateVideoChat(chat.Id, string.Empty, startDate, popup.IsStartWithSelected));
                if (response is GroupCallId groupCallId && clientService.TryGetGroupCall(groupCallId.Id, out GroupCall groupCall))
                {
                    await JoinAsyncInternal(clientService, navigation, chat, groupCall, alias, string.Empty, false);
                }
            }
        }

        private async Task JoinAsyncInternal(IClientService clientService, INavigationService navigation, Chat chat, GroupCall groupCall, MessageSender alias, string inviteHash, bool isLiveStory)
        {
            alias ??= chat.VideoChat.DefaultParticipantId;

            if (alias == null && !groupCall.IsRtmpStream)
            {
                MessageSenders availableAliases;
                availableAliases = await clientService.SendAsync(new GetVideoChatAvailableParticipants(chat.Id)) as MessageSenders;
                availableAliases ??= new MessageSenders(0, Array.Empty<MessageSender>());

                var popup = new VideoChatAliasesPopup(clientService, chat, false, availableAliases.Senders);

                var confirm = await popup.ShowQueuedAsync(navigation.XamlRoot);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                alias = popup.SelectedSender ?? new MessageSenderUser(clientService.Options.MyId);
            }

            if (!groupCall.IsRtmpStream)
            {
                var permissions = await MediaDevicePermissions.CheckAccessAsync(navigation.XamlRoot, MediaDeviceAccess.Audio);
                if (permissions == false)
                {
                    return;
                }
            }

            var xamlRoot = navigation.XamlRoot;

            ThreadPool.QueueUserWorkItem(state =>
            {
                var changed = false;

                lock (_activeLock)
                {
                    var settings = clientService.Session.Resolve<ISettingsService>();
                    var aggregator = clientService.Session.Resolve<IEventAggregator>();

                    _activeCall = new VoipGroupCall(clientService, settings, aggregator, xamlRoot, chat, groupCall, alias, inviteHash, isLiveStory);
                    changed = groupCall.ScheduledStartDate > 0;
                }

                foreach (var aggregator in LifetimeService.Current.ResolveAll<IEventAggregator>())
                {
                    aggregator.Publish(new UpdateActiveCall());
                }

                if (changed)
                {
                    var aggregator = clientService.Session.Resolve<IEventAggregator>();
                    aggregator.Publish(new UpdateGroupCall(new GroupCall(groupCall.Id, groupCall.UniqueId, groupCall.Title, groupCall.InviteLink, groupCall.PaidMessageStarCount, groupCall.ScheduledStartDate, groupCall.EnabledStartNotification, groupCall.IsActive, groupCall.IsVideoChat, groupCall.IsLiveStory, groupCall.IsRtmpStream, true, false, groupCall.IsOwned, groupCall.CanBeManaged, groupCall.ParticipantCount, groupCall.HasHiddenListeners, groupCall.LoadedAllParticipants, groupCall.MessageSenderId, groupCall.RecentSpeakers, groupCall.IsMyVideoEnabled, groupCall.IsMyVideoPaused, groupCall.CanEnableVideo, groupCall.MuteNewParticipants, groupCall.CanToggleMuteNewParticipants, groupCall.CanSendMessages, groupCall.AreMessagesAllowed, groupCall.CanToggleAreMessagesAllowed, groupCall.CanDeleteMessages, groupCall.RecordDuration, groupCall.IsVideoRecorded, groupCall.Duration)));
                }
            });
        }

        #endregion

        public void Handle(IClientService clientService, UpdateNewCallSignalingData update)
        {
            lock (_activeLock)
            {
                if (_activeCall is VoipCall privateCall && privateCall.Id == update.CallId && privateCall.ClientService == clientService)
                {
                    privateCall.ReceiveSignalingData(update.Data);
                }
            }
        }

        public void Handle(IClientService clientService, UpdateCall update)
        {
            var state = ToState(update.Call);
            if (state == VoipState.None)
            {
                return;
            }

            var changed = false;

            lock (_activeLock)
            {
                if (state == VoipState.Requesting || (state == VoipState.Ringing && !update.Call.IsOutgoing))
                {
                    if (_activeCall != null)
                    {
                        // Line is busy
                        clientService.Send(new DiscardCall(update.Call.Id, true, string.Empty, 0, false, 0));
                    }
                    else
                    {
                        var settings = clientService.Session.Resolve<ISettingsService>();
                        var aggregator = clientService.Session.Resolve<IEventAggregator>();

                        _activeCall = new VoipCall(clientService, settings, aggregator, update.Call, state);
                        changed = true;
                    }
                }
                else if (_activeCall is VoipCall privateCall && privateCall.Id == update.Call.Id)
                {
                    privateCall.Update(update.Call, state);

                    if (state is VoipState.Discarded or VoipState.Error)
                    {
                        _activeCall = null;
                        changed = true;
                    }
                    //else if (state is VoipState.Ready && update.Call.GroupCallId != 0)
                    //{
                    //    ClientService.Send(new GetGroupCall(update.Call.GroupCallId));
                    //}
                }
            }

            if (changed)
            {
                foreach (var aggregator in LifetimeService.Current.ResolveAll<IEventAggregator>())
                {
                    aggregator.Publish(new UpdateActiveCall());
                }
            }
        }

        public bool Handle(IClientService clientService, UpdateGroupCall update)
        {
            var changed = false;
            var handled = true;

            lock (_activeLock)
            {
                if (_activeCall is VoipGroupCall groupCall && groupCall.Id == update.GroupCall.Id && groupCall.ClientService == clientService)
                {
                    groupCall.Update(update.GroupCall, out bool closed);
                    handled = false;

                    if (closed)
                    {
                        _activeCall = null;
                        changed = true;
                    }
                }
                //else if (_activeCall is VoipCall call && call.GroupCallId == update.GroupCall.Id && !_upgrading)
                //{
                //    _upgrading = true;

                //    ClientService.TryGetChatFromUser(call.UserId, out Chat chat);

                //    WindowContext.ForEach(window =>
                //    {
                //        if (window.Content is VoipPage page)
                //        {
                //            _ = JoinAsyncInternal(page.XamlRoot, chat, call.GroupCallId, null, string.Empty, true);
                //        }
                //    });
                //}
            }

            if (changed)
            {
                foreach (var aggregator in LifetimeService.Current.ResolveAll<IEventAggregator>())
                {
                    aggregator.Publish(new UpdateActiveCall());
                }
            }

            return handled;
        }

        private bool _upgrading;

        public bool Handle(IClientService clientService, UpdateGroupCallParticipant update)
        {
            lock (_activeLock)
            {
                if (_activeCall is VoipGroupCall groupCall && groupCall.Id == update.GroupCallId && groupCall.ClientService == clientService)
                {
                    groupCall.UpdateParticipant(update.Participant);
                    return false;
                }
            }

            return true;
        }

        public bool Handle(IClientService clientService, UpdateGroupCallVerificationState update)
        {
            lock (_activeLock)
            {
                if (_activeCall is VoipGroupCall groupCall && groupCall.Id == update.GroupCallId && groupCall.ClientService == clientService)
                {
                    groupCall.UpdateVerificationState(update.Generation, update.Emojis);
                    return false;
                }
            }

            return true;
        }

        public bool Handle(IClientService clientService, UpdateGroupCallMessageSendFailed update)
        {
            lock (_activeLock)
            {
                if (_activeCall is VoipGroupCall groupCall && groupCall.Id == update.GroupCallId && groupCall.ClientService == clientService)
                {
                    //groupCall.Update(update.Message);
                    return false;
                }
            }

            return true;
        }

        public bool Handle(IClientService clientService, UpdateGroupCallMessagesDeleted update)
        {
            lock (_activeLock)
            {
                if (_activeCall is VoipGroupCall groupCall && groupCall.Id == update.GroupCallId && groupCall.ClientService == clientService)
                {
                    //groupCall.Update(update.Message);
                    return false;
                }
            }

            return true;
        }

        public bool Handle(IClientService clientService, UpdateNewGroupCallMessage update)
        {
            lock (_activeLock)
            {
                if (_activeCall is VoipGroupCall groupCall && groupCall.Id == update.GroupCallId && groupCall.ClientService == clientService)
                {
                    //groupCall.Update(update.Message);
                    return false;
                }
            }

            return true;
        }

        public bool Handle(IClientService clientService, UpdateNewGroupCallPaidReaction update)
        {
            lock (_activeLock)
            {
                if (_activeCall is VoipGroupCall groupCall && groupCall.Id == update.GroupCallId && groupCall.ClientService == clientService)
                {
                    //groupCall.Update(update.Message);
                    return false;
                }
            }

            return true;
        }

        private static VoipState ToState(Call call)
        {
            return call.State switch
            {
                CallStatePending { IsCreated: false, IsReceived: false } => VoipState.Requesting, // outgoing only
                CallStatePending { IsCreated: true, IsReceived: false } => VoipState.Waiting, // outgoing only
                CallStatePending { IsCreated: true, IsReceived: true } => VoipState.Ringing,
                CallStateExchangingKeys => VoipState.Connecting,
                CallStateReady => VoipState.Ready,
                CallStateHangingUp => VoipState.HangingUp,
                CallStateDiscarded => VoipState.Discarded,
                CallStateError => VoipState.Error,
                _ => VoipState.None
            };
        }
    }
}
