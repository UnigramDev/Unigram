namespace Telegram.Api.TL.Functions.Help
{
    public class TLGetConfig : TLObject
    {
        public const string Signature = "#c4f9186b";

        public override byte[] ToBytes()
        {
            return TLUtils.SignatureToBytes(Signature);
        }
    }
}
