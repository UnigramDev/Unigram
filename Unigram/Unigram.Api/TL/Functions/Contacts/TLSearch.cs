namespace Telegram.Api.TL.Functions.Contacts
{
    public class TLSearch : TLObject
    {
        public const string Signature = "#11f812d8";

        public TLString Q { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Q.ToBytes(),
                Limit.ToBytes());
        }
    }
}
