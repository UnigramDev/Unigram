using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
#if WIN_RT
using Windows.UI.Xaml;
#endif
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Extensions;
using Telegram.Api.TL.Interfaces;
using Telegram.Logs;

namespace Telegram.Api.TL
{
    public enum MessageStatus
    {
        Sending = 1,
        Confirmed = 0,
        Failed = 2,
        Read = 3,
        Broadcast = 4,
        Compressing = 5,
    }

    [Flags]
    public enum MessageFlags
    {
        Unread = 0x1,           // 0
        Out = 0x2,              // 1
        Fwd = 0x4,              // 2
        Reply = 0x8,            // 3
        Mention = 0x10,         // 4
        NotListened = 0x20,     // 5
        ReplyMarkup = 0x40,     // 6
        Entities = 0x80,        // 7
        Author = 0x100,         // 8
        Media = 0x200,          // 9
        Views = 0x400,          // 10
    }

    public abstract class TLMessageBase : TLObject, ISelectable
    {
        public static string MessageFlagsString(TLInt flags)
        {
            if (flags == null) return string.Empty;

            var list = (MessageFlags)flags.Value;

            return string.Format("{0} [{1}]", flags, list);
        }


        public abstract int DateIndex { get; set; }

        private TLLong _randomId;

        public TLLong RandomId
        {
            get { return _randomId; }
            set 
            { 
                _randomId = value;
            }
        }

        public long RandomIndex
        {
            get { return RandomId != null ? RandomId.Value : 0; }
            set { RandomId = new TLLong(value); }
        }

        /// <summary>
        /// Message Id
        /// </summary>
        public TLInt Id { get; set; }
       
        public int Index
        {
            get { return Id != null ?  Id.Value : 0; }
            set { Id = new TLInt(value); }
        }

        public virtual void Update(TLMessageBase message)
        {
            Id = message.Id;
            Status = message.Status;
        }

        public override string ToString()
        {
            return "Id=" + Index + " RndId=" + RandomIndex;
        }

        #region Additional

        public string WebPageTitle { get; set; }

        public bool DisableWebPagePreview { get; set; }

        public TLMessageBase Reply { get; set; }

        public virtual ReplyInfo ReplyInfo
        {
            get { return null; }
        }

        public virtual Visibility ReplyVisibility { get { return Visibility.Collapsed; } }

        public virtual double ReplyWidth { get { return 311.0; } }

        public MessageStatus _status;

        public virtual MessageStatus Status
        {
            get { return _status; }
            set { _status = value; }
        }

        public bool _isAnimated;

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

        private bool _isHighlighted;

        public bool IsHighlighted
        {
            get { return _isHighlighted; }
            set { SetField(ref _isHighlighted, value, () => IsHighlighted); }
        }

        public virtual int MediaSize { get { return 0; } }

        public virtual Visibility MediaSizeVisibility { get { return Visibility.Collapsed; } }

        public virtual bool IsAudioVideoMessage()
        {
            return false;
        }

        public virtual bool IsSticker()
        {
            return false;
        }

        public static bool IsSticker(TLDocumentBase document)
        {
#if WP8
            var document22 = document as TLDocument22;
            if (document22 != null
                && document22.DocumentSize > 0
                && document22.DocumentSize < Constants.StickerMaxSize)
            {
                var documentStickerAttribute = document22.Attributes.FirstOrDefault(x => x is TLDocumentAttributeSticker);

                if (documentStickerAttribute != null
                    && string.Equals(document22.MimeType.ToString(), "image/webp", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
#endif

            return false;
        }

        public TLMessageBase Self { get { return this; } }

        public bool ShowSeparator { get; set; }

        #endregion

    }

    public class ReplyInfo
    {
        public TLInt ReplyToMsgId { get; set; }

        public TLMessageBase Reply { get; set; }
    }

    public abstract class TLMessageCommon : TLMessageBase
    {
        public virtual TLInt FromId { get; set; }

        public TLPeerBase ToId { get; set; }

        public virtual TLBool Out { get; set; }

        private TLBool _unread;

        public virtual void SetUnread(TLBool value)
        {
            _unread = value;
        }

        public virtual TLBool Unread
        {
            get { return _unread; }
            set
            {
                SetField(ref _unread, value, () => Unread);
                NotifyOfPropertyChange(() => Status);
            }
        }

        public bool IsChannelMessage { get { return FromId == null || FromId.Value <= 0; } }

        public override MessageStatus Status
        {
            get 
            {
                if (_status == MessageStatus.Broadcast)
                {
                    return _status;
                }

                if (!Unread.Value)
                {
                    return MessageStatus.Read;
                }

                return _status;
            }
            set
            {
                if (_status == MessageStatus.Broadcast) return;

                SetField(ref _status, value, () => Status);
            }
        }

        public override int DateIndex
        {
            get { return Date.Value; }
            set { Date = new TLInt(value); }
        }

        public TLInt _date;

        public TLInt Date
        {
            get { return _date; }
            set { _date = value; }
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
                dateTimeString = dateTime.Value.ToString("H:mm:ss dd.MM");
            }
            catch (Exception ex)
            {
                
            }

            return base.ToString() + string.Format(" [{0} {4}] FromId={1} ToId=[{2}] U={3} S={5}", Date, FromId, ToId, Unread, dateTimeString, Status);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);
            Out = GetObject<TLBool>(bytes, ref position);
            _unread = GetObject<TLBool>(bytes, ref position);
            _date = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);
            Out = GetObject<TLBool>(input);
            _unread = GetObject<TLBool>(input);
            _date = GetObject<TLInt>(input);

            var randomId = GetObject<TLLong>(input);
            if (randomId.Value != 0)
            {
                RandomId = randomId;
            }
            var status = GetObject<TLInt>(input);
            Status = (MessageStatus) status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(FromId.ToBytes());
            ToId.ToStream(output);
            output.Write(Out.ToBytes());
            output.Write(Unread.ToBytes());
            output.Write(Date.ToBytes());

            RandomId = RandomId ?? new TLLong(0);
            RandomId.ToStream(output);
            var status = new TLInt((int) Status);
            output.Write(status.ToBytes());
        }


        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = (TLMessageCommon) message;
            FromId = m.FromId;
            ToId = m.ToId;
            Out = m.Out;
            if (Unread.Value != m.Unread.Value)
            {
                if (Unread.Value)
                {
                    _unread = m.Unread;
                }
            }
            _date = m.Date;
        }

        #region Additional

        public TLObject From
        {
            get
            {
                var cacheService = InMemoryCacheService.Instance;
                if (FromId == null || FromId.Value <= 0)
                {
                    return cacheService.GetChat(ToId.Id);
                }

                return cacheService.GetUser(FromId);
            }
        }

        public override bool ShowFrom
        {
            get { return (ToId is TLPeerChat || ToId is TLPeerChannel) && !(this is TLMessageService); }
        }
        #endregion
    }

    public class TLMessageEmpty : TLMessageBase
    {
        public override int DateIndex { get; set; }

        public const uint Signature = TLConstructors.TLMessageEmpty;

        public override string ToString()
        {
            return base.ToString() + ", EmptyMessage";
        }

        public override Visibility SelectionVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            RandomId = GetObject<TLObject>(input) as TLLong;
            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id = Id ?? new TLInt(0);
            output.Write(Id.ToBytes());

            RandomId.NullableToStream(output);
            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());
        }
    }

    public class TLMessagesContainter : TLMessageBase
    {
        public const uint Signature = TLConstructors.TLMessageForwardedContainter;

        public TLMessageMediaBase WebPageMedia { get; set; }

        public TLVector<TLMessage25> FwdMessages { get; set; }

        public TLVector<TLInt> FwdIds { get; set; } 

        public override int DateIndex { get; set; }

        public override Visibility SelectionVisibility
        {
            get { return Visibility.Collapsed; }
        }

        public TLObject From
        {
            get
            {
                if (FwdMessages != null && FwdMessages.Count > 0)
                {
                    var fwdMessage = FwdMessages[0] as TLMessage40;
                    if (fwdMessage != null)
                    {
                        var fwdPeer = fwdMessage.FwdFromPeer;
                        if (fwdPeer != null)
                        {
                            var cacheService = InMemoryCacheService.Instance;
                            if (fwdPeer is TLPeerChannel)
                            {
                                return cacheService.GetChat(fwdPeer.Id);
                            }

                            return cacheService.GetUser(fwdPeer.Id);
                        }
                    }

                    return FwdMessages[0].FwdFrom;
                }

                return null;
            }
        }

        public TLString Message
        {
            get
            {
                if (FwdMessages != null && FwdMessages.Count > 0)
                {
                    return FwdMessages[0].Message;
                }

                return null;
            }
        }

        public TLMessageMediaBase Media
        {
            get
            {
                if (FwdMessages != null && FwdMessages.Count > 0)
                {
                    return FwdMessages[0].Media;
                }
                
                return null;
            }
        }
    }

    public class TLMessage40 : TLMessage36
    {
        public new const uint Signature = TLConstructors.TLMessage40;

        private TLPeerBase _fwdFromPeer;

        public TLPeerBase FwdFromPeer
        {
            get
            {
                return _fwdFromPeer;
            }
            set
            {
                if (_fwdFromPeer != null && value == null)
                {
                    
                }
                
                _fwdFromPeer = value;
                
            }
        }

        public Visibility FwdFromPeerVisibility
        {
            get
            {
                var peerChannel = FwdFromPeer as TLPeerChannel;
                if (peerChannel != null)
                {
                    var mediaPhoto = Media as TLMessageMediaPhoto;
                    if (mediaPhoto != null)
                    {
                        return Visibility.Visible;
                    }

                    var mediaVideo = Media as TLMessageMediaVideo;
                    if (mediaVideo != null)
                    {
                        return Visibility.Visible;
                    }
                }

                var emptyMedia = Media as TLMessageMediaEmpty;
                var webPageMedia = Media as TLMessageMediaWebPage;
                return FwdFromPeer != null && !TLString.IsNullOrEmpty(Message) && (emptyMedia != null || webPageMedia != null) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public TLMessageBase Group { get; set; }

        public void SetFromId()
        {
            Set(ref _flags, (int)MessageFlags.Author);
        }

        public void SetMedia()
        {
            Set(ref _flags, (int)MessageFlags.Media);
        }

        public override TLObject FwdFrom
        {
            get
            {
                if (FwdFromId != null)
                {
                    var cacheService = InMemoryCacheService.Instance;
                    return cacheService.GetUser(FwdFromId);
                }

                if (FwdFromPeer != null)
                {
                    var cacheService = InMemoryCacheService.Instance;

                    if (FwdFromPeer is TLPeerChannel)
                    {
                        return cacheService.GetChat(FwdFromPeer.Id);
                    }

                    return cacheService.GetUser(FwdFromPeer.Id);
                }

                return null;
            }
            set
            {

            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FromId = IsSet(Flags, (int)MessageFlags.Author) 
                ? GetObject<TLInt>(bytes, ref position) 
                : new TLInt(-1);
            ToId = GetObject<TLPeerBase>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromPeer = GetObject<TLPeerBase>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }

            _date = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);

            _media = IsSet(Flags, (int)MessageFlags.Media)
                ? GetObject<TLMessageMediaBase>(bytes, ref position)
                : new TLMessageMediaEmpty();

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(bytes, ref position);
            }

            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            }

            _views = IsSet(Flags, (int) MessageFlags.Views)
                ? GetObject<TLInt>(bytes, ref position)
                : new TLInt(0);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromPeer = GetObject<TLPeerBase>(input);
                FwdDate = GetObject<TLInt>(input);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(input);
            }

            _date = GetObject<TLInt>(input);
            Message = GetObject<TLString>(input);

            _media = IsSet(Flags, (int)MessageFlags.Media)
                ? GetObject<TLMessageMediaBase>(input)
                : new TLMessageMediaEmpty();

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(input);
            }

            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(input);
            }

            if (IsSet(Flags, (int) MessageFlags.Views))
            {
                Views = GetObject<TLInt>(input);
            }
            else
            {
                Views = new TLInt(0);
            }

            CustomFlags = GetNullableObject<TLLong>(input);

            var randomId = GetObject<TLLong>(input);
            if (randomId.Value != 0)
            {
                RandomId = randomId;
            }
            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            try
            {

                Flags.ToStream(output);
                Id = Id ?? new TLInt(0);
                output.Write(Id.ToBytes());
                output.Write(FromId.ToBytes());
                ToId.ToStream(output);

                if (IsSet(Flags, (int)MessageFlags.Fwd))
                {
                    FwdFromPeer.ToStream(output);
                    FwdDate.ToStream(output);
                }

                if (IsSet(Flags, (int)MessageFlags.Reply))
                {
                    ReplyToMsgId.ToStream(output);
                }

                output.Write(Date.ToBytes());
                Message.ToStream(output);

                if (IsSet(Flags, (int)MessageFlags.Media))
                {
                    _media.ToStream(output);
                }

                if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
                {
                    ReplyMarkup.ToStream(output);
                }
                if (IsSet(Flags, (int)MessageFlags.Entities))
                {
                    Entities.ToStream(output);
                }
                if (IsSet(Flags, (int)MessageFlags.Views))
                {
                    if (Views == null)
                    {
                        var logString = string.Format("TLMessage40.ToStream id={0} flags={1} fwd_from_peer={2} fwd_date={3} reply_to_msg_id={4} media={5} reply_markup={6} entities={7} views={8} from_id={9}", Index, MessageFlagsString(Flags), FwdFromPeer, FwdDate, ReplyToMsgId, Media, ReplyMarkup, Entities, Views, FromId);
                        Log.Write(logString);
                    }

                    Views = Views ?? new TLInt(0);
                    Views.ToStream(output);
                }

                CustomFlags.NullableToStream(output);

                RandomId = RandomId ?? new TLLong(0);
                RandomId.ToStream(output);
                var status = new TLInt((int)Status);
                output.Write(status.ToBytes());
            }
            catch (Exception ex)
            {
                var logString = string.Format("TLMessage40.ToStream id={0} flags={1} fwd_from_peer={2} fwd_date={3} reply_to_msg_id={4} media={5} reply_markup={6} entities={7} views={8} from_id={9}", Index, MessageFlagsString(Flags), FwdFromPeer, FwdDate, ReplyToMsgId, Media, ReplyMarkup, Entities, Views, FromId);

                TLUtils.WriteException(logString, ex);
            }
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = message as TLMessage40;
            if (m != null)
            {

                FwdFromPeer = m.FwdFromPeer;

                if (m.Views != null)
                {
                    var currentViews = Views != null ? Views.Value : 0;
                    if (currentViews < m.Views.Value)
                    {
                        Views = m.Views;
                    }
                } 
            }
        }
    }

    public class TLMessage36 : TLMessage34
    {
        public new const uint Signature = TLConstructors.TLMessage36;

        protected TLInt _views;

        public TLInt Views
        {
            get { return _views; }
            set
            {
                if (value != null)
                {
                    if (_views == null || _views.Value < value.Value)
                    {
                        Set(ref _flags, (int)MessageFlags.Views);
                        _views = value;
                    }
                }
            }
        }

        public Visibility ViewsVisibility
        {
            get
            {
                var message40 = this as TLMessage40;
                if (message40 != null)
                {
                    return Views != null && Views.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
                }

                return Visibility.Collapsed;
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }

            _date = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);

            _media = IsSet(Flags, (int) MessageFlags.Media) 
                ? GetObject<TLMessageMediaBase>(bytes, ref position) 
                : new TLMessageMediaEmpty();

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(bytes, ref position);
            }

            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            }

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(input);
                FwdDate = GetObject<TLInt>(input);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(input);
            }

            _date = GetObject<TLInt>(input);
            Message = GetObject<TLString>(input);
            _media = GetObject<TLMessageMediaBase>(input);

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(input);
            }

            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(input);
            }

            CustomFlags = GetNullableObject<TLLong>(input);

            var randomId = GetObject<TLLong>(input);
            if (randomId.Value != 0)
            {
                RandomId = randomId;
            }
            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Flags.ToStream(output);
            Id = Id ?? new TLInt(0);
            output.Write(Id.ToBytes());
            output.Write(FromId.ToBytes());
            ToId.ToStream(output);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId.ToStream(output);
                FwdDate.ToStream(output);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId.ToStream(output);
            }

            output.Write(Date.ToBytes());
            Message.ToStream(output);
            _media.ToStream(output);

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup.ToStream(output);
            }
            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities.ToStream(output);
            }

            CustomFlags.NullableToStream(output);

            RandomId = RandomId ?? new TLLong(0);
            RandomId.ToStream(output);
            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());
        }
    }

    public class TLMessage34 : TLMessage31
    {
        public new const uint Signature = TLConstructors.TLMessage34;

        private TLVector<TLMessageEntityBase> _entities;

        public TLVector<TLMessageEntityBase> Entities
        {
            get { return _entities; }
            set
            {
                if (_entities != null && value == null)
                {
                    
                }
                _entities = value;
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }

            _date = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            _media = GetObject<TLMessageMediaBase>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(bytes, ref position);
            }

            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(bytes, ref position);
            }

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(input);
                FwdDate = GetObject<TLInt>(input);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(input);
            }

            _date = GetObject<TLInt>(input);
            Message = GetObject<TLString>(input);
            _media = GetObject<TLMessageMediaBase>(input);

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(input);
            }

            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities = GetObject<TLVector<TLMessageEntityBase>>(input);
            }

            CustomFlags = GetNullableObject<TLLong>(input);

            var randomId = GetObject<TLLong>(input);
            if (randomId.Value != 0)
            {
                RandomId = randomId;
            }
            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Flags.ToStream(output);
            Id = Id ?? new TLInt(0);
            output.Write(Id.ToBytes());
            output.Write(FromId.ToBytes());
            ToId.ToStream(output);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId.ToStream(output);
                FwdDate.ToStream(output);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId.ToStream(output);
            }

            output.Write(Date.ToBytes());
            Message.ToStream(output);
            _media.ToStream(output);

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup.ToStream(output);
            }
            if (IsSet(Flags, (int)MessageFlags.Entities))
            {
                Entities.ToStream(output);
            }

            CustomFlags.NullableToStream(output);

            RandomId = RandomId ?? new TLLong(0);
            RandomId.ToStream(output);
            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = message as TLMessage34;
            if (m != null)
            {
                if (m.Entities != null)
                {
                    Entities = m.Entities;
                }
            }
        }
    }

    public class TLMessage31 : TLMessage25
    {
        public new const uint Signature = TLConstructors.TLMessage31;

        public TLReplyKeyboardBase ReplyMarkup { get; set; }

        public TLLong CustomFlags { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }
            
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }

            _date = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            _media = GetObject<TLMessageMediaBase>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(bytes, ref position);
            }

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(input);
                FwdDate = GetObject<TLInt>(input);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(input);
            }

            _date = GetObject<TLInt>(input);
            Message = GetObject<TLString>(input);
            _media = GetObject<TLMessageMediaBase>(input);

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup = GetObject<TLReplyKeyboardBase>(input);
            }

            CustomFlags = GetNullableObject<TLLong>(input);

            var randomId = GetObject<TLLong>(input);
            if (randomId.Value != 0)
            {
                RandomId = randomId;
            }
            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Flags.ToStream(output);
            Id = Id ?? new TLInt(0);
            output.Write(Id.ToBytes());
            output.Write(FromId.ToBytes());
            ToId.ToStream(output);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId.ToStream(output);
                FwdDate.ToStream(output);
            }

            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId.ToStream(output);
            }

            output.Write(Date.ToBytes());
            Message.ToStream(output);
            _media.ToStream(output);

            if (IsSet(Flags, (int)MessageFlags.ReplyMarkup))
            {
                ReplyMarkup.ToStream(output);
            }

            CustomFlags.NullableToStream(output);

            RandomId = RandomId ?? new TLLong(0);
            RandomId.ToStream(output);
            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = message as TLMessage31;
            if (m != null)
            {
                if (m.ReplyMarkup != null)
                {
                    var oldCustomFlags = ReplyMarkup != null ? ReplyMarkup.CustomFlags : null;
                    ReplyMarkup = m.ReplyMarkup;
                    ReplyMarkup.CustomFlags = oldCustomFlags;
                }

                if (m.CustomFlags != null)
                {
                    CustomFlags = m.CustomFlags;
                }
            }
        }
    }

    public class TLMessage25 : TLMessage17
    {
        public new const uint Signature = TLConstructors.TLMessage25;

        public TLInt FwdFromId { get; set; }

        public TLInt FwdDate { get; set; }

        public TLInt ReplyToMsgId { get; set; }

        public override ReplyInfo ReplyInfo
        {
            get { return ReplyToMsgId != null ? new ReplyInfo{ ReplyToMsgId = ReplyToMsgId, Reply = Reply } : null; }
        }

        public override double ReplyWidth
        {
            get
            {
                if (Media is TLMessageMediaGeo)
                {
                    return 156.0;
                }

                if (Media is TLMessageMediaVideo)
                {
                    return 218.0;
                }

                return base.ReplyWidth;
            }
        }

        public override Visibility ReplyVisibility
        {
            get { return ReplyToMsgId != null && ReplyToMsgId.Value != 0 ? Visibility.Visible : Visibility.Collapsed; }
        }

        public void SetFwd()
        {
            Set(ref _flags, (int)MessageFlags.Fwd);
        }

        public void SetReply()
        {
            Set(ref _flags, (int)MessageFlags.Reply);
        }

        public void SetListened()
        {
            Unset(ref _flags, (int)MessageFlags.NotListened);
        }

        public bool NotListened
        {
            get { return IsSet(_flags, (int)MessageFlags.NotListened); }
            set { SetUnset(ref _flags, value, (int)MessageFlags.NotListened); }
        }

        public virtual TLObject FwdFrom
        {
            get
            {
                if (FwdFromId == null) return null;

                var cacheService = InMemoryCacheService.Instance;
                return cacheService.GetUser(FwdFromId);
            }
            set
            {
                
            }
        }

        public override string ToString()
        {
            return base.ToString() + string.Format("Flags={2} ReplyToMsgId={0} Reply={1}", ReplyToMsgId != null ? ReplyToMsgId.Value.ToString(CultureInfo.InvariantCulture) : "null", Reply != null ? Reply.GetType().Name : "null", TLMessageBase.MessageFlagsString(Flags));
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(bytes, ref position);
                FwdDate = GetObject<TLInt>(bytes, ref position);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(bytes, ref position);
            }

            _date = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            _media = GetObject<TLMessageMediaBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId = GetObject<TLInt>(input);
                FwdDate = GetObject<TLInt>(input);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId = GetObject<TLInt>(input);
            }

            _date = GetObject<TLInt>(input);
            Message = GetObject<TLString>(input);
            _media = GetObject<TLMessageMediaBase>(input);

            var randomId = GetObject<TLLong>(input);
            if (randomId.Value != 0)
            {
                RandomId = randomId;
            }
            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Flags.ToStream(output);
            Id = Id ?? new TLInt(0);
            output.Write(Id.ToBytes());
            output.Write(FromId.ToBytes());
            ToId.ToStream(output);

            if (IsSet(Flags, (int)MessageFlags.Fwd))
            {
                FwdFromId.ToStream(output);
                FwdDate.ToStream(output);
            }
            if (IsSet(Flags, (int)MessageFlags.Reply))
            {
                ReplyToMsgId.ToStream(output);
            }

            output.Write(Date.ToBytes());
            Message.ToStream(output);
            _media.ToStream(output);

            RandomId = RandomId ?? new TLLong(0);
            RandomId.ToStream(output);
            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = message as TLMessage25;
            if (m != null)
            {
                FwdFromId = m.FwdFromId;
                FwdDate = m.FwdDate;
                ReplyToMsgId = m.ReplyToMsgId;

                if (m.Reply != null)
                {
                    Reply = m.Reply;
                }
            }
        }
    }

    public class TLMessage17 : TLMessage
    {
        public new const uint Signature = TLConstructors.TLMessage17;

        protected TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public override TLBool Out
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Out)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)MessageFlags.Out);
                }
            }
        }

        public override void SetUnread(TLBool value)
        {
            Unread = value;
        }

        public override TLBool Unread
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Unread)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)MessageFlags.Unread);
                    NotifyOfPropertyChange(() => Status);
                }
            }
        }

        public bool IsMention 
        {
            get { return IsSet(_flags, (int) MessageFlags.Mention); }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);
            _date = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            _media = GetObject<TLMessageMediaBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);
            _date = GetObject<TLInt>(input);
            Message = GetObject<TLString>(input);
            _media = GetObject<TLMessageMediaBase>(input);

            var randomId = GetObject<TLLong>(input);
            if (randomId.Value != 0)
            {
                RandomId = randomId;
            }
            var status = GetObject<TLInt>(input);
            Status = (MessageStatus)status.Value;

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            
            Flags.ToStream(output);
            Id = Id ?? new TLInt(0);
            output.Write(Id.ToBytes());
            output.Write(FromId.ToBytes());
            ToId.ToStream(output);
            output.Write(Date.ToBytes());
            Message.ToStream(output);
            _media.ToStream(output);

            RandomId = RandomId ?? new TLLong(0);
            RandomId.ToStream(output);
            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());
        }
        
        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = (TLMessage17)message;
            Flags = m.Flags;
        }
    }

    public class TLMessage : TLMessageCommon, IMessage
    {
        public const uint Signature = TLConstructors.TLMessage;

        public TLString Message { get; set; }

        public TLMessageMediaBase _media;

        public TLMessageMediaBase Media
        {
            get { return _media; }
            set { SetField(ref _media, value, () => Media); }
        }

        public override Visibility SelectionVisibility
        {
            get { return Visibility.Visible; }
        }

        public override int MediaSize
        {
            get { return Media.MediaSize; }
        }

        public override Visibility MediaSizeVisibility
        {
            get { return _media is TLMessageMediaVideo ? Visibility.Visible : Visibility.Collapsed; }
        }

        public override bool IsAudioVideoMessage()
        {
            return _media is TLMessageMediaAudio || _media is TLMessageMediaVideo;
        }

        public override bool IsSticker()
        {
            var mediaDocument = _media as TLMessageMediaDocument;
            if (mediaDocument != null)
            {
                return IsSticker(mediaDocument.Document);
            }

            return false;
        }

        public override string ToString()
        {
            var messageString = Message.ToString();

            var mediaString = Media.GetType().Name;
            var mediaPhoto = Media as TLMessageMediaPhoto;
            if (mediaPhoto != null)
            {
                mediaString = mediaPhoto.ToString() + " ";
            }

            return base.ToString() + ((Media == null || Media is TLMessageMediaEmpty)? " Msg=" + messageString.Substring(0, Math.Min(messageString.Length, 5)) : " Media=" + mediaString);
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            base.FromBytes(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            _media = GetObject<TLMessageMediaBase>(bytes, ref position);          

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            base.FromStream(input);
            Message = GetObject<TLString>(input);
            _media = GetObject<TLMessageMediaBase>(input); 

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id = Id ?? new TLInt(0);
            output.Write(Id.ToBytes());
            base.ToStream(output);
            Message.ToStream(output);
            _media.ToStream(output);
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = (TLMessage)message;
            Message = m.Message;
            var oldMedia = Media;
            var newMedia = m.Media;
            if (oldMedia.GetType() != newMedia.GetType())
            {
                _media = m.Media;
            }
            else
            {
                var oldMediaDocument = oldMedia as TLMessageMediaDocument;
                var newMediaDocument = newMedia as TLMessageMediaDocument;
                if (oldMediaDocument != null && newMediaDocument != null)
                {
                    if (oldMediaDocument.Document.GetType() != newMediaDocument.Document.GetType())
                    {
                        _media = m.Media;
                    }
                    else
                    {
                        var oldDocument = oldMediaDocument.Document as TLDocument;
                        var newDocument = newMediaDocument.Document as TLDocument;
                        if (oldDocument != null
                            && newDocument != null
                            && (oldDocument.Id.Value != newDocument.Id.Value
                                || oldDocument.AccessHash.Value != newDocument.AccessHash.Value))
                        {
                            var isoFileName = Media.IsoFileName;
#if WP8
                            var file = Media.File;
#endif
                            _media = m.Media;
                            _media.IsoFileName = isoFileName;
#if WP8
                            _media.File = file;
#endif
                        }
                    }

                    return;
                }

                var oldMediaVideo = oldMedia as TLMessageMediaVideo;
                var newMediaVideo = newMedia as TLMessageMediaVideo;
                if (oldMediaVideo != null && newMediaVideo != null)
                {
                    if (oldMediaVideo.Video.GetType() != newMediaVideo.Video.GetType())
                    {
                        _media = m.Media;
                    }
                    else
                    {
                        var oldVideo = oldMediaVideo.Video as TLVideo;
                        var newVideo = newMediaVideo.Video as TLVideo;
                        if (oldVideo != null
                            && newVideo != null
                            && (oldVideo.Id.Value != newVideo.Id.Value
                                || oldVideo.AccessHash.Value != newVideo.AccessHash.Value))
                        {
                            var isoFileName = Media.IsoFileName;
                            _media = m.Media;
                            _media.IsoFileName = isoFileName;
                        }
                    }

                    return;
                }

                var oldMediaAudio = oldMedia as TLMessageMediaAudio;
                var newMediaAudio = newMedia as TLMessageMediaAudio;
                if (oldMediaAudio != null && newMediaAudio != null)
                {
                    if (oldMediaAudio.Audio.GetType() != newMediaAudio.Audio.GetType())
                    {
                        _media = m.Media;
                    }
                    else
                    {
                        var oldAudio = oldMediaAudio.Audio as TLAudio;
                        var newAudio = newMediaAudio.Audio as TLAudio;
                        if (oldAudio != null
                            && newAudio != null
                            && (oldAudio.Id.Value != newAudio.Id.Value
                                || oldAudio.AccessHash.Value != newAudio.AccessHash.Value))
                        {
                            var isoFileName = Media.IsoFileName;
                            var notListened = Media.NotListened;
                            _media = m.Media;
                            _media.IsoFileName = isoFileName;
                            _media.NotListened = notListened;
                        }
                    }

                    return;
                }

                var oldMediaPhoto = oldMedia as TLMessageMediaPhoto;
                var newMediaPhoto = newMedia as TLMessageMediaPhoto;
                if (oldMediaPhoto == null || newMediaPhoto == null)
                {
                    _media = m.Media;
                }
                else
                {
                    var oldPhoto = oldMediaPhoto.Photo as TLPhoto;
                    var newPhoto = newMediaPhoto.Photo as TLPhoto;
                    if (oldPhoto == null || newPhoto == null)
                    {
                        _media = m.Media;
                    }
                    else
                    {
                        if (oldPhoto.AccessHash.Value != newPhoto.AccessHash.Value)
                        {
                            _media = m.Media;
                        }
                    }
                }
            }
        }

        #region Additional

        private TLInputMediaBase _inputMedia;

        /// <summary>
        /// To resend canceled message
        /// </summary>
        public TLInputMediaBase InputMedia
        {
            get { return _inputMedia; }
            set { SetField(ref _inputMedia, value, () => InputMedia); }
        }

        public List<string> Links { get; set; }

        #endregion
    }

    [Obsolete]
    public class TLMessageForwarded17 : TLMessageForwarded
    {
        public new const uint Signature = TLConstructors.TLMessageForwarded17;

        private TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public override TLBool Out
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Out)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)MessageFlags.Out);
                }
            }
        }

        public override void SetUnread(TLBool value)
        {
            Unread = value;
        }

        public override TLBool Unread
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Unread)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)MessageFlags.Unread);
                    NotifyOfPropertyChange(() => Status);
                }
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FwdFromId = GetObject<TLInt>(bytes, ref position);
            FwdDate = GetObject<TLInt>(bytes, ref position);
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);
            _date = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            _media = GetObject<TLMessageMediaBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FwdFromId = GetObject<TLInt>(input);
            FwdDate = GetObject<TLInt>(input);
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);
            _date = GetObject<TLInt>(input);
            Message = GetObject<TLString>(input);
            _media = GetObject<TLMessageMediaBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Flags.ToStream(output);
            Id = Id ?? new TLInt(0);
            Id.ToStream(output);
            FwdFromId.ToStream(output);
            FwdDate.ToStream(output);
            FromId.ToStream(output);
            ToId.ToStream(output);
            _date.ToStream(output);
            Message.ToStream(output);
            _media.ToStream(output);
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = (TLMessageForwarded17)message;
            Flags = m.Flags;
        }
    }

    [Obsolete]
    public class TLMessageForwarded : TLMessage
    {
        public new const uint Signature = TLConstructors.TLMessageForwarded;
        
        public TLInt FwdFromId { get; set; }

        public TLUserBase FwdFrom
        {
            get
            {
                var cacheService = InMemoryCacheService.Instance;
                return cacheService.GetUser(FwdFromId);
            }
        }

        public TLInt FwdDate { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            FwdFromId = GetObject<TLInt>(bytes, ref position);
            FwdDate = GetObject<TLInt>(bytes, ref position);
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);
            Out = GetObject<TLBool>(bytes, ref position);
            Unread = GetObject<TLBool>(bytes, ref position);
            _date = GetObject<TLInt>(bytes, ref position);
            Message = GetObject<TLString>(bytes, ref position);
            _media = GetObject<TLMessageMediaBase>(bytes, ref position);
            
            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            } 
            FwdFromId = GetObject<TLInt>(input);
            FwdDate = GetObject<TLInt>(input);
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);
            Out = GetObject<TLBool>(input);
            Unread = GetObject<TLBool>(input);
            _date = GetObject<TLInt>(input);
            Message = GetObject<TLString>(input);
            _media = GetObject<TLMessageMediaBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id = Id ?? new TLInt(0);
            Id.ToStream(output);
            FwdFromId.ToStream(output);
            FwdDate.ToStream(output);
            FromId.ToStream(output);
            ToId.ToStream(output);
            Out.ToStream(output);
            Unread.ToStream(output);
            _date.ToStream(output);
            Message.ToStream(output);
            _media.ToStream(output);
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = (TLMessageForwarded)message;
            FwdFromId = m.FwdFromId;
            FwdDate = m.FwdDate;
        }
    }

    public class TLMessageService40 : TLMessageService17
    {
        public new const uint Signature = TLConstructors.TLMessageService40;

        private TLLong _customFlags;

        public TLLong CustomFlags
        {
            get { return _customFlags; }
            set { _customFlags = value; }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FromId = IsSet(Flags, (int) MessageFlags.Author)
                ? GetObject<TLInt>(bytes, ref position)
                : new TLInt(-1);
            ToId = GetObject<TLPeerBase>(bytes, ref position);
            _date = GetObject<TLInt>(bytes, ref position);
            Action = GetObject<TLMessageActionBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);
            _date = GetObject<TLInt>(input);

            // workaround: RandomId and Status were missing here, so Flags.31 (bits 100000...000) is recerved to handle this issue
            if (IsSet(Flags, int.MinValue))
            {
                var randomId = GetObject<TLLong>(input);
                if (randomId.Value != 0)
                {
                    RandomId = randomId;
                }
                var status = GetObject<TLInt>(input);
                Status = (MessageStatus)status.Value;
            }

            Action = GetObject<TLMessageActionBase>(input);

            CustomFlags = GetNullableObject<TLLong>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Set(ref _flags, int.MinValue); // workaround

            Flags.ToStream(output);
            Id = Id ?? new TLInt(0);
            Id.ToStream(output);
            FromId.ToStream(output);
            ToId.ToStream(output);
            _date.ToStream(output);

            // workaround: RandomId and Status were missing here, so Flags.31 is recerved to handle this issue

            RandomId = RandomId ?? new TLLong(0);
            RandomId.ToStream(output);
            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());

            Action.ToStream(output);

            CustomFlags.NullableToStream(output);
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);

            var m = message as TLMessageService40;
            if (m != null)
            {
                if (m.CustomFlags != null)
                {
                    CustomFlags = m.CustomFlags;
                }
            }
        }
    }

    public class TLMessageService17 : TLMessageService
    {
        public new const uint Signature = TLConstructors.TLMessageService17;

        protected TLInt _flags;

        public TLInt Flags
        {
            get { return _flags; }
            set { _flags = value; }
        }

        public override TLBool Out
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Out)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)MessageFlags.Out);
                }
            }
        }

        public override void SetUnread(TLBool value)
        {
            Unread = value;
        }

        public override TLBool Unread
        {
            get { return new TLBool(IsSet(_flags, (int)MessageFlags.Unread)); }
            set
            {
                if (value != null)
                {
                    SetUnset(ref _flags, value.Value, (int)MessageFlags.Unread);
                    NotifyOfPropertyChange(() => Status);
                }
            }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Flags = GetObject<TLInt>(bytes, ref position);
            Id = GetObject<TLInt>(bytes, ref position);
            FromId = GetObject<TLInt>(bytes, ref position);
            ToId = GetObject<TLPeerBase>(bytes, ref position);
            _date = GetObject<TLInt>(bytes, ref position);
            Action = GetObject<TLMessageActionBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            Flags = GetObject<TLInt>(input);
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            FromId = GetObject<TLInt>(input);
            ToId = GetObject<TLPeerBase>(input);
            _date = GetObject<TLInt>(input);

            // workaround: RandomId and Status were missing here, so Flags.31 (bits 100000...000) is recerved to handle this issue
            if (IsSet(_flags, int.MinValue))
            {
                var randomId = GetObject<TLLong>(input);
                if (randomId.Value != 0)
                {
                    RandomId = randomId;
                }
                var status = GetObject<TLInt>(input);
                Status = (MessageStatus)status.Value;
            }

            Action = GetObject<TLMessageActionBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));

            Set(ref _flags, int.MinValue); // workaround

            Flags.ToStream(output);
            Id = Id ?? new TLInt(0);
            Id.ToStream(output);
            FromId.ToStream(output);
            ToId.ToStream(output);
            _date.ToStream(output);

            // workaround: RandomId and Status were missing here, so Flags.31 is recerved to handle this issue
            
            RandomId = RandomId ?? new TLLong(0);
            RandomId.ToStream(output);
            var status = new TLInt((int)Status);
            output.Write(status.ToBytes());

            Action.ToStream(output);
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = (TLMessageService17)message;

            Flags = m.Flags;
        }
    }

    public class TLMessageService : TLMessageCommon
    {
        public const uint Signature = TLConstructors.TLMessageService;

        public TLMessageActionBase Action { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            Id = GetObject<TLInt>(bytes, ref position);
            base.FromBytes(bytes, ref position);
            Action = GetObject<TLMessageActionBase>(bytes, ref position);

            return this;
        }

        public override TLObject FromStream(Stream input)
        {
            var id = GetObject<TLInt>(input);
            if (id.Value != 0)
            {
                Id = id;
            }
            base.FromStream(input);
            Action = GetObject<TLMessageActionBase>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            Id = Id ?? new TLInt(0);
            Id.ToStream(output);
            base.ToStream(output);
            Action.ToStream(output);
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = (TLMessageService)message;

            if (Action != null)
            {
                Action.Update(m.Action);
            }
            else
            {
                Action = m.Action;
            }
        }

        public override Visibility SelectionVisibility
        {
            get { return Visibility.Collapsed; }
        }
    }
}
