namespace Telegram.Api.TL.Functions.Channels
{
    class TLEditAbout : TLObject
    {
        public const uint Signature = 0x13e27f1e;

        public TLInputChannelBase Channel { get; set; }

        public TLString About { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                About.ToBytes());
        }
    }
}
