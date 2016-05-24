namespace Telegram.Api.TL
{
    public abstract class TLCheckedPhoneBase : TLObject
    {
        public TLBool PhoneRegistered { get; set; }
    }

    public class TLCheckedPhone : TLCheckedPhoneBase
    {
        public const uint Signature = TLConstructors.TLCheckedPhone;

        public TLBool PhoneInvited { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhoneRegistered = GetObject<TLBool>(bytes, ref position);
            PhoneInvited = GetObject<TLBool>(bytes, ref position);

            return this;
        }
    }

    public class TLCheckedPhone24 : TLCheckedPhoneBase
    {
        public const uint Signature = TLConstructors.TLCheckedPhone24;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            PhoneRegistered = GetObject<TLBool>(bytes, ref position);

            return this;
        }
    }
}
