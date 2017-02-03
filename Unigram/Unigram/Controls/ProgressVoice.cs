using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Controls
{
    public class ProgressVoice : ProgressBar
    {
        private Path ProgressBarIndicator;
        private Path HorizontalTrackRect;

        public ProgressVoice()
        {
            DefaultStyleKey = typeof(ProgressVoice);
        }

        protected override void OnApplyTemplate()
        {
            ProgressBarIndicator = (Path)GetTemplateChild("ProgressBarIndicator");
            HorizontalTrackRect = (Path)GetTemplateChild("HorizontalTrackRect");

            OnMediaChanged(Media, Media);

            base.OnApplyTemplate();
        }

        #region Media

        public TLMessageMediaBase Media
        {
            get { return (TLMessageMediaBase)GetValue(MediaProperty); }
            set { SetValue(MediaProperty, value); }
        }

        public static readonly DependencyProperty MediaProperty =
            DependencyProperty.Register("Media", typeof(TLMessageMediaBase), typeof(ProgressVoice), new PropertyMetadata(null, OnMediaChanged));

        private static void OnMediaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ProgressVoice)d).OnMediaChanged((TLMessageMediaBase)e.NewValue, (TLMessageMediaBase)e.OldValue);
        }

        private void OnMediaChanged(TLMessageMediaBase newValue, TLMessageMediaBase oldValue)
        {
            var documentMedia = newValue as TLMessageMediaDocument;
            if (documentMedia != null)
            {
                var document = documentMedia.Document as TLDocument;
                if (document != null)
                {
                    var audioAttribute = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                    if (audioAttribute != null)
                    {
                        if (audioAttribute.HasWaveform)
                        {
                            UpdateSlide(audioAttribute.Waveform);
                        }
                        else
                        {
                            UpdateSlide(new byte[] { 0, 0, 0 });
                        }
                    }
                }
            }
        }

        #endregion

        private void UpdateSlide(byte[] waveform)
        {
            if (ProgressBarIndicator == null || HorizontalTrackRect == null)
            {
                return;
            }

            var result = new double[waveform.Length * 8 / 5];
            for (int i = 0; i < result.Length; i++)
            {
                int j = (i * 5) / 8, shift = (i * 5) % 8;
                result[i] = ((waveform[j] | (waveform[j + 1] << 8)) >> shift & 0x1F) / 31.0;
            }

            var imageWidth = 209.0;
            var imageHeight = 28;

            var space = 1.0;
            var lineWidth = 2.0;
            var lines = waveform.Length * 8 / 5;
            var maxLines = (imageWidth - space) / (lineWidth + space);
            var maxWidth = (double)lines / maxLines;

            //var background = new WriteableBitmap((int)imageWidth, imageHeight);
            //var foreground = new WriteableBitmap((int)imageWidth, imageHeight);

            var geometry1 = new GeometryGroup();
            var geometry2 = new GeometryGroup();

            for (int index = 0; index < maxLines; index++)
            {
                var lineIndex = (int)(index * maxWidth);
                var lineHeight = result[lineIndex] * (double)(imageHeight - 4.0) + 4.0;

                var x1 = (int)(index * (lineWidth + space));
                var y1 = imageHeight - (int)lineHeight;
                var x2 = (int)(index * (lineWidth + space) + lineWidth);
                var y2 = imageHeight;

                //DrawFilledRectangle(ref backgroundBuffer, background.PixelWidth, background.PixelHeight, x1, y1, x2, y2, backgroundColor);
                //DrawFilledRectangle(ref foregroundBuffer, foreground.PixelWidth, foreground.PixelHeight, x1, y1, x2, y2, foregroundColor);

                var rectangle1 = new RectangleGeometry();
                rectangle1.Rect = new Rect(new Point(x1, y1), new Point(x2, y2));
                geometry1.Children.Add(rectangle1);

                var rectangle2 = new RectangleGeometry();
                rectangle2.Rect = new Rect(new Point(x1, y1), new Point(x2, y2));
                geometry2.Children.Add(rectangle2);
            }

            ProgressBarIndicator.Data = geometry1;
            HorizontalTrackRect.Data = geometry2;

            //using (Stream backgroundStream = background.PixelBuffer.AsStream())
            //using (Stream foregroundStream = foreground.PixelBuffer.AsStream())
            //{
            //    backgroundStream.Write(backgroundBuffer, 0, backgroundBuffer.Length);
            //    foregroundStream.Write(foregroundBuffer, 0, foregroundBuffer.Length);
            //}

            //Slide.Background = new ImageBrush { ImageSource = background, AlignmentX = AlignmentX.Right, AlignmentY = AlignmentY.Center, Stretch = Stretch.None };
            //Slide.Foreground = new ImageBrush { ImageSource = foreground, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Center, Stretch = Stretch.None };
        }
    }
}
