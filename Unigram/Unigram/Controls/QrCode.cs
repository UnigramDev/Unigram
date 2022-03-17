using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Unigram.Native;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class QrCode : Control
    {
        private string _text;

        public QrCode()
        {
            DefaultStyleKey = typeof(QrCode);

            RegisterPropertyChangedCallback(ForegroundProperty, OnBrushChanged);
        }

        private void OnBrushChanged(DependencyObject sender, DependencyProperty dp)
        {
            OnTextChanged(Text, null);
        }

        private void OnTextChanged(string newValue, string oldValue)
        {
            if (string.IsNullOrEmpty(newValue))
            {
                ElementCompositionPreview.SetElementChildVisual(this, null);
                return;
            }

            if (oldValue != null)
            {
                if (string.Equals(newValue, oldValue, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(newValue, _text, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _text = newValue;

            var foreground = Colors.Black;
            if (Foreground is SolidColorBrush foreBrush)
            {
                foreground = foreBrush.Color;
            }

            Draw(newValue, foreground, string.IsNullOrEmpty(oldValue));
        }

        private void Draw(string text, Color foreground, bool fadeIn)
        {
            if (string.IsNullOrEmpty(text))
            {
                ElementCompositionPreview.SetElementChildVisual(this, null);
                return;
            }

            var data = QrBuffer.FromString(text);
            var replaceFrom = data.ReplaceFrom;
            var replaceTill = data.ReplaceTill;

            var size = 259;
            var pixel = size / data.Size;

            bool value(int row, int column)
            {
                return (row >= 0)
                    && (row < data.Size)
                    && (column >= 0)
                    && (column < data.Size)
                    && (row < replaceFrom
                        || row >= replaceTill
                        || column < replaceFrom
                        || column >= replaceTill)
                    && data.Values[row * data.Size + column];
            };
            bool blackFull(int row, int column)
            {
                return (value(row - 1, column) && value(row + 1, column))
                    || (value(row, column - 1) && value(row, column + 1));
            };
            bool whiteCorner(int row, int column, int dx, int dy)
            {
                return !value(row + dy, column)
                    || !value(row, column + dx)
                    || !value(row + dy, column + dx);
            };
            bool whiteFull(int row, int column)
            {
                return whiteCorner(row, column, -1, -1)
                    && whiteCorner(row, column, 1, -1)
                    && whiteCorner(row, column, 1, 1)
                    && whiteCorner(row, column, -1, 1);
            };

            var skip = pixel - pixel / 2;

            var geometries = new CanvasGeometry[6];
            var geometry = 0;

            using var builder = new CanvasPathBuilder(null);

            void large(float x, float y)
            {
                var rect1 = CanvasGeometry.CreateRoundedRectangle(null, x, y, pixel * 7 + 2, pixel * 7 + 2, 15, 15);
                var rect2 = CanvasGeometry.CreateRoundedRectangle(null, x + pixel, y + pixel, pixel * 5 + 2, pixel * 5 + 2, 9, 9);
                var rect3 = CanvasGeometry.CreateRoundedRectangle(null, x + pixel * 2, y + pixel * 2, pixel * 3 + 2, pixel * 3 + 2, pixel, pixel);

                geometries[geometry++] = rect1.CombineWith(rect2, Matrix3x2.Identity, CanvasGeometryCombine.Exclude);
                geometries[geometry++] = rect3;
            };
            void brect(float x, float y, float width, float height)
            {
                builder.AddGeometry(CanvasGeometry.CreateRectangle(null, x, y, width, height));
            };
            void barch(float x, float y, bool topLeft, bool topRight, bool bottomRight, bool bottomLeft)
            {
                var width = pixel / 2.0f;
                var height = pixel / 2.0f;

                builder.BeginFigure(x, y + width);

                if (topLeft)
                {
                    builder.AddArc(new Vector2(x + width, y), width, height, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                }
                else
                {
                    builder.AddLine(x, y);
                    builder.AddLine(x + width, y);
                }

                if (topRight)
                {
                    builder.AddArc(new Vector2(x + pixel, y + height), width, height, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                }
                else
                {
                    builder.AddLine(x + pixel, y);
                    builder.AddLine(x + pixel, y + height);
                }

                if (bottomRight)
                {
                    builder.AddArc(new Vector2(x + width, y + pixel), width, height, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                }
                else
                {
                    builder.AddLine(x + pixel, y + pixel);
                    builder.AddLine(x + width, y + pixel);
                }

                if (bottomLeft)
                {
                    builder.AddArc(new Vector2(x, y + height), width, height, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                }
                else
                {
                    builder.AddLine(x, y + pixel);
                }

                builder.EndFigure(CanvasFigureLoop.Closed);
            };
            void warch(float x, float y, float width, float height, int direction)
            {
                if (direction == 0)
                {
                    builder.BeginFigure(x, y + height);
                    builder.AddArc(new Vector2(x + width, y), width, height, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                    builder.AddLine(x, y);
                }
                else if (direction == 1)
                {
                    builder.BeginFigure(x, y);
                    builder.AddArc(new Vector2(x + width, y + height), width, height, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                    builder.AddLine(x + width, y);
                }
                else if (direction == 2)
                {
                    builder.BeginFigure(x + width, y);
                    builder.AddArc(new Vector2(x, y + height), width, height, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                    builder.AddLine(x + width, y + width);
                }
                else if (direction == 3)
                {
                    builder.BeginFigure(x + width, y + height);
                    builder.AddArc(new Vector2(x, y), width, height, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
                    builder.AddLine(x, y + height);
                }

                builder.EndFigure(CanvasFigureLoop.Closed);
            }

            for (int row = 0; row != data.Size; ++row)
            {
                for (int column = 0; column != data.Size; ++column)
                {
                    if ((row < 7 && (column < 7 || column >= data.Size - 7))
                        || (column < 7 && (row < 7 || row >= data.Size - 7)))
                    {
                        continue;
                    }

                    var x = column * pixel;
                    var y = row * pixel;

                    if (value(row, column))
                    {
                        if (blackFull(row, column))
                        {
                            brect(x, y, pixel, pixel);
                        }
                        else
                        {
                            var top = value(row - 1, column);
                            var bottom = !top && value(row + 1, column);
                            var left = value(row, column - 1);
                            var right = !left && value(row, column + 1);

                            barch(x, y, !top && !left, !top && !right, !bottom && !right, !bottom && !left);
                        }
                    }
                    else if (!whiteFull(row, column))
                    {
                        if (!whiteCorner(row, column, -1, -1))
                        {
                            warch(x, y, pixel / 2, pixel / 2, 0);
                        }
                        if (!whiteCorner(row, column, 1, -1))
                        {
                            warch(x + skip, y, pixel / 2, pixel / 2, 1);
                        }
                        if (!whiteCorner(row, column, 1, 1))
                        {
                            warch(x + skip, y + skip, pixel / 2, pixel / 2, 2);
                        }
                        if (!whiteCorner(row, column, -1, 1))
                        {
                            warch(x, y + skip, pixel / 2, pixel / 2, 3);
                        }
                    }
                }
            }

            large(0, 0);
            large((data.Size - 7) * pixel - 2, 0);
            large(0, (data.Size - 7) * pixel - 2);

            var compositor = Window.Current.Compositor;
            var blackBrush = compositor.CreateColorBrush(foreground);

            var path1 = compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding)));
            var path2 = compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreatePath(builder)));

            var shape1 = compositor.CreateSpriteShape(path1);
            shape1.FillBrush = blackBrush;

            var shape2 = compositor.CreateSpriteShape(path2);
            shape2.FillBrush = blackBrush;

            var visual1 = compositor.CreateShapeVisual();
            visual1.Size = new Vector2(259, 259);
            visual1.Shapes.Add(shape1);

            var visual2 = compositor.CreateShapeVisual();
            visual2.Size = new Vector2(259, 259);
            visual2.Shapes.Add(shape2);

            var container = compositor.CreateContainerVisual();
            container.Size = new Vector2(259, 259);
            container.Children.InsertAtTop(visual1);
            container.Children.InsertAtTop(visual2);

            ElementCompositionPreview.SetElementChildVisual(this, container);

            if (fadeIn)
            {
                var opacity = compositor.CreateScalarKeyFrameAnimation();
                opacity.InsertKeyFrame(0, 0);
                opacity.InsertKeyFrame(1, 1);

                visual2.StartAnimation("Opacity", opacity);
            }
        }

        #region Text

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(QrCode), new PropertyMetadata(null, OnTextChanged));

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((QrCode)d).OnTextChanged((string)e.NewValue, (string)e.OldValue);
        }

        #endregion
    }
}
