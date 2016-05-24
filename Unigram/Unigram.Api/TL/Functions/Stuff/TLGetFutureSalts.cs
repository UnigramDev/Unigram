namespace Telegram.Api.TL.Functions.Stuff
{
    public class TLGetFutureSalts : TLObject
    {
        public const string Signature = "#b921bd04";

        public TLInt Num { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Num.ToBytes());
        }
    }
}
