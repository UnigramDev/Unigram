namespace Telegram.Api.TL
{
    public class TLPasswordSettings : TLObject
    {
        public const uint Signature = TLConstructors.TLPasswordSettings;

        public TLString Email { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Email = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }
}