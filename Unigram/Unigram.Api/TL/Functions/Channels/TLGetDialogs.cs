namespace Telegram.Api.TL.Functions.Channels
{
    class TLGetDialogs : TLObject
    {
        public const uint Signature = 0xa9d3d249;

        public TLInt Offset { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Offset.ToBytes(),
                Limit.ToBytes());
        }
    }
}
