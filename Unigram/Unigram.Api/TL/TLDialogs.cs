namespace Telegram.Api.TL
{
    public abstract class TLDialogsBase : TLObject
    {
        public TLVector<TLDialogBase> Dialogs { get; set; }

        public TLVector<TLMessageBase> Messages { get; set; }

        public TLVector<TLChatBase> Chats { get; set; }

        public TLVector<TLUserBase> Users { get; set; }

        public abstract TLDialogsBase GetEmptyObject();
    }

    public class TLDialogs : TLDialogsBase
    {
        public const uint Signature = TLConstructors.TLDialogs;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Dialogs = GetObject<TLVector<TLDialogBase>>(bytes, ref position);
            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLDialogsBase GetEmptyObject()
        {
            return new TLDialogs
            {
                Dialogs = new TLVector<TLDialogBase>(Dialogs.Count),
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }

    public class TLDialogsSlice : TLDialogsBase
    {
        public const uint Signature = TLConstructors.TLDialogsSlice;

        public TLInt Count { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Count = GetObject<TLInt>(bytes, ref position);
            Dialogs = GetObject<TLVector<TLDialogBase>>(bytes, ref position);
            Messages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);

            return this;
        }

        public override TLDialogsBase GetEmptyObject()
        {
            return new TLDialogsSlice
            {
                Count = Count,
                Dialogs = new TLVector<TLDialogBase>(Dialogs.Count),
                Messages = new TLVector<TLMessageBase>(Messages.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                Users = new TLVector<TLUserBase>(Users.Count)
            };
        }
    }
}
