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
    public partial class CompositionBlobVisual : CompositionLevelVisual
    {
        private readonly ShapeVisual _visual;
        private readonly Visual _smallVisual;

        private readonly CompositionBlobShape _smallBlob;
        private readonly CompositionBlobShape _mediumBlob;
        private readonly CompositionBlobShape _largeBlob;

        // Two templates rather than one: a key frame can be replaced but never removed, and the
        // show/hide animation needs a frame at 0 that the small visual must not have.
        private readonly Vector3KeyFrameAnimation _visualAnimation;
        private readonly Vector3KeyFrameAnimation _smallAnimation;

        public CompositionBlobVisual(UIElement element, float width, float height, float maxLevel, Visual smallVisual = null)
            : base(maxLevel)
        {
            var owner = ElementComposition.GetElementVisual(element);
            var compositor = owner.Compositor;

            var size = new Vector2(width, height);
            var halfSize = size / 2;

            owner.CenterPoint = new Vector3(halfSize, 0);

            var small = compositor.CreateEllipseGeometry();
            var medium = compositor.CreatePathGeometry();
            var large = compositor.CreatePathGeometry();

            small.Radius = halfSize;

            var smallShape = compositor.CreateSpriteShape(small);
            smallShape.Offset = halfSize;

            var mediumShape = compositor.CreateSpriteShape(medium);
            mediumShape.Offset = halfSize;

            var largeShape = compositor.CreateSpriteShape(large);
            largeShape.Offset = halfSize;

            _visual = compositor.CreateShapeVisual();
            _visual.Size = size;
            _visual.CenterPoint = new Vector3(halfSize, 0);

            _visual.Shapes.Add(mediumShape);
            _visual.Shapes.Add(largeShape);
            _visual.Shapes.Add(smallShape);

            _visualAnimation = compositor.CreateVector3KeyFrameAnimation();

            if (smallVisual != null)
            {
                _smallVisual = smallVisual;
                _smallVisual.CenterPoint = new Vector3(width / 2, height / 2, 0);
                _smallVisual.Scale = new Vector3(0.45f);

                _smallAnimation = compositor.CreateVector3KeyFrameAnimation();
            }

            _smallBlob = new CompositionBlobShape(smallShape, size, 8, 0.1f, 0.5f, 0.2f, 0.6f, 0.45f, 0.55f);
            _mediumBlob = new CompositionBlobShape(mediumShape, size, 8, 1, 1, 0.9f, 4, 0.55f, 0.87f);
            _largeBlob = new CompositionBlobShape(largeShape, size, 8, 1, 1, 0.9f, 4, 0.57f, 1.0f);

            ElementCompositionPreview.SetElementChildVisual(element, _visual);
        }

        protected override void UpdateSpeedLevel(float level)
        {
            _smallBlob.UpdateSpeedLevel(level);
            _mediumBlob.UpdateSpeedLevel(level);
            _largeBlob.UpdateSpeedLevel(level);
        }

        protected override void OnLevelChanged(float level)
        {
            _smallBlob.SetLevel(level);
            _mediumBlob.SetLevel(level);
            _largeBlob.SetLevel(level);

            if (_smallVisual != null)
            {
                _smallAnimation.InsertKeyFrame(1, new Vector3(0.45f + (0.55f - 0.45f) * level));
                _smallVisual.StartAnimation("Scale", _smallAnimation);
            }
        }

        private Color _fillColor;
        public Color FillColor
        {
            get => _fillColor;
            set
            {
                if (_fillColor != value)
                {
                    _fillColor = value;

                    _smallBlob.FillColor = Color.FromArgb(0xFF, value.R, value.G, value.B);
                    _mediumBlob.FillColor = Color.FromArgb(0x44, value.R, value.G, value.B); // 0x4D 0.3 alpha
                    _largeBlob.FillColor = Color.FromArgb(0x44, value.R, value.G, value.B); // 0x26 0.15 alpha
                }
            }
        }

        public Vector2 ActualSize
        {
            get => _visual.Size;
            set
            {
                _visual.Size = value;
                _smallBlob.Size = value;
                _mediumBlob.Size = value;
                _largeBlob.Size = value;
            }
        }

        protected override void OnStartAnimating(bool immediately)
        {
            if (!immediately)
            {
                _visualAnimation.InsertKeyFrame(0, new Vector3(0));
                _visualAnimation.InsertKeyFrame(1, new Vector3(1));

                _visual.StartAnimation("Scale", _visualAnimation);
            }
            else
            {
                _visual.Scale = Vector3.One;
            }
        }

        protected override void OnStopAnimating(double duration)
        {
            _visualAnimation.InsertKeyFrame(0, new Vector3(1));
            _visualAnimation.InsertKeyFrame(1, new Vector3(0));

            _visual.CenterPoint = new Vector3(_visual.Size / 2, 0);
            _visual.StartAnimation("Scale", _visualAnimation);
        }

        protected override void UpdateShapesState(bool animating)
        {
            if (animating)
            {
                _smallBlob.StartAnimating();
                _mediumBlob.StartAnimating();
                _largeBlob.StartAnimating();
            }
            else
            {
                _smallBlob.StopAnimating();
                _mediumBlob.StopAnimating();
                _largeBlob.StopAnimating();
            }
        }

        public override void Clear()
        {
            _mediumBlob.Clear();
            _largeBlob.Clear();
        }
    }

    public partial class CompositionBlobShape : CompositionMorphShape
    {
        private readonly float _smoothness;

        private readonly float _minRandomness;
        private readonly float _maxRandomness;

        private readonly float _minScale;
        private readonly float _maxScale;

        private readonly Vector2KeyFrameAnimation _levelAnimation;

        public CompositionBlobShape(CompositionSpriteShape shape, Vector2 size, int pointsCount, float minRandomness, float maxRandomness, float minSpeed, float maxSpeed, float minScale, float maxScale)
            : base(shape, size, pointsCount, minSpeed, maxSpeed)
        {
            _minRandomness = minRandomness;
            _maxRandomness = maxRandomness;
            _minScale = minScale;
            _maxScale = maxScale;

            var angle = (MathF.PI * 2) / (float)pointsCount;
            _smoothness = ((4f / 3f) * MathF.Tan(angle / 4)) / MathF.Sin(angle / 2) / 2;

            _levelAnimation = shape.Compositor.CreateVector2KeyFrameAnimation();

            _shape.Scale = new Vector2(minScale);
        }

        public void SetLevel(float level)
        {
            var lv = _minScale + (_maxScale - _minScale) * level;

            _levelAnimation.InsertKeyFrame(1, new Vector2(lv));
            _shape.StartAnimation("Scale", _levelAnimation);
        }

        private Color _fillColor;
        public Color FillColor
        {
            get => _fillColor;
            set
            {
                if (_fillColor != value)
                {
                    _fillColor = value;
                    _shape.FillBrush = _shape.Compositor.CreateColorBrush(value);
                }
            }
        }

        protected override CompositionPath CreateNextPath()
        {
            GenerateNextBlob();
            return _shapeLayer.Compositor.CreateSmoothCurve(_points, _smoothness);
        }

        private void GenerateNextBlob()
        {
            var randomness = _minRandomness + (_maxRandomness - _minRandomness) * _speedLevel;

            var pointsCount = _points.Length;
            var angle = (MathF.PI * 2) / (float)pointsCount;
            var rangeStart = 1 / (1 + randomness / 10);

            var startAngle = angle * (float)_random.Next(45) / 90f;

            for (int i = 0; i < pointsCount; i++)
            {
                var randPointOffset = (rangeStart + NextRandom() * (1 - rangeStart)) / 2;
                var angleRandomness = angle * 0.1f;
                var randAngle = angle + angle * ((angleRandomness * (float)_random.Next(45) / 90f) - angleRandomness * 0.5f);
                var pointX = MathF.Sin(startAngle + (float)i * randAngle);
                var pointY = MathF.Cos(startAngle + (float)i * randAngle);

                _points[i] = new Vector2(
                    x: pointX * randPointOffset * Size.X,
                    y: pointY * randPointOffset * Size.Y
                );
            }
        }
    }
}
