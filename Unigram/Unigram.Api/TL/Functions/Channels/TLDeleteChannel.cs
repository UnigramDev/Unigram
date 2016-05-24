namespace Telegram.Api.TL.Functions.Channels
{
    class TLDeleteChannel : TLObject
    {
        public const uint Signature = 0xc0111fe3;

        public TLInputChannelBase Channel { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes());
        }
    }
}