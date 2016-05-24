namespace Telegram.Api.TL.Functions.Channels
{
    class TLGetParticipant : TLObject
    {
        public const uint Signature = 0x546dd7a6;

        public TLInputChannelBase Channel { get; set; }

        public TLInputUserBase UserId { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                UserId.ToBytes());
        }
    }
}
