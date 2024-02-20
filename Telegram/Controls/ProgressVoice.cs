//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls
{
    public class ProgressVoice : ProgressBar
    {
        private Path ProgressBarIndicator;
        private Path HorizontalTrackRect;

        private GeometryGroup _group1;
        private GeometryGroup _group2;

        public ProgressVoice()
        {
            DefaultStyleKey = typeof(ProgressVoice);
        }

        protected override void OnApplyTemplate()
        {
            ProgressBarIndicator = GetTemplateChild("ProgressBarIndicator") as Path;
            HorizontalTrackRect = GetTemplateChild("HorizontalTrackRect") as Path;

            ProgressBarIndicator.Data = _group1 = new GeometryGroup();
            HorizontalTrackRect.Data = _group2 = new GeometryGroup();

            if (_deferred != null && _deferred.Duration != 0)
            {
                UpdateWaveform(_deferred);
                //_deferred = null;
            }

            base.OnApplyTemplate();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_deferred != null && _deferred.Duration == 0)
            {
                UpdateWaveform(_deferred.Waveform, 0, finalSize.Width);
            }

            return base.ArrangeOverride(finalSize);
        }

        private VoiceNote _deferred;

        public void UpdateWaveform(VoiceNote voiceNote)
        {
            _deferred = voiceNote;

            if (ProgressBarIndicator == null || HorizontalTrackRect == null)
            {
                //_deferred = voiceNote;
                return;
            }

            if (voiceNote.Duration == 0)
            {
                // Recording
                InvalidateArrange();
            }
            else
            {
                // Bubble
                var maxVoiceLength = 30.0;
                var minVoiceLength = 2.0;

                var minVoiceWidth = 72.0;
                var maxVoiceWidth = 226.0;

                var calcDuration = Math.Max(minVoiceLength, Math.Min(maxVoiceLength, voiceNote.Duration));
                var waveformWidth = minVoiceWidth + (maxVoiceWidth - minVoiceWidth) * (calcDuration - minVoiceLength) / (maxVoiceLength - minVoiceLength);

                UpdateWaveform(voiceNote.Waveform, voiceNote.Duration, waveformWidth);
            }
        }

        private void UpdateWaveform(IList<byte> waveform, double duration, double waveformWidth)
        {
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

            //var maxVoiceLength = 30.0;
            //var minVoiceLength = 2.0;

            //var minVoiceWidth = 72.0;
            //var maxVoiceWidth = 226.0;

            //var calcDuration = Math.Max(minVoiceLength, Math.Min(maxVoiceLength, duration));
            //var waveformWidth = minVoiceWidth + (maxVoiceWidth - minVoiceWidth) * (calcDuration - minVoiceLength) / (maxVoiceLength - minVoiceLength);

            //var imageWidth = 209.0;
            //var imageHeight = 24;
            var imageWidth = waveformWidth; // 142d; // double.IsNaN(ActualWidth) ? 142 : ActualWidth;
            var imageHeight = 20;

            var space = 1.0;
            var lineWidth = 2.0;
            var lines = waveform.Count * 8 / 5;
            var maxLines = (imageWidth - space) / (lineWidth + space);
            var maxWidth = lines / maxLines;

            _group1.Children.Clear();
            _group2.Children.Clear();

            for (int index = 0; index < maxLines; index++)
            {
                var lineIndex = (int)(index * maxWidth);
                var lineHeight = result[lineIndex] * (double)(imageHeight - 2.0) + 2.0;

                var x1 = (int)(index * (lineWidth + space));
                var y1 = (imageHeight - (int)lineHeight) / 2;
                var x2 = (int)(index * (lineWidth + space) + lineWidth);
                var y2 = imageHeight - y1;

                _group1.Children.Add(new RectangleGeometry { Rect = new Rect(new Point(x1, y1), new Point(x2, y2)) });
                _group2.Children.Add(new RectangleGeometry { Rect = new Rect(new Point(x1, y1), new Point(x2, y2)) });
            }

            //ProgressBarIndicator.Data = geometry1;
            //HorizontalTrackRect.Data = geometry2;

            //Width = waveformWidth;

            if (duration != 0)
            {
                Width = waveformWidth;
            }
        }
    }
}
