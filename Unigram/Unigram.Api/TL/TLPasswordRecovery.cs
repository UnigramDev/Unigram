namespace Telegram.Api.TL
{
    public class TLPasswordRecovery : TLObject
    {
        public const uint Signature = TLConstructors.TLPasswordRecovery;

        public TLString EmailPattern { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            EmailPattern = GetObject<TLString>(bytes, ref position);

            return this;
        }
    }
}