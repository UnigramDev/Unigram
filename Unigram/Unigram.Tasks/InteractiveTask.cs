using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.DeviceInfo;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.Transport;
using Unigram.Core.Notifications;
using Windows.ApplicationModel.Background;
using Windows.UI.Notifications;

namespace Unigram.Tasks
{
    public sealed class InteractiveTask : IBackgroundTask
    {
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            var deferral = taskInstance.GetDeferral();

            var data = Toast.GetData(taskInstance);
            if (data != null)
            {
                if (data.ContainsKey("QuickMessage"))
                {
                    var deviceInfoService = new DeviceInfoService();
                    var eventAggregator = new TelegramEventAggregator();
                    var cacheService = new InMemoryCacheService(eventAggregator);
                    var updatesService = new UpdatesService(cacheService, eventAggregator);
                    var transportService = new TransportService();
                    var connectionService = new ConnectionService(deviceInfoService);
                    var manualResetEvent = new ManualResetEvent(false);
                    var protoService = new MTProtoService(deviceInfoService, updatesService, cacheService, transportService, connectionService);

                    protoService.Initialized += (s, args) =>
                    {
                        var text = data["QuickMessage"];

                        var replyToMsgId = 0;
                        var toId = default(TLPeerBase);
                        if (data.ContainsKey("from_id"))
                        {
                            toId = new TLPeerUser { Id = int.Parse(data["from_id"]) };
                        }
                        else if (data.ContainsKey("chat_id"))
                        {
                            toId = new TLPeerChat { Id = int.Parse(data["chat_id"]) };
                            replyToMsgId = -1;
                        }
                        else if (data.ContainsKey("channel_id"))
                        {
                            toId = new TLPeerChannel { Id = int.Parse(data["channel_id"]) };
                            replyToMsgId = -1;
                        }

                        if (data.ContainsKey("msg_id") && replyToMsgId == -1)
                        {
                            replyToMsgId = int.Parse(data["msg_id"]);
                        }
                        else
                        {
                            replyToMsgId = 0;
                        }

                        var date = TLUtils.DateToUniversalTimeTLInt(protoService.ClientTicksDelta, DateTime.Now);
                        var message = TLUtils.GetMessage(SettingsHelper.UserId, toId, TLMessageState.Sending, true, true, date, text, new TLMessageMediaEmpty(), TLLong.Random(), replyToMsgId);
                        var history = cacheService.GetHistory(SettingsHelper.UserId, toId, 1);

                        cacheService.SyncSendingMessage(message, null, toId, async (m) =>
                        {
                            await protoService.SendMessageAsync(message);
                            manualResetEvent.Set();
                        });
                    };
                    protoService.InitializationFailed += (s, args) =>
                    {
                        manualResetEvent.Set();
                    };
                    cacheService.Initialize();
                    protoService.Initialize();
                    manualResetEvent.WaitOne(15000);
                }
            }

            deferral.Complete();
        }
    }

    internal class DeviceInfoService : IDeviceInfoService
    {
        public string AppVersion { get { return "ciao"; } }

        public int BackgroundTaskId { get { return 0; } }

        public string BackgroundTaskName { get { return "ciao"; } }

        public bool IsBackground { get { return true; } }

        public string Model { get { return "ciao"; } }

        public string SystemVersion { get { return "ciao"; } }
    }
}
