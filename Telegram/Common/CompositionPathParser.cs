//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;

namespace Telegram.Common
{
    public static class CompositionPathParser
    {
        public static CompositionPath Parse(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return null;



            //var segments = ParseSegments(data);
            //if (segments?.Count > 0)
            //{
            //    using var builder = new CanvasPathBuilder(null);
            //    RenderPath(segments, builder);
            //    return new CompositionPath(CanvasGeometry.CreatePath(builder));
            //}

            using var builder = new CanvasPathBuilder(null);
            ParseAndRender(data, builder);
            return new CompositionPath(CanvasGeometry.CreatePath(builder));

            return null;
        }

        public static CanvasGeometry Parse(ICanvasResourceCreator resourceCreator, string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return null;

            //var segments = ParseSegments(data);
            //if (segments?.Count > 0)
            //{
            //    using var builder = new CanvasPathBuilder(resourceCreator);
            //    RenderPath(segments, builder);
            //    return CanvasGeometry.CreatePath(builder);
            //}

            using var builder = new CanvasPathBuilder(resourceCreator);
            ParseAndRender(data, builder);
            return CanvasGeometry.CreatePath(builder);

            return null;
        }

        public static CompositionPath Parse(IList<ClosedVectorPath> contours)
        {
            return new CompositionPath(Parse(null, contours));
        }

        public static CanvasGeometry Parse(ICanvasResourceCreator sender, IList<ClosedVectorPath> contours)
        {
            using var builder = new CanvasPathBuilder(sender);

            foreach (var path in contours)
            {
                var open = true;

                for (int i = 0; i <= path.Commands.Count; i++)
                {
                    var command = path.Commands[i % path.Commands.Count];
                    if (command is VectorPathCommandLine line)
                    {
                        var point = line.EndPoint;
                        if (open)
                        {
                            open = false;
                            builder.BeginFigure((float)point.X, (float)point.Y);
                        }
                        else
                        {
                            builder.AddLine((float)point.X, (float)point.Y);
                        }
                    }
                    else if (command is VectorPathCommandCubicBezierCurve cubicBezierCurve)
                    {
                        if (open)
                        {
                            open = false;
                            builder.BeginFigure((float)cubicBezierCurve.EndPoint.X, (float)cubicBezierCurve.EndPoint.Y);
                        }
                        else
                        {
                            builder.AddCubicBezier(cubicBezierCurve.StartControlPoint.ToVector2(),
                                cubicBezierCurve.EndControlPoint.ToVector2(),
                                cubicBezierCurve.EndPoint.ToVector2());
                        }
                    }
                }

                builder.EndFigure(CanvasFigureLoop.Closed);
            }

            return CanvasGeometry.CreatePath(builder);
        }

        public static CompositionAnimation ParseThumbnail(float width, float height, IList<ClosedVectorPath> contours, out ShapeVisual visual, bool animated = true)
        {
            CompositionPath path = contours?.Count > 0
                ? new CompositionPath(Parse(null, contours))
                : new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, 0, 0, width, height, 80, 80));

            return CreateThumbnail(width, height, path, out visual, animated);
        }

        public static CompositionAnimation CreateThumbnail(float width, float height, float cornerRadius, out ShapeVisual visual, bool animated = true)
        {
            var path = new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, 0, 0, width, height, cornerRadius, cornerRadius));
            return CreateThumbnail(width, height, path, out visual, animated);
        }

        public static CompositionAnimation CreateThumbnail(float width, float height, CompositionPath path, out ShapeVisual visual, bool animated = true)
        {
            var backgroundColor = Color.FromArgb(0x33, 0x7A, 0x8A, 0x96);
            var compositor = BootStrapper.Current.Compositor;

            var background = compositor.CreatePathGeometry(path);
            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.FillBrush = compositor.CreateColorBrush(backgroundColor);

            visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(backgroundShape);
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.ViewBox = compositor.CreateViewBox();
            visual.ViewBox.Size = new Vector2(width, height);
            visual.ViewBox.Stretch = CompositionStretch.Uniform;

            return animated ? CreateAnimation(compositor, background, visual, width) : null;
        }

        private static CompositionAnimation CreateAnimation(Compositor compositor, CompositionPathGeometry background, ShapeVisual visual, float width)
        {
            var transparent = Color.FromArgb(0x00, 0x7A, 0x8A, 0x96);
            var foregroundColor = Color.FromArgb(0x33, 0x7A, 0x8A, 0x96);

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = Vector2.Zero;
            gradient.EndPoint = Vector2.UnitX;

            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, transparent));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, foregroundColor));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, transparent));

            var foregroundShape = compositor.CreateSpriteShape(background);
            foregroundShape.FillBrush = gradient;
            visual.Shapes.Add(foregroundShape);

            var animation = compositor.CreateVector2KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector2(-width, 0));
            animation.InsertKeyFrame(1, new Vector2(width, 0));
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            gradient.StartAnimation("Offset", animation);
            return animation;
        }


        private const int MaxSegmentsStackAlloc = 256;
        private const int MaxDataStackAlloc = 1024;

        public static void ParseAndRender(string data, CanvasPathBuilder builder)
        {
            ParseAndRender(data.AsSpan(), builder);
        }

        public static void ParseAndRender(ReadOnlySpan<char> path, CanvasPathBuilder builder)
        {
            Span<PathSegment> segmentBuffer = stackalloc PathSegment[MaxSegmentsStackAlloc];
            Span<float> dataBuffer = stackalloc float[MaxDataStackAlloc];

            var reader = new PathDataReader(path, segmentBuffer, dataBuffer);
            var renderer = new PathRenderer(builder, dataBuffer);

            while (reader.TryReadSegment(out var segments, out var data))
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    renderer.RenderSegment(segments[i].Type, segments[i].GetData(data));
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct PathSegment
        {
            public enum SegmentType : byte
            {
                M, L, C, Q, A, z, H, V, S, T,
                m, l, c, q, a, h, v, s, t,
                E, e
            }

            public readonly SegmentType Type;
            public readonly int DataOffset;
            public readonly int DataLength;

            public PathSegment(SegmentType type, int dataOffset, int dataLength)
            {
                Type = type;
                DataOffset = dataOffset;
                DataLength = dataLength;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool IsAbsolute() => Type switch
            {
                SegmentType.M or SegmentType.L or SegmentType.H or SegmentType.V or
                SegmentType.C or SegmentType.S or SegmentType.Q or SegmentType.T or
                SegmentType.A or SegmentType.E => true,
                _ => false
            };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ReadOnlySpan<float> GetData(ReadOnlySpan<float> dataBuffer)
            {
                return dataBuffer.Slice(DataOffset, DataLength);
            }
        }

        private ref struct PathRenderer
        {
            private readonly CanvasPathBuilder _builder;
            private readonly Span<float> _dataBuffer;
            private Vector2? _currentPoint;
            private Vector2? _cubicPoint;
            private Vector2? _initialPoint;

            public PathRenderer(CanvasPathBuilder builder, Span<float> dataBuffer)
            {
                _builder = builder;
                _dataBuffer = dataBuffer;
                _currentPoint = null;
                _cubicPoint = null;
                _initialPoint = null;
            }

            public void RenderSegment(PathSegment.SegmentType type, ReadOnlySpan<float> data)
            {
                switch (type)
                {
                    case PathSegment.SegmentType.M:
                        MoveTo(data[0], data[1]);
                        data = data[2..];
                        while (data.Length >= 2)
                        {
                            LineTo(data[0], data[1]);
                            data = data[2..];
                        }
                        break;

                    case PathSegment.SegmentType.m:
                        MoveToRelative(data[0], data[1]);
                        data = data[2..];
                        while (data.Length >= 2)
                        {
                            LineToRelative(data[0], data[1]);
                            data = data[2..];
                        }
                        break;

                    case PathSegment.SegmentType.L:
                        while (data.Length >= 2)
                        {
                            LineTo(data[0], data[1]);
                            data = data[2..];
                        }
                        break;

                    case PathSegment.SegmentType.l:
                        while (data.Length >= 2)
                        {
                            LineToRelative(data[0], data[1]);
                            data = data[2..];
                        }
                        break;

                    case PathSegment.SegmentType.H:
                        HorizontalLineTo(data[0]);
                        break;

                    case PathSegment.SegmentType.h:
                        HorizontalLineToRelative(data[0]);
                        break;

                    case PathSegment.SegmentType.V:
                        VerticalLineTo(data[0]);
                        break;

                    case PathSegment.SegmentType.v:
                        VerticalLineToRelative(data[0]);
                        break;

                    case PathSegment.SegmentType.C:
                        while (data.Length >= 6)
                        {
                            CubicBezierTo(data[0], data[1], data[2], data[3], data[4], data[5]);
                            data = data[6..];
                        }
                        break;

                    case PathSegment.SegmentType.c:
                        while (data.Length >= 6)
                        {
                            CubicBezierToRelative(data[0], data[1], data[2], data[3], data[4], data[5]);
                            data = data[6..];
                        }
                        break;

                    case PathSegment.SegmentType.S:
                        while (data.Length >= 4)
                        {
                            SmoothCubicBezierTo(data[0], data[1], data[2], data[3]);
                            data = data[4..];
                        }
                        break;

                    case PathSegment.SegmentType.s:
                        while (data.Length >= 4)
                        {
                            SmoothCubicBezierToRelative(data[0], data[1], data[2], data[3]);
                            data = data[4..];
                        }
                        break;

                    case PathSegment.SegmentType.z:
                        ClosePath();
                        break;
                }
            }

            #region Movement Commands

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void MoveTo(float x, float y)
            {
                var point = new Vector2(x, y);
                _builder.BeginFigure(point);
                SetInitialPoint(point);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void MoveToRelative(float x, float y)
            {
                if (_currentPoint is Vector2 current)
                {
                    var next = new Vector2(x + current.X, y + current.Y);
                    _builder.BeginFigure(next);
                    SetInitialPoint(next);
                }
                else
                {
                    MoveTo(x, y);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LineTo(float x, float y)
            {
                var point = new Vector2(x, y);
                _builder.AddLine(point);
                SetCurrentPoint(point);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void LineToRelative(float x, float y)
            {
                if (_currentPoint is Vector2 current)
                {
                    LineTo(x + current.X, y + current.Y);
                }
                else
                {
                    LineTo(x, y);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void HorizontalLineTo(float x)
            {
                if (_currentPoint is Vector2 current)
                {
                    LineTo(x, current.Y);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void HorizontalLineToRelative(float x)
            {
                if (_currentPoint is Vector2 current)
                {
                    LineTo(x + current.X, current.Y);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void VerticalLineTo(float y)
            {
                if (_currentPoint is Vector2 current)
                {
                    LineTo(current.X, y);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void VerticalLineToRelative(float y)
            {
                if (_currentPoint is Vector2 current)
                {
                    LineTo(current.X, y + current.Y);
                }
            }

            #endregion

            #region Curve Commands

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CubicBezierTo(float x1, float y1, float x2, float y2, float x, float y)
            {
                var endPoint = new Vector2(x, y);
                var controlPoint1 = new Vector2(x1, y1);
                var controlPoint2 = new Vector2(x2, y2);
                _builder.AddCubicBezier(controlPoint1, controlPoint2, endPoint);
                SetCubicPoint(endPoint, controlPoint2);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CubicBezierToRelative(float x1, float y1, float x2, float y2, float x, float y)
            {
                if (_currentPoint is Vector2 current)
                {
                    var endPoint = new Vector2(x + current.X, y + current.Y);
                    var controlPoint1 = new Vector2(x1 + current.X, y1 + current.Y);
                    var controlPoint2 = new Vector2(x2 + current.X, y2 + current.Y);
                    _builder.AddCubicBezier(controlPoint1, controlPoint2, endPoint);
                    SetCubicPoint(endPoint, controlPoint2);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SmoothCubicBezierTo(float x2, float y2, float x, float y)
            {
                if (_currentPoint is Vector2 current)
                {
                    var nextCubic = new Vector2(x2, y2);
                    var next = new Vector2(x, y);
                    var controlPoint1 = _cubicPoint is Vector2 cubic
                        ? new Vector2(2 * current.X - cubic.X, 2 * current.Y - cubic.Y)
                        : current;

                    _builder.AddCubicBezier(controlPoint1, nextCubic, next);
                    SetCubicPoint(next, nextCubic);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SmoothCubicBezierToRelative(float x2, float y2, float x, float y)
            {
                if (_currentPoint is Vector2 current)
                {
                    var nextCubic = new Vector2(x2 + current.X, y2 + current.Y);
                    var next = new Vector2(x + current.X, y + current.Y);
                    var controlPoint1 = _cubicPoint is Vector2 cubic
                        ? new Vector2(2 * current.X - cubic.X, 2 * current.Y - cubic.Y)
                        : current;

                    _builder.AddCubicBezier(controlPoint1, nextCubic, next);
                    SetCubicPoint(next, nextCubic);
                }
            }

            #endregion

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void ClosePath()
            {
                _builder.EndFigure(CanvasFigureLoop.Closed);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetCubicPoint(Vector2 point, Vector2 cubic)
            {
                _currentPoint = point;
                _cubicPoint = cubic;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetInitialPoint(Vector2 point)
            {
                SetCurrentPoint(point);
                _initialPoint = point;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetCurrentPoint(Vector2 point)
            {
                _currentPoint = point;
                _cubicPoint = null;
            }
        }

        private ref struct PathDataReader
        {
            private readonly ReadOnlySpan<char> _input;
            private readonly Span<PathSegment> _segmentBuffer;
            private readonly Span<float> _dataBuffer;
            private int _segmentCount;
            private int _dataOffset;
            private int _position;

            public PathDataReader(ReadOnlySpan<char> input, Span<PathSegment> segmentBuffer, Span<float> dataBuffer)
            {
                _input = input;
                _segmentBuffer = segmentBuffer;
                _dataBuffer = dataBuffer;
                _position = 0;
            }

            public bool TryReadSegment(out ReadOnlySpan<PathSegment> segments, out ReadOnlySpan<float> data)
            {
                if (TryReadSegmentGroup(_segmentBuffer[_segmentCount..], _dataBuffer[_dataOffset..], out int segmentsRead, out int dataRead))
                {
                    segments = _segmentBuffer.Slice(_segmentCount, segmentsRead);
                    data = _dataBuffer.Slice(_dataOffset, dataRead);

                    _segmentCount += segmentsRead;
                    _dataOffset += dataRead;

                    if (_segmentCount > MaxSegmentsStackAlloc - 32 || _dataOffset > MaxDataStackAlloc - 128)
                    {
                        _segmentCount = 0;
                        _dataOffset = 0;
                    }

                    return true;
                }

                segments = ReadOnlySpan<PathSegment>.Empty;
                data = ReadOnlySpan<float>.Empty;
                return false;
            }

            public bool TryReadSegmentGroup(Span<PathSegment> segmentBuffer, Span<float> dataBuffer,
                out int segmentsWritten, out int dataWritten)
            {
                segmentsWritten = 0;
                dataWritten = 0;

                if (!TryReadSegmentType(out var type))
                    return false;

                int argCount = GetArgumentCount(type);

                if (argCount == 0)
                {
                    segmentBuffer[0] = new PathSegment(type, 0, 0);
                    segmentsWritten = 1;
                    return true;
                }

                int dataStart = 0;
                int floatCount = type is PathSegment.SegmentType.A or PathSegment.SegmentType.a
                    ? ReadArcData(dataBuffer)
                    : ReadNumericData(dataBuffer);

                if (floatCount == 0)
                    return false;

                segmentsWritten = CreateSegments(type, dataBuffer.Slice(0, floatCount),
                    segmentBuffer, argCount);
                dataWritten = floatCount;

                return segmentsWritten > 0;
            }

            private int CreateSegments(PathSegment.SegmentType type, ReadOnlySpan<float> data,
                Span<PathSegment> output, int argCount)
            {
                int segmentCount = 0;
                bool isFirstSegment = true;

                for (int i = 0; i < data.Length; i += argCount)
                {
                    if (i + argCount > data.Length)
                        break;

                    var currentType = type;
                    if (!isFirstSegment)
                    {
                        currentType = type switch
                        {
                            PathSegment.SegmentType.M => PathSegment.SegmentType.L,
                            PathSegment.SegmentType.m => PathSegment.SegmentType.l,
                            _ => type
                        };
                    }

                    output[segmentCount] = new PathSegment(currentType, i, argCount);
                    segmentCount++;
                    isFirstSegment = false;
                }

                return segmentCount;
            }

            private int ReadNumericData(Span<float> output)
            {
                int count = 0;

                while (count < output.Length)
                {
                    SkipWhitespace();
                    if (TryReadNumber(out float value))
                    {
                        output[count++] = value;
                    }
                    else
                    {
                        break;
                    }
                }

                return count;
            }

            private int ReadArcData(Span<float> output)
            {
                int count = 0;
                const int arcArgCount = 7;

                while (count < output.Length)
                {
                    SkipWhitespace();
                    int argPosition = count % arcArgCount;

                    bool success = argPosition is 3 or 4
                        ? TryReadFlag(out output[count])
                        : TryReadNumber(out output[count]);

                    if (success)
                    {
                        count++;
                    }
                    else
                    {
                        break;
                    }
                }

                return count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryReadSegmentType(out PathSegment.SegmentType type)
            {
                SkipWhitespace();

                if (_position < _input.Length)
                {
                    char ch = _input[_position];
                    if (TryGetSegmentType(ch, out type))
                    {
                        _position++;
                        return true;
                    }
                }

                type = default;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool TryGetSegmentType(char ch, out PathSegment.SegmentType type)
            {
                type = ch switch
                {
                    'M' => PathSegment.SegmentType.M,
                    'm' => PathSegment.SegmentType.m,
                    'L' => PathSegment.SegmentType.L,
                    'l' => PathSegment.SegmentType.l,
                    'C' => PathSegment.SegmentType.C,
                    'c' => PathSegment.SegmentType.c,
                    'Q' => PathSegment.SegmentType.Q,
                    'q' => PathSegment.SegmentType.q,
                    'A' => PathSegment.SegmentType.A,
                    'a' => PathSegment.SegmentType.a,
                    'z' or 'Z' => PathSegment.SegmentType.z,
                    'H' => PathSegment.SegmentType.H,
                    'h' => PathSegment.SegmentType.h,
                    'V' => PathSegment.SegmentType.V,
                    'v' => PathSegment.SegmentType.v,
                    'S' => PathSegment.SegmentType.S,
                    's' => PathSegment.SegmentType.s,
                    'T' => PathSegment.SegmentType.T,
                    't' => PathSegment.SegmentType.t,
                    'E' => PathSegment.SegmentType.E,
                    'e' => PathSegment.SegmentType.e,
                    _ => default
                };

                return ch is 'M' or 'm' or 'L' or 'l' or 'C' or 'c' or 'Q' or 'q' or
                       'A' or 'a' or 'z' or 'Z' or 'H' or 'h' or 'V' or 'v' or
                       'S' or 's' or 'T' or 't' or 'E' or 'e';
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int GetArgumentCount(PathSegment.SegmentType type) => type switch
            {
                PathSegment.SegmentType.H or PathSegment.SegmentType.h or
                PathSegment.SegmentType.V or PathSegment.SegmentType.v => 1,
                PathSegment.SegmentType.M or PathSegment.SegmentType.m or
                PathSegment.SegmentType.L or PathSegment.SegmentType.l or
                PathSegment.SegmentType.T or PathSegment.SegmentType.t => 2,
                PathSegment.SegmentType.S or PathSegment.SegmentType.s or
                PathSegment.SegmentType.Q or PathSegment.SegmentType.q => 4,
                PathSegment.SegmentType.C or PathSegment.SegmentType.c => 6,
                PathSegment.SegmentType.A or PathSegment.SegmentType.a => 7,
                _ => 0
            };

            private bool TryReadNumber(out float value)
            {
                if (_position >= _input.Length)
                {
                    value = 0;
                    return false;
                }

                int start = _position;
                char ch = _input[_position];

                if (!(char.IsDigit(ch) || ch == '.' || ch == '-'))
                {
                    value = 0;
                    return false;
                }

                bool hasDot = ch == '.';
                _position++;

                while (_position < _input.Length)
                {
                    ch = _input[_position];
                    if (char.IsDigit(ch) || ch == 'e' || ch == 'E')
                    {
                        _position++;
                    }
                    else if (ch == '.' && !hasDot)
                    {
                        hasDot = true;
                        _position++;
                    }
                    else if (ch == '-' && _position > start &&
                             (_input[_position - 1] == 'e' || _input[_position - 1] == 'E'))
                    {
                        _position++;
                    }
                    else
                    {
                        break;
                    }
                }

                ReadOnlySpan<char> numberSpan = _input.Slice(start, _position - start);

#if NET6_0_OR_GREATER
            return float.TryParse(numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#else
                // Fallback for older frameworks
                return float.TryParse(numberSpan.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryReadFlag(out float value)
            {
                if (_position < _input.Length)
                {
                    char ch = _input[_position];
                    _position++;

                    if (ch == '0')
                    {
                        value = 0f;
                        return true;
                    }
                    if (ch == '1')
                    {
                        value = 1f;
                        return true;
                    }
                }

                value = 0;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SkipWhitespace()
            {
                while (_position < _input.Length)
                {
                    char ch = _input[_position];
                    if (ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r' || ch == ',')
                    {
                        _position++;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }
}
