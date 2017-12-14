using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Windows.UI.Xaml;

namespace Telegram.Api.TL
{
    public abstract partial class TLMessageBase : ITLRandomId, INotifyPropertyChanged
    {
        // TODO:
        public bool IsUnread
        {
            get;
            set;
        } = false;

        public TLMessageBase Reply { get; set; }

        public TLMessageBase SelfBase
        {
            get
            {
                return this;
            }
        }

        //public virtual ReplyInfo ReplyInfo
        //{
        //    get { return null; }
        //}

        public void SetUnreadSilent(bool unread)
        {
            IsUnread = unread;
        }

        public void SetUnread(bool unread)
        {
            IsUnread = unread;
            RaisePropertyChanged(() => IsUnread);
            RaisePropertyChanged(() => State);
        }

        public Int64? RandomId { get; set; }

        public TLMessageState _state;
        public virtual TLMessageState State
        {
            get
            {
                return _state;
            }
            set
            {
                if (_state != value)
                {
                    _state = value;
                    RaisePropertyChanged(() => State);
                }
            }
        }

        public virtual void Update(TLMessageBase message)
        {
            Id = message.Id;
            State = message.State;
        }

        #region Service

        public bool IsService()
        {
            var message = this as TLMessage;
            if (message != null && message.Media is TLMessageMediaPhoto photoMedia && photoMedia.HasTTLSeconds && (photoMedia.Photo is TLPhotoEmpty || !photoMedia.HasPhoto))
            {
                return true;
            }
            else if (message != null && message.Media is TLMessageMediaDocument documentMedia && documentMedia.HasTTLSeconds && (documentMedia.Document is TLDocumentEmpty || !documentMedia.HasDocument))
            {
                return true;
            }

            return this is TLMessageService;
        }

        #endregion

        public virtual bool ShowFrom
        {
            get
            {
                if (this is TLMessageService)
                {
                    return false;
                }

                if (this is TLMessageCommonBase messageCommon && messageCommon.IsOut)
                {
                    return true;
                }

                if (FromId == null || FromId.Value <= 0)
                {
                    return false;
                }

                if (ToId is TLPeerChat)
                {
                    return true;
                }

                if (ToId is TLPeerChannel)
                {
                    if (Parent is TLChannel channel && channel.IsMegaGroup)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public virtual void Edit(TLMessageBase messageBase)
        {

        }

        private TLUser _from;
        public TLUser From
        {
            get
            {
                if (_from == null && FromId.HasValue)
                    _from = InMemoryCacheService.Current.GetUser(FromId) as TLUser;

                return _from;
            }
            set
            {
                _from = value;
            }
        }

        private ITLDialogWith _participant;
        public ITLDialogWith Participant
        {
            get
            {
                if (_participant == null)
                {
                    var channel = Parent as TLChannel;
                    if (channel != null && channel.IsBroadcast)
                        _participant = Parent;
                    else if (this is TLMessage message && message.IsSaved())
                        _participant = message.FwdFromUser ?? (ITLDialogWith)message.FwdFromChannel;
                    else
                        _participant = From;
                }

                return _participant;
            }
        }

        private ITLDialogWith _parent;
        public ITLDialogWith Parent
        {
            get
            {
                if (_parent == null)
                {
                    if (this is TLMessageCommonBase messageCommon)
                    {
                        var peer = messageCommon.IsOut || messageCommon.ToId is TLPeerChannel || messageCommon.ToId is TLPeerChat ? messageCommon.ToId : new TLPeerUser { UserId = messageCommon.FromId ?? 0 };
                        if (peer is TLPeerUser)
                            _parent = InMemoryCacheService.Current.GetUser(peer.Id);
                        if (peer is TLPeerChat || ToId is TLPeerChannel)
                            _parent = InMemoryCacheService.Current.GetChat(peer.Id);
                    }

                }

                return _parent;
            }
        }

        private bool _isFirst;
        public bool IsFirst
        {
            get
            {
                return _isFirst;
            }
            set
            {
                if (_isFirst != value)
                {
                    _isFirst = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _isLast;
        public bool IsLast
        {
            get
            {
                return _isLast;
            }
            set
            {
                if (_isLast != value)
                {
                    _isLast = value;
                    RaisePropertyChanged();
                }
            }
        }

        //public TLMessageBase Reply { get; set; }

        public ReplyInfo ReplyInfo
        {
            get
            {
                if (ReplyToMsgId == null)
                {
                    return null;
                }

                return new ReplyInfo
                {
                    ReplyToMsgId = ReplyToMsgId,
                    Reply = Reply
                };
            }
        }

        public Visibility ReplyVisibility
        {
            get
            {
                return ReplyToMsgId == null || (ReplyToMsgId.HasValue && ReplyToMsgId == 0) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public override void RaisePropertyChanged([CallerMemberName] string propertyName = "")
        {
            Execute.OnUIThread(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
        }
    }

    public class ReplyInfo
    {
        public int? ReplyToMsgId
        {
            get;
            set;
        }

        public TLMessageBase Reply
        {
            get;
            set;
        }
    }

    public partial class TLMessageService
    {
        public TLMessageService Self
        {
            get
            {
                return this;
            }
        }
    }

    public partial class TLMessage
    {

        #region Saved

        public bool IsSaved()
        {
            return HasFwdFrom && FwdFrom != null && FwdFrom.HasSavedFromPeer && FwdFrom.SavedFromPeer != null;
        }

        #endregion

        #region Game

        public bool IsGame()
        {
            return Media is TLMessageMediaGame;
        }

        #endregion

        #region Photo

        public bool IsPhoto()
        {
            return Media is TLMessageMediaPhoto;
        }

        #endregion

        #region Gif
        public bool IsGif()
        {
            var documentMedia = Media as TLMessageMediaDocument;
            return documentMedia != null && IsGif(documentMedia.Document as TLDocument);
        }

        public static bool IsGif(TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsGif(document);
        }

        public static bool IsGif(TLDocument document)
        {
            if (document != null && document.MimeType.Equals("video/mp4", StringComparison.OrdinalIgnoreCase))
            {
                return IsGif(document.Attributes, document.Size);
            }

            return false;

            //TLDocumentExternal tLDocumentExternal = document as TLDocumentExternal;
            //return tLDocumentExternal != null && string.Equals(tLDocumentExternal.Type.ToString(), "gif", 5) && TLMessageBase.IsGif(tLDocumentExternal, null);
        }

        public static bool IsGif(TLVector<TLDocumentAttributeBase> attributes, int size)
        {
            if (/*size > 0 &&*/ size < 10383360)
            {
                var animated = attributes.OfType<TLDocumentAttributeAnimated>().FirstOrDefault();
                var video = attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                if (animated != null && video != null)
                {
                    return !video.IsRoundMessage;
                }
            }

            return false;
        }
        #endregion

        #region Music
        public virtual bool IsMusic()
        {
            var documentMedia = Media as TLMessageMediaDocument;
            return documentMedia != null && IsMusic(documentMedia.Document);
        }

        public static bool IsMusic(TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsMusic(document, document.Size);
        }

        public static bool IsMusic(TLDocument document, int size)
        {
            if (size > 0)
            {
                var audioAttribute = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                if (audioAttribute != null && !audioAttribute.IsVoice)
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Video

        public bool IsVideo()
        {
            // TODO: 24/07/2017, warning: || documentMedia.HasTTLSeconds
            var documentMedia = Media as TLMessageMediaDocument;
            return documentMedia != null && (IsVideo(documentMedia.Document) || documentMedia.HasTTLSeconds);
        }

        public static bool IsVideo(TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsVideo(document, document.Size);
        }

        public static bool IsVideo(TLDocument document, int size)
        {
            if (size > 0)
            {
                var videoAttribute = document.Attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                var animatedAttribute = document.Attributes.OfType<TLDocumentAttributeAnimated>().FirstOrDefault();
                if (videoAttribute != null && animatedAttribute == null)
                {
                    return !videoAttribute.IsRoundMessage;
                }
            }
            return false;
        }

        #endregion

        #region RoundVideo

        public bool IsRoundVideo()
        {
            var documentMedia = Media as TLMessageMediaDocument;
            return documentMedia != null && IsRoundVideo(documentMedia.Document);
        }

        public static bool IsRoundVideo(TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsRoundVideo(document, document.Size);
        }

        public static bool IsRoundVideo(TLDocument document, int size)
        {
            if (size > 0)
            {
                var videoAttribute = document.Attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                //var animatedAttribute = document.Attributes.OfType<TLDocumentAttributeAnimated>().FirstOrDefault();
                if (videoAttribute != null /*&& animatedAttribute == null*/)
                {
                    return videoAttribute.IsRoundMessage;
                }
            }
            return false;
        }

        #endregion

        #region Audio
        public virtual bool IsAudio()
        {
            var documentMedia = Media as TLMessageMediaDocument;
            return documentMedia != null && IsAudio(documentMedia.Document);
        }

        public static bool IsAudio(TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsAudio(document, document.Size);
        }

        public static bool IsAudio(TLDocument document, int size)
        {
            var audioAttribute = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
            return audioAttribute != null && !audioAttribute.IsVoice;
        }
        #endregion

        #region Voice
        public virtual bool IsVoice()
        {
            var documentMedia = Media as TLMessageMediaDocument;
            return documentMedia != null && IsVoice(documentMedia.Document);
        }

        public static bool IsVoice(TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            return document != null && IsVoice(document, document.Size);
        }

        public static bool IsVoice(TLDocument document, int size)
        {
            var audioAttribute = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
            return audioAttribute != null && audioAttribute.IsVoice;
        }
        #endregion

        #region Sticker
        public bool IsSticker()
        {
            var documentMedia = Media as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                return IsSticker(documentMedia.Document);
            }
            return false;
        }

        public static bool IsSticker(TLDocumentBase documentBase)
        {
            var document = documentBase as TLDocument;
            if (document != null && document.Size > 0 && document.Size < 262144)
            {
                var attribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (attribute != null && string.Equals(document.MimeType.ToString(), "image/webp", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Helpers
        public static bool isStickerDocument(TLDocument document)
        {
            if (document != null)
            {
                for (int a = 0; a < document.Attributes.Count; a++)
                {
                    TLDocumentAttributeBase attribute = document.Attributes[a];
                    if (attribute is TLDocumentAttributeSticker)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool isMaskDocument(TLDocument document)
        {
            if (document != null)
            {
                for (int a = 0; a < document.Attributes.Count; a++)
                {
                    TLDocumentAttributeBase attribute = document.Attributes[a];
                    if (attribute is TLDocumentAttributeSticker stickerAttribute && stickerAttribute.IsMask)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool isVoiceDocument(TLDocument document)
        {
            if (document != null)
            {
                for (int a = 0; a < document.Attributes.Count; a++)
                {
                    TLDocumentAttributeBase attribute = document.Attributes[a];
                    if (attribute is TLDocumentAttributeAudio audioAttribute)
                    {
                        return audioAttribute.IsVoice;
                    }
                }
            }
            return false;
        }

        public static bool isVoiceWebDocument(TLWebDocument webDocument)
        {
            return webDocument != null && webDocument.MimeType.Equals("audio/ogg");
        }

        public static bool isImageWebDocument(TLWebDocument webDocument)
        {
            return webDocument != null && webDocument.MimeType.StartsWith("image/");
        }

        public static bool isVideoWebDocument(TLWebDocument webDocument)
        {
            return webDocument != null && webDocument.MimeType.StartsWith("video/");
        }

        public static bool isMusicDocument(TLDocument document)
        {
            if (document != null)
            {
                for (int a = 0; a < document.Attributes.Count; a++)
                {
                    TLDocumentAttributeBase attribute = document.Attributes[a];
                    if (attribute is TLDocumentAttributeAudio audioAttribute)
                    {
                        return !audioAttribute.IsVoice;
                    }
                }
            }
            return false;
        }

        public static bool isVideoDocument(TLDocument document)
        {
            if (document != null)
            {
                bool isAnimated = false;
                bool isVideo = false;
                int width = 0;
                int height = 0;

                for (int a = 0; a < document.Attributes.Count; a++)
                {
                    TLDocumentAttributeBase attribute = document.Attributes[a];
                    if (attribute is TLDocumentAttributeVideo videoAttribute)
                    {
                        isVideo = true;
                        width = videoAttribute.W;
                        height = videoAttribute.H;
                    }
                    else if (attribute is TLDocumentAttributeAnimated)
                    {
                        isAnimated = true;
                    }
                }
                if (isAnimated && (width > 1280 || height > 1280))
                {
                    isAnimated = false;
                }
                return isVideo && !isAnimated;
            }
            return false;
        }
        #endregion

        public TLMessage Self
        {
            get
            {
                return this;
            }
        }

        public Visibility StickerReplyVisibility
        {
            get
            {
                return ReplyVisibility == Visibility.Visible || HasViaBotId || (HasFwdFrom && IsRoundVideo()) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public override TLMessageState State
        {
            get
            {
                if (!IsUnread)
                {
                    return TLMessageState.Read;
                }
                return _state;
            }
            set
            {
                if (_state != value)
                {
                    _state = value;
                    RaisePropertyChanged();
                }
            }
        }

        public override void Edit(TLMessageBase messageBase)
        {
            var message = messageBase as TLMessage;
            if (message != null)
            {
                HasEditDate = message.HasEditDate;
                EditDate = message.EditDate;

                Message = message.Message;
                HasEntities = message.HasEntities;
                Entities = message.Entities;
                ReplyMarkup = message.ReplyMarkup;
                var webpageOld = Media as TLMessageMediaWebPage;
                var webpageNew = message.Media as TLMessageMediaWebPage;
                if ((webpageOld == null && webpageNew != null) || (webpageOld != null && webpageNew == null) || (webpageOld != null && webpageNew != null && webpageOld.WebPage.Id != webpageNew.WebPage.Id))
                {
                    Media = (TLMessageMediaBase)webpageNew ?? new TLMessageMediaEmpty();
                }

                var captionNew = message.Media as ITLMessageMediaCaption;
                var captionOld = Media as ITLMessageMediaCaption;
                if (captionOld != null && captionNew != null)
                {
                    captionOld.Caption = captionNew.Caption;
                }

                var invoiceNew = message.Media as TLMessageMediaInvoice;
                var invoiceOld = Media as TLMessageMediaInvoice;
                if (invoiceOld != null && invoiceNew != null)
                {
                    Media = invoiceNew;
                }

                var geoLiveNew = message.Media as TLMessageMediaGeoLive;
                var geoLiveOld = Media as TLMessageMediaGeoLive;
                if (geoLiveOld != null && geoLiveNew != null)
                {
                    Media = geoLiveNew;
                }
            }
        }

        public override void Update(TLMessageBase message)
        {
            base.Update(message);
            var m = (TLMessage)message;
            FromId = m.FromId;
            ToId = m.ToId;
            IsOut = m.IsOut;
            GroupedId = m.GroupedId;
            HasGroupedId = m.HasGroupedId;
            if (IsUnread != IsUnread)
            {
                if (IsUnread)
                {
                    IsUnread = m.IsUnread;
                }
            }
            Date = m.Date;



            FwdFrom = m.FwdFrom;

            if (m.Views != null)
            {
                var currentViews = Views ?? 0;
                if (currentViews < m.Views)
                {
                    Views = m.Views;
                }
            }



            if (m.Entities != null)
            {
                Entities = m.Entities;
            }



            if (m.ReplyMarkup != null)
            {
                //var oldCustomFlags = ReplyMarkup != null ? ReplyMarkup.CustomFlags : null;
                ReplyMarkup = m.ReplyMarkup;
                //ReplyMarkup.CustomFlags = oldCustomFlags;
            }

            //if (m.CustomFlags != null)
            //{
            //    CustomFlags = m.CustomFlags;
            //}



            //FwdFromId = m.FwdFromId;
            //FwdDate = m.FwdDate;
            if (m.HasReplyToMsgId)
            {
                ReplyToMsgId = m.ReplyToMsgId;

                if (m.Reply != null)
                {
                    Reply = m.Reply;
                }
            }







            Message = m.Message;
            var oldMedia = Media;
            var newMedia = m.Media;
            if (oldMedia?.GetType() != newMedia?.GetType())
            {
                Media = m.Media;
            }
            else
            {
                var oldMediaDocument = oldMedia as TLMessageMediaDocument;
                var newMediaDocument = newMedia as TLMessageMediaDocument;
                if (oldMediaDocument != null && newMediaDocument != null)
                {
                    if (oldMediaDocument.Document == null || oldMediaDocument.Document.GetType() != newMediaDocument.Document.GetType())
                    {
                        Media = m.Media;
                        RaisePropertyChanged(() => Media);
                    }
                    else
                    {
                        var oldDocument = oldMediaDocument.Document as TLDocument;
                        var newDocument = newMediaDocument.Document as TLDocument;
                        if (oldDocument != null
                            && newDocument != null
                            && (oldDocument.Id != newDocument.Id
                                || oldDocument.AccessHash != newDocument.AccessHash))
                        {
                            //var isoFileName = Media.IsoFileName;
#if WP8
                            var file = Media.File;
#endif
                            Media = m.Media;
                            RaisePropertyChanged(() => Media);
                            //Media.IsoFileName = isoFileName;
#if WP8
                            _media.File = file;
#endif
                        }
                    }

                    return;
                }

                //var oldMediaVideo = oldMedia as TLMessageMediaVideo;
                //var newMediaVideo = newMedia as TLMessageMediaVideo;
                //if (oldMediaVideo != null && newMediaVideo != null)
                //{
                //    if (oldMediaVideo.Video.GetType() != newMediaVideo.Video.GetType())
                //    {
                //        Media = m.Media;
                //    }
                //    else
                //    {
                //        var oldVideo = oldMediaVideo.Video as TLVideo;
                //        var newVideo = newMediaVideo.Video as TLVideo;
                //        if (oldVideo != null
                //            && newVideo != null
                //            && (oldVideo.Id.Value != newVideo.Id.Value
                //                || oldVideo.AccessHash.Value != newVideo.AccessHash.Value))
                //        {
                //            //var isoFileName = Media.IsoFileName;
                //            Media = m.Media;
                //            //Media.IsoFileName = isoFileName;
                //        }
                //    }

                //    return;
                //}

                //var oldMediaAudio = oldMedia as TLMessageMediaAudio;
                //var newMediaAudio = newMedia as TLMessageMediaAudio;
                //if (oldMediaAudio != null && newMediaAudio != null)
                //{
                //    if (oldMediaAudio.Audio.GetType() != newMediaAudio.Audio.GetType())
                //    {
                //        Media = m.Media;
                //    }
                //    else
                //    {
                //        var oldAudio = oldMediaAudio.Audio as TLAudio;
                //        var newAudio = newMediaAudio.Audio as TLAudio;
                //        if (oldAudio != null
                //            && newAudio != null
                //            && (oldAudio.Id.Value != newAudio.Id.Value
                //                || oldAudio.AccessHash.Value != newAudio.AccessHash.Value))
                //        {
                //            //var isoFileName = Media.IsoFileName;
                //            //var notListened = Media.NotListened;
                //            Media = m.Media;
                //            //Media.IsoFileName = isoFileName;
                //            //Media.NotListened = notListened;
                //        }
                //    }

                //    return;
                //}

                var oldMediaPhoto = oldMedia as TLMessageMediaPhoto;
                var newMediaPhoto = newMedia as TLMessageMediaPhoto;
                if (oldMediaPhoto == null || newMediaPhoto == null)
                {
                    Media = m.Media;
                }
                else
                {
                    var oldPhoto = oldMediaPhoto.Photo as TLPhoto;
                    var newPhoto = newMediaPhoto.Photo as TLPhoto;
                    if (oldPhoto == null || newPhoto == null)
                    {
                        Media = m.Media;
                    }
                    else
                    {
                        if (oldPhoto.AccessHash != newPhoto.AccessHash)
                        {
                            Media = m.Media;
                        }
                    }
                }
            }
        }

        private TLUser _viaBot;
        public TLUser ViaBot
        {
            get
            {
                if (_viaBot == null && HasViaBotId && ViaBotId.HasValue)
                    _viaBot = InMemoryCacheService.Current.GetUser(ViaBotId) as TLUser;

                return _viaBot;
            }
        }

        private TLUserBase _fwdFromUser;
        public TLUserBase FwdFromUser
        {
            get
            {
                if (_fwdFromUser == null && HasFwdFrom && FwdFrom != null && FwdFrom.HasFromId)
                    _fwdFromUser = InMemoryCacheService.Current.GetUser(FwdFrom.FromId);

                return _fwdFromUser;
            }
        }

        private TLChatBase _fwdFromChannel;
        public TLChatBase FwdFromChannel
        {
            get
            {
                if (_fwdFromChannel == null && HasFwdFrom && FwdFrom != null && FwdFrom.HasChannelId)
                    _fwdFromChannel = InMemoryCacheService.Current.GetChat(FwdFrom.ChannelId);

                return _fwdFromChannel;
            }
        }

        public long InlineBotResultQueryId { get; set; }

        public string InlineBotResultId { get; set; }
    }

    public partial class TLMessageFwdHeader
    {
        private TLUserBase _user;
        public TLUserBase User
        {
            get
            {
                if (_user == null && HasFromId)
                    _user = InMemoryCacheService.Current.GetUser(FromId);

                return _user;
            }
        }

        private TLChatBase _channel;
        public TLChatBase Channel
        {
            get
            {
                if (_channel == null && HasChannelId)
                    _channel = InMemoryCacheService.Current.GetChat(ChannelId);

                return _channel;
            }
        }

    }

    public partial class TLMessage
    {
        public TLMessage Clone()
        {
            var clone = new TLMessage();
            clone.Flags = Flags;
            clone.Id = Id;
            clone.FromId = FromId;
            clone.ToId = ToId;
            clone.FwdFrom = FwdFrom;
            clone.ViaBotId = ViaBotId;
            clone.ReplyToMsgId = ReplyToMsgId;
            clone.Date = Date;
            clone.Message = Message;
            clone.Media = Media;
            clone.ReplyMarkup = ReplyMarkup;
            clone.Entities = Entities;
            clone.Views = Views;
            clone.EditDate = EditDate;

            return clone;
        }
    }
}
