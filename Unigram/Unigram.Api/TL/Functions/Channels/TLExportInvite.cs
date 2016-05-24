namespace Telegram.Api.TL.Functions.Channels
{
    class TLExportInvite : TLObject
    {
        public const uint Signature = 0xc7560885;

        public TLInputChannelBase Channel { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes());
        }
    }
}