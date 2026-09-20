//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Telegram.Common;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
#if NET9_0_OR_GREATER
using WinRT;
#endif

namespace Windows.UI.Xaml.Hosting
{
    public static class ElementComposition
    {
        public static Visual GetElementVisual(UIElement element)
        {
            if (element == null)
            {
                Telegram.Logger.Exception(new ArgumentNullException(nameof(element), Environment.StackTrace));
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
        /// <summary>
        /// Resolves the private <see cref="ICompositionVisualSurfacePartner"/> on a visual surface,
        /// or returns false where the interface does not exist.
        /// </summary>
        /// <remarks>
        /// Older Windows builds do not have this interface, and on CsWinRT the obvious spellings
        /// both go wrong: <c>As&lt;T&gt;</c> throws on a failed QI rather than returning null, so a
        /// null check after it is dead code, and <c>is</c> never matches at all because the
        /// interface is a GeneratedComInterface rather than a projected one. Catching the throw is
        /// not enough either - WatchDog reports InvalidCastException at first chance, so a handled
        /// miss still files a crash report. Probe with a raw QueryInterface instead; once that has
        /// succeeded <c>As&lt;T&gt;</c> cannot throw.
        /// </remarks>
        public static bool TryGetPartner(this CompositionVisualSurface surface, out ICompositionVisualSurfacePartner partner)
        {
#if NET9_0_OR_GREATER
            // NativeObject is the RCW's own reference and is not ours to dispose. The pointer TryAs
            // hands back on success is, and it is only wanted as proof that the QI would work.
            if (surface is IWinRTObject native
                && native.NativeObject.TryAs(typeof(ICompositionVisualSurfacePartner).GUID, out var abi) >= 0)
            {
                Marshal.Release(abi);

                partner = surface.As<ICompositionVisualSurfacePartner>();
                return true;
            }

            partner = null;
            return false;
#else
            var temp = surface as object;
            partner = temp as ICompositionVisualSurfacePartner;
            return partner != null;
#endif
        }

        // The interface is declared as properties on .NET Native and as accessor methods under
        // CsWinRT, where a GeneratedComInterface cannot carry properties. These keep that split out
        // of the call sites.
        public static void SetRealizationSize(this ICompositionVisualSurfacePartner partner, Vector2 value)
        {
#if NET9_0_OR_GREATER
            partner.set_RealizationSize(value);
#else
            partner.RealizationSize = value;
#endif
        }

        public static Vector2 GetRealizationSize(this ICompositionVisualSurfacePartner partner)
        {
#if NET9_0_OR_GREATER
            return partner.get_RealizationSize();
#else
            return partner.RealizationSize;
#endif
        }

        public static void SetStretch(this ICompositionVisualSurfacePartner partner, CompositionStretch value)
        {
#if NET9_0_OR_GREATER
            partner.set_Stretch(value);
#else
            partner.Stretch = value;
#endif
        }

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

            if (freeze && surface.TryGetPartner(out var partner))
            {
                partner.SetStretch(CompositionStretch.Fill);
                partner.SetRealizationSize(sourceSize * (float)source.XamlRoot.RasterizationScale);
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




        // The blob and curve shapes rebuild their paths a few times a second each, so the
        // intermediate points go in a buffer that is reused rather than a list that is allocated.
        // Every caller is on the UI thread and none of them keeps the buffer past the call.
        private static SmoothPoint[] _smoothPoints = Array.Empty<SmoothPoint>();

        private static SmoothPoint[] ToSmoothPoints(Vector2[] points, float smoothness)
        {
            if (_smoothPoints.Length < points.Length)
            {
                _smoothPoints = new SmoothPoint[points.Length];
            }

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

                _smoothPoints[index] = new SmoothPoint(
                    point: curr,
                    inAngle: angle + MathF.PI,
                    inLength: smoothness * Distance(curr, prev),
                    outAngle: angle,
                    outLength: smoothness * Distance(curr, next)
                );
            }

            return _smoothPoints;
        }

        /// <summary>
        /// A closed curve through every point, which is the <c>curve: false</c> case of the overload
        /// below and exists only so the blob shapes need not pass a length they have no use for.
        /// </summary>
        public static CompositionPath CreateSmoothCurve(this Compositor compositor, Vector2[] points, float smoothness)
        {
            return compositor.CreateSmoothCurve(points, 0, smoothness);
        }

        public static CompositionPath CreateSmoothCurve(this Compositor compositor, Vector2[] points, float length, float smoothness, bool curve = false)
        {
            var smoothPoints = ToSmoothPoints(points, smoothness);

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

                var smoothCount = curve ? points.Length - 1 : points.Length;
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

            // Both handles are the same fraction of their chord, as they are on Android and iOS.
            // The smoothness the blob shapes pass is the circular arc constant, which is only the
            // right length while that holds: scaling one side and not the other, as this used to,
            // takes the incoming handle past the point where the segment stays convex.
            public readonly Vector2 SmoothIn()
            {
                return Smooth(_inAngle, _inLength);
            }

            public readonly Vector2 SmoothOut()
            {
                return Smooth(_outAngle, _outLength);
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
