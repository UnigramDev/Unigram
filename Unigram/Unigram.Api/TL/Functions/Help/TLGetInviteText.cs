namespace Telegram.Api.TL.Functions.Help
{
    public class TLGetInviteText : TLObject
    {
        public const string Signature = "#a4a95186";

        public TLString LangCode { get; set; } 

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                LangCode.ToBytes());
        }
    }
}
