namespace Telegram.Api.TL.Functions.Messages
{
#if LAYER_41
    class TLDeleteHistory : TLObject
    {
        public const uint Signature = 0xb7c13bd9;

        public TLInputPeerBase Peer { get; set; }

        public TLInt MaxId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                MaxId.ToBytes());
        }
    }
#else
    class TLDeleteHistory : TLObject
    {
        public const string Signature = "#f4f8fb61";

        public TLInputPeerBase Peer { get; set; }

        public TLInt Offset { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Peer.ToBytes(),
                Offset.ToBytes());
        }
    }
#endif

}
