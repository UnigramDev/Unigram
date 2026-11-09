//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Composition
{
    public partial class CompositionCurveVisual : CompositionLevelVisual
    {
        internal const float BarHeight = 40;

        // How far each layer rests below the one in front. The back layer rests at twice this, so
        // past 8 it would rest lower than where a voice takes it.
        private const float LayerGap = 4;

        private const double ColorTransitionDuration = 0.3;

        private readonly ContainerVisual _visual;
        private readonly SpriteVisual _solid;

        private readonly CompositionCurveShape _smallCurve;
        private readonly CompositionCurveShape _mediumCurve;
        private readonly CompositionCurveShape _largeCurve;

        private readonly CompositionColorGradientStop _startStop;
        private readonly CompositionColorGradientStop _endStop;

        // One template for both stops: StartAnimation instantiates it, so re-inserting the key
        // frame for the second stop leaves the first one's running instance alone.
        private readonly ColorKeyFrameAnimation _colorAnimation;
        private readonly CompositionEasingFunction _colorEasing;

        private Color _startColor;
        private Color _endColor;

        public CompositionCurveVisual(UIElement element, float width, float height, float maxLevel)
            : base(maxLevel, decay: 0.93f, decayRate: 60f)
        {
            var compositor = ElementComposition.GetElementVisual(element).Compositor;

            var size = new Vector2(width, height);

            var small = compositor.CreatePathGeometry();
            var medium = compositor.CreatePathGeometry();
            var large = compositor.CreatePathGeometry();

            var smallShape = compositor.CreateSpriteShape(small);
            var mediumShape = compositor.CreateSpriteShape(medium);
            var largeShape = compositor.CreateSpriteShape(large);

            var smallVisual = compositor.CreateShapeVisual();
            smallVisual.Size = size;

            var mediumVisual = compositor.CreateShapeVisual();
            mediumVisual.Size = size;
            mediumVisual.Opacity = 0.55f;

            var largeVisual = compositor.CreateShapeVisual();
            largeVisual.Size = size;
            largeVisual.Opacity = 0.35f;

            smallVisual.Shapes.Add(smallShape);
            mediumVisual.Shapes.Add(mediumShape);
            largeVisual.Shapes.Add(largeShape);

            _smallCurve = new CompositionCurveShape(smallShape, size, 1, 1.3f, 0.9f, 3.2f, 0, 0);
            _mediumCurve = new CompositionCurveShape(mediumShape, size, 1.2f, 1.5f, 1, 4.4f, LayerGap, 8.8f);
            _largeCurve = new CompositionCurveShape(largeShape, size, 1.2f, 1.7f, 1, 5.8f, LayerGap * 2, 16);

            _startStop = compositor.CreateColorGradientStop(0, default);
            _endStop = compositor.CreateColorGradientStop(1, default);

            _colorEasing = compositor.CreateLinearEasingFunction();
            _colorAnimation = compositor.CreateColorKeyFrameAnimation();
            _colorAnimation.InterpolationColorSpace = CompositionColorSpace.Rgb;
            _colorAnimation.Duration = TimeSpan.FromSeconds(ColorTransitionDuration);

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.ColorStops.Add(_startStop);
            gradient.ColorStops.Add(_endStop);

            // The gradient runs over twice the width of the bar, so only its first half is visible.
            gradient.EndPoint = new Vector2(2, 0);

            _smallCurve.FillBrush = gradient;
            _mediumCurve.FillBrush = gradient;
            _largeCurve.FillBrush = gradient;

            _solid = compositor.CreateSpriteVisual();
            _solid.Brush = gradient;
            _solid.IsVisible = false;

            _visual = compositor.CreateContainerVisual();
            _visual.Size = new Vector2(width, height);
            _visual.Children.InsertAtTop(_solid);
            _visual.Children.InsertAtTop(smallVisual);
            _visual.Children.InsertAtTop(mediumVisual);
            _visual.Children.InsertAtTop(largeVisual);

            ElementCompositionPreview.SetElementChildVisual(element, _visual);
        }

        protected override void UpdateSpeedLevel(float level)
        {
            _smallCurve.UpdateSpeedLevel(level);
            _mediumCurve.UpdateSpeedLevel(level);
            _largeCurve.UpdateSpeedLevel(level);
        }

        protected override void OnLevelChanged(float level)
        {
            _smallCurve.SetLevel(level);
            _mediumCurve.SetLevel(level);
            _largeCurve.SetLevel(level);
        }

        public void SetColorStops(Color start, Color end, bool animated)
        {
            if (start == _startColor && end == _endColor)
            {
                return;
            }

            _startColor = start;
            _endColor = end;

            SetStopColor(_startStop, start, animated);
            SetStopColor(_endStop, end, animated);
        }

        private void SetStopColor(CompositionColorGradientStop stop, Color color, bool animated)
        {
            if (animated)
            {
                // No key frame at 0, so an interrupted transition continues from what is on screen.
                _colorAnimation.InsertKeyFrame(1, color, _colorEasing);
                stop.StartAnimation("Color", _colorAnimation);
            }
            else
            {
                // A write does not override a running animation, which would land on its old target.
                stop.StopAnimation("Color");
                stop.Color = color;
            }
        }

        /// <summary>
        /// A still bar in place of the curves, for when animations are off to save energy.
        /// </summary>
        public void ShowSolid()
        {
            StopAnimating();
            Clear();

            _solid.IsVisible = true;
        }

        public Vector2 ActualSize
        {
            get => _visual.Size;
            set
            {
                _visual.Size = value;
                _smallCurve.Size = value;
                _mediumCurve.Size = value;
                _largeCurve.Size = value;

                foreach (var child in _visual.Children)
                {
                    child.Size = value;
                }

                _solid.Size = new Vector2(value.X, BarHeight);

                _smallCurve.RestartIfResized();
                _mediumCurve.RestartIfResized();
                _largeCurve.RestartIfResized();
            }
        }

        protected override void OnStartAnimating(bool immediately)
        {
            _solid.IsVisible = false;
        }

        protected override void UpdateShapesState(bool animating)
        {
            if (animating)
            {
                _smallCurve.StartAnimating();
                _mediumCurve.StartAnimating();
                _largeCurve.StartAnimating();
            }
            else
            {
                _smallCurve.StopAnimating();
                _mediumCurve.StopAnimating();
                _largeCurve.StopAnimating();
            }
        }

        public override void Clear()
        {
            _smallCurve.Clear();
            _mediumCurve.Clear();
            _largeCurve.Clear();

            _solid.IsVisible = false;
        }
    }

    public partial class CompositionCurveShape : CompositionMorphShape
    {
        private const float Amplitude = 20;

        // Spacing and not a count, so that a wide bar gets more waves instead of flatter ones.
        private const float PointSpacing = 160;
        private const int MinPointsCount = 6;

        private const float SpeedMultiplier = 0.85f;

        // Levels arrive every 100ms, so each animation lands as the next one comes in. Any longer
        // and every sample restarts one that has not arrived, which only adds lag to the smoothing.
        private const double LevelInterval = 0.1;

        private readonly float _smoothness;

        private readonly float _minRandomness;
        private readonly float _maxRandomness;

        private readonly float _minOffset;
        private readonly float _maxOffset;

        private readonly ScalarKeyFrameAnimation _levelAnimation;
        private readonly CompositionEasingFunction _levelEasing;

        // A path is built for one width, so the width the last one was built at is what tells a
        // morph to start over from a path that fits.
        private float _lastWidth;

        public CompositionCurveShape(CompositionSpriteShape shape, Vector2 size, float minRandomness, float maxRandomness, float minSpeed, float maxSpeed, float minOffset, float maxOffset)
            : base(shape, size, MinPointsCount, minSpeed * SpeedMultiplier, maxSpeed * SpeedMultiplier)
        {
            _minRandomness = minRandomness;
            _maxRandomness = maxRandomness;
            _minOffset = minOffset;
            _maxOffset = maxOffset;

            _smoothness = 0.5f;

            var compositor = shape.Compositor;

            MorphEasing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.42f, 0), new Vector2(0.58f, 1));

            _levelEasing = compositor.CreateLinearEasingFunction();
            _levelAnimation = compositor.CreateScalarKeyFrameAnimation();
            _levelAnimation.Duration = TimeSpan.FromSeconds(LevelInterval);

            shape.Offset = new Vector2(0, minOffset);
        }

        public void SetLevel(float level)
        {
            if (_maxOffset == 0)
            {
                return;
            }

            var lv = _minOffset + (_maxOffset - _minOffset) * level;

            _levelAnimation.InsertKeyFrame(1, lv, _levelEasing);
            _shape.StartAnimation("Offset.Y", _levelAnimation);
        }

        // The morph in flight was built for the old width, and would leave the end of a wider bar
        // uncovered until it finished.
        public void RestartIfResized()
        {
            if (IsAnimating && _lastWidth != Size.X)
            {
                StopAnimating();
                StartAnimating();
            }
        }

        public CompositionBrush FillBrush
        {
            get => _shape.FillBrush;
            set => _shape.FillBrush = value;
        }

        // Clear() hides a shape by zeroing its scale, and unlike the blobs nothing here ever
        // writes Scale again - the level drives Offset.Y. The header reuses one control across
        // calls, so without this a curve cleared when a call ends never comes back.
        public override void StartAnimating()
        {
            _shape.Scale = Vector2.One;
            base.StartAnimating();
        }

        protected override bool ShouldResetPath => base.ShouldResetPath || _lastWidth != Size.X;

        protected override void OnPathAnimated()
        {
            _lastWidth = Size.X;
        }

        protected override CompositionPath CreateNextPath()
        {
            GenerateNextCurve();
            return _shapeLayer.Compositor.CreateSmoothCurve(_points, Size.X, _smoothness, true);
        }

        private void GenerateNextCurve()
        {
            var randomness = _minRandomness + (_maxRandomness - _minRandomness) * _speedLevel;

            // A morph needs the same segments at both ends. Only a width change alters the count,
            // and one of those always starts over from a new path.
            var pointsCount = Math.Max(MinPointsCount, (int)MathF.Round(Size.X / PointSpacing) + 1);
            if (_points.Length != pointsCount)
            {
                _points = new Vector2[pointsCount];
            }

            var segment = 1.0f / (float)(pointsCount - 1);
            var rangeStart = 1.0f / (1.0f + randomness / 10.0f);

            for (int i = 0; i < pointsCount; i++)
            {
                var randPointOffset = (rangeStart + NextRandom() * (1 - rangeStart)) / 2;
                var segmentRandomness = randomness;

                float pointX;
                float pointY;
                float randomXDelta;

                if (i == 0)
                {
                    pointX = 0.0f;
                    pointY = 0.0f;
                    randomXDelta = 0.0f;
                }
                else if (i == pointsCount - 1)
                {
                    pointX = 1.0f;
                    pointY = 0.0f;
                    randomXDelta = 0.0f;
                }
                else
                {
                    pointX = segment * (float)i;
                    pointY = ((segmentRandomness * (float)_random.Next(100) / 100f) - segmentRandomness * 0.5f) * randPointOffset;
                    randomXDelta = segment - segment * randPointOffset;
                }

                _points[i] = new Vector2((pointX + randomXDelta) * Size.X, CompositionCurveVisual.BarHeight + pointY * Amplitude);
            }
        }
    }
}
