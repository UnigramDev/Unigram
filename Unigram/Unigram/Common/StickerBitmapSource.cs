using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Common
{
    public class StickerBitmapSource : BitmapSource
    {
        public bool IsSet { get; private set; }

        public void SetStream(IRandomAccessStream streamSource)
        {
            IsSet = true;
            SetSource(streamSource);
        }

        public IAsyncAction SetStreamAsync(IRandomAccessStream streamSource)
        {
            IsSet = true;
            return SetSourceAsync(streamSource);
        }

        #region Source

        public static object GetSource(DependencyObject obj)
        {
            return (object)obj.GetValue(SourceProperty);
        }

        public static void SetSource(DependencyObject obj, object value)
        {
            obj.SetValue(SourceProperty, value);
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.RegisterAttached("Source", typeof(object), typeof(Image), new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = d as Image;
            var document = e.NewValue as TLDocument;
            if (document != null)
            {
                TLDocumentAttributeImageSize imageSize = null;
                for (int i = 0; i < document.Attributes.Count; i++)
                {
                    imageSize = (document.Attributes[i] as TLDocumentAttributeImageSize);
                    if (imageSize != null)
                    {
                        break;
                    }
                }
                if (imageSize != null)
                {
                    var maximum = Math.Max(imageSize.W, imageSize.H);
                    if (maximum > sender.MaxWidth)
                    {
                        var ratioX = sender.MaxWidth / (double)imageSize.W;
                        var ratioY = sender.MaxHeight / (double)imageSize.H;
                        var ratio = Math.Min(ratioX, ratioY);

                        sender.Width = (imageSize.W * ratio);
                        sender.Height = (imageSize.H * ratio);
                    }
                }
            }
        }

        #endregion

    }
}
