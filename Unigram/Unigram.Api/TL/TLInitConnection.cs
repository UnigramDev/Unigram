using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public class TLInitConnection : TLObject
    {
        public const uint Signature = 0x69796de9;

        public TLInt AppId { get; set; }

        public TLString DeviceModel { get; set; }

        public TLString SystemVersion { get; set; }

        public TLString AppVersion { get; set; }

        public TLString LangCode { get; set; }

        public TLObject Data { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                AppId.ToBytes(),
                DeviceModel.ToBytes(),
                SystemVersion.ToBytes(),
                AppVersion.ToBytes(),
                LangCode.ToBytes(),
                Data.ToBytes());
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            AppId.ToStream(output);
            DeviceModel.ToStream(output);
            SystemVersion.ToStream(output);
            AppVersion.ToStream(output);
            LangCode.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            AppId = GetObject<TLInt>(input);
            DeviceModel = GetObject<TLString>(input);
            SystemVersion = GetObject<TLString>(input);
            AppVersion = GetObject<TLString>(input);
            LangCode = GetObject<TLString>(input);

            return this;
        }

        public override string ToString()
        {
            return string.Format("app_id={0} device_model={1} system_version={2} app_version={3} lang_code={4}", AppId, DeviceModel, SystemVersion, AppVersion, LangCode);
        }
    }
}
