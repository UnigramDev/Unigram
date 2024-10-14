using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Common;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Windows.UI.Xaml.Hosting
{
    public static class ElementComposition
    {
        public static Visual GetElementVisual(UIElement element)
        {
            if (element == null)
            {
                Telegram.Logger.Error(new ArgumentNullException(nameof(element), Environment.StackTrace));
            }

            return ElementCompositionPreview.GetElementVisual(element);
        }

        public static void SetElementChildVisual(UIElement element, Visual visual)
        {
            try
            {
                ElementCompositionPreview.SetElementChildVisual(element, visual);
            }
            catch
            {
                // TODO
            }
        }

        public static CanvasDevice GetSharedDevice()
        {
            try
            {
                // This often throws the following exception:
                // The GPU device instance has been suspended. Use GetDeviceRemovedReason to determine the appropriate action. (Exception from HRESULT: 0x887A0005)
                // TODO: it's unclear when and why this happens, nor if this is a good solution to the problem.
                return CanvasDevice.GetSharedDevice();
            }
            catch
            {
                return CanvasDevice.GetSharedDevice(true);
            }
        }
    }
}

namespace Telegram.Composition
{
    public static class CompositionExtensions
    {
        public static CompositionBrush CreateRedirectBrush(this Compositor compositor, UIElement source, Vector2 sourceOffset, Vector2 sourceSize, bool freeze = false)
        {
            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = ElementComposition.GetElementVisual(source);
            surface.SourceOffset = sourceOffset;
            surface.SourceSize = sourceSize;
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.Fill;

            if (freeze && surface is object obj && obj is ICompositionVisualSurfacePartner partner)
            {
                partner.Stretch = CompositionStretch.Fill;
                partner.RealizationSize = sourceSize * (float)source.XamlRoot.RasterizationScale;
                partner.Freeze();
            }

            return surfaceBrush;
        }

        public static CompositionBrush CreateRedirectBrush(this Compositor compositor, Visual source, Vector2 sourceOffset, Vector2 sourceSize)
        {
            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = source;
            surface.SourceOffset = sourceOffset;
            surface.SourceSize = sourceSize;
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.Fill;

            return surfaceBrush;
        }

        public static SpriteVisual CreateRedirectVisual(this Compositor compositor, UIElement source, Vector2 sourceOffset, Vector2 sourceSize, bool freeze = false)
        {
            var redirect = compositor.CreateSpriteVisual();
            redirect.Brush = compositor.CreateRedirectBrush(source, sourceOffset, sourceSize, freeze);
            redirect.Size = sourceSize;

            return redirect;
        }




        public static CompositionPath CreateSmoothCurve(this Compositor compositor, Vector2[] points, float smoothness)
        {
            var smoothPoints = new List<SmoothPoint>();

            for (int index = 0; index < points.Length; index++)
            {
                var prevIndex = index - 1;
                var prev = points[prevIndex >= 0 ? prevIndex : points.Length + prevIndex];
                var curr = points[index];
                var next = points[(index + 1) % points.Length];

                var dx = next.X - prev.X;
                var dy = -next.Y + prev.Y;
                var angle = MathF.Atan2(dy, dx);
                if (angle < 0)
                {
                    angle = MathF.Abs(angle);
                }
                else
                {
                    angle = 2 * MathF.PI - angle;
                }

                smoothPoints.Add(
                    new SmoothPoint(
                        point: curr,
                        inAngle: angle + MathF.PI,
                        inLength: smoothness * Distance(curr, prev),
                        outAngle: angle,
                        outLength: smoothness * Distance(curr, next)
                    )
                );
            }

            CanvasGeometry result;
            using (var builder = new CanvasPathBuilder(null))
            {
                builder.BeginFigure(smoothPoints[0].Point);

                for (int i = 0; i < smoothPoints.Count; i++)
                {
                    var prev = smoothPoints[i >= 0 ? i : smoothPoints.Count + i];
                    var curr = smoothPoints[i];
                    var next = smoothPoints[(i + 1) % points.Length];
                    var currSmoothOut = curr.SmoothOut(0.5f);
                    var nextSmoothIn = next.SmoothIn(2.0f);

                    builder.AddCubicBezier(currSmoothOut, nextSmoothIn, next.Point);
                }

                builder.EndFigure(CanvasFigureLoop.Closed);
                result = CanvasGeometry.CreatePath(builder);
            }
            return new CompositionPath(result);
        }

        public static CompositionPath CreateSmoothCurve(this Compositor compositor, Vector2[] points, float length, float smoothness, bool curve = false)
        {
            var smoothPoints = new List<SmoothPoint>();

            for (int index = 0; index < points.Length; index++)
            {
                var prevIndex = index - 1;
                var prev = points[prevIndex >= 0 ? prevIndex : points.Length + prevIndex];
                var curr = points[index];
                var next = points[(index + 1) % points.Length];

                var dx = next.X - prev.X;
                var dy = -next.Y + prev.Y;
                var angle = MathF.Atan2(dy, dx);
                if (angle < 0)
                {
                    angle = MathF.Abs(angle);
                }
                else
                {
                    angle = 2 * MathF.PI - angle;
                }

                smoothPoints.Add(
                    new SmoothPoint(
                        point: curr,
                        inAngle: angle + MathF.PI,
                        inLength: smoothness * Distance(curr, prev),
                        outAngle: angle,
                        outLength: smoothness * Distance(curr, next)
                    )
                );
            }

            CanvasGeometry result;
            using (var builder = new CanvasPathBuilder(null))
            {
                if (curve)
                {
                    builder.BeginFigure(0, 0);
                    builder.AddLine(smoothPoints[0].Point);
                }
                else
                {
                    builder.BeginFigure(smoothPoints[0].Point);
                }

                var smoothCount = curve ? smoothPoints.Count - 1 : smoothPoints.Count;
                for (int index = 0; index < smoothCount; index++)
                {
                    var curr = smoothPoints[index];
                    var next = smoothPoints[(index + 1) % points.Length];
                    var currSmoothOut = curr.SmoothOut();
                    var nextSmoothIn = next.SmoothIn();
                    builder.AddCubicBezier(currSmoothOut, nextSmoothIn, next.Point);
                }
                if (curve)
                {
                    builder.AddLine(length, 0);
                }
                builder.EndFigure(CanvasFigureLoop.Closed);
                result = CanvasGeometry.CreatePath(builder);
            }
            return new CompositionPath(result);
        }

        private static float Distance(Vector2 fromPoint, Vector2 toPoint)
        {
            return MathF.Sqrt((fromPoint.X - toPoint.X) * (fromPoint.X - toPoint.X) + (fromPoint.Y - toPoint.Y) * (fromPoint.Y - toPoint.Y));
        }

        private readonly struct SmoothPoint
        {
            public readonly Vector2 Point;

            private readonly float _inAngle;
            private readonly float _inLength;

            private readonly float _outAngle;
            private readonly float _outLength;

            public SmoothPoint(Vector2 point, float inAngle, float inLength, float outAngle, float outLength)
            {
                Point = point;
                _inAngle = inAngle;
                _inLength = inLength;
                _outAngle = outAngle;
                _outLength = outLength;
            }

            public readonly Vector2 SmoothIn(float multiplier = 1)
            {
                // TODO: * 2.0f is arbitrary
                return Smooth(_inAngle, _inLength * multiplier);
            }

            public readonly Vector2 SmoothOut(float multiplier = 1)
            {
                // TODO: * 0.5f is arbistrary
                return Smooth(_outAngle, _outLength * multiplier);
            }

            private readonly Vector2 Smooth(float angle, float length)
            {
                return new Vector2(
                    Point.X + length * MathF.Cos(angle),
                    Point.Y + length * MathF.Sin(angle)
                );
            }
        }
    }
}
