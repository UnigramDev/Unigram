using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Unigram.ViewModels;
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
        public DataTemplate GamePhotoTemplate { get; set; }
        public DataTemplate GeoTemplate { get; set; }
        public DataTemplate GeoLiveTemplate { get; set; }
        public DataTemplate GifTemplate { get; set; }
        public DataTemplate InvoiceTemplate { get; set; }
        public DataTemplate InvoicePhotoTemplate { get; set; }
        public DataTemplate MusicTemplate { get; set; }
        public DataTemplate RoundVideoTemplate { get; set; }
        public DataTemplate PhotoTemplate { get; set; }
        public DataTemplate UnsupportedTemplate { get; set; }
        public DataTemplate VenueTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }
        public DataTemplate StickerTemplate { get; set; }
        public DataTemplate SecretPhotoTemplate { get; set; }
        public DataTemplate SecretVideoTemplate { get; set; }
        public DataTemplate WebPageDocumentTemplate { get; set; }
        public DataTemplate WebPagePendingTemplate { get; set; }
        public DataTemplate WebPagePhotoTemplate { get; set; }
        public DataTemplate WebPageSmallPhotoTemplate { get; set; }
        public DataTemplate WebPageTemplate { get; set; }

        public DataTemplate GroupedTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            var boh = DataTemplate.GetExtensionInstance(container as FrameworkElement);

            var presenter = container as ContentControl;
            if (presenter != null && item is TLDocument doc)
            {
                presenter.Content = new TLMessage { Media = new TLMessageMediaDocument { Document = doc } };
            }

            if (item is TLMessage message)
            {
                item = message.Media;
            }

            if (item is TLMessageMediaEmpty)
            {
                return EmptyTemplate;
            }
            else if (item is TLMessageMediaContact)
            {
                return ContactTemplate;
            }
            else if (item is TLMessageMediaPhoto photoMedia)
            {
                if (photoMedia.HasTTLSeconds)
                {
                    return SecretPhotoTemplate;
                }

                return PhotoTemplate;
            }
            else if (item is TLMessageMediaGame gameMedia)
            {
                return gameMedia.Game.HasDocument ? GameTemplate : GamePhotoTemplate;
            }
            else if (item is TLMessageMediaVenue)
            {
                return VenueTemplate;
            }
            else if (item is TLMessageMediaGeo)
            {
                return GeoTemplate;
            }
            else if (item is TLMessageMediaGeoLive)
            {
                return GeoLiveTemplate;
            }
            else if (item is TLMessageMediaInvoice invoiceMedia)
            {
                if (invoiceMedia.HasPhoto && invoiceMedia.Photo != null)
                {
                    return InvoicePhotoTemplate;
                }

                return InvoiceTemplate;
            }
            else if (item is TLMessageMediaDocument || item is TLDocument)
            {
                if (item is TLMessageMediaDocument documentMedia)
                {
                    if (documentMedia.HasTTLSeconds)
                    {
                        return SecretVideoTemplate;
                    }

                    item = documentMedia.Document;
                }

                if (item is TLDocument document)
                {
                    if (TLMessage.IsVoice(document))
                    {
                        return AudioTemplate;
                    }
                    else if (TLMessage.IsVideo(document))
                    {
                        return VideoTemplate;
                    }
                    else if (TLMessage.IsRoundVideo(document))
                    {
                        return RoundVideoTemplate;
                    }
                    else if (TLMessage.IsGif(document))
                    {
                        return GifTemplate;
                    }
                    else if (TLMessage.IsSticker(document))
                    {
                        return StickerTemplate;
                    }
                    else if (TLMessage.IsMusic(document))
                    {
                        return MusicTemplate;
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
            else if (item is TLMessageMediaWebPage webpageMedia)
            {
                if (webpageMedia.WebPage is TLWebPageEmpty)
                {
                    return EmptyTemplate;
                }
                else if (webpageMedia.WebPage is TLWebPagePending)
                {
                    return EmptyTemplate;
                }
                else if (webpageMedia.WebPage is TLWebPage webpage)
                {
                    /*if (TLMessage.IsGif(webpage.Document))
                    {
                        return WebPageGifTemplate;
                    }
                    else
                    if (webpage.Document != null && webpage.Type.Equals("document", StringComparison.OrdinalIgnoreCase))
                    {
                        return WebPageDocumentTemplate;
                    }*/

                    if (webpage.Document is TLDocument)
                    {
                        return WebPageDocumentTemplate;
                    }

                    if (webpage.Photo is TLPhoto && webpage.Type != null)
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
            else if (item is TLMessageMediaGroup)
            {
                return GroupedTemplate;
            }
            else if (item is TLMessageMediaUnsupported)
            {
                return UnsupportedTemplate;
            }

            return null;
        }

        public static bool IsWebPagePhotoTemplate(TLWebPage webPage)
        {
            if (webPage.Photo != null && webPage.Type != null)
            {
                if (string.Equals(webPage.Type, "photo", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(webPage.Type, "video", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                else if (string.Equals(webPage.Type, "article", StringComparison.OrdinalIgnoreCase))
                {
                    var photo = webPage.Photo as TLPhoto;
                    var full = photo?.Full as TLPhotoSize;
                    var fullCache = photo?.Full as TLPhotoCachedSize;

                    return (full?.W > 400 || fullCache?.W > 400) && webPage.HasCachedPage;
                }
            }
            return false;
        }
    }
}
