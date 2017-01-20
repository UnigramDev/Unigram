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
using Telegram.Api.TL.Methods.Messages;
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
                        var inputPeer = default(TLInputPeerBase);
                        if (data.ContainsKey("from_id"))
                        {
                            inputPeer = new TLInputPeerUser { UserId = int.Parse(data["from_id"]), AccessHash = long.Parse(data["access_hash"]) };
                        }
                        else if (data.ContainsKey("channel_id"))
                        {
                            inputPeer = new TLInputPeerChannel { ChannelId = int.Parse(data["channel_id"]), AccessHash = long.Parse(data["access_hash"]) };
                            replyToMsgId = data.ContainsKey("msg_id") ? int.Parse(data["msg_id"]) : 0;
                        }
                        else if (data.ContainsKey("chat_id"))
                        {
                            inputPeer = new TLInputPeerChat { ChatId = int.Parse(data["chat_id"]) };
                            replyToMsgId = data.ContainsKey("msg_id") ? int.Parse(data["msg_id"]) : 0;
                        }

                        var obj = new TLMessagesSendMessage { Peer = inputPeer, ReplyToMsgId = replyToMsgId, Message = text, IsBackground = true, RandomId = TLLong.Random() };

                        protoService.SendInformativeMessageInternal<TLUpdatesBase>("messages.sendMessage", obj, result =>
                        {
                            manualResetEvent.Set();
                        },
                        faultCallback: fault =>
                        {
                            // TODO: alert user?
                            manualResetEvent.Set();
                        },
                        fastCallback: () =>
                        {
                            manualResetEvent.Set();
                        });

                        //var date = TLUtils.DateToUniversalTimeTLInt(protoService.ClientTicksDelta, DateTime.Now);
                        //var message = TLUtils.GetMessage(SettingsHelper.UserId, inputPeer, TLMessageState.Sending, true, true, date, text, new TLMessageMediaEmpty(), TLLong.Random(), replyToMsgId);
                        //var history = cacheService.GetHistory(inputPeer, 1);

                        //cacheService.SyncSendingMessage(message, null, async (m) =>
                        //{
                        //    await protoService.SendMessageAsync(message, () => 
                        //    {
                        //        // TODO: fast callback
                        //    });
                        //    manualResetEvent.Set();
                        //});
                    };
                    protoService.InitializationFailed += (s, args) =>
                    {
                        manualResetEvent.Set();
                    };

                    //cacheService.Init();
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
