namespace Telegram.Api.TL.Functions.Channels
{
    class TLToggleComments : TLObject
    {
        public const uint Signature = 0xaaa29e88;

        public TLInputChannelBase Channel { get; set; }

        public TLBool Enabled { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                Enabled.ToBytes());
        }
    }
}
