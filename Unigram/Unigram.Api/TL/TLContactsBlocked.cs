namespace Telegram.Api.TL
{
    public abstract class TLContactsBlockedBase : TLObject { }

    public class TLContactsBlocked : TLContactsBlockedBase
    {
        public const uint Signature = TLConstructors.TLContactsBlocked;

        public TLVector<TLContactBlocked> Blocked { get; set; }
        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLContactsBlocked--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Blocked = GetObject<TLVector<TLContactBlocked>>(bytes, ref position);

            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }

    public class TLContactsBlockedSlice : TLContactsBlocked
    {
        public new const uint Signature = TLConstructors.TLContactsBlockedSlice;

        public TLInt Count { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            TLUtils.WriteLine("--Parse TLContactsBlocked--");
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Count = GetObject<TLInt>(bytes, ref position);
            Blocked = GetObject<TLVector<TLContactBlocked>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }
}
