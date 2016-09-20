using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Selectors
{
    public class ReplyTemplateSelector : DataTemplateSelector
    {
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item == null)
            {
                return null;
            }

            var replyInfo = item as ReplyInfo;
            if (replyInfo == null)
            {
                if (item is TLMessageBase)
                {
                    return GetMessageTemplate(item as TLObject);
                }

                return ReplyUnsupportedTemplate;
            }
            else
            {
                if (replyInfo.Reply == null)
                {
                    return ReplyLoadingTemplate;
                }

                var contain = replyInfo.Reply as TLMessagesContainter;
                if (contain != null)
                {
                    return GetMessagesContainerTemplate(contain);
                }

                if (replyInfo.ReplyToMsgId == null || replyInfo.ReplyToMsgId.Value == 0)
                {
                    return ReplyUnsupportedTemplate;
                }

                return GetMessageTemplate(replyInfo.Reply);
            }
        }

        private DataTemplate GetMessagesContainerTemplate(TLMessagesContainter container)
        {
            if (container.WebPageMedia != null)
            {
                var webpageMedia = container.WebPageMedia as TLMessageMediaWebPage;
                if (webpageMedia != null)
                {
                    var pendingWebpage = webpageMedia.Webpage as TLWebPagePending;
                    if (pendingWebpage != null)
                    {
                        return WebPagePendingTemplate;
                    }

                    var webpage = webpageMedia.Webpage as TLWebPage;
                    if (webpage != null)
                    {
                        return WebPageTemplate;
                    }

                    var emptyWebpage = webpageMedia.Webpage as TLWebPageEmpty;
                    if (emptyWebpage != null)
                    {
                        return WebPageEmptyTemplate;
                    }
                }
            }

            if (container.FwdMessages != null)
            {
                if (container.FwdMessages.Count == 1)
                {
                    var forwardMessage = container.FwdMessages[0];
                    if (forwardMessage != null)
                    {
                        var message = container.FwdMessages[0].Message;
                        if (!string.IsNullOrEmpty(message))
                        {
                            return ForwardTextTemplate;
                        }

                        var media = container.FwdMessages[0].Media;
                        if (media != null)
                        {
                            switch (media.TypeId)
                            {
                                case TLType.MessageMediaPhoto:
                                    return ForwardPhotoTemplate;
                                //case TLType.MessageMediaAudio:
                                //    return ForwardAudioTemplate;
                                //case TLType.MessageMediaVideo:
                                //    return ForwardVideoTemplate;
                                case TLType.MessageMediaGeo:
                                    return ForwardGeoPointTemplate;
                                case TLType.MessageMediaContact:
                                    return ForwardContactTemplate;
                                case TLType.MessageMediaEmpty:
                                    return ForwardEmptyTemplate;
                                case TLType.MessageMediaDocument:
                                    if (forwardMessage.IsVoice())
                                    {
                                        return ForwardVoiceMessageTemplate;
                                    }
                                    if (forwardMessage.IsVideo())
                                    {
                                        return ForwardVideoTemplate;
                                    }
                                    if (forwardMessage.IsGif())
                                    {
                                        return ForwardGifTemplate;
                                    }
                                    if (forwardMessage.IsSticker())
                                    {
                                        return ForwardStickerTemplate;
                                    }

                                    return ForwardDocumentTemplate;
                            }
                        }
                    }
                }

                return ForwardedMessagesTemplate;
            }

            if (container.EditMessage != null)
            {
                var editMessage = container.EditMessage;
                if (editMessage != null)
                {
                    if (!string.IsNullOrEmpty(editMessage.Message) && (editMessage.Media == null || editMessage.Media is TLMessageMediaEmpty || editMessage.Media is TLMessageMediaWebPage))
                    {
                        return EditTextTemplate;
                    }

                    var media = editMessage.Media;
                    if (media != null)
                    {
                        switch (media.TypeId)
                        {
                            case TLType.MessageMediaPhoto:
                                return EditPhotoTemplate;
                            //case TLType.MessageMediaAudio:
                            //    return ForwardAudioTemplate;
                            //case TLType.MessageMediaVideo:
                            //    return ForwardVideoTemplate;
                            case TLType.MessageMediaGeo:
                                return EditGeoPointTemplate;
                            case TLType.MessageMediaContact:
                                return EditContactTemplate;
                            case TLType.MessageMediaEmpty:
                                return EditUnsupportedTemplate;
                            case TLType.MessageMediaDocument:
                                if (editMessage.IsVoice())
                                {
                                    return EditVoiceMessageTemplate;
                                }
                                if (editMessage.IsVideo())
                                {
                                    return EditVideoTemplate;
                                }
                                if (editMessage.IsGif())
                                {
                                    return EditGifTemplate;
                                }
                                if (editMessage.IsSticker())
                                {
                                    return EditStickerTemplate;
                                }

                                return EditDocumentTemplate;
                        }
                    }
                }

                return EditUnsupportedTemplate;
            }

            return ReplyUnsupportedTemplate;
        }

        private DataTemplate GetMessageTemplate(TLObject reply)
        {
            var replyMessage = reply as TLMessage;
            if (replyMessage != null)
            {
                if (!string.IsNullOrEmpty(replyMessage.Message) && (replyMessage.Media == null || replyMessage.Media is TLMessageMediaEmpty || replyMessage.Media is TLMessageMediaWebPage))
                {
                    return this.ReplyTextTemplate;
                }

                var media = replyMessage.Media;
                if (media != null)
                {
                    switch (media.TypeId)
                    {
                        case TLType.MessageMediaPhoto:
                            return ReplyPhotoTemplate;
                        //case TLType.MessageMediaAudio:
                        //    return ForwardAudioTemplate;
                        //case TLType.MessageMediaVideo:
                        //    return ForwardVideoTemplate;
                        case TLType.MessageMediaGeo:
                            return ReplyGeoPointTemplate;
                        case TLType.MessageMediaContact:
                            return ReplyContactTemplate;
                        case TLType.MessageMediaEmpty:
                            return ReplyUnsupportedTemplate;
                        case TLType.MessageMediaDocument:
                            if (replyMessage.IsVoice())
                            {
                                return ReplyVoiceMessageTemplate;
                            }
                            if (replyMessage.IsVideo())
                            {
                                return ReplyVideoTemplate;
                            }
                            if (replyMessage.IsGif())
                            {
                                return ReplyGifTemplate;
                            }
                            if (replyMessage.IsSticker())
                            {
                                return ReplyStickerTemplate;
                            }

                            return ReplyDocumentTemplate;
                    }
                }
            }

            var serviceMessage = reply as TLMessageService;
            if (serviceMessage != null)
            {
                var action = serviceMessage.Action;
                if (action is TLMessageActionChatEditPhoto)
                {
                    return ReplyServicePhotoTemplate;
                }

                return ReplyServiceTextTemplate;
            }
            else
            {
                var emptyMessage = reply as TLMessageEmpty;
                if (emptyMessage != null)
                {
                    return ReplyEmptyTemplate;
                }

                return ReplyUnsupportedTemplate;
            }
        }

        public DataTemplate EditAudioTemplate { get; set; }

        public DataTemplate EditContactTemplate { get; set; }

        public DataTemplate EditDocumentTemplate { get; set; }

        public DataTemplate EditGeoPointTemplate { get; set; }

        public DataTemplate EditGifTemplate { get; set; }

        public DataTemplate EditPhotoTemplate { get; set; }

        public DataTemplate EditStickerTemplate { get; set; }

        public DataTemplate EditTextTemplate { get; set; }

        public DataTemplate EditUnsupportedTemplate { get; set; }

        public DataTemplate EditVideoTemplate { get; set; }

        public DataTemplate EditVoiceMessageTemplate { get; set; }

        public DataTemplate ForwardAudioTemplate { get; set; }

        public DataTemplate ForwardContactTemplate { get; set; }

        public DataTemplate ForwardDocumentTemplate { get; set; }

        public DataTemplate ForwardedMessagesTemplate { get; set; }

        public DataTemplate ForwardEmptyTemplate { get; set; }

        public DataTemplate ForwardGeoPointTemplate { get; set; }

        public DataTemplate ForwardGifTemplate { get; set; }

        public DataTemplate ForwardPhotoTemplate { get; set; }

        public DataTemplate ForwardStickerTemplate { get; set; }

        public DataTemplate ForwardTextTemplate { get; set; }

        public DataTemplate ForwardUnsupportedTemplate { get; set; }

        public DataTemplate ForwardVideoTemplate { get; set; }

        public DataTemplate ForwardVoiceMessageTemplate { get; set; }

        public DataTemplate ReplyAudioTemplate { get; set; }

        public DataTemplate ReplyContactTemplate { get; set; }

        public DataTemplate ReplyDocumentTemplate { get; set; }

        public DataTemplate ReplyEmptyTemplate { get; set; }

        public DataTemplate ReplyGeoPointTemplate { get; set; }

        public DataTemplate ReplyGifTemplate { get; set; }

        public DataTemplate ReplyLoadingTemplate { get; set; }

        public DataTemplate ReplyPhotoTemplate { get; set; }

        public DataTemplate ReplyServicePhotoTemplate { get; set; }

        public DataTemplate ReplyServiceTextTemplate { get; set; }

        public DataTemplate ReplyStickerTemplate { get; set; }

        public DataTemplate ReplyTextTemplate { get; set; }

        public DataTemplate ReplyUnsupportedTemplate { get; set; }

        public DataTemplate ReplyVideoTemplate { get; set; }

        public DataTemplate ReplyVoiceMessageTemplate { get; set; }

        public DataTemplate WebPageEmptyTemplate { get; set; }

        public DataTemplate WebPagePendingTemplate { get; set; }

        public DataTemplate WebPageTemplate { get; set; }
    }
}
