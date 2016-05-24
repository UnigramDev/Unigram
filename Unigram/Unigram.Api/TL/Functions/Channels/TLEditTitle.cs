namespace Telegram.Api.TL.Functions.Channels
{
    class TLEditTitle : TLObject
    {
        public const uint Signature = 0x566decd0;

        public TLInputChannelBase Channel { get; set; }

        public TLString Title { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                Title.ToBytes());
        }
    }
}
