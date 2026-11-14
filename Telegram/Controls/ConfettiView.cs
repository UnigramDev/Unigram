//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Telegram.Navigation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls
{
    public partial class ConfettiView : Control
    {
        private static readonly Color[] _colors = new Color[]
        {
            Color.FromArgb(0xFF, 0x2C, 0xBC, 0xE8),
            Color.FromArgb(0xFF, 0x9E, 0x04, 0xD0),
            Color.FromArgb(0xFF, 0xFE, 0xCB, 0x02),
            Color.FromArgb(0xFF, 0xFD, 0x23, 0x57),
            Color.FromArgb(0xFF, 0x27, 0x8C, 0xFE),
            Color.FromArgb(0xFF, 0x59, 0xB8, 0x6C)
        };

        private const int _fallParticlesCount = 30;
        private const int _particlesCount = 60;

        // Circles are 4-5 px in radius and strips 4-7 px in half-length, so every particle draws
        // from a handful of geometries shared across the view.
        private const int _minTypeSize = 4;
        private const int _maxTypeSize = 7;

        private readonly List<Particle> _particles = new(_particlesCount + _fallParticlesCount);
        private readonly Stack<Particle> _pool = new(_particlesCount + _fallParticlesCount);
        private readonly Random _random = new();

        private ShapeVisual _visual;
        private CompositionColorBrush[] _brushes;
        private CompositionEllipseGeometry[] _circles;
        private CompositionRoundedRectangleGeometry[] _strips;

        private Vector2 _size;
        private int _fallingDownCount;
        private float _speedCoef = 1.0f;
        private bool _started;
        private bool _spawned;
        private bool _startedFall;
        private bool _rendering;
        private long _lastTimestamp;

        public ConfettiView()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public event EventHandler Completed;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_started)
            {
                StartRendering();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRendering();
        }

        public void Start()
        {
            EnsureVisual();

            for (int i = 0; i < _particles.Count; i++)
            {
                Recycle(_particles[i]);
            }

            _particles.Clear();
            _started = true;
            _spawned = false;
            _startedFall = false;
            _fallingDownCount = 0;
            _speedCoef = 1.0f;

            StartRendering();
        }

        private void StartRendering()
        {
            if (_rendering)
            {
                return;
            }

            _rendering = true;
            _lastTimestamp = 0;

            CompositionTarget.Rendering += OnRendering;
        }

        private void StopRendering()
        {
            if (_rendering)
            {
                _rendering = false;
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        private void OnRendering(object sender, object e)
        {
            // Start usually runs right after x:Load, before the first layout pass, and the particles
            // are placed relative to the size, so spawning waits for one.
            if (!_spawned)
            {
                _size = ActualSize;

                if (_size.X <= 0 || _size.Y <= 0)
                {
                    return;
                }

                _spawned = true;

                for (int i = 0; i < _particlesCount; i++)
                {
                    _particles.Add(CreateParticle(false));
                }
            }
            else
            {
                _size = ActualSize;
            }

            var timestamp = Stopwatch.GetTimestamp();

            // The motion constants are tuned per 16 ms step. A stall is clamped instead of replayed,
            // or every particle would jump past the bottom at once.
            var elapsed = _lastTimestamp == 0
                ? 0
                : MathF.Min((timestamp - _lastTimestamp) * 1000f / Stopwatch.Frequency, 50);

            _lastTimestamp = timestamp;

            for (int j = 0; j < _particles.Count; j++)
            {
                var particle = _particles[j];

                if (particle.Update(elapsed))
                {
                    Recycle(particle);

                    _particles.RemoveAt(j);
                    j--;
                }
                else
                {
                    particle.Apply();
                }
            }

            if (_fallingDownCount >= _particlesCount / 2 && _speedCoef > 0.2f)
            {
                StartFall();

                _speedCoef -= elapsed / 16.0f * 0.15f;

                if (_speedCoef < 0.2f)
                {
                    _speedCoef = 0.2f;
                }
            }

            if (_particles.Count > 0)
            {
                return;
            }

            _started = false;

            StopRendering();
            Completed?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureVisual()
        {
            if (_visual != null)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;

            _brushes = new CompositionColorBrush[_colors.Length];
            _circles = new CompositionEllipseGeometry[_maxTypeSize - _minTypeSize + 1];
            _strips = new CompositionRoundedRectangleGeometry[_maxTypeSize - _minTypeSize + 1];

            for (int i = 0; i < _colors.Length; i++)
            {
                _brushes[i] = compositor.CreateColorBrush(_colors[i]);
            }

            _visual = compositor.CreateShapeVisual();
            _visual.RelativeSizeAdjustment = Vector2.One;

            ElementComposition.SetElementChildVisual(this, _visual);
        }

        private CompositionGeometry GetGeometry(byte type, byte typeSize)
        {
            var index = typeSize - _minTypeSize;

            if (type == 0)
            {
                if (_circles[index] == null)
                {
                    var circle = _visual.Compositor.CreateEllipseGeometry();
                    circle.Radius = new Vector2(typeSize);

                    _circles[index] = circle;
                }

                return _circles[index];
            }

            if (_strips[index] == null)
            {
                // Centred on the origin so the shape's offset is the particle's position and its
                // rotation turns about the middle of the strip.
                var strip = _visual.Compositor.CreateRoundedRectangleGeometry();
                strip.Offset = new Vector2(-typeSize, -2);
                strip.Size = new Vector2(typeSize * 2, 4);
                strip.CornerRadius = new Vector2(2);

                _strips[index] = strip;
            }

            return _strips[index];
        }

        private void Recycle(Particle particle)
        {
            _visual.Shapes.Remove(particle.Shape);
            _pool.Push(particle);
        }

        private void StartFall()
        {
            if (_startedFall)
            {
                return;
            }

            _startedFall = true;

            for (int i = 0; i < _fallParticlesCount; i++)
            {
                _particles.Add(CreateParticle(true));
            }
        }

        private Particle CreateParticle(bool falling)
        {
            var particle = _pool.Count > 0
                ? _pool.Pop()
                : new Particle(this, _visual.Compositor.CreateSpriteShape());

            particle.Reset();
            particle.colorType = (byte)NextInt(_colors.Length);
            particle.type = (byte)NextInt(2);
            particle.side = (byte)NextInt(2);
            particle.finishedStart = (byte)(NextInt(2) + 1);

            if (particle.type == 0)
            {
                particle.typeSize = (byte)(int)(NextFloat() * 2.0f + 4.0f);
            }
            else
            {
                particle.typeSize = (byte)(int)(NextFloat() * 4.0f + 4.0f);
            }

            if (falling)
            {
                particle.y = -NextFloat() * _size.Y * 1.2f;
                particle.x = 5.0f + NextInt((int)(_size.X - 10.0f));
                particle.xFinished = particle.finishedStart;
            }
            else
            {
                int dp = NextInt(10) + 4;
                int measuredHeight = (int)(_size.Y / 4);
                if (particle.side == 0)
                {
                    particle.x = -dp;
                }
                else
                {
                    particle.x = _size.X + dp;
                }

                int i = 1;
                if (particle.side != 0)
                {
                    i = -1;
                }
                particle.moveX = i * (1.2f + NextFloat() * 4.0f);
                particle.moveY = -(4.0f + NextFloat() * 4.0f);
                particle.y = measuredHeight / 2 + NextInt(measuredHeight * 2);
            }

            particle.Shape.Geometry = GetGeometry(particle.type, particle.typeSize);
            particle.Shape.FillBrush = _brushes[particle.colorType];
            particle.Apply();

            _visual.Shapes.Add(particle.Shape);
            return particle;
        }

        private class Particle
        {
            private readonly ConfettiView _confetti;

            public readonly CompositionSpriteShape Shape;

            public byte colorType;
            public byte finishedStart;
            public float moveX;
            public float moveY;
            public float rotation;
            public byte side;
            public byte type;
            public byte typeSize;
            public float x;
            public byte xFinished;
            public float y;

            public Particle(ConfettiView confetti, CompositionSpriteShape shape)
            {
                _confetti = confetti;
                Shape = shape;
            }

            public void Reset()
            {
                moveX = 0;
                moveY = 0;
                rotation = 0;
                xFinished = 0;
            }

            public void Apply()
            {
                Shape.Offset = new Vector2(x, y);

                if (type == 1)
                {
                    Shape.RotationAngleInDegrees = rotation;
                }
            }

            public bool Update(float elapsed)
            {
                float coefficient = _confetti._size.X / 360f;

                float f = elapsed / 16.0f;
                float f2 = x;
                float f3 = moveX;
                x = f2 + f3 * f * coefficient;
                y += moveY * f;
                if (xFinished != 0)
                {
                    if (xFinished == 1)
                    {
                        moveX += 0.5f * f * 0.05f;
                        if (moveX >= 0.5f)
                        {
                            xFinished = 2;
                        }
                    }
                    else
                    {
                        moveX -= 0.5f * f * 0.05f;
                        if (moveX <= -0.5f)
                        {
                            xFinished = 1;
                        }
                    }
                }
                else if (side == 0)
                {
                    if (f3 > 0.0f)
                    {
                        moveX = f3 - 0.05f * f;
                        if (moveX <= 0.0f)
                        {
                            moveX = 0.0f;
                            xFinished = finishedStart;
                        }
                    }
                }
                else if (f3 < 0.0f)
                {
                    moveX = f3 + 0.05f * f;
                    if (moveX >= 0.0f)
                    {
                        moveX = 0.0f;
                        xFinished = finishedStart;
                    }
                }

                bool z = moveY < -0.5f;
                if (moveY > -0.5f)
                {
                    moveY += 1.0f / 3.0f * f * _confetti._speedCoef;
                }
                else
                {
                    moveY += 1.0f / 3.0f * f;
                }

                if (z && moveY > -0.5f)
                {
                    _confetti._fallingDownCount += 1;
                }

                if (type == 1)
                {
                    rotation += f * 10.0f;
                    if (rotation > 360)
                    {
                        rotation -= 360;
                    }
                }

                return y >= _confetti._size.Y;
            }
        }

        private float NextFloat()
        {
            return (float)_random.NextDouble();
        }

        private int NextInt(int max)
        {
            return _random.Next(max);
        }
    }
}
