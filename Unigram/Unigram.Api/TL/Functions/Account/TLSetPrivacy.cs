namespace Telegram.Api.TL.Functions.Account
{
    public class TLSetPrivacy : TLObject
    {
        public const string Signature = "#c9f81ce8";

        public TLInputPrivacyKeyBase Key { get; set; }

        public TLVector<TLInputPrivacyRuleBase> Rules { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                Key.ToBytes(),
                Rules.ToBytes());
        }
    }
}