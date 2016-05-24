namespace Telegram.Api.TL.Functions.Channels
{
    class TLUpdateUsername : TLObject
    {
        public const uint Signature = 0x3514b3de;

        public TLInputChannelBase Channel { get; set; }

        public TLString Username { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                Username.ToBytes());
        }
    }
}
