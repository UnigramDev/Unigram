namespace Telegram.Api.TL.Functions.Channels
{
    class TLReadChannelHistory : TLObject
    {
        public const uint Signature = 0xcc104937;

        public TLInputChannelBase Channel { get; set; }

        public TLInt MaxId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                MaxId.ToBytes());
        }
    }
}
