//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public class ChatListSkeleton : Control
    {
        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = finalSize.ToVector2();

            var rows = Math.Ceiling(size.Y / 68);
            var shapes = new List<CanvasGeometry>();

            var maxWidth = (int)Math.Min(size.X - 32 - 12 - 12 - 48 - 12, 280);
            var random = new Random();

            for (int i = 0; i < rows; i++)
            {
                var y = 68 * i;

                shapes.Add(CanvasGeometry.CreateEllipse(null, 12 + 24, y + 10 + 24, 24, 24));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 12 + 48 + 12, y + 14, random.Next(80, maxWidth), 18, 4, 4));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 12 + 48 + 12, y + 14 + 18 + 4, random.Next(80, maxWidth), 18, 4, 4));

                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, size.X - 32 - 12, y + 16, 32, 14, 4, 4));
            }

            var compositor = Window.Current.Compositor;

            var geometries = shapes.ToArray();
            var path = compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding)));

            var transparent = Color.FromArgb(0x00, 0x7A, 0x8A, 0x96);
            var foregroundColor = Color.FromArgb(0x33, 0x7A, 0x8A, 0x96);
            var backgroundColor = Color.FromArgb(0x33, 0x7A, 0x8A, 0x96);

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(1, 0);
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, transparent));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, foregroundColor));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, transparent));

            var background = compositor.CreateRectangleGeometry();
            background.Size = size;
            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.FillBrush = compositor.CreateColorBrush(backgroundColor);

            var foreground = compositor.CreateRectangleGeometry();
            foreground.Size = size;
            var foregroundShape = compositor.CreateSpriteShape(foreground);
            foregroundShape.FillBrush = gradient;

            var clip = compositor.CreateGeometricClip(path);
            var visual = compositor.CreateShapeVisual();
            visual.Clip = clip;
            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.RelativeSizeAdjustment = Vector2.One;

            var animation = compositor.CreateVector2KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector2(-size.X, 0));
            animation.InsertKeyFrame(1, new Vector2(size.X, 0));
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            foregroundShape.StartAnimation("Offset", animation);

            ElementCompositionPreview.SetElementChildVisual(this, visual);

            return base.ArrangeOverride(finalSize);
        }
    }
}
