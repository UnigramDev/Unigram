namespace Telegram.Api.TL.Functions.Messages
{
    public class TLGetDialogs : TLObject
    {
#if LAYER_40
        public const string Signature = "#859b3d3c";

        public TLInt Offset { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Limit.ToBytes());
        }
#else
        public const string Signature = "#eccf1df6";

        public TLInt Offset { get; set; }

        public TLInt MaxId { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                MaxId.ToBytes(),
                Limit.ToBytes());
        }
#endif
    }
}
