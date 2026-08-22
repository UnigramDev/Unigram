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
using Telegram.Services.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Media
{
    /// <summary>
    /// Paints a message bubble that has a tail, by masking the theme fill with the nine grid that
    /// PlaceholderImageHelper draws for a given set of corner radii.
    /// </summary>
    /// <remarks>
    /// Instances are shared by every bubble with the same shape, and so is everything they point at,
    /// so a bubble holds a reference and nothing else. A LayerVisual with an alpha mask effect gives
    /// the same result at the cost of one offscreen intermediate per bubble.
    /// </remarks>
    public sealed partial class MessageBubbleBrush : XamlCompositionBrushBase
    {
        [ThreadStatic]
        private static Dictionary<int, MessageBubbleBrush> _brushes;

        // The nine grid behind the mask is rasterized at its window's scale, so a brush cannot be
        // shared across windows. _brushes is still keyed on shape alone and has to gain that
        // dimension before this is used.
        private readonly XamlRoot _xamlRoot;

        private readonly int _topLeft;
        private readonly int _topRight;
        private readonly int _bottomRight;
        private readonly int _bottomLeft;

        private readonly bool _outgoing;
        private readonly TelegramTheme _parent;

        private MessageBubbleBrush(XamlRoot xamlRoot, int topLeft, int topRight, int bottomRight, int bottomLeft, bool outgoing, TelegramTheme parent)
        {
            _xamlRoot = xamlRoot;

            _topLeft = topLeft;
            _topRight = topRight;
            _bottomRight = bottomRight;
            _bottomLeft = bottomLeft;

            _outgoing = outgoing;
            _parent = parent;
        }

        public static MessageBubbleBrush GetTail(XamlRoot xamlRoot, float topLeft, float topRight, float bottomRight, float bottomLeft, bool outgoing, TelegramTheme parent)
        {
            // Same packing as the nine grid cache in PlaceholderImageHelper: four 5 bit radii, plus
            // one bit for each of the two dimensions the fill varies on.
            var key = ((int)topLeft << 15) | ((int)topRight << 10) | ((int)bottomRight << 5) | (int)bottomLeft
                | (outgoing ? 1 << 20 : 0)
                | (parent == TelegramTheme.Dark ? 1 << 21 : 0);

            _brushes ??= new Dictionary<int, MessageBubbleBrush>();

            if (_brushes.TryGetValue(key, out MessageBubbleBrush brush))
            {
                return brush;
            }

            brush = new MessageBubbleBrush(xamlRoot, (int)topLeft, (int)topRight, (int)bottomRight, (int)bottomLeft, outgoing, parent);
            _brushes[key] = brush;

            return brush;
        }

        public static void Release()
        {
            _brushes = null;
        }

        // OnDisconnected is deliberately not overridden: the reference count drops to zero whenever
        // the last bubble with this shape is recycled, and tearing the brush down there would
        // rebuild it on the next scroll back.
        protected override void OnConnected()
        {
            var fill = _outgoing
                ? ThemeOutgoing.Background(_parent)
                : ThemeIncoming.Background(_parent);

            // Used while the nine grid is unavailable, which is the case when a bubble is realized
            // before XamlRoot is ready. Without it the bubble would have no fill at all.
            FallbackColor = fill.Color;

            if (CompositionBrush == null)
            {
                var mask = PlaceholderHelper.Foreground.GetTailMask(_xamlRoot, _topLeft, _topRight, _bottomRight, _bottomLeft);
                if (mask != null)
                {
                    var brush = BootStrapper.Current.Compositor.CreateMaskBrush();
                    brush.Source = fill;
                    brush.Mask = mask;

                    CompositionBrush = brush;
                }
            }

            base.OnConnected();
        }
    }
}
