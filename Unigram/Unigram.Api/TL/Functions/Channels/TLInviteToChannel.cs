namespace Telegram.Api.TL.Functions.Channels
{
    class TLInviteToChannel : TLObject
    {
        public const uint Signature = 0x199f3a6c;

        public TLInputChannelBase Channel { get; set; }

        public TLVector<TLInputUserBase> Users { get; set; } 

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                Users.ToBytes());
        }
    }
}