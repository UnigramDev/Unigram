//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using Telegram.Common;
using Telegram.Navigation;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls
{
    public partial class ProgressVoice : PlaybackSlider
    {
        private Grid RootGrid;
        private Rectangle ProgressBarIndicator;
        private Rectangle HorizontalTrackRect;

        private CompositionGeometricClip _clip;

        public ProgressVoice()
        {
            DefaultStyleKey = typeof(ProgressVoice);

            _clip = BootStrapper.Current.Compositor.CreateGeometricClip();
        }

        protected override void OnApplyTemplate()
        {
            RootGrid = GetTemplateChild(nameof(RootGrid)) as Grid;
            ProgressBarIndicator = GetTemplateChild(nameof(ProgressBarIndicator)) as Rectangle;
            HorizontalTrackRect = GetTemplateChild(nameof(HorizontalTrackRect)) as Rectangle;

            var visual = ElementComposition.GetElementVisual(RootGrid);
            visual.Clip = _clip;

            base.OnApplyTemplate();
        }

        private IList<byte> _waveform;
        private int _duration;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_waveform != null)
            {
                var maxVoiceLength = 30.0;
                var minVoiceLength = 2.0;

                var minVoiceWidth = 72.0;
                var maxVoiceWidth = 226.0;

                var calcDuration = Math.Max(minVoiceLength, Math.Min(maxVoiceLength, _duration));
                var waveformWidth = minVoiceWidth + (maxVoiceWidth - minVoiceWidth) * (calcDuration - minVoiceLength) / (maxVoiceLength - minVoiceLength);

                availableSize = new Size(waveformWidth, 20);

                RootGrid.Measure(availableSize);
                return availableSize;
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (_waveform != null)
            {
                UpdateWaveform(_waveform, 0, finalSize.Width);
            }

            return base.ArrangeOverride(finalSize);
        }

        public void UpdateWaveform(IList<byte> waveform, int duration)
        {
            _waveform = waveform;
            _duration = duration;
            InvalidateMeasure();
            InvalidateArrange();
        }

        private void UpdateWaveform(IList<byte> waveform, double duration, double waveformWidth)
        {
            if (waveform.Count < 1)
            {
                waveform = new byte[1] { 0 };
            }

            var clip = PlaceholderHelper.Foreground.GetVoiceNoteClip(waveform, waveformWidth);
            _clip.Geometry = BootStrapper.Current.Compositor.CreatePathGeometry(clip);
        }
    }
}
