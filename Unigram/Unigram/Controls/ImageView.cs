using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Foundation;
using Telegram.Api.TL;

namespace Unigram.Controls
{
    public class ImageView : Control
    {
        public ImageView()
        {
            DefaultStyleKey = typeof(ImageView);
        }

        #region Source

        public ImageSource Source
        {
            get { return (ImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(ImageSource), typeof(ImageView), new PropertyMetadata(null));

        #endregion

        #region Constraint

        public object Constraint
        {
            get { return (object)GetValue(ConstraintProperty); }
            set { SetValue(ConstraintProperty, value); }
        }

        public static readonly DependencyProperty ConstraintProperty =
            DependencyProperty.Register("Constraint", typeof(object), typeof(ImageView), new PropertyMetadata(null));

        #endregion

        protected override Size MeasureOverride(Size availableSize)
        {
            //if (availableSize.Width > MaxWidth || availableSize.Height > MaxHeight)
            //{
            //    return base.MeasureOverride(availableSize);
            //}

            var availableWidth = Math.Min(availableSize.Width, Math.Min(double.IsNaN(Width) ? double.PositiveInfinity : Width, MaxWidth));
            var availableHeight = Math.Min(availableSize.Height, Math.Min(double.IsNaN(Height) ? double.PositiveInfinity : Height, MaxHeight));

            var width = 0.0;
            var height = 0.0;

            var photo = Constraint as TLPhoto;
            if (photo != null)
            {
                //var photoSize = photo.Sizes.Where(x => x is TLPhotoSize || x is TLPhotoCachedSize).FirstOrDefault();
                var photoSize = photo.Sizes.OrderByDescending(x => x.W).FirstOrDefault();
                if (photoSize != null)
                {
                    width = photoSize.W;
                    height = photoSize.H;
                }
            }

            if (width > availableWidth || height > availableHeight)
            {
                var ratioX = availableWidth / width;
                var ratioY = availableHeight / height;
                var ratio = Math.Min(ratioX, ratioY);

                return new Size(width * ratio, height * ratio);
            }
            else
            {
                return new Size(width, height);
            }
        }
    }
}
