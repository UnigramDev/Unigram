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
    /// <summary>
    /// ic_fluent_play_20_filled and ic_fluent_pause_20_filled, each bar of the pause morphing into
    /// one half of the play triangle while the icon turns a quarter.
    /// </summary>
    public partial class PlaybackPlayButton : GlyphButton
    {
        // A path animation only interpolates between paths made of the same segments, so every
        // shape is one figure of four corners and four edges, in the same order: a start point,
        // then a cubic per corner, each followed by a line to the next one but the last. A corner
        // the shape does not have is a cubic collapsed onto a point.
        //
        // The shapes are drawn in a frame that play shows at 0 degrees and pause at 90, so in it
        // the pause bars lie flat, and each turns into the half of the triangle on its side of
        // the tip.

        private const float PauseAngle = 90;

        // A pixel thinner than ic_fluent_pause_20_filled's bars, within the same outer edges, so
        // the gap between them doubles. Whole pixels, so they stay sharp at 100%.
        private static readonly Vector2[] PauseTop =
        {
            new(2, 5),
            new(2, 3.89543f), new(2.89543f, 3), new(4, 3),
            new(16, 3),
            new(17.1046f, 3), new(18, 3.89543f), new(18, 5),
            new(18, 6),
            new(18, 7.10457f), new(17.1046f, 8), new(16, 8),
            new(4, 8),
            new(2.89543f, 8), new(2, 7.10457f), new(2, 6)
        };

        private static readonly Vector2[] PauseBottom =
        {
            new(2, 14),
            new(2, 12.89543f), new(2.89543f, 12), new(4, 12),
            new(16, 12),
            new(17.1046f, 12), new(18, 12.89543f), new(18, 14),
            new(18, 15),
            new(18, 16.10457f), new(17.1046f, 17), new(16, 17),
            new(4, 17),
            new(2.89543f, 17), new(2, 16.10457f), new(2, 15)
        };

        // The icon's rounded tip is a single cubic, split in two so that each half of the triangle
        // takes one, as a corner. The top half runs on to 10.5 underneath the bottom one: two
        // shapes that only touch leave an antialiased seam between them.
        private static readonly Vector2[] PlayTop =
        {
            new(5, 4.50214f),
            new(5, 3.36137f), new(6.22292f, 2.63812f), new(7.22259f, 3.18766f),
            new(17.2221f, 8.68458f),
            new(17.74035f, 8.96948f), new(17.99948f, 9.48426f), new(17.99948f, 9.99904f),
            new(17.5f, 10.5f),
            new(17.5f, 10.5f), new(17.5f, 10.5f), new(17.5f, 10.5f),
            new(5, 10.5f),
            new(5, 10.5f), new(5, 10.5f), new(5, 10.5f)
        };

        private static readonly Vector2[] PlayBottom =
        {
            new(5, 9.99904f),
            new(5, 9.99904f), new(5, 9.99904f), new(5, 9.99904f),
            new(17.99948f, 9.99904f),
            new(17.99948f, 9.99904f), new(17.99948f, 9.99904f), new(17.99948f, 9.99904f),
            new(17.99948f, 9.99904f),
            new(17.99948f, 10.51382f), new(17.74035f, 11.0286f), new(17.2221f, 11.3135f),
            new(7.22259f, 16.8105f),
            new(6.22292f, 17.36f), new(5, 16.6367f), new(5, 15.496f)
        };

        private CompositionColorBrush _brush;
        private ShapeVisual _visual;

        private CompositionPathGeometry _top;
        private CompositionPathGeometry _bottom;

        private PathKeyFrameAnimation _pauseTop;
        private PathKeyFrameAnimation _pauseBottom;
        private ScalarKeyFrameAnimation _pauseRotation;

        private PathKeyFrameAnimation _playTop;
        private PathKeyFrameAnimation _playBottom;
        private ScalarKeyFrameAnimation _playRotation;

        public PlaybackPlayButton()
        {
            DefaultStyleKey = typeof(PlaybackPlayButton);
            RegisterPropertyChangedCallback(ForegroundProperty, OnForegroundChanged);
            RegisterPropertyChangedCallback(FontSizeProperty, OnFontSizeChanged);
        }

        protected override void OnApplyTemplate()
        {
            var target = GetTemplateChild("Target") as UIElement;
            var compositor = BootStrapper.Current.Compositor;

            var pauseTop = CreatePath(PauseTop);
            var pauseBottom = CreatePath(PauseBottom);
            var playTop = CreatePath(PlayTop);
            var playBottom = CreatePath(PlayBottom);

            _top = compositor.CreatePathGeometry(IsPlaying ? pauseTop : playTop);
            _bottom = compositor.CreatePathGeometry(IsPlaying ? pauseBottom : playBottom);

            _brush = compositor.CreateColorBrush(Colors.Black);

            var top = compositor.CreateSpriteShape(_top);
            top.FillBrush = _brush;

            var bottom = compositor.CreateSpriteShape(_bottom);
            bottom.FillBrush = _brush;

            // The shapes are in the icon's 20px box, scaled to FontSize: it is the icon's size, as it
            // is the glyph's for every other GlyphButton.
            var viewBox = compositor.CreateViewBox();
            viewBox.Size = new Vector2(20, 20);

            _visual = compositor.CreateShapeVisual();
            _visual.ViewBox = viewBox;
            _visual.RotationAngleInDegrees = IsPlaying ? PauseAngle : 0;
            _visual.Shapes.Add(top);
            _visual.Shapes.Add(bottom);

            _pauseTop = CreateMorph(compositor, playTop, pauseTop);
            _pauseBottom = CreateMorph(compositor, playBottom, pauseBottom);
            _pauseRotation = CreateRotation(compositor, 0, PauseAngle);

            _playTop = CreateMorph(compositor, pauseTop, playTop);
            _playBottom = CreateMorph(compositor, pauseBottom, playBottom);
            _playRotation = CreateRotation(compositor, PauseAngle, 0);

            ApplyForeground();
            ApplyFontSize();

            ElementCompositionPreview.SetElementChildVisual(target, _visual);
        }

        private void Morph(bool playing)
        {
            if (_visual == null)
            {
                return;
            }

            _top.StartAnimation("Path", playing ? _pauseTop : _playTop);
            _bottom.StartAnimation("Path", playing ? _pauseBottom : _playBottom);
            _visual.StartAnimation("RotationAngleInDegrees", playing ? _pauseRotation : _playRotation);
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
                var size = (float)FontSize;

                _visual.Size = new Vector2(size);
                _visual.CenterPoint = new Vector3(size / 2, size / 2, 0);
            }
        }

        private void ApplyForeground()
        {
            if (_visual != null && Foreground is SolidColorBrush solid)
            {
                // The halves of the triangle overlap, so a translucent fill would darken where
                // they do: the fill is opaque and the alpha goes on the visual instead.
                var color = solid.Color;

                _brush.Color = Color.FromArgb(255, color.R, color.G, color.B);
                _visual.Opacity = color.A / 255f;
            }
        }

        private static PathKeyFrameAnimation CreateMorph(Compositor compositor, CompositionPath from, CompositionPath to)
        {
            var animation = compositor.CreatePathKeyFrameAnimation();
            animation.InsertKeyFrame(0, from);
            animation.InsertKeyFrame(1, to);

            return animation;
        }

        private static ScalarKeyFrameAnimation CreateRotation(Compositor compositor, float from, float to)
        {
            var animation = compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, from);
            animation.InsertKeyFrame(1, to);

            return animation;
        }

        private static CompositionPath CreatePath(Vector2[] points)
        {
            using var builder = new CanvasPathBuilder(null);

            builder.BeginFigure(points[0]);

            for (int i = 0; i < 4; i++)
            {
                builder.AddCubicBezier(points[1 + i * 4], points[2 + i * 4], points[3 + i * 4]);

                if (i < 3)
                {
                    builder.AddLine(points[4 + i * 4]);
                }
            }

            builder.EndFigure(CanvasFigureLoop.Closed);

            return new CompositionPath(CanvasGeometry.CreatePath(builder));
        }

        #region IsPlaying

        /// <summary>
        /// Whether playback is running, and so whether the button shows pause rather than play.
        /// </summary>
        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register("IsPlaying", typeof(bool), typeof(PlaybackPlayButton), new PropertyMetadata(false, OnIsPlayingChanged));

        private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PlaybackPlayButton)d).Morph((bool)e.NewValue);
        }

        #endregion
    }
}
