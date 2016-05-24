namespace Telegram.Api.TL.Functions.Channels
{
    class TLLeaveChannel : TLObject
    {
        public const uint Signature = 0xf836aa95;

        public TLInputChannelBase Channel { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes());
        }
    }
}