using System.Text;

namespace Telegram.Api.TL
{
    public class TLAccountAuthorization : TLObject
    {
        public const uint Signature = TLConstructors.TLAccountAuthorization;

        public TLLong Hash { get; set; }

        public TLInt Flags { get; set; }

        public TLString DeviceModel { get; set; }

        public TLString Platform { get; set; }

        public TLString SystemVersion { get; set; }

        public TLInt ApiId { get; set; }

        public TLString AppName { get; set; }

        public TLString AppVersion { get; set; }

        public TLInt DateCreated { get; set; }

        public TLInt DateActive { get; set; }

        public TLString Ip { get; set; }

        public TLString Country { get; set; }

        public TLString Region { get; set; }

        public string Location
        {
            get { return string.Format("{0} – {1}", Ip, Country); }
        }

        public string AppFullName
        {
            get { return string.Format("{0} {1}", AppName, AppVersion); }
        }

        public string DeviceFullName
        {
            get
            {
                var name = new StringBuilder();
                name.Append(DeviceModel);
                if (!TLString.IsNullOrEmpty(Platform))
                {
                    name.Append(string.Format(", {0}", Platform));
                }
                if (!TLString.IsNullOrEmpty(SystemVersion))
                {
                    name.Append(string.Format(" {0}", SystemVersion));
                }

                return name.ToString();
            }
        }

        public bool IsCurrent
        {
            get { return IsSet(Flags, 1); }
        }

        public bool IsOfficialApp
        {
            get { return IsSet(Flags, 2); }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Hash = GetObject<TLLong>(bytes, ref position);
            Flags = GetObject<TLInt>(bytes, ref position);
            DeviceModel = GetObject<TLString>(bytes, ref position);
            Platform = GetObject<TLString>(bytes, ref position);
            SystemVersion = GetObject<TLString>(bytes, ref position);
            ApiId = GetObject<TLInt>(bytes, ref position);
            AppName = GetObject<TLString>(bytes, ref position);
            AppVersion = GetObject<TLString>(bytes, ref position);
            DateCreated = GetObject<TLInt>(bytes, ref position);
            DateActive = GetObject<TLInt>(bytes, ref position);
            Ip = GetObject<TLString>(bytes, ref position);
            Country = GetObject<TLString>(bytes, ref position);
            Region = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public void Update(TLAccountAuthorization authorization)
        {
            Hash = authorization.Hash;
            Flags = authorization.Flags;
            DeviceModel = authorization.DeviceModel;
            Platform = authorization.Platform;
            SystemVersion = authorization.SystemVersion;
            ApiId = authorization.ApiId;
            AppName = authorization.AppName;
            AppVersion = authorization.AppVersion;
            DateCreated = authorization.DateCreated;
            DateActive = authorization.DateActive;
            Ip = authorization.Ip;
            Country = authorization.Country;
            Region = authorization.Region;
        }
    }
}
