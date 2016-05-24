namespace Telegram.Api.TL
{
    public class TLSentEncryptedFile : TLObject
    {
        public const uint Signature = TLConstructors.TLSentEncryptedFile;

        public TLInt Date { get; set; }

        public TLEncryptedFileBase EncryptedFile { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);
            EncryptedFile = GetObject<TLEncryptedFileBase>(bytes, ref position);

            return this;
        }
    }
}
