namespace Telegram.Api.TL.Functions.Account
{
    public class TLGetPrivacy : TLObject
    {
        public const string Signature = "#dadbc950";

        public TLInputPrivacyKeyBase Key { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Key.ToBytes());
        }
    }
}
