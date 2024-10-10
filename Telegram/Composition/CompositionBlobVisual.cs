using System;
using System.Numerics;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Composition
{
    public partial class CompositionBlobVisual
    {
        private readonly ShapeVisual _visual;
        private readonly Visual _smallVisual;

        private readonly CompositionBlobShape _smallBlob;
        private readonly CompositionBlobShape _mediumBlob;
        private readonly CompositionBlobShape _largeBlob;

        private readonly ScalarKeyFrameAnimation _rotate;

        private readonly CompositionVSync _vsync = new(30);

        private readonly float _maxLevel;

        private float _presentationAudioLevel;
        private float _audioLevel;

        private bool _animating;

        public CompositionBlobVisual(UIElement element, float width, float height, float maxLevel, Visual smallVisual = null)
        {
            _maxLevel = maxLevel;

            var compositor = BootStrapper.Current.Compositor;
            var owner = ElementCompositionPreview.GetElementVisual(element);

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

            if (smallVisual != null)
            {
                _smallVisual = smallVisual;
                _smallVisual.CenterPoint = new Vector3(width / 2, height / 2, 0);
                _smallVisual.Scale = new Vector3(0.45f);
            }

            _smallBlob = new CompositionBlobShape(smallShape, size, 8, 0.1f, 0.5f, 0.2f, 0.6f, 0.45f, 0.55f);
            _mediumBlob = new CompositionBlobShape(mediumShape, size, 8, 1, 1, 0.9f, 4, 0.55f, 0.87f);
            _largeBlob = new CompositionBlobShape(largeShape, size, 8, 1, 1, 0.9f, 4, 0.57f, 1.0f);

            ElementCompositionPreview.SetElementChildVisual(element, _visual);

            //var linear = visual.Compositor.CreateLinearEasingFunction();
            //var rotate = visual.Compositor.CreateScalarKeyFrameAnimation();
            //rotate.InsertKeyFrame(0, 0, linear);
            //rotate.InsertKeyFrame(1, 360, linear);
            //rotate.IterationBehavior = AnimationIterationBehavior.Forever;
            //rotate.Duration = TimeSpan.FromSeconds(24);

            //visual.StartAnimation("RotationAngleInDegrees", _anim = rotate);
        }

        private void OnRendering(object sender, object e)
        {
            _presentationAudioLevel = _presentationAudioLevel * 0.9f + _audioLevel * 0.1f;

            _smallBlob.Level = _presentationAudioLevel;
            _mediumBlob.Level = _presentationAudioLevel;
            _largeBlob.Level = _presentationAudioLevel;

            SmallLevel = _presentationAudioLevel;
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

        public void UpdateLevel(float level)
        {
            UpdateLevel(level, immediately: false);
        }

        public void UpdateLevel(float level, bool immediately = false)
        {
            var normalizedLevel = MathF.Min(1, MathF.Max(level / _maxLevel, 0));

            _smallBlob.UpdateSpeedLevel(normalizedLevel);
            _mediumBlob.UpdateSpeedLevel(normalizedLevel);
            _largeBlob.UpdateSpeedLevel(normalizedLevel);

            _audioLevel = normalizedLevel;

            if (immediately)
            {
                _presentationAudioLevel = normalizedLevel;
            }
        }

        public void StartAnimating()
        {
            StartAnimating(false);
        }

        public void StartAnimating(bool immediately = false)
        {
            if (_animating)
            {
                return;
            }

            _animating = true;

            if (!immediately)
            {
                var animation = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                animation.InsertKeyFrame(0, new Vector3(0));
                animation.InsertKeyFrame(1, new Vector3(1));

                _visual.StartAnimation("Scale", animation);
            }
            else
            {
                _visual.Scale = Vector3.One;
            }

            UpdateBlobsState();
            _vsync.Rendering += OnRendering;
        }

        public void StopAnimating()
        {
            StopAnimating(duration: 0.15);
        }

        public void StopAnimating(double duration)
        {
            if (!_animating)
            {
                return;
            }

            _animating = false;

            var animation = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector3(1));
            animation.InsertKeyFrame(1, new Vector3(0));

            _visual.CenterPoint = new Vector3(_visual.Size / 2, 0);
            _visual.StartAnimation("Scale", animation);

            UpdateBlobsState();
            _vsync.Rendering -= OnRendering;
        }

        private void UpdateBlobsState()
        {
            if (_animating)
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

        public void Clear()
        {
            _mediumBlob.Clear();
            _largeBlob.Clear();
        }

        private float _smallLevel;
        public float SmallLevel
        {
            get => _smallLevel;
            set
            {
                if (_smallVisual != null && MathF.Abs(value - _smallLevel) > 0.01)
                {
                    var lv = 0.45f + (0.55f - 0.45f) * value;
                    var animation = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                    animation.InsertKeyFrame(1, new Vector3(lv));
                    _smallVisual.StartAnimation("Scale", animation);
                }

                _smallLevel = value;
            }
        }
    }

    public partial class CompositionBlobShape
    {
        private readonly int _pointsCount;
        private readonly float _smoothness;

        private readonly float _minRandomness;
        private readonly float _maxRandomness;

        private readonly float _minSpeed;
        private readonly float _maxSpeed;

        private readonly float _minScale;
        private readonly float _maxScale;

        private readonly bool _isCircle;

        private float _level;
        public float Level
        {
            get => _level;
            set
            {
                if (MathF.Abs(value - _level) > 0.01)
                {
                    var lv = _minScale + (_maxScale - _minScale) * value;
                    var animation = BootStrapper.Current.Compositor.CreateVector2KeyFrameAnimation();
                    animation.InsertKeyFrame(1, new Vector2(lv));
                    _shape.StartAnimation("Scale", animation);
                }

                _level = value;
            }
        }

        private float _speedLevel = 0;
        private readonly float _scaleLevel = 0;

        private float _lastSpeedLevel = 0;
        private readonly float _lastScaleLevel = 0;

        private readonly CompositionSpriteShape _shape;
        private readonly CompositionPathGeometry _shapeLayer;

        private readonly Random _random = new();

        public CompositionBlobShape(CompositionSpriteShape shape, Vector2 size, int pointsCount, float minRandomness, float maxRandomness, float minSpeed, float maxSpeed, float minScale, float maxScale)
        {
            _shape = shape;
            _shapeLayer = shape.Geometry as CompositionPathGeometry;
            _size = size;
            _isCircle = _shapeLayer == null;

            _pointsCount = pointsCount;
            _minRandomness = minRandomness;
            _maxRandomness = maxRandomness;
            _minSpeed = minSpeed;
            _maxSpeed = maxSpeed;
            _minScale = minScale;
            _maxScale = maxScale;

            var angle = (MathF.PI * 2) / (float)pointsCount;
            _smoothness = ((4 / 3) * MathF.Tan(angle / 4)) / MathF.Sin(angle / 2) / 2;
            _shape.Scale = new Vector2(minScale);
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

        private Vector2 _size;
        public Vector2 Size
        {
            get => _size;
            set => _size = value;
        }

        public void UpdateSpeedLevel(float newSpeedLevel)
        {
            _speedLevel = MathF.Max(_speedLevel, newSpeedLevel);
        }

        private bool _animating;

        public void StartAnimating()
        {
            _animating = true;
            AnimateToNewShape();
        }

        public void StopAnimating()
        {
            _animating = false;
            _shapeLayer?.StopAnimation("Path");
        }

        public void Clear()
        {
            _shape.Scale = Vector2.Zero;
        }

        private void AnimateToNewShape()
        {
            if (_isCircle || !_animating)
            {
                return;
            }

            var compositor = _shapeLayer.Compositor;

            if (_shapeLayer.Path == null)
            {
                var points = GenerateNextBlob(_size);
                _shapeLayer.Path = compositor.CreateSmoothCurve(points, _smoothness);
            }

            var nextPoints = GenerateNextBlob(_size);
            var nextPath = compositor.CreateSmoothCurve(nextPoints, _smoothness);

            //var linear = compositor.CreateLinearEasingFunction();

            //var rotate = compositor.CreateScalarKeyFrameAnimation();
            //rotate.InsertKeyFrame(0, shape.RotationAngle, linear);
            //rotate.InsertKeyFrame(1, shape.RotationAngle + (MathF.PI * 2) / (float)pointsCount, linear);
            //rotate.Duration = TimeSpan.FromSeconds(1 / (minSpeed + (maxSpeed - minSpeed) * speedLevel));

            //shape.StartAnimation("RotationAngle", rotate);

            var animation = compositor.CreatePathKeyFrameAnimation();
            animation.InsertKeyFrame(0, _shapeLayer.Path);
            animation.InsertKeyFrame(1, nextPath);
            animation.Duration = TimeSpan.FromSeconds(1 / (_minSpeed + (_maxSpeed - _minSpeed) * _speedLevel));

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                AnimateToNewShape();
            };

            _shapeLayer.StartAnimation("Path", animation);
            batch.End();

            _lastSpeedLevel = _speedLevel;
            _speedLevel = 0;
        }

        private Vector2[] GenerateNextBlob(Vector2 size)
        {
            var randomness = _minRandomness + (_maxRandomness - _minRandomness) * _speedLevel;
            var blob = Blob(_pointsCount, randomness);
            var points = new Vector2[_pointsCount];

            for (int i = 0; i < _pointsCount; i++)
            {
                points[i] = new Vector2(blob[i].X * size.X, blob[i].Y * size.Y);
            }

            return points;
        }

        private Vector2[] Blob(int pointsCount, float randomness)
        {
            var angle = (MathF.PI * 2) / (float)pointsCount;

            float rgen()
            {
                var accuracy = 1000;
                var random = _random.Next(accuracy);
                return (float)random / (float)accuracy;
            }

            var rangeStart = 1 / (1 + randomness / 10);

            var startAngle = angle * (float)_random.Next(45) / 90f;
            var points = new Vector2[pointsCount];

            for (int i = 0; i < pointsCount; i++)
            {
                var randPointOffset = (rangeStart + rgen() * (1 - rangeStart)) / 2;
                var angleRandomness = angle * 0.1f;
                var randAngle = angle + angle * ((angleRandomness * (float)_random.Next(45) / 90f) - angleRandomness * 0.5f);
                var pointX = MathF.Sin(startAngle + (float)i * randAngle);
                var pointY = MathF.Cos(startAngle + (float)i * randAngle);
                points[i] = new Vector2(
                    x: pointX * randPointOffset,
                    y: pointY * randPointOffset
                );
            }

            return points;
        }
    }
}
