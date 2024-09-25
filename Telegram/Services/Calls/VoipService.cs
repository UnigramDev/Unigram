using System.Diagnostics;
using Telegram.Common;
using Telegram.Native.Calls;
using Telegram.Navigation.Services;
using Telegram.Services.Calls;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;

namespace Telegram.Services
{
    public interface IVoipService
    {
        VoipCall ActiveCall { get; }

        void Start(INavigationService navigation, Chat chat, bool video);
        void Start(INavigationService navigation, User user, bool video);
    }

    public partial class VoipService : ServiceBase, IVoipService
    {
        private readonly object _activeLock = new();
        private VoipCall _activeCall;

        public VoipService(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            MediaDeviceCoordinator.Start();

            Aggregator.Subscribe<UpdateCall>(this, Handle)
                .Subscribe<UpdateNewCallSignalingData>(Handle);
        }

        public VoipCall ActiveCall
        {
            get
            {
                lock (_activeLock)
                {
                    return _activeCall;
                }
            }
        }

        public void Start(INavigationService navigation, Chat chat, bool video)
        {
            if (chat == null)
            {
                return;
            }

            if (ClientService.TryGetUser(chat, out User user))
            {
                Start(navigation, user, video);
            }
        }

        public async void Start(INavigationService navigation, User user, bool video)
        {
            if (MediaDeviceWatcher.IsUnsupported(navigation.XamlRoot))
            {
                return;
            }

            if (user == null)
            {
                return;
            }

            VoipCall activeCall;
            lock (_activeLock)
            {
                activeCall = _activeCall;
            }

            if (activeCall != null)
            {
                if (activeCall.UserId != user.Id && ClientService.TryGetUser(activeCall.UserId, out User activeUser))
                {
                    var confirm = await navigation.ShowPopupAsync(string.Format(Strings.VoipOngoingAlert, activeUser.FullName(), user.FullName()), Strings.VoipOngoingAlertTitle, Strings.OK, Strings.Cancel);
                    if (confirm == ContentDialogResult.Primary)
                    {
                        activeCall.Discard();
                    }
                }
                else
                {
                    activeCall.Show();
                    return;
                }
            }

            var fullInfo = ClientService.GetUserFull(user.Id);
            if (fullInfo != null && fullInfo.HasPrivateCalls)
            {
                await navigation.ShowPopupAsync(string.Format(Strings.CallNotAvailable, user.FirstName), Strings.VoipFailed, Strings.OK);
                return;
            }

            var permissions = await MediaDeviceWatcher.CheckAccessAsync(navigation.XamlRoot, video, false);
            if (permissions == false)
            {
                return;
            }

            var protocol = VoipManager.Protocol;

            var response = await ClientService.SendAsync(new CreateCall(user.Id, protocol, video));
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

        public void Handle(UpdateNewCallSignalingData update)
        {
            lock (_activeLock)
            {
                if (_activeCall.Id == update.CallId)
                {
                    _activeCall.ReceiveSignalingData(update.Data);
                }
            }
        }

        public void Handle(UpdateCall update)
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
                        ClientService.Send(new DiscardCall(update.Call.Id, true, 0, false, 0));
                    }
                    else
                    {
                        _activeCall = new VoipCall(ClientService, Settings, Aggregator, update.Call, state);
                        changed = true;
                    }
                }
                else if (_activeCall?.Id == update.Call.Id)
                {
                    _activeCall.Update(update.Call, state);

                    if (state is VoipState.Discarded or VoipState.Error)
                    {
                        _activeCall = null;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                Aggregator.Publish(new UpdateActiveCall());
            }
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
