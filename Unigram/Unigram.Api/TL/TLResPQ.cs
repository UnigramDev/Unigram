namespace Telegram.Api.TL
{
    public class TLResPQ : TLObject
    {
        public const uint Signature = TLConstructors.TLResPQ;

        public TLInt128 Nonce { get; set; }

        public TLInt128 ServerNonce { get; set; }

        public TLString PQ { get; set; }

        public TLVector<TLLong> ServerPublicKeyFingerprints { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Nonce = GetObject<TLInt128>(bytes, ref position);
            ServerNonce = GetObject<TLInt128>(bytes, ref position);
            PQ = GetObject<TLString>(bytes, ref position);
            ServerPublicKeyFingerprints = GetObject<TLVector<TLLong>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Nonce.ToBytes(),
                ServerNonce.ToBytes(),
                PQ.ToBytes(),
                ServerPublicKeyFingerprints.ToBytes());
        }
    }
}
