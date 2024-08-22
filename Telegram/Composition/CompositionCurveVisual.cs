﻿using System;
using System.Numerics;
using Windows.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Telegram.Composition
{
    public class CompositionCurveVisual
    {
        private readonly ShapeVisual _visual;

        private readonly CompositionCurveShape _smallCurve;
        private readonly CompositionCurveShape _mediumCurve;
        private readonly CompositionCurveShape _largeCurve;

        private readonly CompositionVSync _vsync = new(30);

        private readonly float _maxLevel;

        private float _presentationAudioLevel;
        private float _audioLevel;

        private bool _animating;

        public CompositionCurveVisual(UIElement element, float width, float height, float maxLevel)
        {
            _maxLevel = maxLevel;

            var compositor = Window.Current.Compositor;

            var size = new Vector2(width, height);

            var small = compositor.CreatePathGeometry();
            var medium = compositor.CreatePathGeometry();
            var large = compositor.CreatePathGeometry();

            var smallShape = compositor.CreateSpriteShape(small);
            var mediumShape = compositor.CreateSpriteShape(medium);
            var largeShape = compositor.CreateSpriteShape(large);

            _visual = compositor.CreateShapeVisual();
            _visual.Size = size;

            _visual.Shapes.Add(mediumShape);
            _visual.Shapes.Add(largeShape);
            _visual.Shapes.Add(smallShape);

            _smallCurve = new CompositionCurveShape(smallShape, size, 8, 1, 1.3f, 0.9f, 3.2f, 0, 0);
            _mediumCurve = new CompositionCurveShape(mediumShape, size, 8, 1.2f, 1.5f, 1, 4.4f, 0.1f, 0.55f);
            _largeCurve = new CompositionCurveShape(largeShape, size, 8, 1, 1.7f, 1, 5.8f, 0.1f, 1.0f);

            ElementCompositionPreview.SetElementChildVisual(element, _visual);
        }

        private void OnRendering(object sender, object e)
        {
            _presentationAudioLevel = _presentationAudioLevel * 0.9f + _audioLevel * 0.1f;

            _smallCurve.Level = _presentationAudioLevel;
            _mediumCurve.Level = _presentationAudioLevel;
            _largeCurve.Level = _presentationAudioLevel;
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

                    _smallCurve.FillColor = Color.FromArgb(0xFF, value.R, value.G, value.B);
                    _mediumCurve.FillColor = Color.FromArgb(0x8C, value.R, value.G, value.B); // 0.55 alpha
                    _largeCurve.FillColor = Color.FromArgb(0x59, value.R, value.G, value.B); // 0.35 alpha
                }
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
            }
        }

        public void UpdateLevel(float level)
        {
            UpdateLevel(level, immediately: false);
        }

        public void UpdateLevel(float level, bool immediately = false)
        {
            var normalizedLevel = MathF.Min(1, MathF.Max(level / _maxLevel, 0));

            _smallCurve.UpdateSpeedLevel(normalizedLevel);
            _mediumCurve.UpdateSpeedLevel(normalizedLevel);
            _largeCurve.UpdateSpeedLevel(normalizedLevel);

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

            //if (!immediately)
            //{
            //    _mediumBlob.layer.animateScale(from: 0.75, to: 1, duration: 0.35, removeOnCompletion: false);
            //    _largeBlob.layer.animateScale(from: 0.75, to: 1, duration: 0.35, removeOnCompletion: false);
            //}
            //else
            //{
            //    _mediumBlob.layer.removeAllAnimations();
            //    _largeBlob.layer.removeAllAnimations();
            //}

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

            //_mediumBlob.layer.animateScale(from: 1.0, to: 0.75, duration: duration, removeOnCompletion: false);
            //_largeBlob.layer.animateScale(from: 1.0, to: 0.75, duration: duration, removeOnCompletion: false);

            UpdateBlobsState();
            _vsync.Rendering -= OnRendering;
        }

        private void UpdateBlobsState()
        {
            if (_animating)
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

        public void Clear()
        {
            _smallCurve.Clear();
            _mediumCurve.Clear();
            _largeCurve.Clear();
        }
    }

    public class CompositionCurveShape
    {
        private readonly int _pointsCount;
        private readonly float _smoothness;

        private readonly float _minRandomness;
        private readonly float _maxRandomness;

        private readonly float _minSpeed;
        private readonly float _maxSpeed;

        private readonly float _minOffset;
        private readonly float _maxOffset;

        private float _level;
        public float Level
        {
            get => _level;
            set
            {
                if (MathF.Abs(value - _level) > 0.01)
                {
                    var lv = _minOffset + (_maxOffset - _minOffset) * value;
                    var animation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
                    animation.InsertKeyFrame(1, -14 + lv * 16.0f);
                    _shape.StartAnimation("Offset.Y", animation);
                }

                _level = value;
            }
        }

        private float _speedLevel = 0;
        private float _lastSpeedLevel = 0;

        private float _lastWidth;

        private readonly CompositionSpriteShape _shape;
        private readonly CompositionPathGeometry _shapeLayer;

        private readonly Random _random = new();

        public CompositionCurveShape(CompositionSpriteShape shape, Vector2 size, int pointsCount, float minRandomness, float maxRandomness, float minSpeed, float maxSpeed, float minOffset, float maxOffset)
        {
            _shape = shape;
            _shapeLayer = shape.Geometry as CompositionPathGeometry;
            _size = size;

            _pointsCount = pointsCount;
            _minRandomness = minRandomness;
            _maxRandomness = maxRandomness;
            _minSpeed = minSpeed;
            _maxSpeed = maxSpeed;
            _minOffset = minOffset;
            _maxOffset = maxOffset;

            _smoothness = 0.35f;

            _shape.Offset = new Vector2(0, -14);
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
            if (!_animating)
            {
                return;
            }

            var compositor = _shapeLayer.Compositor;

            if (_shapeLayer.Path == null || _lastWidth == 0)
            {
                var points = GenerateNextCurve(_size);
                _shapeLayer.Path = compositor.CreateSmoothCurve(points, _size.X, _smoothness, true);
            }

            var nextPoints = GenerateNextCurve(_size);
            var nextPath = compositor.CreateSmoothCurve(nextPoints, _size.X, _smoothness, true);

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

            _lastWidth = _size.X;

            _lastSpeedLevel = _speedLevel;
            _speedLevel = 0;
        }

        private Vector2[] GenerateNextCurve(Vector2 size)
        {
            var randomness = _minRandomness + (_maxRandomness - _minRandomness) * _speedLevel;
            var curve = Curve(_pointsCount, randomness);
            var points = new Vector2[_pointsCount];

            for (int i = 0; i < _pointsCount; i++)
            {
                points[i] = new Vector2(curve[i].X * size.X, 18 + curve[i].Y * 12);
            }

            return points;
        }

        private Vector2[] Curve(int pointsCount, float randomness)
        {
            var segment = 1.0f / (float)(pointsCount - 1);

            float rgen()
            {

                var accuracy = 1000;
                var random = _random.Next(accuracy);
                return (float)random / (float)accuracy;
            }

            var rangeStart = 1.0f / (1.0f + randomness / 10.0f);

            var points = new Vector2[pointsCount];

            for (int i = 0; i < pointsCount; i++)
            {
                var randPointOffset = (rangeStart + rgen() * (1 - rangeStart)) / 2;
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

                points[i] = new Vector2(pointX + randomXDelta, pointY);
            }

            return points;
        }
    }
}
