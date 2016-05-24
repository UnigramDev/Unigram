namespace Telegram.Api.TL
{
    public abstract class TLChatsBase : TLObject
    {
        public TLVector<TLChatBase> Chats { get; set; }
    }

    public class TLChats : TLChatsBase
    {
        public const uint Signature = TLConstructors.TLChats;

        public TLVector<TLUserBase> Users { get; set; } 

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }

    public class TLChats24 : TLChatsBase
    {
        public const uint Signature = TLConstructors.TLChats24;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);

            return this;
        }
    }
}
