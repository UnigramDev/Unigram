namespace Telegram.Api.TL.Functions.Channels
{
    class TLKickFromChannel : TLObject
    {
        public const uint Signature = 0xa672de14;

        public TLInputChannelBase Channel { get; set; }

        public TLInputUserBase UserId { get; set; }

        public TLBool Kicked { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                UserId.ToBytes(),
                Kicked.ToBytes());
        }
    }
}