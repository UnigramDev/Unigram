namespace Telegram.Api.TL.Account
{
    class TLRequestPasswordRecovery : TLObject
    {
        public const string Signature = "#d897bc66";

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
        }
    }
}
