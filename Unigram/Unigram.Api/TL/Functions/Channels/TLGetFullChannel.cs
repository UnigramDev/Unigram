namespace Telegram.Api.TL.Functions.Channels
{
    class TLGetFullChannel : TLObject
    {
        public const uint Signature = 0x08736a09;

        public TLInputChannelBase Channel { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes());
        }
    }
}
