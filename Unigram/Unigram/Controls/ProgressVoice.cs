using System.Collections.Generic;
using Telegram.Td.Api;
using Windows.Foundation;
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

            if (_deferred != null)
            {
                UpdateSlide(_deferred);
                _deferred = null;
            }

            base.OnApplyTemplate();
        }

        private IList<byte> _deferred;

        public void UpdateWave(VoiceNote voiceNote)
        {
            UpdateSlide(voiceNote.Waveform);
        }

        private void UpdateSlide(IList<byte> waveform)
        {
            if (ProgressBarIndicator == null || HorizontalTrackRect == null)
            {
                _deferred = waveform;
                return;
            }

            if (waveform.Count < 1)
            {
                waveform = new byte[1] { 0 };
            }

            var result = new double[waveform.Count * 8 / 5];
            for (int i = 0; i < result.Length; i++)
            {
                int j = (i * 5) / 8, shift = (i * 5) % 8;
                result[i] = ((waveform[j] | ((j + 1 < waveform.Count ? waveform[j + 1] : 0) << 8)) >> shift & 0x1F) / 31.0;
            }

            //var imageWidth = 209.0;
            //var imageHeight = 24;
            var imageWidth = 142d; // double.IsNaN(ActualWidth) ? 142 : ActualWidth;
            var imageHeight = 20;

            var space = 1.0;
            var lineWidth = 2.0;
            var lines = waveform.Count * 8 / 5;
            var maxLines = (imageWidth - space) / (lineWidth + space);
            var maxWidth = (double)lines / maxLines;

            var geometry1 = new GeometryGroup();
            var geometry2 = new GeometryGroup();

            for (int index = 0; index < maxLines; index++)
            {
                var lineIndex = (int)(index * maxWidth);
                var lineHeight = result[lineIndex] * (double)(imageHeight - 2.0) + 2.0;

                var x1 = (int)(index * (lineWidth + space));
                var y1 = (imageHeight - (int)lineHeight) / 2;
                var x2 = (int)(index * (lineWidth + space) + lineWidth);
                var y2 = imageHeight - y1;

                geometry1.Children.Add(new RectangleGeometry { Rect = new Rect(new Point(x1, y1), new Point(x2, y2)) });
                geometry2.Children.Add(new RectangleGeometry { Rect = new Rect(new Point(x1, y1), new Point(x2, y2)) });
            }

            //var width = 142;
            //if (waveform == null || width == 0)
            //{
            //    return;
            //}

            //float totalBarsCount = width / 3;
            //if (totalBarsCount <= 0.1f)
            //{
            //    return;
            //}

            //int value;
            //int samplesCount = (waveform.Count * 8 / 5);
            //float samplesPerBar = samplesCount / totalBarsCount;
            //float barCounter = 0;
            //int nextBarNum = 0;

            ////paintInner.setColor(messageObject != null && !messageObject.isOutOwner() && messageObject.isContentUnread() ? outerColor : (selected ? selectedColor : innerColor));
            ////paintOuter.setColor(outerColor);

            //int y = (24 - 14) / 2;
            //int barNum = 0;
            //int lastBarNum;
            //int drawBarCount;

            //var geometry1 = new GeometryGroup();
            //var geometry2 = new GeometryGroup();

            //for (int a = 0; a < samplesCount; a++)
            //{
            //    if (a != nextBarNum)
            //    {
            //        continue;
            //    }
            //    drawBarCount = 0;
            //    lastBarNum = nextBarNum;
            //    while (lastBarNum == nextBarNum)
            //    {
            //        barCounter += samplesPerBar;
            //        nextBarNum = (int)barCounter;
            //        drawBarCount++;
            //    }

            //    int bitPointer = a * 5;
            //    int byteNum = bitPointer / 8;
            //    int byteBitOffset = bitPointer - byteNum * 8;
            //    int currentByteCount = 8 - byteBitOffset;
            //    int nextByteRest = 5 - currentByteCount;
            //    value = (byte)((waveform[byteNum] >> byteBitOffset) & ((2 << (Math.Min(5, currentByteCount) - 1)) - 1));
            //    if (nextByteRest > 0)
            //    {
            //        value <<= nextByteRest;
            //        value |= waveform[byteNum + 1] & ((2 << (nextByteRest - 1)) - 1);
            //    }

            //    for (int b = 0; b < drawBarCount; b++)
            //    {
            //        int x = barNum * 3;

            //        geometry1.Children.Add(new RectangleGeometry { Rect = new Rect(new Point(x, y + 14 - Math.Max(1, 14.0f * value / 31.0f)), new Point(x + 2, y + 14)) });
            //        geometry2.Children.Add(new RectangleGeometry { Rect = new Rect(new Point(x, y + 14 - Math.Max(1, 14.0f * value / 31.0f)), new Point(x + 2, y + 14)) });

            //        barNum++;
            //    }
            //}

            ProgressBarIndicator.Data = geometry1;
            HorizontalTrackRect.Data = geometry2;
        }
    }
}
