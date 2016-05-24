using System;

namespace Telegram.Api.TL.Functions.Auth
{
    class TLSendCode : TLObject
    {
        public const string Signature = "#768d5f4d";

        public TLString PhoneNumber { get; set; }

        public TLSmsType SmsType { get; set; }

        public TLInt ApiId { get; set; }

        public TLString ApiHash { get; set; }

        public TLString LangCode { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PhoneNumber.ToBytes(),
                BitConverter.GetBytes((int)SmsType),
                ApiId.ToBytes(),
                ApiHash.ToBytes(),
                LangCode.ToBytes());
        }
    }

    public enum TLSmsType
    {
        Code = 0,
        AppName_Code = 1
    }
}
