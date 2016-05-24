using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLDifferenceBase : TLObject
    {
        public abstract TLDifferenceBase GetEmptyObject();
    }

    public class TLDifferenceEmpty : TLDifferenceBase
    {
        public const uint Signature = TLConstructors.TLDifferenceEmpty;

        public TLInt Date { get; set; }

        public TLInt Seq { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Date = GetObject<TLInt>(bytes, ref position);
            Seq = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Date.ToStream(output);
            Seq.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Date = GetObject<TLInt>(input);
            Seq = GetObject<TLInt>(input);

            return this;
        }

        public override TLDifferenceBase GetEmptyObject()
        {
            return new TLDifferenceEmpty { Date = Date, Seq = Seq };
        }

        public override string ToString()
        {
            return string.Format("TLDifferenceEmpty date={0} seq={1}", Date, Seq);
        }
    }

    public class TLDifference : TLDifferenceBase
    {
        public const uint Signature = TLConstructors.TLDifference;

        public TLVector<TLMessageBase> NewMessages { get; set; }
        public TLVector<TLEncryptedMessageBase> NewEncryptedMessages { get; set; }
        public TLVector<TLUpdateBase> OtherUpdates { get; set; }
        public TLVector<TLUserBase> Users { get; set; }
        public TLVector<TLChatBase> Chats { get; set; }
        public TLState State { get; set; }

        public override TLDifferenceBase GetEmptyObject()
        {
            return new TLDifference
            {
                NewMessages = new TLVector<TLMessageBase>(NewMessages.Count),
                NewEncryptedMessages = new TLVector<TLEncryptedMessageBase>(NewEncryptedMessages.Count),
                OtherUpdates = new TLVector<TLUpdateBase>(OtherUpdates.Count),
                Users = new TLVector<TLUserBase>(Users.Count),
                Chats = new TLVector<TLChatBase>(Chats.Count),
                State = State
            };
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            NewMessages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            NewEncryptedMessages = GetObject<TLVector<TLEncryptedMessageBase>>(bytes, ref position);
            OtherUpdates = GetObject<TLVector<TLUpdateBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            State = GetObject<TLState>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            NewMessages.ToStream(output);
            NewEncryptedMessages.ToStream(output);
            OtherUpdates.ToStream(output);
            Chats.ToStream(output);
            Users.ToStream(output);
            State.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            NewMessages = GetObject<TLVector<TLMessageBase>>(input);
            NewEncryptedMessages = GetObject<TLVector<TLEncryptedMessageBase>>(input);
            OtherUpdates = GetObject<TLVector<TLUpdateBase>>(input);
            Chats = GetObject<TLVector<TLChatBase>>(input);
            Users = GetObject<TLVector<TLUserBase>>(input);
            State = GetObject<TLState>(input);

            return this;
        }

        public override string ToString()
        {
            return string.Format("TLDifference state=[{0}] messages_count={1}", State, NewMessages.Count);
        }
    }

    public class TLDifferenceSlice : TLDifference
    {
        public new const uint Signature = TLConstructors.TLDifferenceSlice;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            NewMessages = GetObject<TLVector<TLMessageBase>>(bytes, ref position);
            NewEncryptedMessages = GetObject<TLVector<TLEncryptedMessageBase>>(bytes, ref position);
            OtherUpdates = GetObject<TLVector<TLUpdateBase>>(bytes, ref position);
            Chats = GetObject<TLVector<TLChatBase>>(bytes, ref position);
            Users = GetObject<TLVector<TLUserBase>>(bytes, ref position);
            State = GetObject<TLState>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            NewMessages.ToStream(output);
            NewEncryptedMessages.ToStream(output);
            OtherUpdates.ToStream(output);
            Chats.ToStream(output);
            Users.ToStream(output);
            State.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            NewMessages = GetObject<TLVector<TLMessageBase>>(input);
            NewEncryptedMessages = GetObject<TLVector<TLEncryptedMessageBase>>(input);
            OtherUpdates = GetObject<TLVector<TLUpdateBase>>(input);
            Chats = GetObject<TLVector<TLChatBase>>(input);
            Users = GetObject<TLVector<TLUserBase>>(input);
            State = GetObject<TLState>(input);

            return this;
        }

        public override string ToString()
        {
            return string.Format("TLDifferenceSlice state=[{0}] messages_count={1}", State, NewMessages.Count);
        }
    }
}
