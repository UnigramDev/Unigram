using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Helpers;
using Windows.UI.Xaml;

namespace Telegram.Api.TL
{
    public abstract partial class TLWebPageBase
    {
        public virtual string Info => null;

        public virtual bool IsInstantGallery() => false;
    }

    public partial class TLWebPage
    {
        private string _info;
        public override string Info
        {
            get
            {
                if (_info == null)
                {
                    if (IsInstantGallery())
                    {
                        _info = string.Format(LocaleHelper.GetString("Of"), 1, CountWebPageMedia(this));
                    }
                    else
                    {
                        _info = string.Empty;
                    }
                }

                return _info;
            }
        }

        public override bool IsInstantGallery() => HasCachedPage && (string.Equals(SiteName, "twitter", StringComparison.OrdinalIgnoreCase) || string.Equals(SiteName, "instagram", StringComparison.OrdinalIgnoreCase));

        #region Instant Gallery

        public static TLPhotoBase GetPhotoWithId(TLWebPage webPage, long id)
        {
            if (webPage == null || webPage.CachedPage == null)
            {
                return null;
            }

            if (webPage.Photo != null && webPage.Photo.Id == id)
            {
                return webPage.Photo;
            }

            foreach (var photo in webPage.CachedPage.Photos)
            {
                if (photo.Id == id)
                {
                    return photo;
                }
            }

            return null;
        }

        public static TLDocumentBase GetDocumentWithId(TLWebPage webPage, long id)
        {
            if (webPage == null || webPage.CachedPage == null)
            {
                return null;
            }

            if (webPage.Document != null && webPage.Document.Id == id)
            {
                return webPage.Document;
            }

            foreach (var document in webPage.CachedPage.Documents)
            {
                if (document.Id == id)
                {
                    return document;
                }
            }

            return null;
        }

        private static int CountBlock(TLWebPage webPage, TLPageBlockBase pageBlock, int count)
        {
            if (pageBlock is TLPageBlockPhoto photoBlock)
            {
                var photo = GetPhotoWithId(webPage, photoBlock.PhotoId) as TLPhoto;
                if (photo == null)
                {
                    return count;
                }

                return count + 1;
            }
            else if (pageBlock is TLPageBlockVideo videoBlock)
            {
                var document = GetDocumentWithId(webPage, videoBlock.VideoId) as TLDocument;
                if (document == null)
                {
                    return count;
                }

                return count + 1;
            }

            return count;
        }

        public static int CountWebPageMedia(TLWebPage webPage)
        {
            var result = 0;
            var blocks = webPage.CachedPage?.Blocks ?? new TLVector<TLPageBlockBase>();

            foreach (var block in blocks)
            {
                if (block is TLPageBlockSlideshow slideshow)
                {
                    foreach (var item in slideshow.Items)
                    {
                        result = CountBlock(webPage, item, result);
                    }
                }
                else if (block is TLPageBlockCollage collage)
                {
                    foreach (var item in collage.Items)
                    {
                        result = CountBlock(webPage, item, result);
                    }
                }
            }

            return result;
        }

        #endregion
    }
}
