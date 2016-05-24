namespace Telegram.Api.TL
{
    public class TLAuthorization : TLObject
    {
        public const uint Signature = TLConstructors.TLAuthorization;

        public TLInt Expires { get; set; }

        public TLUserBase User { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Expires = GetObject<TLInt>(bytes, ref position);
            User = GetObject<TLUserBase>(bytes, ref position);

            return this;
        }
    }

    public class TLAuthorization31 : TLAuthorization
    {
        public new const uint Signature = TLConstructors.TLAuthorization31;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            User = GetObject<TLUserBase>(bytes, ref position);

            return this;
        }
    }
}
