using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class StickerView : ImageView
    {
        protected override void OnConstraintChanged(object newValue, object oldValue)
        {
            var document = newValue as TLDocument;
            if (document != null)
            {
                var stickerAttribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (stickerAttribute != null)
                {
                    IsHitTestVisible = stickerAttribute.StickerSet.TypeId != TLType.InputStickerSetEmpty;
                }
            }
        }

        //protected override Size MeasureOverride(Size availableSize)
        //{
        //    if (Constraint == null)
        //    {
        //        return base.MeasureOverride(availableSize);
        //    }

        //    var availableWidth = Math.Min(availableSize.Width, Math.Min(double.IsNaN(Width) ? double.PositiveInfinity : Width, availableSize.Width < 500 ? 180 : 256));
        //    var availableHeight = Math.Min(availableSize.Height, Math.Min(double.IsNaN(Height) ? double.PositiveInfinity : Height, availableSize.Width < 500 ? 180 : 256));

        //    var width = 0.0;
        //    var height = 0.0;

        //    if (Constraint is TLMessageMediaGeo || Constraint is TLMessageMediaVenue)
        //    {
        //        width = 320;
        //        height = 240;

        //        goto Calculate;
        //    }

        //    var photo = Constraint as TLPhoto;
        //    if (photo != null)
        //    {
        //        //var photoSize = photo.Sizes.OrderByDescending(x => x.W).FirstOrDefault();
        //        var photoSize = photo.Sizes.OfType<TLPhotoSize>().OrderByDescending(x => x.W).FirstOrDefault();
        //        if (photoSize != null)
        //        {
        //            width = photoSize.W;
        //            height = photoSize.H;

        //            goto Calculate;
        //        }
        //    }

        //    var document = Constraint as TLDocument;
        //    if (document != null)
        //    {
        //        var imageSize = document.Attributes.OfType<TLDocumentAttributeImageSize>().FirstOrDefault();
        //        if (imageSize != null)
        //        {
        //            width = imageSize.W;
        //            height = imageSize.H;

        //            goto Calculate;
        //        }

        //        var video = document.Attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
        //        if (video != null)
        //        {
        //            width = video.W;
        //            height = video.H;

        //            goto Calculate;
        //        }
        //    }

        //    var inlineResult = Constraint as TLBotInlineResult;
        //    if (inlineResult != null)
        //    {
        //        width = inlineResult.HasW ? inlineResult.W.Value : 0;
        //        height = inlineResult.HasH ? inlineResult.H.Value : 0;

        //        goto Calculate;
        //    }

        //    Calculate:
        //    if (width > availableWidth || height > availableHeight)
        //    {
        //        var ratioX = availableWidth / width;
        //        var ratioY = availableHeight / height;
        //        var ratio = Math.Min(ratioX, ratioY);

        //        return new Size(width * ratio, height * ratio);
        //    }
        //    else
        //    {
        //        return new Size(width, height);
        //    }
        //}
    }
}
