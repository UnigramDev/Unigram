//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using Telegram.Navigation.Services;
using Telegram.Services.Calls;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public interface IVoipService
    {
        VoipCallBase ActiveCall { get; }

        void StartPrivateCall(INavigationService navigation, Chat chat, bool video);
        void StartPrivateCall(INavigationService navigation, User user, bool video);

        void JoinGroupCall(INavigationService navigation, InputGroupCall groupCall);
        void JoinGroupCall(INavigationService navigation, long chatId, string inviteHash = null);

        void CreateGroupCall(INavigationService navigation, IList<long> userIds);
        void CreateGroupCall(INavigationService navigation, long chatId);
    }

    public partial class VoipService : ServiceBase, IVoipService
    {
        public VoipService(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Aggregator.Subscribe<UpdateCall>(this, Handle)
                .Subscribe<UpdateNewCallSignalingData>(Handle)
                .Subscribe<UpdateGroupCall>(Handle)
                .Subscribe<UpdateGroupCallParticipant>(Handle)
                .Subscribe<UpdateGroupCallVerificationState>(Handle)
                .Subscribe<UpdateGroupCallMessageSendFailed>(Handle)
                .Subscribe<UpdateGroupCallMessagesDeleted>(Handle)
                .Subscribe<UpdateNewGroupCallMessage>(Handle)
                .Subscribe<UpdateNewGroupCallPaidReaction>(Handle)
                .Subscribe<UpdateLiveStoryTopDonors>(Handle);
        }

        public VoipCallBase ActiveCall => LifetimeService.Current.Voip.ActiveCall;

        #region Private

        public void StartPrivateCall(INavigationService navigation, Chat chat, bool video)
        {
            LifetimeService.Current.Voip.StartPrivateCall(ClientService, navigation, chat, video);
        }

        public void StartPrivateCall(INavigationService navigation, User user, bool video)
        {
            LifetimeService.Current.Voip.StartPrivateCall(ClientService, navigation, user, video);
        }

        #endregion

        #region Group

        public void JoinGroupCall(INavigationService navigation, InputGroupCall groupCall)
        {
            LifetimeService.Current.Voip.JoinGroupCall(ClientService, navigation, groupCall);
        }

        public void CreateGroupCall(INavigationService navigation, IList<long> userIds)
        {
            LifetimeService.Current.Voip.CreateGroupCall(ClientService, navigation, userIds);
        }

        public void JoinGroupCall(INavigationService navigation, long chatId, string inviteHash)
        {
            LifetimeService.Current.Voip.JoinGroupCall(ClientService, navigation, chatId, inviteHash);
        }

        public void CreateGroupCall(INavigationService navigation, long chatId)
        {
            LifetimeService.Current.Voip.CreateGroupCall(ClientService, navigation, chatId);
        }

        #endregion

        public void Handle(UpdateNewCallSignalingData update)
        {
            LifetimeService.Current.Voip.Handle(ClientService, update);
        }

        public void Handle(UpdateCall update)
        {
            LifetimeService.Current.Voip.Handle(ClientService, update);
        }

        public void Handle(UpdateGroupCall update)
        {
            if (LifetimeService.Current.Voip.Handle(ClientService, update))
            {
                Aggregator.Publish(update, EventType.GroupCall, update.GroupCall.Id);
            }
        }

        public void Handle(UpdateGroupCallParticipant update)
        {
            if (LifetimeService.Current.Voip.Handle(ClientService, update))
            {
                Aggregator.Publish(update, EventType.GroupCall, update.GroupCallId);
            }
        }

        public void Handle(UpdateGroupCallVerificationState update)
        {
            if (LifetimeService.Current.Voip.Handle(ClientService, update))
            {
                Aggregator.Publish(update, EventType.GroupCall, update.GroupCallId);
            }
        }

        public void Handle(UpdateGroupCallMessageSendFailed update)
        {
            if (LifetimeService.Current.Voip.Handle(ClientService, update))
            {
                Aggregator.Publish(update, EventType.GroupCall, update.GroupCallId);
            }
        }

        public void Handle(UpdateGroupCallMessagesDeleted update)
        {
            if (LifetimeService.Current.Voip.Handle(ClientService, update))
            {
                Aggregator.Publish(update, EventType.GroupCall, update.GroupCallId);
            }
        }

        public void Handle(UpdateNewGroupCallMessage update)
        {
            if (LifetimeService.Current.Voip.Handle(ClientService, update))
            {
                Aggregator.Publish(update, EventType.GroupCall, update.GroupCallId);
            }
        }

        public void Handle(UpdateNewGroupCallPaidReaction update)
        {
            if (LifetimeService.Current.Voip.Handle(ClientService, update))
            {
                Aggregator.Publish(update, EventType.GroupCall, update.GroupCallId);
            }
        }

        public void Handle(UpdateLiveStoryTopDonors update)
        {
            Aggregator.Publish(update, EventType.GroupCall, update.GroupCallId);
        }
    }
}
