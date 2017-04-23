using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.Transport;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Calls;

namespace Unigram.Tasks
{
    public sealed class VoIPCallTask : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private CallMediator _mediator;
        private VoipPhoneCall _systemCall;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnCanceled;

            _mediator = new CallMediator();

            var deviceInfoService = new DeviceInfoService();
            var eventAggregator = new TelegramEventAggregator();
            var cacheService = new InMemoryCacheService(eventAggregator);
            var updatesService = new UpdatesService(cacheService, eventAggregator);
            var transportService = new TransportService();
            var connectionService = new ConnectionService(deviceInfoService);
            var protoService = new MTProtoService(deviceInfoService, updatesService, cacheService, transportService, connectionService);

            eventAggregator.Subscribe(_mediator);
            protoService.Initialize();
            updatesService.LoadStateAndUpdate(() => { });

            var coordinator = VoipCallCoordinator.GetDefault();
            var call = coordinator.RequestNewIncomingCall("Unigram", "Lumia 435", "Lumia 435", null, "Unigram", null, "Unigram", null, VoipPhoneCallMedia.Audio, TimeSpan.FromSeconds(128));

            _systemCall = call;
            _systemCall.AnswerRequested += _systemCall_AnswerRequested;
        }

        private void _systemCall_AnswerRequested(VoipPhoneCall sender, CallAnswerEventArgs args)
        {
            throw new NotImplementedException();
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            _deferral.Complete();
        }
    }

    internal class CallMediator : IHandle<TLUpdatePhoneCall>, IHandle
    {
        public void Handle(TLUpdatePhoneCall message)
        {
            Debug.WriteLine(message.PhoneCall);
        }
    }
}
