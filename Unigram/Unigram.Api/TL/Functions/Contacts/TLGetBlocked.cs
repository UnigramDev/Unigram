namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLGetBlocked : TLObject
    {
        public const string Signature = "#f57c350f";

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
