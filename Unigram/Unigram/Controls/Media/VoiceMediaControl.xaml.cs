using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Unigram.Controls.Media
{
    public sealed partial class VoiceMediaControl : UserControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;

        public VoiceMediaControl()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                if (ViewModel != null)
                {
                    Loading?.Invoke(s, null);

                    var mediaDocument = ViewModel.Media as TLMessageMediaDocument;
                    if (mediaDocument != null)
                    {
                        var document = mediaDocument.Document as TLDocument;
                        if (document != null)
                        {
                            var audioAttribute = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                            if (audioAttribute != null && audioAttribute.HasWaveform)
                            {
                                LengthLabel.Text = TimeSpan.FromSeconds(audioAttribute.Duration).ToString("mm\\:ss");
                                UpdateSlide(audioAttribute.Waveform);
                            }
                        }
                    }
                }
            };
        }

        #region Drawing

        private byte[] _oldWaveform;

        private void UpdateSlide(byte[] waveform)
        {
            if (waveform == _oldWaveform) return;

            _oldWaveform = waveform;

            var backgroundColor = (Color)((SolidColorBrush)Resources["SliderTrackFill"]).Color;
            var foregroundColor = (Color)((SolidColorBrush)Resources["SliderTrackValueFill"]).Color;

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

            var background = new WriteableBitmap((int)imageWidth, imageHeight);
            var foreground = new WriteableBitmap((int)imageWidth, imageHeight);

            var backgroundOut = new byte[background.PixelWidth * background.PixelHeight * 4];
            var foregroundOut = new byte[foreground.PixelWidth * foreground.PixelHeight * 4];

            int index = 0;
            while (index < maxLines)
            {
                var lineIndex = (int)(index * maxWidth);
                var lineHeight = result[lineIndex] * (double)(imageHeight - 3.0) + 3.0;

                var x1 = (int)(index * (lineWidth + space));
                var y1 = imageHeight - (int)lineHeight;
                var x2 = (int)(index * (lineWidth + space) + lineWidth);
                var y2 = imageHeight;

                DrawFilledRectangle(ref backgroundOut, background.PixelWidth, background.PixelHeight, x1, y1, x2, y2, backgroundColor);
                DrawFilledRectangle(ref foregroundOut, foreground.PixelWidth, foreground.PixelHeight, x1, y1, x2, y2, foregroundColor);

                index++;
            }

            using (Stream backgroundStream = background.PixelBuffer.AsStream())
            using (Stream foregroundStream = foreground.PixelBuffer.AsStream())
            {
                backgroundStream.Write(backgroundOut, 0, backgroundOut.Length);
                foregroundStream.Write(foregroundOut, 0, foregroundOut.Length);
            }

            Slide.Background = new ImageBrush { ImageSource = background, AlignmentX = AlignmentX.Right, AlignmentY = AlignmentY.Center, Stretch = Stretch.None };
            Slide.Foreground = new ImageBrush { ImageSource = foreground, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Center, Stretch = Stretch.None };
        }

        private void DrawFilledRectangle(ref byte[] pixels, int width, int height, int x1, int y1, int x2, int y2, Color color)
        {
            if (x1 < 0) x1 = 0;
            if (y1 < 0) y1 = 0;
            if (x2 < 0) x2 = 0;
            if (y2 < 0) y2 = 0;

            if (x1 >= width) x1 = width - 1;
            if (y1 >= height) y1 = height - 1;
            if (x2 >= width) x2 = width;
            if (y2 >= height) y2 = height;

            var line = y1 * width;
            for (int i = y1; i < y2; i++)
            {
                var j = line + x1;
                while (j < line + x2)
                {
                    pixels[j * 4 + 0] = color.B;
                    pixels[j * 4 + 1] = color.G;
                    pixels[j * 4 + 2] = color.R;
                    pixels[j * 4 + 3] = color.A;
                    j++;
                }

                line += width;
            }
        }

        #endregion

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;
    }
}
