namespace Telegram.Api.TL.Functions.Help
{
    public class TLInvokeWithoutUpdates : TLObject
    {
        public const string Signature = "#bf9459b7";

        public TLObject Object { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Object.ToBytes());
        }
    }
}
