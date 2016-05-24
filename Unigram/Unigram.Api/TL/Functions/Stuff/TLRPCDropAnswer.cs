namespace Telegram.Api.TL.Functions.Stuff
{
    public class TLRPCDropAnswer : TLObject
    {
        public const string Signature = "#5e2ad36e";

        public TLLong ReqMsgId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                ReqMsgId.ToBytes());
        }
    }
}
