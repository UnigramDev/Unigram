//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls
{
    public class CustomEmojiIcon : AnimatedImage
    {
        public CustomEmojiIcon()
        {
            DefaultStyleKey = typeof(CustomEmojiIcon);
        }

        public string Emoji { get; set; }



        public static void Add(RichTextBlock parent, InlineCollection inlines, IClientService clientService, FormattedText message, string style = null)
        {
            inlines.Clear();

            if (message != null)
            {
                var clean = message.ReplaceSpoilers();
                var previous = 0;

                if (message.Entities != null)
                {
                    foreach (var entity in clean.Entities)
                    {
                        if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                        {
                            continue;
                        }

                        if (entity.Offset > previous)
                        {
                            inlines.Add(clean.Text.Substring(previous, entity.Offset - previous));
                        }

                        var player = new CustomEmojiIcon();
                        player.LoopCount = 0;
                        player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);

                        if (style != null)
                        {
                            // "InfoCustomEmojiStyle"
                            player.Style = BootStrapper.Current.Resources[style] as Style;
                        }

                        var inline = new InlineUIContainer();
                        inline.Child = new CustomEmojiContainer(parent, player);

                        // If the Span starts with a InlineUIContainer the RichTextBlock bugs and shows ellipsis
                        if (inlines.Empty())
                        {
                            inlines.Add(Icons.ZWNJ);
                        }

                        inlines.Add(inline);
                        inlines.Add(Icons.ZWNJ);

                        previous = entity.Offset + entity.Length;
                    }
                }

                if (clean.Text.Length > previous)
                {
                    inlines.Add(clean.Text.Substring(previous));
                }
            }
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
            var within = args.BringIntoViewDistanceX < sender.ActualWidth && args.BringIntoViewDistanceY < sender.ActualHeight;
            if (within && !_withinViewport)
            {
                // TODO: performance here is a little concerning.
                // TransformToVisual seems to be faster than GetCharacterRect,
                // at least in some conditions. So at the moment we use it.
                var transform = TransformToVisual(_parent);
                var point = transform.TransformPoint(new Point());

                // This is mostly heuristics, not sure it works in all scenarios.
                // Does not seem to work when display scaling is set to 125%.
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
