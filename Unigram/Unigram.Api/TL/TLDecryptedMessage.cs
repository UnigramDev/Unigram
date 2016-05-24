using System;
using System.IO;
using System.Windows;
#if WIN_RT
using Windows.UI.Xaml;
#endif
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services;

namespace Telegram.Api.TL
{
    public abstract class TLDecryptedMessageBase : TLObject
    {
        public TLDecryptedMessageBase Self { get { return this; } }

        public TLLong RandomId { get; set; }

        public long RandomIndex
        {
            get { return RandomId != null ? RandomId.Value : 0; }
            set { RandomId = new TLLong(value); }
        }

        public TLString RandomBytes { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            RandomId = GetObject<TLLong>(bytes, ref position);
            RandomBytes = GetObject<TLString>(bytes, ref position);

            return this;
        }

        #region Additional 
        public TLInt ChatId { get; set; }
        public TLInputEncryptedFileBase InputFile { get; set; }     // to send media

        public TLInt FromId { get; set; }
        public TLBool Out { get; set; }
        public TLBool Unread { get; set; }
        
        public TLInt Date { get; set; }
        public int DateIndex
        {
            get { return Date != null ? Date.Value : 0; }
            set { Date = new TLInt(value); }
        }

        public TLInt Qts { get; set; }
        public int QtsIndex
        {
            get { return Qts != null ? Qts.Value : 0; }
            set { Qts = new TLInt(value); }
        }

        public TLLong DeleteDate { get; set; }
        public long DeleteIndex
        {
            get { return DeleteDate != null ? DeleteDate.Value : 0; }
            set { DeleteDate = new TLLong(value); }
        }

        public MessageStatus Status { get; set; }

        public virtual bool ShowFrom
        {
            get { return false; }
        }

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetField(ref _isSelected, value, () => IsSelected); }
        }

        public abstract Visibility SelectionVisibility { get; }

        public TLInt TTL { get; set; }

        private bool _isTTLStarted;

        public bool IsTTLStarted
        {
            get { return _isTTLStarted; }
            set { SetField(ref _isTTLStarted, value, () => IsTTLStarted); }
        }

        public abstract Visibility SecretPhotoMenuVisibility { get; }

        public abstract Visibility MessageVisibility { get; }

        #endregion

        public virtual void Update(TLDecryptedMessageBase message)
        {
            ChatId = message.ChatId ?? ChatId;
            InputFile = message.InputFile ?? InputFile;
            FromId = message.FromId ?? FromId;
            Out = message.Out ?? Out;
            Unread = message.Unread ?? Unread;
            Date = message.Date ?? Date;
            Qts = message.Qts ?? Qts;
            DeleteDate = message.DeleteDate ?? DeleteDate;
            Status = message.Status;
            TTL = message.TTL ?? TTL;
        }

        public virtual bool IsSticker()
        {
            return false;
        }

        public static bool IsSticker(TLDecryptedMessageMediaExternalDocument document)
        {
#if WP8
            if (document != null
                && document.Size.Value > 0
                && document.Size.Value < Constants.StickerMaxSize)
            {
                //var documentStickerAttribute = document22.Attributes.FirstOrDefault(x => x is TLDocumentAttributeSticker);

                if (//documentStickerAttribute != null
                    //&& 
                    string.Equals(document.MimeType.ToString(), "image/webp", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(document.FileExt, "webp", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
#endif

            return false;
        }


    }

    public interface ISeqNo
    {
        TLInt InSeqNo { get; set; }

        TLInt OutSeqNo { get; set; }
    }

    public class TLDecryptedMessage17 : TLDecryptedMessage, ISeqNo
    {
        public new const uint Signature = TLConstructors.TLDecryptedMessage17;

        public TLInt InSeqNo { get; set; }

        public TLInt OutSeqNo { get; set; }

        public TLInt Flags { get; set; }

        public override Visibility SecretPhotoMenuVisibility
        {
            get
            {
                var isSecretPhoto = Media is TLDecryptedMessageMediaPhoto;
                return isSecretPhoto && TTL.Value > 0.0 && TTL.Value <= 60.0 ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            RandomId = GetObject<TLLong>(bytes, ref position);
            TTL = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            Media = GetObject<TLDecryptedMessageMediaBase>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                RandomId.ToBytes(),
                TTL.ToBytes(),
                Message.ToBytes(),
                Media.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            RandomId = GetObject<TLLong>(input);
            TTL = GetObject<TLInt>(input);
            //RandomBytes = GetObject<TLString>(input);
            Message = GetObject<TLString>(input);
            Media = GetObject<TLDecryptedMessageMediaBase>(input);

            ChatId = GetNullableObject<TLInt>(input);
            InputFile = GetNullableObject<TLInputEncryptedFileBase>(input);
            FromId = GetNullableObject<TLInt>(input);
            Out = GetNullableObject<TLBool>(input);
            Unread = GetNullableObject<TLBool>(input);
            Date = GetNullableObject<TLInt>(input);
            DeleteDate = GetNullableObject<TLLong>(input);
            Qts = GetNullableObject<TLInt>(input);

            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            InSeqNo = GetNullableObject<TLInt>(input);
            OutSeqNo = GetNullableObject<TLInt>(input);
            Flags = GetNullableObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(RandomId.ToBytes());
            output.Write(TTL.ToBytes());
            //output.Write(RandomBytes.ToBytes());
            output.Write(Message.ToBytes());
            Media.ToStream(output);

            ChatId.NullableToStream(output);
            InputFile.NullableToStream(output);
            FromId.NullableToStream(output);
            Out.NullableToStream(output);
            Unread.NullableToStream(output);
            Date.NullableToStream(output);
            DeleteDate.NullableToStream(output);
            Qts.NullableToStream(output);

            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());

            InSeqNo.NullableToStream(output);
            OutSeqNo.NullableToStream(output);
            Flags.NullableToStream(output);
        }

        public override string ToString()
        {
            return string.Format("InSeqNo={0} OutSeqNo={1} ", InSeqNo, OutSeqNo) + base.ToString();
        }
    }

    public class TLDecryptedMessage : TLDecryptedMessageBase, IMessage
    {
        public const uint Signature = TLConstructors.TLDecryptedMessage;

        public TLString Message { get; set; }

        public TLDecryptedMessageMediaBase Media { get; set; }

        public override bool IsSticker()
        {
            var mediaDocument = Media as TLDecryptedMessageMediaExternalDocument;
            if (mediaDocument != null)
            {
                return IsSticker(mediaDocument);
            }

            return false;
        }

        public override Visibility MessageVisibility
        {
            get { return Message == null || string.IsNullOrEmpty(Message.ToString()) ? Visibility.Collapsed : Visibility.Visible; }
        }

        public override Visibility SelectionVisibility
        {
            get { return Visibility.Visible; }
        }

        public override Visibility SecretPhotoMenuVisibility
        {
            get { return Visibility.Visible; }
        }

        public override string ToString()
        {
            string dateTimeString = null;
            try
            {
                var clientDelta = MTProtoService.Instance.ClientTicksDelta;
                var utc0SecsLong = Date.Value * 4294967296 - clientDelta;
                var utc0SecsInt = utc0SecsLong / 4294967296.0;
                DateTime? dateTime = Helpers.Utils.UnixTimestampToDateTime(utc0SecsInt);
                dateTimeString = dateTime.Value.ToString("H:mm:ss");
            }
            catch (Exception ex)
            {

            }

            if (Media is TLDecryptedMessageMediaEmpty)
            {
                return string.Format("Qts={0} [{1} {2}] DeleteIndex={3} TTL={4} U={5} S={6} {7}", QtsIndex, DateIndex, dateTimeString, DeleteIndex, TTL, Unread, Status, Message);
            }

            return string.Format("Qts={0} [{1} {2}] DeleteIndex={3} TTL={4} U={5} S={6} {7}", QtsIndex, DateIndex, dateTimeString, DeleteIndex, TTL, Unread, Status, Media);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            base.FromBytes(bytes, ref position);

            Message = GetObject<TLString>(bytes, ref position);
            Media = GetObject<TLDecryptedMessageMediaBase>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                RandomId.ToBytes(),
                RandomBytes.ToBytes(),
                Message.ToBytes(),
                Media.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            RandomId = GetObject<TLLong>(input);
            RandomBytes = GetObject<TLString>(input);
            Message = GetObject<TLString>(input);
            Media = GetObject<TLDecryptedMessageMediaBase>(input);

            ChatId = GetNullableObject<TLInt>(input);
            InputFile = GetNullableObject<TLInputEncryptedFileBase>(input);
            FromId = GetNullableObject<TLInt>(input);
            Out = GetNullableObject<TLBool>(input);
            Unread = GetNullableObject<TLBool>(input);
            Date = GetNullableObject<TLInt>(input);
            DeleteDate = GetNullableObject<TLLong>(input);
            Qts = GetNullableObject<TLInt>(input);

            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(RandomId.ToBytes());
            output.Write(RandomBytes.ToBytes());
            output.Write(Message.ToBytes());
            Media.ToStream(output);

            ChatId.NullableToStream(output);
            InputFile.NullableToStream(output);
            FromId.NullableToStream(output);
            Out.NullableToStream(output);
            Unread.NullableToStream(output);
            Date.NullableToStream(output);
            DeleteDate.NullableToStream(output);
            Qts.NullableToStream(output);

            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());
        }

        public TLDecryptedMessageBase ToDecryptedMessage17(TLInt inSeqNo, TLInt outSeqNo, TLInt ttl)
        {
            var decryptedMessage17 = new TLDecryptedMessage17();

            decryptedMessage17.RandomId = RandomId;
            decryptedMessage17.RandomBytes = RandomBytes;
            decryptedMessage17.Message = Message;
            decryptedMessage17.Media = Media;

            decryptedMessage17.ChatId = ChatId;
            decryptedMessage17.InputFile = InputFile;
            decryptedMessage17.FromId = FromId;
            decryptedMessage17.Out = Out;
            decryptedMessage17.Unread = Unread;
            decryptedMessage17.Date = Date;
            decryptedMessage17.DeleteDate = DeleteDate;
            decryptedMessage17.Qts = Qts;
            decryptedMessage17.Status = Status;

            decryptedMessage17.TTL = ttl;
            decryptedMessage17.InSeqNo = inSeqNo;
            decryptedMessage17.OutSeqNo = outSeqNo;

            return decryptedMessage17;
        }
    }

    public interface IMessage
    {
        TLString Message { get; set; }
    }

    public class TLDecryptedMessageService17 : TLDecryptedMessageService, ISeqNo
    {
        public new const uint Signature = TLConstructors.TLDecryptedMessageService17;

        public TLInt InSeqNo { get; set; }

        public TLInt OutSeqNo { get; set; }

        public TLInt Flags { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            RandomId = GetObject<TLLong>(bytes, ref position);
            Action = GetObject<TLDecryptedMessageActionBase>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                RandomId.ToBytes(),
                //RandomBytes.ToBytes(),
                Action.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            RandomId = GetObject<TLLong>(input);
            //RandomBytes = GetObject<TLString>(input);
            Action = GetObject<TLDecryptedMessageActionBase>(input);

            ChatId = GetNullableObject<TLInt>(input);
            FromId = GetNullableObject<TLInt>(input);
            Out = GetNullableObject<TLBool>(input);
            Unread = GetNullableObject<TLBool>(input);
            Date = GetNullableObject<TLInt>(input);
            DeleteDate = GetNullableObject<TLLong>(input);
            Qts = GetNullableObject<TLInt>(input);

            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            InSeqNo = GetNullableObject<TLInt>(input);
            OutSeqNo = GetNullableObject<TLInt>(input);
            Flags = GetNullableObject<TLInt>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(RandomId.ToBytes());
            //output.Write(RandomBytes.ToBytes());
            output.Write(Action.ToBytes());

            ChatId.NullableToStream(output);
            FromId.NullableToStream(output);
            Out.NullableToStream(output);
            Unread.NullableToStream(output);
            Date.NullableToStream(output);
            DeleteDate.NullableToStream(output);
            Qts.NullableToStream(output);

            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());

            InSeqNo.NullableToStream(output);
            OutSeqNo.NullableToStream(output);
            Flags.NullableToStream(output);
        }

        public override string ToString()
        {
            return string.Format("InSeqNo={0} OutSeqNo={1} ", InSeqNo, OutSeqNo) + base.ToString();
        }
    }

    public class TLDecryptedMessageService : TLDecryptedMessageBase
    {
        public const uint Signature = TLConstructors.TLDecryptedMessageService;

        public TLDecryptedMessageActionBase Action { get; set; }

        public override Visibility SelectionVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override Visibility SecretPhotoMenuVisibility
        {
            get { return Visibility.Visible; }
        }

        public override Visibility MessageVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            base.FromBytes(bytes, ref position);

            Action = GetObject<TLDecryptedMessageActionBase>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                RandomId.ToBytes(),
                RandomBytes.ToBytes(),
                Action.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            RandomId = GetObject<TLLong>(input);
            RandomBytes = GetObject<TLString>(input);
            Action = GetObject<TLDecryptedMessageActionBase>(input);

            ChatId = GetNullableObject<TLInt>(input);
            FromId = GetNullableObject<TLInt>(input);
            Out = GetNullableObject<TLBool>(input);
            Unread = GetNullableObject<TLBool>(input);
            Date = GetNullableObject<TLInt>(input);
            DeleteDate = GetNullableObject<TLLong>(input);
            Qts = GetNullableObject<TLInt>(input);

            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(RandomId.ToBytes());
            output.Write(RandomBytes.ToBytes());
            output.Write(Action.ToBytes());

            ChatId.NullableToStream(output);
            FromId.NullableToStream(output);
            Out.NullableToStream(output);
            Unread.NullableToStream(output);
            Date.NullableToStream(output);
            DeleteDate.NullableToStream(output);
            Qts.NullableToStream(output);

            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());
        }

        public override string ToString()
        {
            string dateTimeString = null;
            try
            {
                var clientDelta = MTProtoService.Instance.ClientTicksDelta;
                var utc0SecsLong = Date.Value * 4294967296 - clientDelta;
                var utc0SecsInt = utc0SecsLong / 4294967296.0;
                DateTime? dateTime = Helpers.Utils.UnixTimestampToDateTime(utc0SecsInt);
                dateTimeString = dateTime.Value.ToString("H:mm:ss");
            }
            catch (Exception ex)
            {

            }

            return string.Format("Qts={0} [{1} {2}] DeleteIndex={3} TTL={4} U={5} S={6} {7}", QtsIndex, DateIndex, dateTimeString, DeleteIndex, TTL, Unread, Status, Action);
        }
    }
}
