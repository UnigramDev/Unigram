using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    public class AspectView : Grid
    {
        #region Constraint

        public object Constraint
        {
            get { return (object)GetValue(ConstraintProperty); }
            set { SetValue(ConstraintProperty, value); }
        }

        public static readonly DependencyProperty ConstraintProperty =
            DependencyProperty.Register("Constraint", typeof(object), typeof(AspectView), new PropertyMetadata(null, OnConstraintChanged));

        private static void OnConstraintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AspectView)d).InvalidateMeasure();
        }

        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Constraint == null)
            {
                return base.MeasureOverride(availableSize);
            }

            var availableWidth = Math.Min(availableSize.Width, Math.Min(double.IsNaN(Width) ? double.PositiveInfinity : Width, MaxWidth));
            var availableHeight = Math.Min(availableSize.Height, Math.Min(double.IsNaN(Height) ? double.PositiveInfinity : Height, MaxHeight));

            var width = 0.0;
            var height = 0.0;

            var constraint = Constraint;

            if (constraint is TLMessageMediaGeo || constraint is TLMessageMediaGeoLive || constraint is TLMessageMediaVenue)
            {
                width = 320;
                height = 240;

                goto Calculate;
            }

            if (constraint is TLMessageMediaPhoto photoMedia)
            {
                if (photoMedia.HasTTLSeconds)
                {
                    width = 240;
                    height = 240;

                    goto Calculate;
                }
                else
                {
                    constraint = photoMedia.Photo;
                }
            }

            if (constraint is TLMessageMediaDocument documentMedia)
            {
                if (documentMedia.HasTTLSeconds)
                {
                    width = 240;
                    height = 240;

                    goto Calculate;
                }
                else
                {
                    constraint = documentMedia.Document;
                }
            }

            if (constraint is TLMessageMediaWebPage webPageMedia)
            {
                constraint = webPageMedia.WebPage;
            }

            if (constraint is TLPhoto photo)
            {
                //var photoSize = photo.Sizes.OrderByDescending(x => x.W).FirstOrDefault();
                constraint = photo.Full;
            }

            if (constraint is TLPhotoSize photoSize)
            {
                width = photoSize.W;
                height = photoSize.H;

                goto Calculate;
            }

            if (constraint is TLDocument document)
            {
                constraint = document.Attributes;
            }

            if (constraint is TLWebDocument webDocument)
            {
                constraint = webDocument.Attributes;
            }

            if (constraint is TLWebPage webPage)
            {
                width = webPage.EmbedWidth ?? 320;
                height = webPage.EmbedHeight ?? 240;

                goto Calculate;
            }

            if (constraint is TLVector<TLDocumentAttributeBase> attributes)
            {
                var imageSize = attributes.OfType<TLDocumentAttributeImageSize>().FirstOrDefault();
                if (imageSize != null)
                {
                    width = imageSize.W;
                    height = imageSize.H;

                    goto Calculate;
                }

                var video = attributes.OfType<TLDocumentAttributeVideo>().FirstOrDefault();
                if (video != null)
                {
                    if (video.IsRoundMessage)
                    {
                        width = 200;
                        height = 200;
                    }
                    else
                    {
                        width = video.W;
                        height = video.H;
                    }

                    goto Calculate;
                }
            }

            if (constraint is TLBotInlineResult inlineResult)
            {
                width = inlineResult.HasW ? inlineResult.W.Value : 0;
                height = inlineResult.HasH ? inlineResult.H.Value : 0;

                goto Calculate;
            }

            Calculate:
            if (width > availableWidth || height > availableHeight)
            {
                var ratioX = availableWidth / width;
                var ratioY = availableHeight / height;
                var ratio = Math.Min(ratioX, ratioY);

                return base.MeasureOverride(new Size(width * ratio, height * ratio));
            }
            else
            {
                return base.MeasureOverride(new Size(width, height));
            }
        }
    }
}
