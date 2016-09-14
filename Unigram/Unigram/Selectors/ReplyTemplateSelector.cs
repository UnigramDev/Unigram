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
        public DataTemplate WebPageEmptyTemplate { get; set; }
        public DataTemplate WebPagePendingTemplate { get; set; }
        public DataTemplate WebPageTemplate { get; set; }

        public DataTemplate ForwardedMessagesTemplate { get; set; }
        public DataTemplate ForwardEmptyTemplate { get; set; }
        public DataTemplate ForwardTextTemplate { get; set; }
        public DataTemplate ForwardContactTemplate { get; set; }
        public DataTemplate ForwardPhotoTemplate { get; set; }
        public DataTemplate ForwardVideoTemplate { get; set; }
        public DataTemplate ForwardGeoPointTemplate { get; set; }
        public DataTemplate ForwardDocumentTemplate { get; set; }
        public DataTemplate ForwardStickerTemplate { get; set; }

        public DataTemplate ForwardAudioTemplate { get; set; }
        public DataTemplate ForwardUnsupportedTemplate { get; set; }

        public DataTemplate ReplyEmptyTemplate { get; set; }
        public DataTemplate ReplyLoadingTemplate { get; set; }
        public DataTemplate ReplyTextTemplate { get; set; }
        public DataTemplate ReplyContactTemplate { get; set; }
        public DataTemplate ReplyPhotoTemplate { get; set; }
        public DataTemplate ReplyVideoTemplate { get; set; }
        public DataTemplate ReplyGeoPointTemplate { get; set; }
        public DataTemplate ReplyDocumentTemplate { get; set; }
        public DataTemplate ReplyStickerTemplate { get; set; }
        public DataTemplate ReplyAudioTemplate { get; set; }
        public DataTemplate ReplyUnsupportedTemplate { get; set; }
        public DataTemplate ReplyServiceTextTemplate { get; set; }
        public DataTemplate ReplyServicePhotoTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item == null)
            {
                return null;
            }

            var replyInfo = item as ReplyInfo;
            if (replyInfo == null)
            {
                return ReplyUnsupportedTemplate;
            }

            if (replyInfo.Reply == null)
            {
                return ReplyLoadingTemplate;
            }

            //var container = replyInfo.Reply as TLMessagesContainter;
            //if (container != null)
            //{
            //    return GetMessagesContainerTemplate(container);
            //}

            if (replyInfo.ReplyToMsgId == null || replyInfo.ReplyToMsgId.Value == 0)
            {
                return ReplyUnsupportedTemplate;
            }

            var serviceMessage = replyInfo.Reply as TLMessageService;
            if (serviceMessage != null)
            {
                item = serviceMessage.Action;

                if (item is TLMessageActionChatAddUser)
                {
                    return ReplyServiceTextTemplate;
                }
                if (item is TLMessageActionChatCreate)
                {
                    return ReplyServiceTextTemplate;
                }
                if (item is TLMessageActionChatDeletePhoto)
                {
                    return ReplyServiceTextTemplate;
                }
                if (item is TLMessageActionChatDeleteUser)
                {
                    return ReplyServiceTextTemplate;
                }
                if (item is TLMessageActionChatEditPhoto)
                {
                    return ReplyServicePhotoTemplate;
                }
                if (item is TLMessageActionChatEditTitle)
                {
                    return ReplyServiceTextTemplate;
                }
            }

            var message = replyInfo.Reply as TLMessage;
            if (message != null)
            {
                if (!string.IsNullOrEmpty(message.Message.ToString()))
                {
                    return ReplyTextTemplate;
                }

                item = message.Media;

                if (item is TLMessageMediaEmpty)
                {
                    return ReplyUnsupportedTemplate;
                }
                else if (item is TLMessageMediaContact)
                {
                    return ReplyContactTemplate;
                }
                else if (item is TLMessageMediaPhoto)
                {
                    return ReplyPhotoTemplate;
                }
                else if (item is TLMessageMediaDocument)
                {
                    if (message.IsSticker())
                    {
                        return ReplyStickerTemplate;
                    }

                    return ReplyDocumentTemplate;
                }
                //else if (item is TLMessageMediaVideo)
                //{
                //    return ReplyVideoTemplate;
                //}
                else if (item is TLMessageMediaGeo)
                {
                    return ReplyGeoPointTemplate;
                }
                //else if (item is TLMessageMediaAudio)
                //{
                //    return ReplyAudioTemplate;
                //}
            }

            var emptyMessage = replyInfo.Reply as TLMessageEmpty;
            if (emptyMessage != null)
            {
                return ReplyEmptyTemplate;
            }

            return ReplyUnsupportedTemplate;
        }

        //private DataTemplate GetMessagesContainerTemplate(TLMessagesContainter container)
        //{
        //    if (container.WebPageMedia != null)
        //    {
        //        TLMessageMediaWebPage tLMessageMediaWebPage = container.WebPageMedia as TLMessageMediaWebPage;
        //        if (tLMessageMediaWebPage != null)
        //        {
        //            TLWebPagePending tLWebPagePending = tLMessageMediaWebPage.WebPage as TLWebPagePending;
        //            if (tLWebPagePending != null)
        //            {
        //                return WebPagePendingTemplate;
        //            }
        //            TLWebPage tLWebPage = tLMessageMediaWebPage.WebPage as TLWebPage;
        //            if (tLWebPage != null)
        //            {
        //                return WebPageTemplate;
        //            }
        //            TLWebPageEmpty tLWebPageEmpty = tLMessageMediaWebPage.WebPage as TLWebPageEmpty;
        //            if (tLWebPageEmpty != null)
        //            {
        //                return WebPageEmptyTemplate;
        //            }
        //        }
        //    }
        //    if (container.FwdMessages != null)
        //    {
        //        if (container.FwdMessages.Count == 1)
        //        {
        //            TLMessage25 tLMessage = container.FwdMessages[0];
        //            if (tLMessage != null)
        //            {
        //                TLString message = container.FwdMessages[0].Message;
        //                if (!string.IsNullOrEmpty(message.ToString()))
        //                {
        //                    return ForwardTextTemplate;
        //                }
        //                TLMessageMediaBase media = container.FwdMessages[0].Media;
        //                if (media != null)
        //                {
        //                    TLMessageMediaPhoto tLMessageMediaPhoto = media as TLMessageMediaPhoto;
        //                    if (tLMessageMediaPhoto != null)
        //                    {
        //                        return ForwardPhotoTemplate;
        //                    }
        //                    TLMessageMediaAudio tLMessageMediaAudio = media as TLMessageMediaAudio;
        //                    if (tLMessageMediaAudio != null)
        //                    {
        //                        return ForwardAudioTemplate;
        //                    }
        //                    TLMessageMediaDocument tLMessageMediaDocument = media as TLMessageMediaDocument;
        //                    if (tLMessageMediaDocument != null)
        //                    {
        //                        if (tLMessage.IsSticker())
        //                        {
        //                            return ForwardStickerTemplate;
        //                        }
        //                        return ForwardDocumentTemplate;
        //                    }
        //                    else
        //                    {
        //                        TLMessageMediaVideo tLMessageMediaVideo = media as TLMessageMediaVideo;
        //                        if (tLMessageMediaVideo != null)
        //                        {
        //                            return ForwardVideoTemplate;
        //                        }
        //                        TLMessageMediaGeo tLMessageMediaGeo = media as TLMessageMediaGeo;
        //                        if (tLMessageMediaGeo != null)
        //                        {
        //                            return ForwardGeoPointTemplate;
        //                        }
        //                        TLMessageMediaContact tLMessageMediaContact = media as TLMessageMediaContact;
        //                        if (tLMessageMediaContact != null)
        //                        {
        //                            return ForwardContactTemplate;
        //                        }
        //                        TLMessageMediaEmpty tLMessageMediaEmpty = media as TLMessageMediaEmpty;
        //                        if (tLMessageMediaEmpty != null)
        //                        {
        //                            return ForwardEmptyTemplate;
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //        return ForwardedMessagesTemplate;
        //    }
        //    return ReplyUnsupportedTemplate;
        //}
    }
}
