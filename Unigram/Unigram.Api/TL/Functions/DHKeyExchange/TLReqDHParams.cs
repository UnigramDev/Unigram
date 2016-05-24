namespace Telegram.Api.TL.Functions.DHKeyExchange
{
    public class TLReqDHParams : TLObject
    {
        public const string Signature = "#d712e4be";

        public TLInt128 Nonce { get; set; }

        public TLInt128 ServerNonce { get; set; }

        public TLString P { get; set; }

        public TLString Q { get; set; }

        public TLLong PublicKeyFingerprint { get; set; }

        public TLString EncryptedData { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Nonce.ToBytes(),
                ServerNonce.ToBytes(),
                P.ToBytes(),
                Q.ToBytes(),
                PublicKeyFingerprint.ToBytes(),
                EncryptedData.ToBytes());
        }
    }
}
