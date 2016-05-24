namespace Telegram.Api.TL.Functions.Channels
{
    class TLGetParticipants : TLObject
    {
        public const uint Signature = 0x24d98f92;

        public TLInputChannelBase Channel { get; set; }

        public TLChannelParticipantsFilterBase Filter { get; set; }

        public TLInt Offset { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                Filter.ToBytes(),
                Offset.ToBytes(),
                Limit.ToBytes());
        }
    }
}
