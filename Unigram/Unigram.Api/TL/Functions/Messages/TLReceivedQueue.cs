namespace Telegram.Api.TL.Functions.Messages
{
    public class TLReceivedQueue : TLObject
    {
        public const string Signature = "#55a5bb66";

        public TLLong MaxQts { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                MaxQts.ToBytes());
        }
    }
}