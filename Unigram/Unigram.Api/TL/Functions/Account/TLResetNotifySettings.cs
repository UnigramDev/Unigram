namespace Telegram.Api.TL.Functions.Account
{
    public class TLResetNotifySettings : TLObject
    {
        public const string Signature = "#db7e1747";

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
        }
    }
}
