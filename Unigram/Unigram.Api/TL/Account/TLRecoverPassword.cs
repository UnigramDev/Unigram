namespace Telegram.Api.TL.Account
{
    class TLRecoverPassword : TLObject
    {
        public const string Signature = "#4ea56e92";

        public TLString Code { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Code.ToBytes());
        }
    }
}
