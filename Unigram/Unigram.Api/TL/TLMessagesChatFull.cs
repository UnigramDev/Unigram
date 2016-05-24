namespace Telegram.Api.TL
{
    public class TLMessagesChatFull : TLObject
    {
        public const uint Signature = TLConstructors.TLMessagesChatFull;

        public TLChatFull FullChat { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            FullChat = GetObject<TLChatFull>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }
    }
}
