namespace Telegram.Api.TL
{
    public class TLInviteText : TLObject
    {
        public const uint Signature = TLConstructors.TLInviteText;

        public TLString Message { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Message = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }
}
