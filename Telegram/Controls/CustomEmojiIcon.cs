//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using Windows.Foundation;

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

        public CustomEmojiContainer(RichTextBlock parent, CustomEmojiIcon child)
        {
            _parent = parent;
            _child = child;

            Children.Add(child);

            EffectiveViewportChanged += OnEffectiveViewportChanged;
        }

        private bool _withinViewport;

        private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            var within = args.BringIntoViewDistanceX == 0 || args.BringIntoViewDistanceY == 0;
            if (within && !_withinViewport)
            {
                // TODO: performance here is a little concerning.
                // TransformToVisual seems to be faster than GetCharacterRect,
                // at least in some conditions. So at the moment we use it.
                var transform = TransformToVisual(_parent);
                var point = transform.TransformPoint(new Point());

                if (point.Y < -1 || point.X < 0)
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
