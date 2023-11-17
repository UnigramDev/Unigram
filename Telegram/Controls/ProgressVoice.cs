//
// Copyright Fela Ameghino 2015-2023
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
                UpdateWaveform(_deferred);
                _deferred = null;
            }

            base.OnApplyTemplate();
        }

        private VoiceNote _deferred;

        public void UpdateWaveform(VoiceNote voiceNote)
        {
            if (ProgressBarIndicator == null || HorizontalTrackRect == null)
            {
                _deferred = voiceNote;
                return;
            }

            _deferred = null;
            UpdateWaveform(voiceNote.Waveform, voiceNote.Duration);
        }

        private void UpdateWaveform(IList<byte> waveform, double duration)
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

            var maxVoiceLength = 30.0;
            var minVoiceLength = 2.0;

            var minVoiceWidth = 68.0;
            var maxVoiceWidth = 226.0;

            var calcDuration = Math.Max(minVoiceLength, Math.Min(maxVoiceLength, duration));
            var waveformWidth = minVoiceWidth + (maxVoiceWidth - minVoiceWidth) * (calcDuration - minVoiceLength) / (maxVoiceLength - minVoiceLength);

            //var imageWidth = 209.0;
            //var imageHeight = 24;
            var imageWidth = waveformWidth; // 142d; // double.IsNaN(ActualWidth) ? 142 : ActualWidth;
            var imageHeight = 20;

            var space = 1.0;
            var lineWidth = 2.0;
            var lines = waveform.Count * 8 / 5;
            var maxLines = (imageWidth - space) / (lineWidth + space);
            var maxWidth = lines / maxLines;

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

            ProgressBarIndicator.Data = geometry1;
            HorizontalTrackRect.Data = geometry2;

            Width = waveformWidth;
        }
    }
}
