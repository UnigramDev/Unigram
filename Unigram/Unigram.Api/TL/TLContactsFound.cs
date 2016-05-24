namespace Telegram.Api.TL
{
    public abstract class TLContactsFoundBase : TLObject
    {
        public TLVector<TLUserBase> Users { get; set; }
    }

    public class TLContactsFound : TLContactsFoundBase
    {
        public const uint Signature = TLConstructors.TLContactsFound;

        public TLVector<TLContactFound> Results { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Results = GetObject<TLVector<TLContactFound>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }

    public class TLContactsFound40 : TLContactsFoundBase
    {
        public const uint Signature = TLConstructors.TLContactsFound40;

        public TLVector<TLPeerBase> Results { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Results = GetObject<TLVector<TLPeerBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }
}
