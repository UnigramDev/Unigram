using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Native.TL;
using Windows.ApplicationModel;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;

namespace Telegram.Api.Native.Test
{

    class UserConfiguration : IUserConfiguration
    {
        #region properties
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
                var v = Package.Current.Id.Version;
                return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
            }
        }

        public string Language => Windows.System.UserProfile.GlobalizationPreferences.Languages[0];

        public string DeviceModel
        {
            get
            {
                var info = new EasClientDeviceInformation();
                return string.IsNullOrWhiteSpace(info.SystemProductName) ? info.FriendlyName : info.SystemProductName;
            }
        }
        #endregion

    }


    public class TLTestObject : ITLObject
    {
        public string TestString
        {
            get;
            set;
        } = "Bello di padella";

        public void Read(TLBinaryReader reader)
        {
            TestString = reader.ReadString();
        }

        public void Write(TLBinaryWriter writer)
        {
            writer.WriteString(TestString);
        }

        public bool IsLayerRequired => false;

        public uint Constructor => 0;
    }

}
