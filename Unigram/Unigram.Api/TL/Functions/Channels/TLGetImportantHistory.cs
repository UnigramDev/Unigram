namespace Telegram.Api.TL.Functions.Channels
{
    class TLGetImportantHistory : TLObject
    {
        public const uint Signature = 0xddb929cb;

        public TLInputChannelBase Channel { get; set; }

        public TLInt OffsetId { get; set; }

        public TLInt AddOffset { get; set; }

        public TLInt Limit { get; set; }

        public TLInt MaxId { get; set; }

        public TLInt MinId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                OffsetId.ToBytes(),
                AddOffset.ToBytes(),
                Limit.ToBytes(),
                MaxId.ToBytes(),
                MinId.ToBytes());
        }
    }
}
