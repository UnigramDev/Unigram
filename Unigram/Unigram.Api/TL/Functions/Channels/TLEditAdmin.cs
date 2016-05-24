namespace Telegram.Api.TL.Functions.Channels
{
    class TLEditAdmin : TLObject
    {
        public const uint Signature = 0x52b16962;

        public TLInputChannelBase Channel { get; set; }

        public TLInputUserBase UserId { get; set; }

        public TLChannelParticipantRoleBase Role { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                UserId.ToBytes(),
                Role.ToBytes());
        }
    }
}
