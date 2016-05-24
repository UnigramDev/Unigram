namespace Telegram.Api.TL.Functions.Messages
{
    public class TLGetHistory : TLObject
    {
#if LAYER_40
        public const string Signature = "#8a8ec2da";

        public TLInputPeerBase Peer { get; set; }

        public TLInt OffsetId { get; set; }

        public TLInt AddOffset { get; set; }

        public TLInt Limit { get; set; }

        public TLInt MaxId { get; set; }
        
        public TLInt MinId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                OffsetId.ToBytes(),
                AddOffset.ToBytes(),
                Limit.ToBytes(),
                MaxId.ToBytes(),
                MinId.ToBytes());
        }
#else
        public const string Signature = "#92a1df2f";

        public TLInputPeerBase Peer { get; set; }

        public TLInt Offset { get; set; }

        public TLInt MaxId { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                Offset.ToBytes(),
                MaxId.ToBytes(),
                Limit.ToBytes());
        }
#endif
    }
}
