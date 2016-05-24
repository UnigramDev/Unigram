namespace Telegram.Api.TL.Functions.Channels
{
    class TLJoinChannel : TLObject
    {
        public const uint Signature = 0x24b524c5;

        public TLInputChannelBase Channel { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes());
        }
    }
}
