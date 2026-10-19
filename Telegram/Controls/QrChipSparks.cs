//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Diagnostics;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    // The light of the free-message chip: a burst and a trail of dust as its fill sweeps in, sparks and
    // embers off the edge while a timed code burns down, and droplets when the fill shatters as the fee comes
    // back. Drawn only while there is something to draw.
    public partial class QrChipSparks : Grid
    {
        // How far this element reaches past the chip on each side, which is where the particles fly.
        private static readonly Thickness Reach = new(60, 90, 140, 30);

        private const float SweepDuration = 0.34f;

        private CanvasControl _canvas;

        private long _start;
        private long _lastFrame;

        private float _from;
        private float _remaining;
        private float _lifetime;
        private bool _timer;
        private bool _burning;
        private bool _burst;

        private readonly Random _random = new();

        public QrChipSparks()
        {
            IsHitTestVisible = false;
            Margin = new Thickness(-Reach.Left, -Reach.Top, -Reach.Right, -Reach.Bottom);
            Unloaded += OnUnloaded;
        }

        // The fill sweeps from where it is to what the code has left, then, on a timer, burns down with it.
        public void Burn(float remaining, float lifetime, bool timer, bool burst)
        {
            _from = _burning ? Fraction(Elapsed()) : 0;
            _start = Stopwatch.GetTimestamp();
            _remaining = remaining;
            _lifetime = lifetime;
            _timer = timer;
            _burning = true;
            _burst = burst;

            Start();
        }

        // The fill's torn end, as a clip: everything left of a ragged line along x = 0, a couple of pixels
        // either side of it. Built once and slid along with the edge, so the tear keeps its shape as it burns.
        public static CompositionPath CreateTornEdge(float height)
        {
            using var builder = new CanvasPathBuilder(null);
            builder.BeginFigure(-4096, -4);

            for (float y = -4; ; y += 0.75f)
            {
                var clamped = MathF.Min(y, height + 4);
                builder.AddLine(TornOffset(clamped), clamped);

                if (clamped >= height + 4)
                {
                    break;
                }
            }

            builder.AddLine(-4096, height + 4);
            builder.EndFigure(CanvasFigureLoop.Closed);

            return new CompositionPath(CanvasGeometry.CreatePath(builder));
        }

        // Two octaves of value noise down the edge: notches of one to three pixels, finer along the height.
        private static float TornOffset(float y)
        {
            var u = y / 1.7f;
            var n = 0.68f * ValueNoise(u, 17) + 0.32f * ValueNoise(u * 2.6f, 91);

            return (n - 0.5f) * 3.6f;
        }

        private static float ValueNoise(float x, int seed)
        {
            var xi = (int)MathF.Floor(x);
            var fx = x - xi;
            fx = fx * fx * (3 - 2 * fx);

            var a = Hash01(xi, seed);
            var b = Hash01(xi + 1, seed);

            return a + (b - a) * fx;
        }

        private static float Hash01(int x, int seed)
        {
            var h = unchecked(x * 374761393 + seed * 1442695041);
            h = unchecked((h ^ (int)((uint)h >> 13)) * 1274126177);

            return ((h ^ (int)((uint)h >> 16)) & 0xFFFF) / 65535f;
        }

        // The fee is back: the fill, as far as it still reached, breaks into droplets.
        public void Shatter()
        {
            if (!_burning)
            {
                return;
            }

            var fraction = Fraction(Elapsed());
            _burning = false;

            var chip = Chip();
            var left = (float)chip.Left;
            var top = (float)chip.Top;
            var height = (float)chip.Height;
            var right = left + (float)chip.Width * fraction;
            var radius = height / 2;
            var cy = top + radius;
            var cx = left + (float)chip.Width / 2;

            // Thrown out from the chip's centre and up.
            const float step = 3;

            var cols = Math.Max(1, (int)((right - left) / step));
            var rows = Math.Max(1, (int)(height / step));
            var keep = MathF.Min(1, 260f / (cols * rows));

            for (int iy = 0; iy < rows; iy++)
            {
                for (int ix = 0; ix < cols; ix++)
                {
                    if (NextFloat() > keep || _count >= Capacity)
                    {
                        continue;
                    }

                    var x = left + (ix + 0.5f + (NextFloat() - 0.5f) * 0.8f) * step;
                    var y = top + (iy + 0.5f + (NextFloat() - 0.5f) * 0.8f) * step;

                    if (x < left + radius && Vector2.Distance(new Vector2(x, y), new Vector2(left + radius, cy)) > radius)
                    {
                        continue;
                    }

                    var dx = x - cx;
                    var dy = y - cy;
                    var d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
                    var speed = 60 + NextFloat() * 140;
                    var spark = NextFloat() < 0.15f;

                    var i = _count++;
                    _kind[i] = (byte)(spark ? Droplet + 1 : Droplet);
                    _x[i] = x;
                    _y[i] = y;
                    _vx[i] = dx / d * speed + (NextFloat() - 0.5f) * 60;
                    _vy[i] = dy / d * speed - (40 + NextFloat() * 80) + (NextFloat() - 0.5f) * 60;
                    _size[i] = spark ? 1.4f + NextFloat() * 0.8f : 1.0f + NextFloat() * 1.2f;
                    _life[i] = 0.35f + NextFloat() * 0.3f;
                    _age[i] = 0;
                }
            }

            Start();
        }

        private void Start()
        {
            if (_canvas == null)
            {
                _canvas = new CanvasControl();
                _canvas.Draw += OnDraw;

                Children.Add(_canvas);
            }

            _canvas.Visibility = Visibility.Visible;
            _canvas.Invalidate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_canvas != null)
            {
                _canvas.Draw -= OnDraw;
                _canvas.RemoveFromVisualTree();

                Children.Remove(_canvas);
                _canvas = null;
            }

            _burning = false;
            _count = 0;
            _lastFrame = 0;
        }

        private Rect Chip()
        {
            var size = ActualSize;
            return new Rect(Reach.Left, Reach.Top, Math.Max(0, size.X - Reach.Left - Reach.Right), Math.Max(0, size.Y - Reach.Top - Reach.Bottom));
        }

        private float Elapsed()
        {
            return _start == 0 ? 0 : (float)((Stopwatch.GetTimestamp() - _start) / (double)Stopwatch.Frequency);
        }

        // How much of the chip the fill covers at t: the same curve the chip's own clip is animated on.
        private float Fraction(float t)
        {
            var target = !_timer ? 1 : _lifetime > 0 ? MathF.Max(0, _remaining - MathF.Min(t, SweepDuration)) / _lifetime : 0;

            if (t < SweepDuration)
            {
                var u = 1 - t / SweepDuration;
                return _from + (target - _from) * (1 - u * u * u);
            }

            return !_timer ? 1 : MathF.Max(0, _remaining - t) / MathF.Max(0.001f, _lifetime);
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var now = Stopwatch.GetTimestamp();
            var dt = _lastFrame == 0 ? 1 / 60f : MathF.Min(0.05f, (float)((now - _lastFrame) / (double)Stopwatch.Frequency));
            _lastFrame = now;

            var t = Elapsed();
            var chip = Chip();

            if (_burning && chip.Width > 0)
            {
                Emit(chip, t, dt);
            }

            Draw(args.DrawingSession, chip, dt);

            var sweeping = _burning && t < SweepDuration + 0.25f;
            var burning = _burning && _timer && Fraction(t) > 0.001f;

            if (_count > 0 || sweeping || burning)
            {
                sender.Invalidate();
            }
            else
            {
                // Nothing left to draw: the surface goes, and the next frame starts a fresh clock.
                _lastFrame = 0;
                sender.Visibility = Visibility.Collapsed;
            }
        }

        private void Emit(Rect chip, float t, float dt)
        {
            var left = (float)chip.Left;
            var top = (float)chip.Top;
            var width = (float)chip.Width;
            var height = (float)chip.Height;
            var edge = left + width * Fraction(t);

            if (_burst)
            {
                // The tap: a burst from the left 40%, where the fill starts.
                _burst = false;

                for (int q = 0; q < 24; q++)
                {
                    EmitDust(left + NextFloat() * width * 0.4f, top + NextFloat() * height, 0.5f);
                }
            }

            if (t < SweepDuration)
            {
                // Dust off the moving edge as the fill sweeps in, gone a moment after it lands.
                var maxLife = SweepDuration + 0.25f - t;
                var n = (int)MathF.Floor(260 * dt + NextFloat());

                for (int q = 0; q < n; q++)
                {
                    EmitDust(edge, top + NextFloat() * height, maxLife);
                }

                return;
            }

            if (!_timer || edge <= left + 0.5f)
            {
                return;
            }

            // Burning: a dense bright fizz hugging the edge...
            var fizz = (int)MathF.Floor(110 * dt + NextFloat());
            for (int q = 0; q < fizz; q++)
            {
                EmitEmber(edge + NextFloat() * 5, top + NextFloat() * height,
                    10 + NextFloat() * 20, -(4 + NextFloat() * 10), 0,
                    0.8f + NextFloat() * 0.7f, 0.25f + NextFloat() * 0.25f, 0.9f + NextFloat() * 0.1f, false, false);
            }

            // ...and a trail drifting right and lifted by the air, a few of them sparks.
            var trail = (int)MathF.Floor(70 * dt + NextFloat());
            for (int q = 0; q < trail; q++)
            {
                var spark = NextFloat() < 0.12f;

                EmitEmber(edge + 2 + NextFloat() * 10, top + NextFloat() * height,
                    40 + NextFloat() * 70, -(10 + NextFloat() * 30), -(26 + NextFloat() * 34),
                    spark ? 1.4f + NextFloat() * 0.8f : 0.9f + NextFloat() * 1.1f,
                    0.6f + NextFloat() * 0.7f, 1, spark, true);
            }
        }

        #region Particles

        private const int Capacity = 320;

        private const byte Dust = 0;
        private const byte Spark = 1;
        private const byte Chunk = 2;
        private const byte Droplet = 3;

        private readonly float[] _x = new float[Capacity];
        private readonly float[] _y = new float[Capacity];
        private readonly float[] _vx = new float[Capacity];
        private readonly float[] _vy = new float[Capacity];
        private readonly float[] _size = new float[Capacity];
        private readonly float[] _life = new float[Capacity];
        private readonly float[] _age = new float[Capacity];

        // Embers: where they came from, how bright, their buoyancy and their flutter.
        private readonly float[] _sx = new float[Capacity];
        private readonly float[] _sy = new float[Capacity];
        private readonly float[] _alpha = new float[Capacity];
        private readonly float[] _lift = new float[Capacity];
        private readonly float[] _wobble = new float[Capacity];
        private readonly float[] _wobblePhase = new float[Capacity];

        private readonly byte[] _kind = new byte[Capacity];
        private readonly bool[] _ember = new bool[Capacity];
        private int _count;

        private void EmitDust(float x, float y, float maxLife)
        {
            if (_count >= Capacity)
            {
                return;
            }

            var i = _count++;
            var roll = NextFloat();

            _kind[i] = roll < 0.65f ? Dust : roll < 0.9f ? Spark : Chunk;
            _ember[i] = false;
            _x[i] = x + (NextFloat() - 0.5f) * 4;
            _y[i] = y;
            _size[i] = _kind[i] == Dust ? 1.4f + NextFloat() * 1.6f : _kind[i] == Spark ? 0.7f + NextFloat() * 0.7f : 3 + NextFloat() * 1.5f;
            _vx[i] = -30 + NextFloat() * 80;
            _vy[i] = -50 - NextFloat() * 100;
            _life[i] = MathF.Max(0.05f, MathF.Min(0.30f + NextFloat() * 0.20f, maxLife));
            _age[i] = 0;
        }

        private void EmitEmber(float x, float y, float vx, float vy, float lift, float size, float life, float alpha, bool spark, bool wobble)
        {
            if (_count >= Capacity)
            {
                return;
            }

            var i = _count++;

            _kind[i] = spark ? Spark : Dust;
            _ember[i] = true;
            _x[i] = _sx[i] = x;
            _y[i] = _sy[i] = y;
            _vx[i] = vx;
            _vy[i] = vy;
            _lift[i] = lift;
            _wobble[i] = wobble ? 4 + NextFloat() * 3 : 0;
            _wobblePhase[i] = NextFloat() * 6.2832f;
            _size[i] = size;
            _life[i] = life;
            _age[i] = 0;
            _alpha[i] = alpha;
        }

        private void Draw(CanvasDrawingSession ds, Rect chip, float dt)
        {
            var top = (float)chip.Top;
            var ceiling = top - 40;
            var emberCeiling = top - 48;
            var dropletCeiling = top - 70;
            var transform = ds.Transform;

            for (int i = _count - 1; i >= 0; i--)
            {
                _age[i] += dt;

                if (_age[i] >= _life[i])
                {
                    var last = --_count;
                    _x[i] = _x[last];
                    _y[i] = _y[last];
                    _vx[i] = _vx[last];
                    _vy[i] = _vy[last];
                    _size[i] = _size[last];
                    _life[i] = _life[last];
                    _age[i] = _age[last];
                    _sx[i] = _sx[last];
                    _sy[i] = _sy[last];
                    _alpha[i] = _alpha[last];
                    _lift[i] = _lift[last];
                    _wobble[i] = _wobble[last];
                    _wobblePhase[i] = _wobblePhase[last];
                    _kind[i] = _kind[last];
                    _ember[i] = _ember[last];
                    continue;
                }

                if (_kind[i] >= Droplet)
                {
                    // Droplets: drag, then a little gravity, under a soft ceiling.
                    _vx[i] *= 1 - 2.4f * dt;
                    _vy[i] *= 1 - 2.4f * dt;

                    if (_age[i] > 0.12f)
                    {
                        _vy[i] += 120 * dt;
                    }

                    if (_y[i] < dropletCeiling)
                    {
                        _vy[i] *= 0.85f;
                    }
                }
                else if (_ember[i])
                {
                    _vy[i] += _lift[i] * dt;
                    _vx[i] *= 1 - 1.6f * dt;
                    _vy[i] *= 1 - 0.8f * dt;

                    if (_y[i] < emberCeiling)
                    {
                        _vy[i] *= MathF.Pow(0.9f, dt * 60);
                    }
                }
                else
                {
                    _vy[i] -= 20 * dt;
                    _vx[i] *= 1 - 3 * dt;
                    _vy[i] *= 1 - 3 * dt;

                    if (_y[i] < ceiling)
                    {
                        _vy[i] *= 0.85f;
                    }
                }

                _x[i] += _vx[i] * dt;
                _y[i] += _vy[i] * dt;

                var t = _age[i] / _life[i];
                var speed = MathF.Sqrt(_vx[i] * _vx[i] + _vy[i] * _vy[i]);
                var heading = MathF.Atan2(_vy[i], _vx[i]);

                if (_kind[i] >= Droplet)
                {
                    // Round droplets stretched along their velocity, the sparks among them with a faint halo.
                    var a = t < 0.12f ? t / 0.12f : MathF.Pow(1 - (t - 0.12f) / 0.88f, 0.7f);
                    if (_y[i] < dropletCeiling)
                    {
                        a *= MathF.Max(0, 1 - (dropletCeiling - _y[i]) / 16);
                    }

                    var r = _size[i] * (1 - 0.4f * t) / 2;
                    var stretch = MathF.Min(2, 1 + speed / 200);

                    ds.Transform = Matrix3x2.CreateRotation(heading, new Vector2(_x[i], _y[i])) * transform;
                    ds.FillEllipse(_x[i], _y[i], r * stretch, r, WithAlpha(a));
                    ds.Transform = transform;

                    if (_kind[i] == Droplet + 1)
                    {
                        ds.FillCircle(_x[i], _y[i], r * 2.2f, WithAlpha(0.14f * a));
                    }
                }
                else if (_ember[i])
                {
                    // Brightest and densest at the edge: it fades and thins with the distance it has come.
                    var dx = _x[i] - _sx[i];
                    var dy = _y[i] - _sy[i];
                    var distance = MathF.Sqrt(dx * dx + dy * dy);

                    var a = t < 0.08f ? t / 0.08f : MathF.Pow(1 - (t - 0.08f) / 0.92f, 0.55f);
                    a *= _alpha[i] * MathF.Exp(-distance / 44);

                    if (_y[i] < emberCeiling)
                    {
                        a *= MathF.Max(0, 1 - (emberCeiling - _y[i]) / 16);
                    }

                    var r = _size[i] / 2 * (1 - 0.4f * MathF.Min(1, distance / 70));
                    var x = _x[i];
                    var y = _y[i];

                    // A small flutter across the direction of travel.
                    if (_wobble[i] > 0 && speed > 1)
                    {
                        var wobble = 3 * MathF.Sin(_age[i] * _wobble[i] + _wobblePhase[i]);
                        x += -_vy[i] / speed * wobble;
                        y += _vx[i] / speed * wobble;
                    }

                    var stretch = MathF.Min(1.8f, 1 + speed / 220);

                    ds.Transform = Matrix3x2.CreateRotation(heading, new Vector2(x, y)) * transform;
                    ds.FillEllipse(x, y, r * stretch, r, WithAlpha(a));
                    ds.Transform = transform;

                    if (_kind[i] == Spark)
                    {
                        ds.FillCircle(x, y, r * 2.2f, WithAlpha(0.14f * a));
                    }
                }
                else
                {
                    var a = t < 0.12f ? t / 0.12f : MathF.Pow(1 - (t - 0.12f) / 0.88f, 0.7f);
                    if (_y[i] < ceiling)
                    {
                        a *= MathF.Max(0, 1 - (ceiling - _y[i]) / 24);
                    }

                    var r = _size[i] * (1 - 0.5f * t) / 2;

                    ds.FillCircle(_x[i], _y[i], _kind[i] == Spark ? r * 2 : r, WithAlpha(a));

                    if (_kind[i] == Spark)
                    {
                        ds.FillCircle(_x[i], _y[i], r * 4.4f, WithAlpha(0.14f * a));
                    }
                }
            }
        }

        #endregion

        private float NextFloat()
        {
            return (float)_random.NextDouble();
        }

        private Color WithAlpha(float alpha)
        {
            return Color.FromArgb((byte)(255 * Math.Clamp(alpha, 0, 1)), 255, 255, 255);
        }
    }
}
