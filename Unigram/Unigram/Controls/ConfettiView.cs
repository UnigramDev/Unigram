using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls
{
    [TemplatePart(Name = "Canvas", Type = typeof(CanvasAnimatedControl))]
    public class ConfettiView : Control
    {
        private CanvasAnimatedControl Canvas;
        private string CanvasPartName = "Canvas";

        public static Color[] _colors = new Color[]
        {
            Color.FromArgb(0xFF, 0x2C, 0xBC, 0xE8),
            Color.FromArgb(0xFF, 0x9E, 0x04, 0xD0),
            Color.FromArgb(0xFF, 0xFE, 0xCB, 0x02),
            Color.FromArgb(0xFF, 0xFD, 0x23, 0x57),
            Color.FromArgb(0xFF, 0x27, 0x8C, 0xFE),
            Color.FromArgb(0xFF, 0x59, 0xB8, 0x6C)
        };

        private const int _fallParticlesCount = 30;//(SharedConfig.getDevicePerfomanceClass() == 0 ? 20 : 30);
        private const int _particlesCount = 60; //(SharedConfig.getDevicePerfomanceClass() == 0 ? 50 : 60);
        private int _fallingDownCount;
        private long lastUpdateTime;
        private readonly List<Particle> _particles = new List<Particle>(_particlesCount + _fallParticlesCount);
        private float _speedCoef = 1.0f;
        private bool _started;
        private bool _startedFall;

        private Random _random = new Random();

        public ConfettiView()
        {
            DefaultStyleKey = typeof(ConfettiView);
        }

        public float CanvasWidth => (float)(Canvas?.Size.Width ?? 0);
        public float CanvasHeight => (float)(Canvas?.Size.Height ?? 0);

        public event EventHandler Completed;

        protected override void OnApplyTemplate()
        {
            var canvas = GetTemplateChild(CanvasPartName) as CanvasAnimatedControl;
            if (canvas == null)
            {
                return;
            }

            Canvas = canvas;
            Canvas.Loaded += OnLoaded;
            Canvas.Unloaded += OnUnloaded;
            //Canvas.CreateResources += OnCreateResources;
            Canvas.Draw += OnDraw;

            base.OnApplyTemplate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_started)
            {
                Start();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Canvas.Loaded -= OnLoaded;
            Canvas.Unloaded -= OnUnloaded;
            //Canvas.CreateResources -= OnCreateResources;
            Canvas.Draw -= OnDraw;
            Canvas.RemoveFromVisualTree();
            Canvas = null;
        }

        private void OnDraw(ICanvasAnimatedControl sender, CanvasAnimatedDrawEventArgs args)
        {
            int i = (int)args.Timing.ElapsedTime.TotalMilliseconds;
            if (i > 18)
            {
                i = 16;
            }

            for (int j = 0; j < _particles.Count; j++)
            {
                _particles[j].Draw(args);

                if (_particles[j].Update(i))
                {
                    _particles.RemoveAt(j);
                    j--;
                }
            }

            if (_fallingDownCount >= _particlesCount / 2 && _speedCoef > 0.2f)
            {
                StartFall();

                _speedCoef -= (((float)i) / 16.0f) * 0.15f;

                if (_speedCoef < 0.2f)
                {
                    _speedCoef = 0.2f;
                }
            }

            if (_particles.Count > 0)
            {
                //invalidate();
                return;
            }

            _started = false;
            sender.Paused = true;

            Completed?.Invoke(this, EventArgs.Empty);
        }

        public void Start()
        {
            _particles.Clear();
            _started = true;
            _startedFall = false;
            _fallingDownCount = 0;
            _speedCoef = 1.0f;

            for (int i = 0; i < _particlesCount; i++)
            {
                _particles.Add(CreateParticle(false));
            }

            //invalidate();
            if (Canvas != null)
            {
                Canvas.Paused = false;
            }
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
            Particle particle = new Particle(this)
            {
                colorType = (byte)NextInt(_colors.Length),
                type = (byte)NextInt(2),
                side = (byte)NextInt(2),
                finishedStart = (byte)(NextInt(2) + 1)
            };
            if (particle.type == 0)
            {
                particle.typeSize = (byte)((int)((NextFloat() * 2.0f) + 4.0f));
            }
            else
            {
                particle.typeSize = (byte)((int)((NextFloat() * 4.0f) + 4.0f));
            }
            if (falling)
            {
                particle.y = (-NextFloat()) * CanvasHeight * 1.2f;
                particle.x = 5.0f + NextInt((int)(CanvasWidth - 10.0f));
                particle.xFinished = particle.finishedStart;
            }
            else
            {
                int dp = (int)(NextInt(10) + 4);
                int measuredHeight = (int)(CanvasHeight / 4);
                if (particle.side == 0)
                {
                    particle.x = -dp;
                }
                else
                {
                    particle.x = CanvasWidth + dp;
                }

                int i = 1;
                if (particle.side != 0)
                {
                    i = -1;
                }
                particle.moveX = i * (1.2f + (NextFloat() * 4.0f));
                particle.moveY = -(4.0f + (NextFloat() * 4.0f));
                particle.y = (measuredHeight / 2) + NextInt(measuredHeight * 2);
            }
            return particle;
        }

        private class Particle
        {
            private readonly ConfettiView _confetti;

            public byte colorType;
            public byte finishedStart;
            public float moveX;
            public float moveY;
            public short rotation;
            public byte side;
            public byte type;
            public byte typeSize;
            public float x;
            public byte xFinished;
            public float y;

            public Particle(ConfettiView confetti)
            {
                this._confetti = confetti;
            }

            public void Draw(CanvasAnimatedDrawEventArgs canvas)
            {
                if (type == 0)
                {
                    canvas.DrawingSession.FillCircle(x, y, typeSize, _colors[colorType]);
                    return;
                }

                Rect Rect(double x1, double y1, double x2, double y2)
                {
                    return new Rect(x1, y1, x2 - x1, y2 - y1);
                }

                var rect = Rect(x - typeSize, y - 2.0f, x + typeSize, y + 2.0f);
                canvas.DrawingSession.Transform = Matrix3x2.CreateRotation(DegreeToRadian(rotation), new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2).ToVector2());
                canvas.DrawingSession.FillRoundedRectangle(rect, 2.0f, 2.0f, _colors[colorType]);
                canvas.DrawingSession.Transform = Matrix3x2.Identity;
            }

            public bool Update(int i)
            {
                float coefficient = _confetti.CanvasWidth / 360f;

                float f = ((float)i) / 16.0f;
                float f2 = x;
                float f3 = moveX;
                x = f2 + (f3 * f * coefficient);
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
                        moveX = f3 - (0.05f * f);
                        if (moveX <= 0.0f)
                        {
                            moveX = 0.0f;
                            xFinished = finishedStart;
                        }
                    }
                }
                else if (f3 < 0.0f)
                {
                    moveX = f3 + (0.05f * f);
                    if (moveX >= 0.0f)
                    {
                        moveX = 0.0f;
                        xFinished = finishedStart;
                    }
                }

                bool z = moveY < -0.5f;
                if (moveY > -0.5f)
                {
                    moveY += ((1.0f / 3.0f) * f * _confetti._speedCoef);
                }
                else
                {
                    moveY += ((1.0f / 3.0f) * f);
                }

                if (z && moveY > -0.5f)
                {
                    _confetti._fallingDownCount += 1;
                }

                if (type == 1)
                {
                    rotation = (short)(int)(rotation + (f * 10.0f));
                    if (rotation > 360)
                    {
                        rotation -= 360;
                    }
                }

                if (y >= _confetti.CanvasHeight)
                {
                    return true;
                }

                return false;
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

        public static float DegreeToRadian(float angle)
        {
            return (float)Math.PI * angle / 180.0f;
        }
    }
}
