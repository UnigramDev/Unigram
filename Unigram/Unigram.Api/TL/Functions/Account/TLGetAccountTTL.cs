namespace Telegram.Api.TL.Functions.Account
{
    public class TLGetAccountTTL : TLObject
    {
        public const string Signature = "#8fc711d";

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
        }
    }

    public class TLSetAccountTTL : TLObject
    {
        public const string Signature = "#2442485e";

        public TLAccountDaysTTL TTL { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                TTL.ToBytes());
        }
    }
}
