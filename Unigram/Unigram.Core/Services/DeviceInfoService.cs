using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Services.DeviceInfo;
using Windows.ApplicationModel;
using Windows.Networking.Connectivity;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System;
using Windows.System.Profile;

namespace Unigram.Core.Services
{
    public class DeviceInfoService : IDeviceInfoService, IExtendedDeviceInfoService
    {
        public bool IsBackground
        {
            get
            {
                return false;
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

        public string Model
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

        public bool IsLowMemoryDevice
        {
            get
            {
                return MemoryManager.AppMemoryUsageLevel == Windows.System.AppMemoryUsageLevel.Low;
            }
        }

        public bool IsWiFiEnabled
        {
            get
            {
                return NetworkInformation.GetInternetConnectionProfile().IsWlanConnectionProfile;
            }
        }

        public bool IsCellularDataEnabled
        {
            get
            {
                return NetworkInformation.GetInternetConnectionProfile().IsWwanConnectionProfile;
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
            {"rm923", "Lumia505"},
            {"rm898", "Lumia510"},
            {"rm889", "Lumia510"},
            {"rm915", "Lumia520"},
            {"rm917", "Lumia521"},
            {"rm998", "Lumia525"},
            {"rm997", "Lumia526"},
            {"rm1017", "Lumia530"},
            {"rm1018", "Lumia530"},
            {"rm1019", "Lumia530"},
            {"rm1020", "Lumia530"},
            {"rm1090", "Lumia535"},
            {"rm836", "Lumia610"},
            {"rm849", "Lumia610"},
            {"rm846", "Lumia620"},
            {"rm941", "Lumia625"},
            {"rm942", "Lumia625"},
            {"rm943", "Lumia625"},
            {"rm974", "Lumia630"},
            {"rm976", "Lumia630"},
            {"rm977", "Lumia630"},
            {"rm978", "Lumia630"},
            {"rm975", "Lumia635"},
            {"rm803", "Lumia710"},
            {"rm809", "Lumia710"},
            {"rm885", "Lumia720"},
            {"rm887", "Lumia720"},
            {"rm1038", "Lumia730"},
            {"rm801", "Lumia800"},
            {"rm802", "Lumia800"},
            {"rm819", "Lumia800"},
            {"rm878", "Lumia810"},
            {"rm824", "Lumia820"},
            {"rm825", "Lumia820"},
            {"rm826", "Lumia820"},
            {"rm845", "Lumia822"},
            {"rm983", "Lumia830"},
            {"rm984", "Lumia830"},
            {"rm985", "Lumia830"},
            {"rm808", "Lumia900"},
            {"rm823", "Lumia900"},
            {"rm820", "Lumia920"},
            {"rm821", "Lumia920"},
            {"rm822", "Lumia920"},
            {"rm867", "Lumia920"},
            {"rm892", "Lumia925"},
            {"rm893", "Lumia925"},
            {"rm910", "Lumia925"},
            {"rm955", "Lumia925"},
            {"rm860", "Lumia928"},
            {"rm1045", "Lumia930"},
            {"rm875", "Lumia1020"},
            {"rm876", "Lumia1020"},
            {"rm877", "Lumia1020"},
            {"rm994", "Lumia1320"},
            {"rm995", "Lumia1320"},
            {"rm996", "Lumia1320"},
            {"rm937", "Lumia1520"},
            {"rm938", "Lumia1520"},
            {"rm939", "Lumia1520"},
            {"rm940", "Lumia1520"},
            {"rm927", "LumiaIcon"},

        };
    }
}
