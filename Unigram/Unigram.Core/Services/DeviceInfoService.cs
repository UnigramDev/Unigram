using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.DeviceInfo;
using Windows.ApplicationModel;
using Windows.Networking.Connectivity;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System;
using Windows.System.Profile;

namespace Unigram.Core.Services
{
    interface IUserConfiguration
    {
        string DeviceModel { get; }
        string SystemVersion { get; }
        string AppVersion { get; }
        string Language { get; }
        //[propget] HRESULT ConfigurationPath([out][retval] HSTRING* value);
        //[propget] HRESULT LogPath([out][retval] HSTRING* value);
        int UserId { get; }
    }

    public class DeviceInfoService : IDeviceInfoService, IUserConfiguration
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

        public string DeviceModel
        {
            get
            {
                var info = new EasClientDeviceInformation();
                return string.IsNullOrWhiteSpace(info.SystemProductName) ? info.FriendlyName : info.SystemProductName;
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

        public string AppVersion
        {
            get
            {
                //return "4.7";

                var v = Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
        }

        public string Language => Windows.System.UserProfile.GlobalizationPreferences.Languages[0];

        public int UserId => SettingsHelper.UserId;

        public bool IsLowMemoryDevice
        {
            get
            {
                return MemoryManager.AppMemoryUsageLevel == Windows.System.AppMemoryUsageLevel.Low;
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
