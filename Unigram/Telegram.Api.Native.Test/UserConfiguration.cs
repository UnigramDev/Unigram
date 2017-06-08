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

    //public class TLAuthSendCode : ITLObject
    //{
    //    [Flags]
    //    public enum Flag : Int32
    //    {
    //        AllowFlashcall = (1 << 0),
    //        CurrentNumber = (1 << 0),
    //    }

    //    public Flag Flags
    //    {
    //        get;
    //        set;

    //    }

    //    public String PhoneNumber
    //    {
    //        get;
    //        set;
    //    }

    //    public Boolean? CurrentNumber
    //    {
    //        get;
    //        set;
    //    }
    //    public Int32 ApiId
    //    {
    //        get;
    //        set;
    //    }

    //    public String ApiHash
    //    {
    //        get;
    //        set;
    //    }

    //    public void Read(TLBinaryReader reader)
    //    {
    //        Flags = (Flag)reader.ReadInt32();
    //        PhoneNumber = reader.ReadString();

    //        if ((Flags & Flag.CurrentNumber) == Flag.CurrentNumber)
    //            CurrentNumber = reader.ReadBoolean();

    //        ApiId = reader.ReadInt32();
    //        ApiHash = reader.ReadString();
    //    }

    //    public void Write(TLBinaryWriter writer)
    //    {
    //        writer.WriteInt32((Int32)Flags);
    //        writer.WriteString(PhoneNumber);

    //        if ((Flags & Flag.CurrentNumber) == Flag.CurrentNumber)
    //            writer.WriteBoolean(CurrentNumber.Value);

    //        writer.WriteInt32(ApiId);
    //        writer.WriteString(ApiHash);
    //    }

    //    public bool IsLayerRequired => true;

    //    public uint Constructor => 0x86AEF0EC;
    //}

}
