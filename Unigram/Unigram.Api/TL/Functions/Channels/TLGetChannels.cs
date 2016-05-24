namespace Telegram.Api.TL.Functions.Channels
{
    class TLGetChannels : TLObject
    {
        public const uint Signature = 0xa7f6bbb;

        public TLVector<TLInputChannelBase> Id { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Id.ToBytes());
        }
    }
}
