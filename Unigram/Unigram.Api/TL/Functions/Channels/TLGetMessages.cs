namespace Telegram.Api.TL.Functions.Channels
{
    class TLGetMessages : TLObject
    {
        public const uint Signature = 0x93d7b347;

        public TLInputChannelBase Channel { get; set; }

        public TLVector<TLInt> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                Id.ToBytes());
        }
    }
}
