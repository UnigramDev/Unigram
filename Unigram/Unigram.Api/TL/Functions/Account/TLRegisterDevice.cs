namespace Telegram.Api.TL.Account
{
    public class TLRegisterDevice : TLObject
    {
        public const string Signature = "#446c712c";

        public TLInt TokenType { get; set; }

        public TLString Token { get; set; }

        public TLString DeviceModel { get; set; }

        public TLString SystemVersion { get; set; }

        public TLString AppVersion { get; set; }

        public TLBool AppSandbox { get; set; }

        public TLString LangCode { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                TokenType.ToBytes(),
                Token.ToBytes(),
                DeviceModel.ToBytes(),
                SystemVersion.ToBytes(),
                AppVersion.ToBytes(),
                AppSandbox.ToBytes(),
                LangCode.ToBytes());
        }

        public override string ToString()
        {
            return string.Format("token_type={0} token={1} device_model={2} system_version={3} app_version={4} app_sandbox={5} lang_code={6}", TokenType, Token, DeviceModel, SystemVersion, AppVersion, AppSandbox, LangCode);
        }
    }
}
