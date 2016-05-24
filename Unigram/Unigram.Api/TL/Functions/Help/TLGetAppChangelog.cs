namespace Telegram.Api.TL.Functions.Help
{
    public class TLGetAppChangelog : TLObject
    {
        public const string Signature = "#5bab7fb2";

        public TLString DeviceModel { get; set; }

        public TLString SystemVersion { get; set; }

        public TLString AppVersion { get; set; }

        public TLString LangCode { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                DeviceModel.ToBytes(),
                SystemVersion.ToBytes(),
                AppVersion.ToBytes(),
                LangCode.ToBytes());
        }
    }
}
