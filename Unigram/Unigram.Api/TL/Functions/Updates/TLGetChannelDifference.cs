namespace Telegram.Api.TL.Functions.Updates
{
    class TLGetChannelDifference : TLObject
    {
        public const uint Signature = 0xbb32d7c0;

        public TLInputChannelBase Channel { get; set; }

        public TLChannelMessagesFilerBase Filter { get; set; }

        public TLInt Pts { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Channel.ToBytes(),
                Filter.ToBytes(),
                Pts.ToBytes(),
                Limit.ToBytes());
        }
    }
}
