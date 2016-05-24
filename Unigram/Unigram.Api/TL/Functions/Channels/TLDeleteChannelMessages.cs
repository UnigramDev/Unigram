namespace Telegram.Api.TL.Functions.Channels
{
    class TLDeleteChannelMessages : TLObject
    {
        public const uint Signature = 0x84c1fd4e;

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
