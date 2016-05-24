namespace Telegram.Api.TL.Functions.Updates
{
    public class TLGetDifference : TLObject
    {
        public const string Signature = "#a041495";

        public TLInt Pts { get; set; }

        public TLInt Date { get; set; }

        public TLInt Qts { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Pts.ToBytes(),
                Date.ToBytes(),
                Qts.ToBytes());
        }
    }
}
