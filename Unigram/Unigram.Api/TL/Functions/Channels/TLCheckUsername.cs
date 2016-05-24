namespace Telegram.Api.TL.Functions.Channels
{
    class TLCheckUsername : TLObject
    {
        public const uint Signature = 0x10e6bd2c;

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
