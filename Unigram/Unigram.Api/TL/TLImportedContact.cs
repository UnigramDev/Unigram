namespace Telegram.Api.TL
{
    public class TLImportedContact : TLObject
    {
        public const uint Signature = TLConstructors.TLImportedContact;

        public TLInt UserId { get; set; }

        public TLLong ClientId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLImportedContact--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            ClientId = GetObject<TLLong>(bytes, ref position);

            return this;
        }
    }
}
