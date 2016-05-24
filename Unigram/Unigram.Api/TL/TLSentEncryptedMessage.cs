namespace Telegram.Api.TL
{
    public class TLSentEncryptedMessage : TLObject
    {
        public const uint Signature = TLConstructors.TLSentEncryptedMessage;

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }
    }
}
