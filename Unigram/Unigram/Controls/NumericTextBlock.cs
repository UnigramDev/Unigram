using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Numerics;
using Unigram.Common;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public class NumericTextBlock : Control
    {
        private ShapeVisual _shape;
        private CompositionBrush _foreground;

        private float _nineRight;
        private float _nineBottom;

        public NumericTextBlock()
        {
            if (!ApiInfo.CanUseDirectComposition)
            {
                return;
            }

            var shape = Window.Current.Compositor.CreateShapeVisual();
            //shape.Clip = Window.Current.Compositor.CreateInsetClip(0, 0, 1, 0);
            //shape.Size = new Vector2(100, 36);

            var part = GetPart('9');
            var bounds = part.ComputeBounds();

            _shape = shape;
            _nineRight = (float)bounds.Right;
            _nineBottom = (float)bounds.Bottom;

            Height = _nineBottom;
            shape.Size = new Vector2(0, _nineBottom);

            ElementCompositionPreview.SetElementChildVisual(this, shape);

            ApplyForeground();
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            OnValueChanged(Value, Value);
        }

        private void ApplyForeground()
        {
            if (Foreground is SolidColorBrush solid)
            {
                _foreground = Window.Current.Compositor.CreateColorBrush(solid.Color);
                OnValueChanged(Value, Value);
            }
        }

        public int Value
        {
            get { return (int)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(int), typeof(NumericTextBlock), new PropertyMetadata(0, OnValueChanged));

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((NumericTextBlock)d).OnValueChanged((int)e.NewValue, (int)e.OldValue);
        }

        private void OnValueChanged(int nextValue, int prevValue)
        {
            if (_shape == null)
            {
                return;
            }

            if (nextValue < 0)
            {
                _shape.Shapes.Clear();
                return;
            }

            var next = $"{nextValue}";
            var prev = $"{prevValue}";

            var nextArr = new CanvasGeometry[Math.Max(next.Length, prev.Length)];
            var prevArr = new CanvasGeometry[Math.Max(next.Length, prev.Length)];
            var prevFor = new bool[Math.Max(next.Length, prev.Length)];

            for (int i = 0; i < Math.Max(next.Length, prev.Length); i++)
            {
                if (next.Length > i && prev.Length > i)
                {
                    prevArr[i] = GetPart(prev[i]);

                    if (next[i] != prev[i])
                    {
                        nextArr[i] = GetPart(next[i]);
                    }
                }
                else if (prev.Length > i)
                {
                    prevArr[i] = GetPart(prev[i]);
                    prevFor[i] = true;
                }
                else if (next.Length > i)
                {
                    nextArr[i] = GetPart(next[i]);
                }
            }

            _shape.Shapes.Clear();

            var dir = nextValue - prevValue;
            var x = 0f;

            var foreground = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
            var background = Color.FromArgb(0x00, 0x00, 0x00, 0x00);

            if (Foreground is SolidColorBrush solid)
            {
                foreground = solid.Color;
                background = Color.FromArgb(0x00, solid.Color.R, solid.Color.G, solid.Color.B);
            }

            for (int i = 0; i < nextArr.Length; i++)
            {
                if (prevArr[i] != null)
                {
                    var brush = _shape.Compositor.CreateColorBrush(foreground);
                    var mask = _shape.Compositor.CreatePathGeometry(new CompositionPath(prevArr[i]));
                    var maskShape = _shape.Compositor.CreateSpriteShape(mask);
                    maskShape.FillBrush = brush;
                    maskShape.Offset = new Vector2(x, 0);

                    _shape.Shapes.Add(maskShape);

                    if (nextArr[i] != null || prevFor[i])
                    {
                        var offset = _shape.Compositor.CreateVector2KeyFrameAnimation();
                        offset.InsertKeyFrame(0, new Vector2(x, 0));
                        offset.InsertKeyFrame(1, new Vector2(x, dir > 0 ? -8 : 8));

                        maskShape.StartAnimation("Offset", offset);

                        var opacity = _shape.Compositor.CreateColorKeyFrameAnimation();
                        opacity.InsertKeyFrame(0, foreground);
                        opacity.InsertKeyFrame(1, background);

                        brush.StartAnimation("Color", opacity);
                    }
                    else
                    {
                        //var bounds = prevArr[i].ComputeBounds();
                        //prevX += (float)bounds.Right;
                        x += _nineRight;
                    }
                }

                if (nextArr[i] != null)
                {
                    var brush = _shape.Compositor.CreateColorBrush(foreground);
                    var mask = _shape.Compositor.CreatePathGeometry(new CompositionPath(nextArr[i]));
                    var maskShape = _shape.Compositor.CreateSpriteShape(mask);
                    maskShape.FillBrush = brush;
                    maskShape.Offset = new Vector2(x, 0);

                    _shape.Shapes.Add(maskShape);

                    //if (prevArr[i] != null)
                    {
                        var offset = _shape.Compositor.CreateVector2KeyFrameAnimation();
                        offset.InsertKeyFrame(0, new Vector2(x, dir > 0 ? 8 : -8));
                        offset.InsertKeyFrame(1, new Vector2(x, 0));

                        maskShape.StartAnimation("Offset", offset);
                    }

                    var opacity = _shape.Compositor.CreateColorKeyFrameAnimation();
                    opacity.InsertKeyFrame(0, background);
                    opacity.InsertKeyFrame(1, foreground);

                    brush.StartAnimation("Color", opacity);

                    //var bounds = nextArr[i].ComputeBounds();
                    //prevX += (float)bounds.Right;
                    x += _nineRight;
                }
            }

            _shape.Size = new Vector2(x, 100);
            Width = x;
        }

        CanvasGeometry GetPart(char value)
        {
            using (var textFormat = new CanvasTextFormat
            {
                FontFamily = "Segoe UI",
                FontWeight = FontWeights.Normal,
                FontSize = 12,
            })
            using (var layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), $"{value}", textFormat, 1000, 1000))
            {
                var text = CanvasGeometry.CreateText(layout);
                return text;
            }
        }
    }
}
