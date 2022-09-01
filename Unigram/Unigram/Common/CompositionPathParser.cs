using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.ViewModels.Drawers;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace Unigram.Common
{
    public class CompositionPathParser
    {
        public static CompositionPath Parse(string data)
        {
            var reader = new PathDataReader(data);

            var segments = reader.read();
            if (segments != null)
            {
                var builder = new CanvasPathBuilder(null);
                renderPath(segments, builder);

                return new CompositionPath(CanvasGeometry.CreatePath(builder));
            }

            return null;
        }

        public static CompositionPath Parse(IList<ClosedVectorPath> contours)
        {
            return new CompositionPath(Parse(null, contours));
        }

        public static CanvasGeometry Parse(ICanvasResourceCreator sender, IList<ClosedVectorPath> contours, float scale = 1.0f)
        {
            var builder = new CanvasPathBuilder(sender);

            foreach (var path in contours)
            {
                var last = path.Commands.Last();
                if (last is VectorPathCommandLine lastLine)
                {
                    builder.BeginFigure(lastLine.EndPoint.ToVector2(scale));
                }
                else if (last is VectorPathCommandCubicBezierCurve lastCubicBezierCurve)
                {
                    builder.BeginFigure(lastCubicBezierCurve.EndPoint.ToVector2(scale));
                }

                for (int i = 0; i < path.Commands.Count; i++)
                {
                    var command = path.Commands[i];
                    if (command is VectorPathCommandLine line)
                    {
                        builder.AddLine(line.EndPoint.ToVector2(scale));
                    }
                    else if (command is VectorPathCommandCubicBezierCurve cubicBezierCurve)
                    {
                        builder.AddCubicBezier(cubicBezierCurve.StartControlPoint.ToVector2(scale),
                            cubicBezierCurve.EndControlPoint.ToVector2(scale),
                            cubicBezierCurve.EndPoint.ToVector2(scale));
                    }
                }

                builder.EndFigure(CanvasFigureLoop.Closed);
            }

            return CanvasGeometry.CreatePath(builder);
        }

        public static CompositionAnimation ParseThumbnail(Sticker sticker, out ShapeVisual visual, bool animated = true)
        {
            return ParseThumbnail(sticker.Width, sticker.Height, sticker.Outline, out visual, animated);
        }

        public static CompositionAnimation ParseThumbnail(StickerViewModel sticker, out ShapeVisual visual, bool animated = true)
        {
            return ParseThumbnail(sticker.Width, sticker.Height, sticker.Outline, out visual, animated);
        }

        public static CompositionAnimation ParseThumbnail(float width, float height, IList<ClosedVectorPath> contours, out ShapeVisual visual, bool animated = true)
        {
            CompositionPath path;
            if (contours?.Count > 0)
            {
                path = Parse(contours);
            }
            else
            {
                path = new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, 0, 0, width, height, 80, 80));
            }

            return CreateThumbnail(width, height, path, out visual, animated);
        }

        public static CompositionAnimation CreateThumbnail(float width, float height, float cornerRadius, out ShapeVisual visual, bool animated = true)
        {
            var path = new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, 0, 0, width, height, cornerRadius, cornerRadius));
            return CreateThumbnail(width, height, path, out visual, animated);
        }

        public static CompositionAnimation CreateThumbnail(float width, float height, CompositionPath path, out ShapeVisual visual, bool animated = true)
        {
            var transparent = Color.FromArgb(0x00, 0x7A, 0x8A, 0x96);
            var foregroundColor = Color.FromArgb(0x33, 0x7A, 0x8A, 0x96);
            var backgroundColor = Color.FromArgb(0x33, 0x7A, 0x8A, 0x96);

            var gradient = Window.Current.Compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(1, 0);
            gradient.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(0.0f, transparent));
            gradient.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(0.5f, foregroundColor));
            gradient.ColorStops.Add(Window.Current.Compositor.CreateColorGradientStop(1.0f, transparent));

            var background = Window.Current.Compositor.CreateRectangleGeometry();
            background.Size = new Vector2(width, height);
            var backgroundShape = Window.Current.Compositor.CreateSpriteShape(background);
            backgroundShape.FillBrush = Window.Current.Compositor.CreateColorBrush(backgroundColor);

            var foreground = Window.Current.Compositor.CreateRectangleGeometry();
            foreground.Size = new Vector2(width, height);
            var foregroundShape = Window.Current.Compositor.CreateSpriteShape(foreground);
            foregroundShape.FillBrush = gradient;

            var clip = Window.Current.Compositor.CreateGeometricClip(Window.Current.Compositor.CreatePathGeometry(path));
            clip.ViewBox = Window.Current.Compositor.CreateViewBox();
            clip.ViewBox.Size = new Vector2(width, height);
            clip.ViewBox.Stretch = CompositionStretch.UniformToFill;

            visual = Window.Current.Compositor.CreateShapeVisual();
            visual.Clip = clip;
            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.RelativeSizeAdjustment = Vector2.One;
            visual.ViewBox = Window.Current.Compositor.CreateViewBox();
            visual.ViewBox.Size = new Vector2(width, height);
            visual.ViewBox.Stretch = CompositionStretch.UniformToFill;

            if (animated)
            {
                var animation = Window.Current.Compositor.CreateVector2KeyFrameAnimation();
                animation.InsertKeyFrame(0, new Vector2(-width, 0));
                animation.InsertKeyFrame(1, new Vector2(width, 0));
                animation.IterationBehavior = AnimationIterationBehavior.Forever;
                animation.Duration = TimeSpan.FromSeconds(1);

                foregroundShape.StartAnimation("Offset", animation);

                return animation;
            }

            return null;
        }


        public struct PathSegment
        {
            public enum SegmentType
            {
                M,
                L,
                C,
                Q,
                A,
                z,
                H,
                V,
                S,
                T,
                m,
                l,
                c,
                q,
                a,
                h,
                v,
                s,
                t,
                E,
                e
            }

            public SegmentType type;
            public float[] data;

            public PathSegment(SegmentType type = SegmentType.M, float[] data = null)
            {
                this.type = type;
                this.data = data ?? new float[0];
            }

            public bool isAbsolute()
            {
                switch (type)
                {
                    case SegmentType.M:
                    case SegmentType.L:
                    case SegmentType.H:
                    case SegmentType.V:
                    case SegmentType.C:
                    case SegmentType.S:
                    case SegmentType.Q:
                    case SegmentType.T:
                    case SegmentType.A:
                    case SegmentType.E:
                        return true;
                    default:
                        return false;
                }
            }

            public override bool Equals(object obj)
            {
                if (obj is PathSegment rhs)
                {
                    return type == rhs.type && data.SequenceEqual(rhs.data);
                }

                return base.Equals(obj);
            }

            //    public static func == (lhs: PathSegment, rhs: PathSegment) -> Bool {
            //return lhs.type == rhs.type && lhs.data == rhs.data
            //}
        }

        private static void renderPath(IList<PathSegment> segments, CanvasPathBuilder builder)
        {
            Vector2? currentPoint = null;
            Vector2? cubicPoint = null;
            Vector2? quadrPoint = null;
            Vector2? initialPoint = null;

            void M(float x, float y)
            {
                var point = new Vector2(x, y);
                builder.BeginFigure(point);
                //context.move(to: point);
                setInitPoint(point);
            }

            void m(float x, float y)
            {
                if (currentPoint is Vector2 cur)
                {
                    var next = new Vector2(x + cur.X, y + cur.Y);
                    builder.BeginFigure(next);
                    //context.move(to: next);
                    setInitPoint(next);
                }
                else
                {
                    M(x, y: y);
                }
            }

            void L(float x, float y)
            {
                lineTo(new Vector2(x, y));
            }

            void l(float x, float y)
            {
                if (currentPoint is Vector2 cur)
                {
                    lineTo(new Vector2(x + cur.X, y + cur.Y));
                }
                else
                {
                    L(x, y: y);
                }
            }

            void H(float x)
            {
                if (currentPoint is Vector2 cur)
                {
                    lineTo(new Vector2(x, cur.Y));
                }
            }

            void h(float x)
            {
                if (currentPoint is Vector2 cur)
                {
                    lineTo(new Vector2(x + cur.X, cur.Y));
                }
            }

            void V(float y)
            {
                if (currentPoint is Vector2 cur)
                {
                    lineTo(new Vector2(cur.X, y));
                }
            }

            void v(float y)
            {
                if (currentPoint is Vector2 cur)
                {
                    lineTo(new Vector2(cur.X, y + cur.Y));
                }
            }

            void lineTo(Vector2 p)
            {
                builder.AddLine(p);
                //context.addLine(to: p);
                setPoint(p);
            }

            void c(float x1, float y1, float x2, float y2, float x, float y)
            {
                if (currentPoint is Vector2 cur)
                {
                    var endPoint = new Vector2(x + cur.X, y + cur.Y);
                    var controlPoint1 = new Vector2(x1 + cur.X, y1 + cur.Y);
                    var controlPoint2 = new Vector2(x2 + cur.X, y2 + cur.Y);
                    builder.AddCubicBezier(controlPoint1, controlPoint2, endPoint);
                    //context.addCurve(to: endPoint, control1: controlPoint1, control2: controlPoint2);
                    setCubicPoint(endPoint, cubic: controlPoint2);
                }
            }

            void C(float x1, float y1, float x2, float y2, float x, float y)
            {
                var endPoint = new Vector2(x, y);
                var controlPoint1 = new Vector2(x1, y1);
                var controlPoint2 = new Vector2(x2, y2);
                builder.AddCubicBezier(controlPoint1, controlPoint2, endPoint);
                //context.addCurve(to: endPoint, control1: controlPoint1, control2: controlPoint2);
                setCubicPoint(endPoint, cubic: controlPoint2);
            }

            void s(float x2, float y2, float x, float y)
            {
                if (currentPoint is Vector2 cur)
                {
                    var nextCubic = new Vector2(x2 + cur.X, y2 + cur.Y);
                    var next = new Vector2(x + cur.X, y + cur.Y);
                    Vector2 xy1;
                    if (cubicPoint is Vector2 curCubicVal)
                    {
                        xy1 = new Vector2((2 * cur.X) - curCubicVal.X, (2 * cur.Y) - curCubicVal.Y);
                    }
                    else
                    {
                        xy1 = cur;
                    }
                    builder.AddCubicBezier(xy1, nextCubic, next);
                    //context.addCurve(to: next, control1: xy1, control2: nextCubic);
                    setCubicPoint(next, cubic: nextCubic);
                }
            }

            void S(float x2, float y2, float x, float y)
            {
                if (currentPoint is Vector2 cur)
                {
                    var nextCubic = new Vector2(x2, y2);
                    var next = new Vector2(x, y);
                    Vector2 xy1;
                    if (cubicPoint is Vector2 curCubicVal)
                    {
                        xy1 = new Vector2((2 * cur.X) - curCubicVal.X, (2 * cur.Y) - curCubicVal.Y);
                    }
                    else
                    {
                        xy1 = cur;
                    }
                    builder.AddCubicBezier(xy1, nextCubic, next);
                    //context.addCurve(to: next, control1: xy1, control2: nextCubic);
                    setCubicPoint(next, cubic: nextCubic);
                }
            }

            void z()
            {
                builder.EndFigure(CanvasFigureLoop.Closed);
                //context.fillPath();
            }

            void setQuadrPoint(Vector2 p, Vector2 quadr)
            {
                currentPoint = p;
                quadrPoint = quadr;
                cubicPoint = null;
            }

            void setCubicPoint(Vector2 p, Vector2 cubic)
            {
                currentPoint = p;
                cubicPoint = cubic;
                quadrPoint = null;
            }

            void setInitPoint(Vector2 p)
            {
                setPoint(p);
                initialPoint = p;
            }

            void setPoint(Vector2 p)
            {
                currentPoint = p;
                cubicPoint = null;
                quadrPoint = null;
            }

            foreach (var segment in segments)
            {
                var data = segment.data.AsSpan();
                switch (segment.type)
                {
                    case PathSegment.SegmentType.M:
                        M(data[0], data[1]);
                        data = data.Slice(2);
                        //data.removeSubrange(Range(uncheckedBounds: (lower: 0, upper: 2)));
                        while (data.Length >= 2)
                        {
                            L(data[0], data[1]);
                            data = data.Slice(2);
                            //data.removeSubrange((0.. < 2));
                        }
                        break;
                    case PathSegment.SegmentType.m:
                        m(data[0], data[1]);
                        data = data.Slice(2);
                        //data.removeSubrange((0.. < 2));
                        while (data.Length >= 2)
                        {
                            l(data[0], data[1]);
                            data = data.Slice(2);
                            //data.removeSubrange((0.. < 2));
                        }
                        break;
                    case PathSegment.SegmentType.L:
                        while (data.Length >= 2)
                        {
                            L(data[0], data[1]);
                            data = data.Slice(2);
                            //data.removeSubrange((0.. < 2));
                        }
                        break;
                    case PathSegment.SegmentType.l:
                        while (data.Length >= 2)
                        {
                            l(data[0], data[1]);
                            data = data.Slice(2);
                            //data.removeSubrange((0.. < 2));
                        }
                        break;
                    case PathSegment.SegmentType.H:
                        H(data[0]);
                        break;
                    case PathSegment.SegmentType.h:
                        h(data[0]);
                        break;
                    case PathSegment.SegmentType.V:
                        V(data[0]);
                        break;
                    case PathSegment.SegmentType.v:
                        v(data[0]);
                        break;
                    case PathSegment.SegmentType.C:
                        while (data.Length >= 6)
                        {
                            C(data[0], data[1], data[2], data[3], data[4], data[5]);
                            data = data.Slice(6);
                            //data.removeSubrange((0.. < 6));
                        }
                        break;
                    case PathSegment.SegmentType.c:
                        while (data.Length >= 6)
                        {
                            c(data[0], data[1], data[2], data[3], data[4], data[5]);
                            data = data.Slice(6);
                            //data.removeSubrange((0.. < 6));
                        }
                        break;
                    case PathSegment.SegmentType.S:
                        while (data.Length >= 4)
                        {
                            S(data[0], data[1], data[2], data[3]);
                            data = data.Slice(4);
                            //data.removeSubrange((0.. < 4));
                        }
                        break;
                    case PathSegment.SegmentType.s:
                        while (data.Length >= 4)
                        {
                            s(data[0], y2: data[1], x: data[2], y: data[3]);
                            data = data.Slice(4);
                            //data.removeSubrange((0.. < 4));
                        }
                        break;
                    case PathSegment.SegmentType.z:
                        z();
                        break;
                    default:
                        //print("unknown");
                        break;
                }
            }
        }

        private class PathDataReader
        {
            private readonly string input;
            private char? current;
            private char? previous;
            private readonly CharEnumerator iterator;

            private static readonly char[] spaces = new[] { '\n', '\r', '\t', ' ', ',' };

            public PathDataReader(string input)
            {
                this.input = input;
                iterator = input.GetEnumerator();
            }

            public IList<PathSegment> read()
            {
                readNext();
                var segments = new List<PathSegment>();

                for (var array = readSegments(); array != null; array = readSegments())
                {
                    segments.AddRange(array);
                }

                return segments;
            }

            private IEnumerable<PathSegment> readSegments()
            {
                var segmentType = readSegmentType();
                if (segmentType is PathSegment.SegmentType type)
                {
                    var argCount = getArgCount(type);
                    if (argCount == 0)
                    {
                        return new[] { new PathSegment(type) };
                    }
                    var result = new List<PathSegment>();
                    IList<float> data;
                    if (type == PathSegment.SegmentType.a || type == PathSegment.SegmentType.A)
                    {
                        data = readDataOfASegment();
                    }
                    else
                    {
                        data = readData();
                    }
                    var index = 0;
                    var isFirstSegment = true;
                    while (index < data.Count)
                    {
                        var end = index + argCount;
                        if (end > data.Count)
                        {
                            break;
                        }
                        var currentType = type;
                        if (type == PathSegment.SegmentType.M && !isFirstSegment)
                        {
                            currentType = PathSegment.SegmentType.L;
                        }
                        if (type == PathSegment.SegmentType.m && !isFirstSegment)
                        {
                            currentType = PathSegment.SegmentType.l;
                        }
                        result.Add(new PathSegment(currentType, data.Skip(index).Take(end).ToArray()));
                        //result.Add(new PathSegment(currentType, data: Array(data[index.. < end])));
                        isFirstSegment = false;
                        index = end;
                    }
                    return result;
                }
                return null;
            }

            private IList<float> readData()
            {
                var data = new List<float>();
                while (true)
                {
                    skipSpaces();
                    var value = readNum();
                    if (value is float floatValue)
                    {
                        data.Add(floatValue);
                    }
                    else
                    {
                        return data;
                    }
                }
            }

            private IList<float> readDataOfASegment()
            {
                var argCount = getArgCount(PathSegment.SegmentType.A);
                var data = new List<float>();
                var index = 0;
                while (true)
                {
                    skipSpaces();
                    float? value;
                    var indexMod = index % argCount;
                    if (indexMod == 3 || indexMod == 4)
                    {
                        value = readFlag();
                    }
                    else
                    {
                        value = readNum();
                    }
                    if (value is float floatValue)
                    {
                        data.Add(floatValue);
                    }
                    else
                    {
                        return data;
                    }
                    index += 1;
                }

                return data;
            }

            private void skipSpaces()
            {
                var currentCharacter = current;
                for (var character = currentCharacter; spaces.Contains(character.Value); character = currentCharacter)
                {
                    currentCharacter = readNext();
                }
            }

            private float? readFlag()
            {
                if (current is char ch)
                {
                    readNext();
                    switch (ch)
                    {
                        case '0':
                            return 0;
                        case '1':
                            return 1;
                        default:
                            return null;
                    }

                }

                return null;
            }

            private float? readNum()
            {
                if (current is char ch)
                {
                    if (ch >= '0' && ch <= '9' || ch == '.' || ch == '-')
                    {
                        var chars = $"{ch}";
                        var hasDot = ch == '.';

                        for (var chj = readDigit(ref hasDot); chj != null; chj = readDigit(ref hasDot))
                        {
                            chars += chj;
                        }

                        if (float.TryParse(chars, out float result))
                        {
                            return result;
                        }
                    }
                }

                return null;
            }

            private char? readDigit(ref bool hasDot)
            {
                var ch = readNext();
                if (ch != null)
                {
                    if ((ch >= '0' && ch <= '9') || ch == 'e' || (previous == 'e' && ch == '-'))
                    {
                        return ch;
                    }
                    else if (ch == '.' && !hasDot)
                    {
                        hasDot = true;
                        return ch;
                    }
                }
                return null;
            }

            private bool isNum(char ch, ref bool hasDot)
            {
                switch (ch)
                {
                    case char value when value >= '0' && value <= '9':
                        return true;
                    case '.':
                        if (hasDot)
                        {
                            return false;
                        }
                        hasDot = true;
                        break;
                    default:
                        return true;
                }
                return false;
            }

            private char? readNext()
            {
                previous = current;
                if (iterator.MoveNext())
                {
                    current = iterator.Current;
                }
                else
                {
                    current = null;
                }
                return current;
            }

            private PathSegment.SegmentType? readSegmentType()
            {
                while (true)
                {
                    var type = getPathSegmentType();
                    if (type != null)
                    {
                        readNext();
                        return type;
                    }
                    if (readNext() == null)
                    {
                        return null;
                    }
                }
            }

            private PathSegment.SegmentType? getPathSegmentType()
            {
                if (current is char ch)
                {
                    switch (ch)
                    {
                        case 'M':
                            return PathSegment.SegmentType.M;
                        case 'm':
                            return PathSegment.SegmentType.m;
                        case 'L':
                            return PathSegment.SegmentType.L;
                        case 'l':
                            return PathSegment.SegmentType.l;
                        case 'C':
                            return PathSegment.SegmentType.C;
                        case 'c':
                            return PathSegment.SegmentType.c;
                        case 'Q':
                            return PathSegment.SegmentType.Q;
                        case 'q':
                            return PathSegment.SegmentType.q;
                        case 'A':
                            return PathSegment.SegmentType.A;
                        case 'a':
                            return PathSegment.SegmentType.a;
                        case 'z':
                        case 'Z':
                            return PathSegment.SegmentType.z;
                        case 'H':
                            return PathSegment.SegmentType.H;
                        case 'h':
                            return PathSegment.SegmentType.h;
                        case 'V':
                            return PathSegment.SegmentType.V;
                        case 'v':
                            return PathSegment.SegmentType.v;
                        case 'S':
                            return PathSegment.SegmentType.S;
                        case 's':
                            return PathSegment.SegmentType.s;
                        case 'T':
                            return PathSegment.SegmentType.T;
                        case 't':
                            return PathSegment.SegmentType.t;
                        default:
                            break;

                    }
                }

                return null;
            }

            private int getArgCount(PathSegment.SegmentType segment)
            {
                switch (segment)
                {
                    case PathSegment.SegmentType.H:
                    case PathSegment.SegmentType.h:
                    case PathSegment.SegmentType.V:
                    case PathSegment.SegmentType.v:
                        return 1;
                    case PathSegment.SegmentType.M:
                    case PathSegment.SegmentType.m:
                    case PathSegment.SegmentType.L:
                    case PathSegment.SegmentType.l:
                    case PathSegment.SegmentType.T:
                    case PathSegment.SegmentType.t:
                        return 2;
                    case PathSegment.SegmentType.S:
                    case PathSegment.SegmentType.s:
                    case PathSegment.SegmentType.Q:
                    case PathSegment.SegmentType.q:
                        return 4;
                    case PathSegment.SegmentType.C:
                    case PathSegment.SegmentType.c:
                        return 6;
                    case PathSegment.SegmentType.A:
                    case PathSegment.SegmentType.a:
                        return 7;
                    default:
                        return 0;
                }
            }
        }

    }
}
