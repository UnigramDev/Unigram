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
    public class MediaTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AudioTemplate { get; set; }
        public DataTemplate ContactTemplate { get; set; }
        public DataTemplate DocumentTemplate { get; set; }
        public DataTemplate DocumentThumbTemplate { get; set; }
        public DataTemplate EmptyTemplate { get; set; }
        public DataTemplate GameTemplate { get; set; }
        public DataTemplate GeoPointTemplate { get; set; }
        public DataTemplate GifTemplate { get; set; }
        public DataTemplate PhotoTemplate { get; set; }
        public DataTemplate UnsupportedTemplate { get; set; }
        public DataTemplate VenueTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }
        public DataTemplate WebPageGifTemplate { get; set; }
        public DataTemplate WebPageDocumentTemplate { get; set; }
        public DataTemplate WebPagePendingTemplate { get; set; }
        public DataTemplate WebPagePhotoTemplate { get; set; }
        public DataTemplate WebPageSmallPhotoTemplate { get; set; }
        public DataTemplate WebPageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var message = item as TLMessage;
            if (message != null)
            {
                item = message.Media;
            }

            var emptyMedia = item as TLMessageMediaEmpty;
            if (emptyMedia != null)
            {
                return EmptyTemplate;
            }

            var contactMedia = item as TLMessageMediaContact;
            if (contactMedia != null)
            {
                return ContactTemplate;
            }

            var photoMedia = item as TLMessageMediaPhoto;
            if (photoMedia != null)
            {
                return PhotoTemplate;
            }

            var gameMedia = item as TLMessageMediaGame;
            if (gameMedia != null)
            {
                return GameTemplate;
            }

            var documentMedia = item as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                var document = documentMedia.Document as TLDocument;
                if (document != null)
                {
                    if (TLMessage.IsVoice(document))
                    {
                        return AudioTemplate;
                    }
                    if (TLMessage.IsVideo(document))
                    {
                        return VideoTemplate;
                    }
                    if (TLMessage.IsGif(document))
                    {
                        return GifTemplate;
                    }

                    // TODO: ???
                    //var externalDocument = documentMedia.Document as TLDocumentExternal;
                    //if (externalDocument != null && TLMessage.IsGif(externalDocument))
                    //{
                    //    return GifTemplate;
                    //}

                    if (document.Thumb != null && !(document.Thumb is TLPhotoSizeEmpty))
                    {
                        return DocumentThumbTemplate;
                    }
                }

                return DocumentTemplate;
            }
            else
            {
                //var videoMedia = item as TLMessageMediaVideo;
                //if (videoMedia != null)
                //{
                //    return VideoTemplate;
                //}

                var venueMedia = item as TLMessageMediaVenue;
                if (venueMedia != null)
                {
                    return VenueTemplate;
                }

                var geoMedia = item as TLMessageMediaGeo;
                if (geoMedia != null)
                {
                    return GeoPointTemplate;
                }

                //var audioMedia = item as TLMessageMediaAudio;
                //if (audioMedia != null)
                //{
                //    return AudioTemplate;
                //}

                var webpageMedia = item as TLMessageMediaWebPage;
                if (webpageMedia == null)
                {
                    return UnsupportedTemplate;
                }

                var emptyWebpage = webpageMedia.WebPage as TLWebPageEmpty;
                if (emptyWebpage != null)
                {
                    return EmptyTemplate;
                }

                var pendingWebpage = webpageMedia.WebPage as TLWebPagePending;
                if (pendingWebpage != null)
                {
                    return EmptyTemplate;
                }

                var webpage = webpageMedia.WebPage as TLWebPage;
                if (webpage != null)
                {
                    if (TLMessage.IsGif(webpage.Document))
                    {
                        return WebPageGifTemplate;
                    }
                    else if (webpage.Document != null && webpage.Type.Equals("document", StringComparison.OrdinalIgnoreCase))
                    {
                        return WebPageDocumentTemplate;
                    }

                    if (webpage.Photo != null && webpage.Type != null)
                    {
                        if (IsWebPagePhotoTemplate(webpage))
                        {
                            return WebPagePhotoTemplate;
                        }

                        return WebPageSmallPhotoTemplate;
                    }
                }
                return WebPageTemplate;
            }
        }

        public static bool IsWebPagePhotoTemplate(TLWebPage webPage)
        {
            if (webPage.Type != null)
            {
                if (string.Equals(webPage.Type, "photo", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(webPage.Type, "video", StringComparison.OrdinalIgnoreCase) || 
                    (webPage.SiteName != null && string.Equals(webPage.SiteName, "twitter", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
