namespace Telegram.Api.TL.Functions.DHKeyExchange
{
    public class TLReqPQ : TLObject
    {
        public const string Signature = "#60469778";

        public TLInt128 Nonce { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Nonce.Value);
        }
    }
}
