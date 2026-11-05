//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls
{
    public partial class PlaybackNextButton : GlyphButton
    {
        // ic_fluent_fast_forward_20_filled is two copies of one rounded triangle, the second this
        // far ahead of the first; ic_fluent_rewind_20_filled is the same thing mirrored.
        private const float Step = 7;

        private CompositionColorBrush _brush;
        private ShapeVisual _visual;

        private CompositionSpriteShape _incoming;
        private CompositionSpriteShape _rear;
        private CompositionSpriteShape _front;

        private Vector2KeyFrameAnimation _grow;
        private Vector2KeyFrameAnimation _advance;
        private Vector2KeyFrameAnimation _shrink;

        public PlaybackNextButton()
        {
            DefaultStyleKey = typeof(PlaybackNextButton);
            Click += OnClick;

            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
            RegisterPropertyChangedCallback(FontSizeProperty, OnFontSizeChanged);
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (_visual == null)
            {
                return;
            }

            // Never reset: at their first key frames the rear triangle is back under the incoming
            // one and the front one is back where the rear one ended, so restarting moves nothing.
            _incoming.StartAnimation("Scale", _grow);
            _rear.StartAnimation("Offset", _advance);
            _front.StartAnimation("Scale", _shrink);
        }

        protected override void OnApplyTemplate()
        {
            var sign = IsPrevious ? -1 : 1;

            // Where the two triangles' bases sit in the 20px icon.
            var rear = IsPrevious ? 17.0003f : 3.0183f;
            var front = rear + sign * Step;

            var target = GetTemplateChild("Target") as UIElement;
            var compositor = BootStrapper.Current.Compositor;

            var geometry = compositor.CreatePathGeometry(CreateTriangle(sign));

            _brush = compositor.CreateColorBrush(Colors.Black);

            _incoming = compositor.CreateSpriteShape(geometry);
            _incoming.FillBrush = _brush;
            _incoming.Offset = new Vector2(rear, 0);
            _incoming.CenterPoint = new Vector2(0, 10);
            _incoming.Scale = Vector2.Zero;

            _rear = compositor.CreateSpriteShape(geometry);
            _rear.FillBrush = _brush;
            _rear.Offset = new Vector2(rear, 0);

            _front = compositor.CreateSpriteShape(geometry);
            _front.FillBrush = _brush;
            _front.Offset = new Vector2(front, 0);
            _front.CenterPoint = new Vector2(sign * 8, 10);

            // The shapes are in the icon's 20px box, scaled to FontSize: it is the icon's size, as it
            // is the glyph's for every other GlyphButton.
            var viewBox = compositor.CreateViewBox();
            viewBox.Size = new Vector2(20, 20);

            _visual = compositor.CreateShapeVisual();
            _visual.ViewBox = viewBox;
            _visual.Shapes.Add(_incoming);
            _visual.Shapes.Add(_rear);
            _visual.Shapes.Add(_front);

            _grow = compositor.CreateVector2KeyFrameAnimation();
            _grow.InsertKeyFrame(0, Vector2.Zero);
            _grow.InsertKeyFrame(1, Vector2.One);

            _advance = compositor.CreateVector2KeyFrameAnimation();
            _advance.InsertKeyFrame(0, new Vector2(rear, 0));
            _advance.InsertKeyFrame(1, new Vector2(front, 0));

            _shrink = compositor.CreateVector2KeyFrameAnimation();
            _shrink.InsertKeyFrame(0, Vector2.One);
            _shrink.InsertKeyFrame(1, Vector2.Zero);

            ApplyForeground();
            ApplyFontSize();

            ElementCompositionPreview.SetElementChildVisual(target, _visual);
        }

        private void OnForegroundChanged(DependencyObject sender, DependencyProperty dp)
        {
            ApplyForeground();
        }

        private void OnFontSizeChanged(DependencyObject sender, DependencyProperty dp)
        {
            ApplyFontSize();
        }

        private void ApplyFontSize()
        {
            if (_visual != null)
            {
                _visual.Size = new Vector2((float)FontSize);
            }
        }

        private void ApplyForeground()
        {
            if (_visual != null && Foreground is SolidColorBrush solid)
            {
                // The triangles overlap, so a translucent fill would darken where they do: the
                // fill is opaque and the alpha goes on the visual instead.
                var color = solid.Color;

                _brush.Color = Color.FromArgb(255, color.R, color.G, color.B);
                _visual.Opacity = color.A / 255f;
            }
        }

        /// <summary>
        /// The icon's triangle, its base on x = 0 and its apex towards <paramref name="sign"/>.
        /// </summary>
        private static CompositionPath CreateTriangle(float sign)
        {
            using var builder = new CanvasPathBuilder(null);

            builder.BeginFigure(new Vector2(0, 5.48617f));
            builder.AddCubicBezier(new Vector2(0, 4.66707f), new Vector2(sign * 0.9308f, 4.19538f), new Vector2(sign * 1.5914f, 4.67976f));
            builder.AddLine(new Vector2(sign * 7.471f, 8.99146f));
            builder.AddCubicBezier(new Vector2(sign * 8.152f, 9.4909f), new Vector2(sign * 8.152f, 10.508f), new Vector2(sign * 7.471f, 11.0075f));
            builder.AddLine(new Vector2(sign * 1.5914f, 15.3192f));
            builder.AddCubicBezier(new Vector2(sign * 0.9308f, 15.8036f), new Vector2(0, 15.3319f), new Vector2(0, 14.5128f));
            builder.EndFigure(CanvasFigureLoop.Closed);

            return new CompositionPath(CanvasGeometry.CreatePath(builder));
        }

        #region IsPrevious

        public bool IsPrevious
        {
            get => (bool)GetValue(IsPreviousProperty);
            set => SetValue(IsPreviousProperty, value);
        }

        public static readonly DependencyProperty IsPreviousProperty =
            DependencyProperty.Register("IsPrevious", typeof(bool), typeof(PlaybackNextButton), new PropertyMetadata(false));

        #endregion
    }
}
