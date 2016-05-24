using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Telegram.Api.Extensions;
using Telegram.Api.TL.Functions.Contacts;

namespace Telegram.Api.TL
{
    public abstract class TLUpdateBase : TLObject
    {
        public abstract IList<TLInt> GetPts();
    }

    public interface IMultiPts
    {
        TLInt Pts { get; set; }

        TLInt PtsCount { get; set; }
    }

    public interface IMultiChannelPts
    {
        TLInt Pts { get; set; }

        TLInt PtsCount { get; set; }
    }

    public class TLUpdateNewMessage : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateNewMessage;

        public TLMessageBase Message { get; set; }
        public TLInt Pts { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Message = GetObject<TLMessageBase>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Message.ToStream(output);
            Pts.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Message = GetObject<TLMessageBase>(input);
            Pts = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt> { Pts };
        }
    }

    public class TLUpdateNewMessage24 : TLUpdateNewMessage, IMultiPts
    {
        public new const uint Signature = TLConstructors.TLUpdateNewMessage24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Message = GetObject<TLMessageBase>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Message.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Message = GetObject<TLMessageBase>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public class TLUpdateChatParticipantAdd37 : TLUpdateChatParticipantAdd
    {
        public new const uint Signature = TLConstructors.TLUpdateChatParticipantAdd37;

        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            InviterId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
            UserId.ToStream(output);
            InviterId.ToStream(output);
            Date.ToStream(output);
            Version.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            UserId = GetObject<TLInt>(input);
            InviterId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Version = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChatParticipantAdd : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChatParticipantAdd;

        public TLInt ChatId { get; set; }
        public TLInt UserId { get; set; }
        public TLInt InviterId { get; set; }
        public TLInt Version { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            InviterId = GetObject<TLInt>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
            UserId.ToStream(output);
            InviterId.ToStream(output);
            Version.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            UserId = GetObject<TLInt>(input);
            InviterId = GetObject<TLInt>(input);
            Version = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChatParticipantDelete : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChatParticipantDelete;

        public TLInt ChatId { get; set; }
        public TLInt UserId { get; set; }
        public TLInt Version { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
            UserId.ToStream(output);
            Version.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            UserId = GetObject<TLInt>(input);
            Version = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateNewEncryptedMessage : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateNewEncryptedMessage;

        public TLEncryptedMessageBase Message { get; set; }
        public TLInt Qts { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);
            
            Message = GetObject<TLEncryptedMessageBase>(bytes, ref position);
            Qts = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Message.ToStream(output);
            Qts.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Message = GetObject<TLEncryptedMessageBase>(input);
            Qts = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateEncryption : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateEncryption;

        public TLEncryptedChatBase Chat { get; set; }
        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Chat = GetObject<TLEncryptedChatBase>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Chat.ToStream(output);
            Date.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Chat = GetObject<TLEncryptedChatBase>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateMessageId : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateMessageId;

        public TLInt Id { get; set; }
        public TLLong RandomId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            RandomId = GetObject<TLLong>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id.ToStream(output);
            RandomId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Id = GetObject<TLInt>(input);
            RandomId = GetObject<TLLong>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateReadMessages : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateReadMessages;

        public TLVector<TLInt> Messages { get; set; }
        public TLInt Pts { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLInt>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Messages.ToStream(output);
            Pts.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Messages = GetObject<TLVector<TLInt>>(input);
            Pts = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>{ Pts };
        }
    }

    public class TLUpdateReadMessages24 : TLUpdateReadMessages, IMultiPts
    {
        public new const uint Signature = TLConstructors.TLUpdateReadMessages24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLInt>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Messages.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Messages = GetObject<TLVector<TLInt>>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public class TLUpdateReadMessagesContents : TLUpdateReadMessages, IMultiPts
    {
        public new const uint Signature = TLConstructors.TLUpdateReadMessagesContents;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLInt>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Messages.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Messages = GetObject<TLVector<TLInt>>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public abstract class TLUpdateReadHistory : TLUpdateBase, IMultiPts
    {
        public TLPeerBase Peer { get; set; }

        public TLInt MaxId { get; set; }

        public TLInt Pts { get; set; }

        public TLInt PtsCount { get; set; }
    }

    public class TLUpdateReadHistoryInbox : TLUpdateReadHistory
    {
        public const uint Signature = TLConstructors.TLUpdateReadHistoryInbox;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLPeerBase>(bytes, ref position);
            MaxId = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);
            MaxId.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLPeerBase>(input);
            MaxId = GetObject<TLInt>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public class TLUpdateReadHistoryOutbox : TLUpdateReadHistory
    {
        public const uint Signature = TLConstructors.TLUpdateReadHistoryOutbox;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLPeerBase>(bytes, ref position);
            MaxId = GetObject<TLInt>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);
            MaxId.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLPeerBase>(input);
            MaxId = GetObject<TLInt>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public class TLUpdateEncryptedMessagesRead : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateEncryptedMessagesRead;

        public TLInt ChatId { get; set; }
        public TLInt MaxDate { get; set; }
        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            MaxDate = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
            MaxDate.ToStream(output);
            Date.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            MaxDate = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }

        public override string ToString()
        {
            return string.Format("{0} ChatId={1} MaxDate={2} Date={3}", GetType().Name, ChatId, MaxDate, Date);
        }
    }

    public class TLUpdateDeleteMessages : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateDeleteMessages;

        public TLVector<TLInt> Messages { get; set; }
        public TLInt Pts { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLInt>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Messages.ToStream(output);
            Pts.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Messages = GetObject<TLVector<TLInt>>(input);
            Pts = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt> { Pts };
        }
    }

    public class TLUpdateDeleteMessages24 : TLUpdateDeleteMessages, IMultiPts
    {
        public new const uint Signature = TLConstructors.TLUpdateDeleteMessages24;

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLInt>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Messages.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Messages = GetObject<TLVector<TLInt>>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public class TLUpdateRestoreMessages : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateRestoreMessages;

        public TLVector<TLInt> Messages { get; set; }
        public TLInt Pts { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Messages = GetObject<TLVector<TLInt>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Messages.ToStream(output);
            Pts.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Messages = GetObject<TLVector<TLInt>>(input);
            Pts = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt> { Pts };
        }
    }

    public interface IUserTypingAction
    {
        TLSendMessageActionBase Action { get; set; }
    }

    public abstract class TLUpdateTypingBase : TLUpdateBase
    {
        public TLInt UserId { get; set; }
    }

    public class TLUpdateUserTyping : TLUpdateTypingBase
    {
        public const uint Signature = TLConstructors.TLUpdateUserTyping;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateUserTyping17 : TLUpdateUserTyping, IUserTypingAction
    {
        public new const uint Signature = TLConstructors.TLUpdateUserTyping17;

        public TLSendMessageActionBase Action { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Action = GetObject<TLSendMessageActionBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            Action.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Action = GetObject<TLSendMessageActionBase>(input);

            return this;
        }
    }


    public class TLUpdateChatUserTyping : TLUpdateTypingBase
    {
        public const uint Signature = TLConstructors.TLUpdateChatUserTyping;

        public TLInt ChatId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
            UserId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            UserId = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChatUserTyping17 : TLUpdateChatUserTyping, IUserTypingAction
    {
        public new const uint Signature = TLConstructors.TLUpdateChatUserTyping17;

        public TLSendMessageActionBase Action { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            Action = GetObject<TLSendMessageActionBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
            UserId.ToStream(output);
            Action.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            UserId = GetObject<TLInt>(input);
            Action = GetObject<TLSendMessageActionBase>(input);

            return this;
        }
    }

    public class TLUpdateEncryptedChatTyping : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateEncryptedChatTyping;

        public TLInt ChatId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChatParticipants : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChatParticipants;

        public TLChatParticipantsBase Participants { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Participants = GetObject<TLChatParticipantsBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Participants.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Participants = GetObject<TLChatParticipantsBase>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateUserStatus : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateUserStatus;

        public TLInt UserId { get; set; }
        public TLUserStatus Status { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Status = GetObject<TLUserStatus>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            Status.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Status = GetObject<TLUserStatus>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateUserName : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateUserName;

        public TLInt UserId { get; set; }
        public TLString FirstName { get; set; }
        public TLString LastName { get; set; }
        public TLString UserName { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            FirstName = GetObject<TLString>(bytes, ref position);
            LastName = GetObject<TLString>(bytes, ref position);
            UserName = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            FirstName.ToStream(output);
            LastName.ToStream(output);
            UserName.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            FirstName = GetObject<TLString>(input);
            LastName = GetObject<TLString>(input);
            UserName = GetObject<TLString>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateUserPhoto : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateUserPhoto;

        public TLInt UserId { get; set; }

        public TLInt Date { get; set; }

        public TLPhotoBase Photo { get; set; }

        public TLBool Previous { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Photo = GetObject<TLPhotoBase>(bytes, ref position);
            Previous = GetObject<TLBool>(bytes, ref position);
            
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            Date.ToStream(output);
            Photo.ToStream(output);
            Previous.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);
            Photo = GetObject<TLPhotoBase>(input);
            Previous = GetObject<TLBool>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateContactRegistered : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateContactRegistered;

        public TLInt UserId { get; set; }
        public TLInt Date { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            Date.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Date = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public abstract class TLUpdateContactLinkBase : TLUpdateBase
    {
        public TLInt UserId { get; set; }
    }

    public class TLUpdateContactLink : TLUpdateContactLinkBase
    {
        public const uint Signature = TLConstructors.TLUpdateContactLink;

        public TLMyLinkBase MyLink { get; set; }
        public TLForeignLinkBase ForeignLink { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            MyLink = GetObject<TLMyLinkBase>(bytes, ref position);
            ForeignLink = GetObject<TLForeignLinkBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            MyLink.ToStream(output);
            ForeignLink.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            MyLink = GetObject<TLMyLinkBase>(input);
            ForeignLink = GetObject<TLForeignLinkBase>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateContactLink24 : TLUpdateContactLinkBase
    {
        public const uint Signature = TLConstructors.TLUpdateContactLink24;

        public TLContactLinkBase MyLink { get; set; }
        public TLContactLinkBase ForeignLink { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            MyLink = GetObject<TLContactLinkBase>(bytes, ref position);
            ForeignLink = GetObject<TLContactLinkBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            MyLink.ToStream(output);
            ForeignLink.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            MyLink = GetObject<TLContactLinkBase>(input);
            ForeignLink = GetObject<TLContactLinkBase>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateActivation : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateActivation;

        public TLInt UserId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateNewAuthorization : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateNewAuthorization;

        public TLLong AuthKeyId { get; set; }
        public TLInt Date { get; set; }
        public TLString Device { get; set; }
        public TLString Location { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            AuthKeyId = GetObject<TLLong>(bytes, ref position);
            Date = GetObject<TLInt>(bytes, ref position);
            Device = GetObject<TLString>(bytes, ref position);
            Location = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            AuthKeyId.ToStream(output);
            Date.ToStream(output);
            Device.ToStream(output);
            Location.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            AuthKeyId = GetObject<TLLong>(input);
            Date = GetObject<TLInt>(input);
            Device = GetObject<TLString>(input);
            Location = GetObject<TLString>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateDCOptions : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateDCOptions;

        public TLVector<TLDCOption> DCOptions { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            DCOptions = GetObject<TLVector<TLDCOption>>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            DCOptions.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            DCOptions = GetObject<TLVector<TLDCOption>>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateNotifySettings : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateNotifySettings;

        public TLNotifyPeerBase Peer { get; set; }

        public TLPeerNotifySettingsBase NotifySettings { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Peer = GetObject<TLNotifyPeerBase>(bytes, ref position);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Peer.ToStream(output);
            NotifySettings.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Peer = GetObject<TLNotifyPeerBase>(input);
            NotifySettings = GetObject<TLPeerNotifySettingsBase>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateUserBlocked : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateUserBlocked;

        public TLInt UserId { get; set; }

        public TLBool Blocked { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Blocked = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            Blocked.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Blocked = GetObject<TLBool>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdatePrivacy : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdatePrivacy;

        public TLPrivacyKeyBase Key { get; set; }

        public TLVector<TLPrivacyRuleBase> Rules { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Key = GetObject<TLPrivacyKeyBase>(bytes, ref position);
            Rules = GetObject<TLVector<TLPrivacyRuleBase>>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Key.ToStream(output);
            Rules.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Key = GetObject<TLPrivacyKeyBase>(input);
            Rules = GetObject<TLVector<TLPrivacyRuleBase>>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateUserPhone : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateUserPhone;

        public TLInt UserId { get; set; }

        public TLString Phone { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Phone = GetObject<TLString>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            UserId.ToStream(output);
            Phone.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Phone = GetObject<TLString>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateServiceNotification : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateServiceNotification;

        public TLString Type { get; set; }

        public TLString Message { get; set; }

        public TLMessageMediaBase Media { get; set; }

        public TLBool Popup { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Type = GetObject<TLString>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Media = GetObject<TLMessageMediaBase>(bytes, ref position);
            Popup = GetObject<TLBool>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Type.ToStream(output);
            Message.ToStream(output);
            Media.ToStream(output);
            Popup.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Type = GetObject<TLString>(input);
            Message = GetObject<TLString>(input);
            Media = GetObject<TLMessageMediaBase>(input);
            Popup = GetObject<TLBool>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateWebPage37 : TLUpdateWebPage, IMultiPts
    {
        public new const uint Signature = TLConstructors.TLUpdateWebPage37;

        public TLInt Pts { get; set; }

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            WebPage = GetObject<TLWebPageBase>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            WebPage.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            WebPage = GetObject<TLWebPageBase>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public class TLUpdateWebPage : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateWebPage;

        public TLWebPageBase WebPage { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            WebPage = GetObject<TLWebPageBase>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            WebPage.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            WebPage = GetObject<TLWebPageBase>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChannelTooLong : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChannelTooLong;

        public TLInt ChannelId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChannelId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChannelId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChannelId = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChannelGroup : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChannelGroup;

        public TLInt ChannelId { get; set; }

        public TLMessageGroup Group { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChannelId = GetObject<TLInt>(bytes, ref position);
            Group = GetObject<TLMessageGroup>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChannelId.ToStream(output);
            Group.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChannelId = GetObject<TLInt>(input);
            Group = GetObject<TLMessageGroup>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateNewChannelMessage : TLUpdateBase, IMultiChannelPts
    {
        public const uint Signature = TLConstructors.TLUpdateNewChannelMessage;

        public TLMessageBase Message { get; set; }

        public TLInt Pts { get; set; }

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Message = GetObject<TLMessageBase>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Message.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            Message = GetObject<TLMessageBase>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public class TLUpdateReadChannelInbox : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateReadChannelInbox;

        public TLInt ChannelId { get; set; }

        public TLInt MaxId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChannelId = GetObject<TLInt>(bytes, ref position);
            MaxId = GetObject<TLInt>(bytes, ref position);
            
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChannelId.ToStream(output);
            MaxId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChannelId = GetObject<TLInt>(input);
            MaxId = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateDeleteChannelMessages : TLUpdateBase, IMultiPts
    {
        public const uint Signature = TLConstructors.TLUpdateDeleteChannelMessages;

        public TLInt ChannelId { get; set; }

        public TLVector<TLInt> Messages { get; set; }

        public TLInt Pts { get; set; }

        public TLInt PtsCount { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChannelId = GetObject<TLInt>(bytes, ref position);
            Messages = GetObject<TLVector<TLInt>>(bytes, ref position);
            Pts = GetObject<TLInt>(bytes, ref position);
            PtsCount = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChannelId.ToStream(output);
            Messages.ToStream(output);
            Pts.ToStream(output);
            PtsCount.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChannelId = GetObject<TLInt>(input);
            Messages = GetObject<TLVector<TLInt>>(input);
            Pts = GetObject<TLInt>(input);
            PtsCount = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return TLUtils.GetPtsRange(Pts, PtsCount);
        }
    }

    public class TLUpdateChannelMessageViews : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChannelMessageViews;

        public TLInt ChannelId { get; set; }

        public TLInt Id { get; set; }

        public TLInt Views { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChannelId = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            Views = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChannelId.ToStream(output);
            Id.ToStream(output);
            Views.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChannelId = GetObject<TLInt>(input);
            Id = GetObject<TLInt>(input);
            Views = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChannel : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChannel;

        public TLInt ChannelId { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChannelId = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChannelId.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChannelId = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChatAdmins : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChatAdmins;

        public TLInt ChatId { get; set; }

        public TLBool Enabled { get; set; }

        public TLInt Version { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            Enabled = GetObject<TLBool>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
            Enabled.ToStream(output);
            Version.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            Enabled = GetObject<TLBool>(input);
            Version = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }

    public class TLUpdateChatParticipantAdmin : TLUpdateBase
    {
        public const uint Signature = TLConstructors.TLUpdateChatParticipantAdmin;

        public TLInt ChatId { get; set; }

        public TLInt UserId { get; set; }

        public TLBool IsAdmin { get; set; }

        public TLInt Version { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            ChatId = GetObject<TLInt>(bytes, ref position);
            UserId = GetObject<TLInt>(bytes, ref position);
            IsAdmin = GetObject<TLBool>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            ChatId.ToStream(output);
            UserId.ToStream(output);
            IsAdmin.ToStream(output);
            Version.ToStream(output);
        }

        public override TLObject FromStream(Stream input)
        {
            ChatId = GetObject<TLInt>(input);
            UserId = GetObject<TLInt>(input);
            IsAdmin = GetObject<TLBool>(input);
            Version = GetObject<TLInt>(input);

            return this;
        }

        public override IList<TLInt> GetPts()
        {
            return new List<TLInt>();
        }
    }
}
