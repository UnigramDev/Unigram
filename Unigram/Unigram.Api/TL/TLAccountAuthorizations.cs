namespace Telegram.Api.TL
{
    public class TLAccountAuthorizations : TLObject
    {
        public const uint Signature = TLConstructors.TLAccountAuthorizations;

        public TLVector<TLAccountAuthorization> Authorizations { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Authorizations = GetObject<TLVector<TLAccountAuthorization>>(bytes, ref position);

            return this;
        }
    }
}
