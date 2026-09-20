//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Composition
{
    public partial class CompositionCurveVisual : CompositionLevelVisual
    {
        private readonly ContainerVisual _visual;

        private readonly CompositionCurveShape _smallCurve;
        private readonly CompositionCurveShape _mediumCurve;
        private readonly CompositionCurveShape _largeCurve;

        private readonly CompositionLinearGradientBrush _gradient;

        public CompositionCurveVisual(UIElement element, float width, float height, float maxLevel)
            : base(maxLevel)
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

            _smallCurve = new CompositionCurveShape(smallShape, size, 8, 1, 1.3f, 0.9f, 3.2f, 0, 0);
            _mediumCurve = new CompositionCurveShape(mediumShape, size, 8, 1.2f, 1.5f, 1, 4.4f, 0.1f, 0.55f);
            _largeCurve = new CompositionCurveShape(largeShape, size, 8, 1, 1.7f, 1, 5.8f, 0.1f, 1.0f);

            _gradient = compositor.CreateLinearGradientBrush();
            _gradient.ColorStops.Add(compositor.CreateColorGradientStop(0, Colors.Red));
            _gradient.ColorStops.Add(compositor.CreateColorGradientStop(1, Colors.Blue));

            _smallCurve.FillBrush = _gradient;
            _mediumCurve.FillBrush = _gradient;
            _largeCurve.FillBrush = _gradient;

            _visual = compositor.CreateContainerVisual();
            _visual.Size = new Vector2(width, height);
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

        public void SetColorStops(params uint[] colorStops)
        {
            _gradient.ColorStops.Clear();

            for (int i = 0; i < colorStops.Length; i++)
            {
                _gradient.ColorStops.Add(_gradient.Compositor.CreateColorGradientStop(i / (colorStops.Length - 1f), ColorEx.FromHex(colorStops[i])));
            }
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
            }
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
        }
    }

    public partial class CompositionCurveShape : CompositionMorphShape
    {
        private readonly float _smoothness;

        private readonly float _minRandomness;
        private readonly float _maxRandomness;

        private readonly float _minOffset;
        private readonly float _maxOffset;

        private readonly ScalarKeyFrameAnimation _levelAnimation;

        // The curve is built before the header knows how wide it is, so the first path it gets is
        // degenerate. Keeping the width the last path was built at is what forces a rebuild once a
        // real one arrives - Path is not null by then, so it cannot be the test.
        private float _lastWidth;

        public CompositionCurveShape(CompositionSpriteShape shape, Vector2 size, int pointsCount, float minRandomness, float maxRandomness, float minSpeed, float maxSpeed, float minOffset, float maxOffset)
            : base(shape, size, pointsCount, minSpeed, maxSpeed)
        {
            _minRandomness = minRandomness;
            _maxRandomness = maxRandomness;
            _minOffset = minOffset;
            _maxOffset = maxOffset;

            _smoothness = 0.35f;

            _levelAnimation = shape.Compositor.CreateScalarKeyFrameAnimation();
        }

        public void SetLevel(float level)
        {
            var lv = _minOffset + (_maxOffset - _minOffset) * level;

            _levelAnimation.InsertKeyFrame(1, lv * 12.0f);
            _shape.StartAnimation("Offset.Y", _levelAnimation);
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

        protected override bool ShouldResetPath => _shapeLayer.Path == null || _lastWidth == 0;

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

            var pointsCount = _points.Length;
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

                _points[i] = new Vector2((pointX + randomXDelta) * Size.X, 40 + pointY * 12);
            }
        }
    }
}
