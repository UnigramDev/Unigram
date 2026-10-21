//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;

namespace Telegram.Controls
{
    // A ProgressBarRing in the colours of an unread story's ring, for your own story uploading.
    public partial class StoryProgressBarRing : ProgressBarRing
    {
        private static readonly Color _topColor = Color.FromArgb(0xFF, 0x34, 0xC7, 0x6F);
        private static readonly Color _bottomColor = Color.FromArgb(0xFF, 0x3D, 0xA1, 0xFD);

        protected override CompositionBrush CreateStroke(Compositor compositor, ShapeVisual visual)
        {
            var stroke = compositor.CreateLinearGradientBrush();
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(0, _topColor));
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(1, _bottomColor));
            stroke.MappingMode = CompositionMappingMode.Absolute;
            stroke.StartPoint = Vector2.Zero;
            stroke.EndPoint = new Vector2(0, visual.Size.Y);
            stroke.CenterPoint = new Vector2(visual.CenterPoint.X, visual.CenterPoint.Y);

            // The whole visual spins, so the brush is turned back by as much and the gradient keeps
            // running downwards.
            var upright = compositor.CreateExpressionAnimation("-Visual.RotationAngleInDegrees");
            upright.SetReferenceParameter("Visual", visual);

            stroke.StartAnimation("RotationAngleInDegrees", upright);
            return stroke;
        }
    }
}
