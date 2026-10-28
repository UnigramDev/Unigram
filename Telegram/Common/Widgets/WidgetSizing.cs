//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;

namespace Telegram.Common.Widgets
{
    // The bubble's size from the media's anchors (widget-sandbox docs/integration.md, "Bubble sizing").
    // 200 / 50 / 600 are the shared v0.1.0 constants, the same on every client.
    public static class WidgetSizing
    {
        public const double MinWidth = 200;
        public const double MinHeight = 50;
        public const double MaxHeight = 600;

        // An auto-height widget before it reports its height and with none cached. The framework
        // leaves the number to each client.
        public const double PlaceholderHeight = 120;

        // 0 takes the bubble's width; W is fixed, within [200, the bubble's maximum].
        public static double Width(int anchor, double maxBubbleWidth)
        {
            return anchor > 0 ? Math.Max(MinWidth, Math.Min(anchor, maxBubbleWidth)) : maxBubbleWidth;
        }

        // A fixed height, or 0 when the content decides.
        public static double FixedHeight(int anchor)
        {
            return anchor > 0 ? ClampHeight(anchor) : 0;
        }

        public static double ClampHeight(double height)
        {
            return Math.Max(MinHeight, Math.Min(height, MaxHeight));
        }
    }
}
