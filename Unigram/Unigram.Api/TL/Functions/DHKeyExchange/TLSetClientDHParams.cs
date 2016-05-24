namespace Telegram.Api.TL.Functions.DHKeyExchange
{
    public class TLSetClientDHParams : TLObject
    {
        public const string Signature = "#f5045f1f";

        public TLInt128 Nonce { get; set; }

        public TLInt128 ServerNonce { get; set; }

        public TLString EncryptedData { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Nonce.ToBytes(),
                ServerNonce.ToBytes(),
                EncryptedData.ToBytes());
        }
    }
}
