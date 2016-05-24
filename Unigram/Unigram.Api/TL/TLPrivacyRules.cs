namespace Telegram.Api.TL
{
    public class TLPrivacyRules : TLObject
    {
        public const uint Signature = TLConstructors.TLPrivacyRules;

        public TLVector<TLPrivacyRuleBase> Rules { get; set; }

        public TLVector<TLUserBase> Users { get; set; } 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Rules = GetObject<TLVector<TLPrivacyRuleBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }
}
