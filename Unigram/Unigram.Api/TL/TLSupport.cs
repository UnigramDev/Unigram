namespace Telegram.Api.TL
{
    public class TLSupport : TLObject
    {
        public const uint Signature = TLConstructors.TLSupport;

        public TLString PhoneNumber { get; set; }

        public TLUserBase User { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhoneNumber = GetObject<TLString>(bytes, ref position);
            User = GetObject<TLUserBase>(bytes, ref position);

            return this;
        }
    }
}
