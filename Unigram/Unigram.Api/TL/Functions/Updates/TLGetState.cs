namespace Telegram.Api.TL.Functions.Updates
{
    public class TLGetState : TLObject
    {
        public const string Signature = "#edd4882a";

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }
}
