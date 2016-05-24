namespace Telegram.Api.TL
{
    public class TLClientDHInnerData : TLObject
    {
        public const string Signature = "#6643b654";

        public TLInt128 Nonce { get; set; }

        public TLInt128 ServerNonce { get; set; }

        public TLLong RetryId { get; set; }

        public TLString GB { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Nonce.ToBytes(),
                ServerNonce.ToBytes(),
                RetryId.ToBytes(),
                GB.ToBytes());
        }
    }
}
