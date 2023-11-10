//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public class CustomEmojiIcon : AnimatedImage
    {
        public CustomEmojiIcon()
        {
            DefaultStyleKey = typeof(CustomEmojiIcon);
        }
    }

    public class CustomEmojiContainer : Grid
    {
        private readonly RichTextBlock _parent;
        private readonly CustomEmojiIcon _child;
        private readonly double _baseline;

        public CustomEmojiContainer(RichTextBlock parent, CustomEmojiIcon child, int baseline = 0)
        {
            _parent = parent;
            _child = child;
            _baseline = baseline + 0.01;

            child.IsViewportAware = false;
            child.IsHitTestVisible = false;
            child.IsEnabled = false;

            Children.Add(child);

            HorizontalAlignment = HorizontalAlignment.Left;
            FlowDirection = FlowDirection.LeftToRight;
            Margin = new Thickness(0, -2, 0, -6);

            Width = 20;
            Height = 20;

            EffectiveViewportChanged += OnEffectiveViewportChanged;
        }

        private bool _withinViewport;

        private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var within = args.BringIntoViewDistanceX == 0 && args.BringIntoViewDistanceY == 0;
            if (within && !_withinViewport)
            {
                // TODO: performance here is a little concerning.
                // TransformToVisual seems to be faster than GetCharacterRect,
                // at least in some conditions. So at the moment we use it.
                var transform = TransformToVisual(_parent);
                var point = transform.TransformPoint(new Point());

                // This is mostly heuristics, not sure it works in all scenarios.
                if (point.Y <= _baseline || point.X < 0)
                {
                    _child.Visibility = Visibility.Collapsed;
                    return;
                }
                else
                {
                    _child.Visibility = Visibility.Visible;
                }

                _withinViewport = true;
                _child.Play();
            }
            else if (_withinViewport && !within)
            {
                _withinViewport = false;
                _child.Pause();
            }
        }
    }
}
