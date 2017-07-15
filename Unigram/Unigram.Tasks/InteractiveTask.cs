using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Native;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.Connection;
using Telegram.Api.Services.DeviceInfo;
using Telegram.Api.Services.Updates;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages.Methods;
using Telegram.Api.TL.Methods;
using Unigram.Core.Notifications;
using Unigram.Core.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System;
using Windows.System.Profile;
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
                    var manualResetEvent = new ManualResetEvent(false);

                    var text = data["QuickMessage"];
                    var messageText = text.Replace("\r\n", "\n").Replace('\v', '\n').Replace('\r', '\n');
                    var entitiesBase = Utils.GetEntities(ref messageText);

                    var replyToMsgId = new int?();
                    var inputPeer = default(TLInputPeerBase);
                    if (data.ContainsKey("from_id"))
                    {
                        inputPeer = new TLInputPeerUser { UserId = int.Parse(data["from_id"]), AccessHash = long.Parse(data["access_hash"]) };
                    }
                    else if (data.ContainsKey("channel_id"))
                    {
                        inputPeer = new TLInputPeerChannel { ChannelId = int.Parse(data["channel_id"]), AccessHash = long.Parse(data["access_hash"]) };
                        replyToMsgId = data.ContainsKey("msg_id") ? int.Parse(data["msg_id"]) : new int?();
                    }
                    else if (data.ContainsKey("chat_id"))
                    {
                        inputPeer = new TLInputPeerChat { ChatId = int.Parse(data["chat_id"]) };
                        replyToMsgId = data.ContainsKey("msg_id") ? int.Parse(data["msg_id"]) : new int?();
                    }

                    TLVector<TLMessageEntityBase> entities = null;
                    if (entitiesBase != null)
                    {
                        entities = new TLVector<TLMessageEntityBase>(entitiesBase);
                    }

                    var obj = new TLMessagesSendMessage { Peer = inputPeer, ReplyToMsgId = replyToMsgId, Message = messageText, Entities = entities, IsBackground = true, RandomId = TLLong.Random() };

                    ConnectionManager.Instance.UserId = SettingsHelper.UserId;
                    ConnectionManager.Instance.SendRequest(new TLInvokeWithoutUpdates { Query = obj }, (message, ex) =>
                    {
                        manualResetEvent.Set();
                    },
                    () =>
                    {
                        manualResetEvent.Set();
                    },
                    ConnectionManager.DefaultDatacenterId, ConnectionType.Generic, RequestFlag.CanCompress | RequestFlag.FailOnServerError | RequestFlag.RequiresQuickAck | RequestFlag.Immediate);

                    manualResetEvent.WaitOne(15000);
                }
            }

            deferral.Complete();
        }
    }

    internal class DeviceInfoService : IDeviceInfoService
    {
        public bool IsBackground
        {
            get
            {
                return true;
            }
        }

        public string BackgroundTaskName
        {
            get
            {
                return string.Empty;
            }
        }

        public int BackgroundTaskId
        {
            get
            {
                return default(int);
            }
        }

        public string DeviceModel
        {
            get
            {
                var info = new EasClientDeviceInformation();
                return string.IsNullOrWhiteSpace(info.SystemProductName) ? info.FriendlyName : info.SystemProductName;
            }
        }

        public string AppVersion
        {
            get
            {
                var v = Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
        }

        public string SystemVersion
        {
            get
            {
                string deviceFamilyVersion = AnalyticsInfo.VersionInfo.DeviceFamilyVersion;
                ulong version = ulong.Parse(deviceFamilyVersion);
                ulong major = (version & 0xFFFF000000000000L) >> 48;
                ulong minor = (version & 0x0000FFFF00000000L) >> 32;
                ulong build = (version & 0x00000000FFFF0000L) >> 16;
                ulong revision = version & 0x000000000000FFFFL;
                return $"{major}.{minor}.{build}.{revision}";
            }
        }

        private static string GetShortModel(string phoneCode)
        {
            var cleanCode = phoneCode.Replace("-", string.Empty).ToLowerInvariant();

            foreach (var model in models)
            {
                if (cleanCode.StartsWith(model.Key))
                {
                    return model.Value;
                }
            }

            return null;
        }

        private static readonly Dictionary<string, string> models = new Dictionary<string, string>
        {
            { "rm915", "Lumia 520" },
            { "rm917", "Lumia 521" },
            { "rm998", "Lumia 525" },
            { "rm997", "Lumia 526" },
            { "rm1017", "Lumia 530" },
            { "rm1018", "Lumia 530" },
            { "rm1019", "Lumia 530" },
            { "rm1020", "Lumia 530" },
            { "rm1090", "Lumia 535" },
            { "rm846", "Lumia 620" },
            { "rm941", "Lumia 625" },
            { "rm942", "Lumia 625" },
            { "rm943", "Lumia 625" },
            { "rm974", "Lumia 630" },
            { "rm976", "Lumia 630" },
            { "rm977", "Lumia 630" },
            { "rm978", "Lumia 630" },
            { "rm975", "Lumia 635" },
            { "rm885", "Lumia 720" },
            { "rm887", "Lumia 720" },
            { "rm1038", "Lumia 730" },
            { "rm878", "Lumia 810" },
            { "rm824", "Lumia 820" },
            { "rm825", "Lumia 820" },
            { "rm826", "Lumia 820" },
            { "rm845", "Lumia 822" },
            { "rm983", "Lumia 830" },
            { "rm984", "Lumia 830" },
            { "rm985", "Lumia 830" },
            { "rm820", "Lumia 920" },
            { "rm821", "Lumia 920" },
            { "rm822", "Lumia 920" },
            { "rm867", "Lumia 920" },
            { "rm892", "Lumia 925" },
            { "rm893", "Lumia 925" },
            { "rm910", "Lumia 925" },
            { "rm955", "Lumia 925" },
            { "rm860", "Lumia 928" },
            { "rm1045", "Lumia 930" },
            { "rm875", "Lumia 1020" },
            { "rm876", "Lumia 1020" },
            { "rm877", "Lumia 1020" },
            { "rm994", "Lumia 1320" },
            { "rm995", "Lumia 1320" },
            { "rm996", "Lumia 1320" },
            { "rm937", "Lumia 1520" },
            { "rm938", "Lumia 1520" },
            { "rm939", "Lumia 1520" },
            { "rm940", "Lumia 1520" },
            { "rm927", "Lumia Icon" },
        };
    }
}
