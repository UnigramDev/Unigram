namespace Telegram.Api.TL
{
    public class TLPQInnerData : TLObject
    {
        public const uint Signature = TLConstructors.TLPQInnerData;

        public TLString PQ { get; set; }

        public TLString P { get; set; }

        public TLString Q { get; set; }

        public TLInt128 Nonce   { get; set; }

        public TLInt128 ServerNonce { get; set; }

        public TLInt256 NewNonce { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                PQ.ToBytes(),
                P.ToBytes(),
                Q.ToBytes(),
                Nonce.ToBytes(),
                ServerNonce.ToBytes(),
                NewNonce.ToBytes());
        }
    }
}
