﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Foundation;
using Telegram.Api.TL;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls
{
    public class ImageView : HyperlinkButton
    {
        private Image Holder;

        public ImageView()
        {
            DefaultStyleKey = typeof(ImageView);
        }

        protected override void OnApplyTemplate()
        {
            Holder = (Image)GetTemplateChild("Holder");
            Holder.ImageFailed += Holder_ImageFailed;
            Holder.ImageOpened += Holder_ImageOpened;
            Holder.Loaded += Holder_Loaded;
        }

        private void Holder_Loaded(object sender, RoutedEventArgs e)
        {
            OnSourceChanged(Source, null);
        }

        private void Holder_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            ImageFailed?.Invoke(this, e);
        }

        private void Holder_ImageOpened(object sender, RoutedEventArgs e)
        {
            ImageOpened?.Invoke(this, e);
        }

        #region Source

        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ImageSource), typeof(ImageView), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImageView)d).OnSourceChanged((ImageSource)e.NewValue, (ImageSource)e.OldValue);
        }

        private void OnSourceChanged(ImageSource newValue, ImageSource oldValue)
        {
            if (newValue is WriteableBitmap bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
            {
                ImageOpened?.Invoke(this, null);
            }
        }

        #endregion

        #region Constraint

        public object Constraint
        {
            get { return (object)GetValue(ConstraintProperty); }
            set { SetValue(ConstraintProperty, value); }
        }

        public static readonly DependencyProperty ConstraintProperty =
            DependencyProperty.Register("Constraint", typeof(object), typeof(ImageView), new PropertyMetadata(null, OnConstraintChanged));

        private static void OnConstraintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImageView)d).OnConstraintChanged(e.NewValue, e.OldValue);
            ((ImageView)d).InvalidateMeasure();
        }

        protected virtual void OnConstraintChanged(object newValue, object oldValue)
        {

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

            if (constraint is TLMessageMediaGeo || constraint is TLMessageMediaVenue)
            {
                width = 320;
                height = 240;

                goto Calculate;
            }

            if (constraint is TLMessageMediaPhoto photoMedia)
            {
                constraint = photoMedia.Photo;
            }

            if (constraint is TLMessageMediaDocument documentMedia)
            {
                constraint = documentMedia.Document;
            }

            if (constraint is TLPhoto photo)
            {
                //var photoSize = photo.Sizes.OrderByDescending(x => x.W).FirstOrDefault();
                var photoSize = photo.Sizes.OfType<TLPhotoSize>().OrderByDescending(x => x.W).FirstOrDefault();
                if (photoSize != null)
                {
                    width = photoSize.W;
                    height = photoSize.H;

                    goto Calculate;
                }
            }

            if (constraint is TLDocument document)
            {
                constraint = document.Attributes;
            }

            if (constraint is TLWebDocument webDocument)
            {
                constraint = webDocument.Attributes;
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
                    width = video.W;
                    height = video.H;

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

                //if (Holder != null)
                //{
                //    Holder.Width = width * ratio;
                //    Holder.Height = height * ratio;
                //}

                return new Size(width * ratio, height * ratio);
            }
            else
            {
                return new Size(width, height);
            }
        }

        public event ExceptionRoutedEventHandler ImageFailed;

        public event RoutedEventHandler ImageOpened;
    }
}
