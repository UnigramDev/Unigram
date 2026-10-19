//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Diagnostics;
using System.Numerics;
using Telegram.Native;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    // The QR card's code while it changes: the first build blooms out of a halftone of dots, and a new code
    // replaces the old one behind a ragged front sweeping up from the bottom, the old modules lifting off as
    // dust. Drawn only while that runs; the host shows its static code the rest of the time.
    public partial class QrCodeTransition : Grid
    {
        private const float CodeSize = 222;

        private const float IntroDuration = 0.92f;
        private const float SwapDuration = 0.45f;

        private CanvasControl _canvas;

        private Code _current;
        private Code _previous;

        private bool _intro;
        private long _start;
        private long _lastFrame;
        private bool _settled;

        private Color _ink = Colors.Black;

        // The ink lightening to white in 32 steps, for the pieces leaving the card: mixing per piece and frame
        // costs a dozen Pow calls each.
        private readonly Color[] _fade = new Color[33];
        private Color _fadeInk = Colors.Transparent;

        private readonly Random _random = new();

        public QrCodeTransition()
        {
            IsHitTestVisible = false;
            Unloaded += OnUnloaded;
        }

        // Raised when the code is still again.
        public event EventHandler Completed;

        // Where the code and the card are, in this element's coordinates.
        public Rect CodeBounds { get; set; }

        public Rect CardBounds { get; set; }

        public Color Ink
        {
            get => _ink;
            set
            {
                _ink = value;
                _canvas?.Invalidate();
            }
        }

        public void PlayIntro(string url)
        {
            _previous?.Release();
            _previous = null;

            if (_current?.Url != url)
            {
                _current?.Release();
                _current = new Code(url);
            }

            _intro = true;
            Start();
        }

        public bool IsPlayingIntro => _start != 0 && _intro;

        // A code that arrives while the intro runs: the intro goes on building it, with no swap of its own.
        public void ReplaceIntro(string url)
        {
            if (_current?.Url != url)
            {
                _current?.Release();
                _current = new Code(url);
            }

            _canvas?.Invalidate();
        }

        public void PlaySwap(string from, string to)
        {
            Code previous;

            // A swap that starts while another runs keeps lifting off the code that was there before it;
            // otherwise the code from the last run is still rendered and can be reused.
            if (_start != 0 && !_intro && !_settled && _previous != null)
            {
                previous = _previous;
                _current?.Release();
            }
            else
            {
                previous = _current?.Url == from ? _current : new Code(from);

                if (previous != _current)
                {
                    _current?.Release();
                }

                _previous?.Release();
            }

            _previous = previous;
            _previous.ResetSpawned();

            _current = new Code(to);
            _intro = false;

            Start();
        }

        private void Start()
        {
            _start = Stopwatch.GetTimestamp();
            _lastFrame = 0;
            _settled = false;

            if (_canvas == null)
            {
                _canvas = new CanvasControl();
                _canvas.CreateResources += OnCreateResources;
                _canvas.Draw += OnDraw;

                Children.Add(_canvas);
            }

            _canvas.Visibility = Visibility.Visible;
            _canvas.Invalidate();
        }

        private void Stop()
        {
            _start = 0;
            _pcount = 0;

            _previous?.Release();
            _previous = null;

            if (_canvas != null)
            {
                _canvas.Visibility = Visibility.Collapsed;
            }

            Completed?.Invoke(this, EventArgs.Empty);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_canvas != null)
            {
                _canvas.CreateResources -= OnCreateResources;
                _canvas.Draw -= OnDraw;
                _canvas.RemoveFromVisualTree();

                Children.Remove(_canvas);
                _canvas = null;
            }

            _current?.Release();
            _previous?.Release();

            _start = 0;
            _pcount = 0;
        }

        private void OnCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            // A new device: the masks belonged to the old one.
            _current?.Release();
            _previous?.Release();
        }

        private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (_start == 0 || _current == null)
            {
                return;
            }

            var now = Stopwatch.GetTimestamp();
            var elapsed = (float)((now - _start) / (double)Stopwatch.Frequency);
            var dt = _lastFrame == 0 ? 1 / 60f : MathF.Min(0.05f, (float)((now - _lastFrame) / (double)Stopwatch.Frequency));
            _lastFrame = now;

            var ds = args.DrawingSession;
            var left = (float)CodeBounds.X;
            var top = (float)CodeBounds.Y;

            _current.Prepare(sender, _ink);
            _previous?.Prepare(sender, _ink);

            bool done;
            if (_intro)
            {
                done = DrawHalftone(sender, ds, _current, left, top, IntroProgress(elapsed));
            }
            else if (_settled)
            {
                ds.DrawImage(_current.Mask, left, top);
                done = true;
            }
            else
            {
                // Once the front has run its course every module has landed; finishing on time as well means a
                // rounding miss can never leave the code half built.
                _settled = DrawSwap(sender, ds, left, top, MathF.Min(1, elapsed / SwapDuration)) || elapsed >= SwapDuration;
                done = _settled;
            }

            DrawParticles(ds, dt);

            if (done && _pcount == 0)
            {
                Stop();
            }
            else
            {
                sender.Invalidate();
            }
        }

        #region Intro

        // How far the build is after el seconds: a spring stretched to the intro's length, whose first half,
        // the halftone dots, takes the first tenth, and from 80% a cubic that lands on 1 with no velocity.
        private static float IntroProgress(float el)
        {
            const float half = 0.10f;
            const float done = 0.467f;

            var u = MathF.Min(1, el / IntroDuration);
            u = u < half ? u * NaturalHalf / half : NaturalHalf + (u - half) * (1 - NaturalHalf) / (1 - half);

            var p = SpringProgress(u * done);
            if (u > Natural80)
            {
                var p0 = SpringProgress(Natural80 * done);
                var m0 = MathF.Min((SpringProgress((Natural80 + 0.002f) * done) - p0) / 0.002f * (1 - Natural80), 3 * (1 - p0));
                var t = (u - Natural80) / (1 - Natural80);
                var t2 = t * t;
                var t3 = t2 * t;

                p = (2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2);
            }

            return u >= 1 ? 1 : MathF.Min(1, p);
        }

        private static float SpringProgress(float t)
        {
            const double z = 0.9;
            var w0 = 2 * Math.PI / 0.62;
            var wd = w0 * Math.Sqrt(1 - z * z);
            var x = 1 - Math.Exp(-z * w0 * t) * (Math.Cos(wd * t) + z / Math.Sqrt(1 - z * z) * Math.Sin(wd * t));

            return (float)Math.Min(1.0, x * 1.02);
        }

        // Where on the spring's own clock it is half done, and 80% done.
        private static readonly float NaturalHalf = FindSpring(0.5f);
        private static readonly float Natural80 = FindSpring(0.8f);

        private static float FindSpring(float value)
        {
            for (int i = 0; i <= 1000; i++)
            {
                if (SpringProgress(i / 1000f * 0.467f) >= value)
                {
                    return i / 1000f;
                }
            }

            return value;
        }

        // A dot grid, blooming outwards from the centre: dots on a module grow from circles into rounded
        // squares and hand over to the module itself, dots off a module shrink away. The modules that have
        // taken over are drawn first, the dots still fading over them.
        private bool DrawHalftone(CanvasControl sender, CanvasDrawingSession ds, Code code, float left, float top, float p)
        {
            var intro = Math.Clamp(p / 0.1f, 0, 1);
            var cell = code.Cell;
            var done = intro >= 1;

            code.ClearSettled();

            for (int i = 0; i < code.Dark.Length; i++)
            {
                if (code.IsLogo(i) || EaseInOut((p - code.Delay[i]) / 0.434f) <= 0.75f)
                {
                    continue;
                }

                code.Settle(i);
            }

            code.DrawSettled(sender, ds, left, top, intro);

            for (int y = 0; y < code.N; y++)
            {
                for (int x = 0; x < code.N; x++)
                {
                    var i = y * code.N + x;
                    if (code.IsLogo(i))
                    {
                        continue;
                    }

                    var e = EaseInOut((p - code.Delay[i]) / 0.434f);
                    var cx = left + (x + 0.5f) * cell;
                    var cy = top + (y + 0.5f) * cell;

                    if (code.Dark[i])
                    {
                        var k = e > 0.75f ? (e - 0.75f) * 4 : 0;
                        if (k >= 1)
                        {
                            continue;
                        }

                        done = false;

                        var alpha = intro * (1 - k);
                        if (alpha > 0.004f)
                        {
                            var half = cell / 2 * (0.2f + 0.8f * e);
                            var round = half * (1 - 0.7f * e);

                            ds.FillRoundedRectangle(cx - half, cy - half, half * 2, half * 2, round, round, WithAlpha(_ink, alpha));
                        }
                    }
                    else
                    {
                        if (e < 1)
                        {
                            done = false;
                        }

                        var scale = 0.2f * (1 - e);
                        var fade = e < 0.7f ? 1 : 1 - (e - 0.7f) / 0.3f;
                        var alpha = intro * 0.9f * fade;

                        if (scale > 0.01f && alpha > 0.004f)
                        {
                            ds.FillCircle(cx, cy, cell / 2 * scale, WithAlpha(_ink, alpha));
                        }
                    }
                }
            }

            return done;
        }

        #endregion

        #region Swap

        // The front, in rows from the bottom: a snappy start and a soft landing. It ends far enough past the
        // top for the last module to have landed: a module's turn comes up to 4.8 rows late (its noise and the
        // gap behind the front), and it takes 2.4 more to pop in.
        private static float Front(Code code, float p)
        {
            var e = 1 - (1 - p) * (1 - p) * (0.55f + 0.45f * (1 - p));
            return -1.5f + e * (code.N + 8);
        }

        // The old code's still part and the new code's settled part are each drawn whole; only what moves is
        // drawn module by module: old modules shivering just above the front, new ones popping in behind it.
        private bool DrawSwap(CanvasControl sender, CanvasDrawingSession ds, float left, float top, float p)
        {
            var o = _previous;
            var c = _current;

            var fo = Front(o, p);
            var fc = Front(c, p);

            var remaining = 0;
            var settled = true;

            o.ClearSettled();
            c.ClearSettled();

            for (int i = 0; i < o.Dark.Length; i++)
            {
                var t = (o.N - 1 - i / o.N) + o.Noise[i] * 3.2f;

                if (!o.IsLogo(i) && fo <= t && t - fo >= 2.2f)
                {
                    o.Settle(i);
                }
            }

            for (int i = 0; i < c.Dark.Length; i++)
            {
                if (c.IsLogo(i))
                {
                    continue;
                }

                var t = (c.N - 1 - i / c.N) + c.Noise[i] * 3.2f + 1.6f;
                if (fc <= t)
                {
                    settled = false;
                }
                else if (IsLanded(fc - t))
                {
                    c.Settle(i);
                }
                else if (c.Dark[i])
                {
                    settled = false;
                }
            }

            o.DrawSettled(sender, ds, left, top, 1);
            c.DrawSettled(sender, ds, left, top, 1);

            for (int y = 0; y < o.N; y++)
            {
                for (int x = 0; x < o.N; x++)
                {
                    var i = y * o.N + x;
                    if (!o.Dark[i] || o.IsLogo(i))
                    {
                        continue;
                    }

                    var t = (o.N - 1 - y) + o.Noise[i] * 3.2f;
                    if (fo > t)
                    {
                        if (!o.Spawned[i])
                        {
                            o.Spawned[i] = true;
                            Spawn(left + (x + 0.5f) * o.Cell, top + (y + 0.5f) * o.Cell, o.Cell);
                        }

                        continue;
                    }

                    remaining++;

                    if (o.Settled[i])
                    {
                        continue;
                    }

                    // Modules just above the front shiver a little before they lift off.
                    var near = 1 - MathF.Min(1, (t - fo) / 2.2f);
                    var jx = MathF.Sin(x * 13 + y * 7 + p * 60) * 1.4f * near;
                    var jy = -near * 1.5f;

                    DrawCell(ds, o, x, y, left + jx, top + jy, 1, 1 - near * 0.25f);
                }
            }

            for (int y = 0; y < c.N; y++)
            {
                for (int x = 0; x < c.N; x++)
                {
                    var i = y * c.N + x;
                    if (!c.Dark[i] || c.IsLogo(i) || c.Settled[i])
                    {
                        continue;
                    }

                    var t = (c.N - 1 - y) + c.Noise[i] * 3.2f + 1.6f;
                    if (fc <= t)
                    {
                        continue;
                    }

                    // The new modules pop in behind the front with a small overshoot.
                    var a = MathF.Min(1, (fc - t) / 2.4f);
                    DrawCell(ds, c, x, y, left, top, 0.35f + 0.65f * BackOut(a), MathF.Min(1, a * 2.5f));
                }
            }

            return settled && remaining == 0;
        }

        // Full and back at its size: the overshoot has come and gone.
        private static bool IsLanded(float distance)
        {
            var a = MathF.Min(1, distance / 2.4f);
            return a >= 1 || (a > 0.4f && MathF.Abs(0.35f + 0.65f * BackOut(a) - 1) < 0.01f);
        }

        private static float BackOut(float u)
        {
            const float c1 = 2.2f;
            const float c3 = c1 + 1;

            u -= 1;
            return 1 + c3 * u * u * u + c1 * u * u;
        }

        #endregion

        #region Particles

        private const int Capacity = 2400;

        private readonly float[] _px = new float[Capacity];
        private readonly float[] _py = new float[Capacity];
        private readonly float[] _pvx = new float[Capacity];
        private readonly float[] _pvy = new float[Capacity];
        private readonly float[] _psize = new float[Capacity];
        private readonly float[] _plife = new float[Capacity];
        private readonly float[] _page = new float[Capacity];
        private readonly float[] _prot = new float[Capacity];
        private readonly float[] _pvr = new float[Capacity];

        // 0 an ink chunk, 1 a chunk that catches the light, 2 a white spark.
        private readonly byte[] _pkind = new byte[Capacity];
        private int _pcount;

        // A module lifting off: a few pieces burst out from the card's centre and up, chunks, fine dust and
        // the odd spark.
        private void Spawn(float x, float y, float size)
        {
            var card = CardBounds;
            var dx = x - (float)(card.X + card.Width / 2);
            var dy = y - (float)(card.Y + card.Height / 2);
            var d = MathF.Max(1, MathF.Sqrt(dx * dx + dy * dy));
            var rx = dx / d;
            var ry = dy / d;

            var roll = NextFloat();
            var count = roll < 0.12f ? 2 : roll < 0.7f ? 3 : 5;

            for (int k = 0; k < count && _pcount < Capacity; k++)
            {
                var i = _pcount++;
                var spark = k > 0 && NextFloat() < 0.3f;

                _pkind[i] = (byte)(spark ? 2 : NextFloat() < 0.14f ? 1 : 0);
                _px[i] = x + (NextFloat() - 0.5f) * size * 0.5f;
                _py[i] = y + (NextFloat() - 0.5f) * size * 0.5f;

                if (spark)
                {
                    _psize[i] = 0.9f + NextFloat() * 0.9f;
                }
                else if (k == 0 && roll < 0.12f)
                {
                    _psize[i] = size * (1.25f + NextFloat() * 0.45f);
                }
                else
                {
                    _psize[i] = size * (0.3f + NextFloat() * 0.55f);
                }

                var speed = 110 + NextFloat() * 150;
                var radial = 0.45f + NextFloat() * 0.35f;

                _pvx[i] = rx * speed * radial + (NextFloat() - 0.5f) * 90;
                _pvy[i] = ry * speed * radial - speed * (1 - radial) - 30;
                _plife[i] = 0.30f + NextFloat() * 0.20f;
                _page[i] = 0;
                _prot[i] = NextFloat() * 90;
                _pvr[i] = (NextFloat() - 0.5f) * 720;
            }
        }

        private void DrawParticles(CanvasDrawingSession ds, float dt)
        {
            var card = CardBounds;
            var ceiling = (float)card.Y - 56;
            var transform = ds.Transform;

            for (int i = _pcount - 1; i >= 0; i--)
            {
                _page[i] += dt;

                if (_page[i] >= _plife[i])
                {
                    var last = --_pcount;
                    _px[i] = _px[last];
                    _py[i] = _py[last];
                    _pvx[i] = _pvx[last];
                    _pvy[i] = _pvy[last];
                    _psize[i] = _psize[last];
                    _plife[i] = _plife[last];
                    _page[i] = _page[last];
                    _prot[i] = _prot[last];
                    _pvr[i] = _pvr[last];
                    _pkind[i] = _pkind[last];
                    continue;
                }

                // A little lift, and air drag: an energetic start that eases out, with a soft ceiling above
                // the card.
                _pvy[i] -= 20 * dt;
                _pvx[i] *= 1 - 2.4f * dt;
                _pvy[i] *= 1 - 3.0f * dt;

                if (_py[i] < ceiling)
                {
                    _pvy[i] *= 0.85f;
                }

                _px[i] += _pvx[i] * dt;
                _py[i] += _pvy[i] * dt;
                _prot[i] += _pvr[i] * dt;

                var t = _page[i] / _plife[i];
                var alpha = t < 0.08f ? t / 0.08f : 1 - (t - 0.08f) / 0.92f;

                var over = ceiling - _py[i];
                if (over > 0)
                {
                    alpha *= MathF.Max(0, 1 - over / 24);
                }

                var size = _psize[i] * (1 - (1 - (1 - t) * (1 - t)) * 0.85f);

                if (_pkind[i] == 2)
                {
                    ds.FillCircle(_px[i], _py[i], size, WithAlpha(Colors.White, alpha * 0.9f));
                    ds.FillCircle(_px[i], _py[i], size * 2.6f, WithAlpha(Colors.White, alpha * 0.22f));
                    continue;
                }

                // Ink inside the card, lightening to white as the piece leaves it.
                var ox = MathF.Max((float)card.X - _px[i], MathF.Max(0, _px[i] - (float)(card.X + card.Width)));
                var oy = MathF.Max((float)card.Y - _py[i], MathF.Max(0, _py[i] - (float)(card.Y + card.Height)));
                var outside = MathF.Min(1, MathF.Max(ox, oy) / 36);

                var color = outside > 0 ? Fade(outside) : _ink;

                ds.Transform = Matrix3x2.CreateRotation(_prot[i] * MathF.PI / 180, new Vector2(_px[i], _py[i])) * transform;
                ds.FillRoundedRectangle(_px[i] - size / 2, _py[i] - size / 2, size, size, size * 0.3f, size * 0.3f, WithAlpha(color, alpha));

                if (_pkind[i] == 1)
                {
                    var glint = MathF.Sin(MathF.PI * MathF.Min(1, t * 1.6f));
                    ds.FillRoundedRectangle(_px[i] - size / 2, _py[i] - size / 2, size, size, size * 0.3f, size * 0.3f, WithAlpha(Colors.White, 0.78f * alpha * glint));
                }

                ds.Transform = transform;
            }
        }

        private float NextFloat()
        {
            return (float)_random.NextDouble();
        }

        #endregion

        // One module that is moving, cut out of the code's own rendering and scaled about its centre. Still
        // modules are never drawn this way: two cut cells side by side leave a seam where their antialiased
        // edges meet.
        private static void DrawCell(CanvasDrawingSession ds, Code code, int x, int y, float left, float top, float scale, float alpha)
        {
            if (alpha <= 0.004f || code.Mask == null)
            {
                return;
            }

            var cell = code.Cell;
            var source = new Rect(x * cell, y * cell, cell, cell);

            var half = cell / 2 * scale;
            var cx = left + (x + 0.5f) * cell;
            var cy = top + (y + 0.5f) * cell;

            ds.DrawImage(code.Mask, new Rect(cx - half, cy - half, half * 2, half * 2), source, MathF.Min(1, alpha));
        }

        private static float EaseInOut(float u)
        {
            u = Math.Clamp(u, 0, 1);

            if (u < 0.5f)
            {
                return 4 * u * u * u;
            }

            var v = -2 * u + 2;
            return 1 - v * v * v * 0.5f;
        }

        private Color Fade(float outside)
        {
            if (_fadeInk != _ink)
            {
                _fadeInk = _ink;

                for (int i = 0; i < _fade.Length; i++)
                {
                    _fade[i] = QrShareColors.MixOklab(_ink, Colors.White, i / (float)(_fade.Length - 1));
                }
            }

            return _fade[(int)(outside * (_fade.Length - 1) + 0.5f)];
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return Color.FromArgb((byte)(color.A * Math.Clamp(alpha, 0, 1)), color.R, color.G, color.B);
        }

        private sealed class Code
        {
            private readonly int _replaceFrom;
            private readonly int _replaceTill;
            private readonly QrBuffer _data;
            private Color _maskInk;

            public Code(string url)
            {
                Url = url;

                _data = QrBuffer.FromString(url);
                _replaceFrom = _data.ReplaceFrom;
                _replaceTill = _data.ReplaceTill;

                N = _data.Size;
                Cell = CodeSize / N;

                Dark = new bool[N * N];
                Noise = new float[N * N];
                Delay = new float[N * N];
                Spawned = new bool[N * N];
                Settled = new bool[N * N];

                var values = _data.Values;
                var mid = (N - 1) / 2f;
                var maxDistance = mid * 1.4142f;

                // Seeded by the link, so the same code always breaks up the same way.
                var random = new Random(StableHash(url));

                for (int y = 0; y < N; y++)
                {
                    for (int x = 0; x < N; x++)
                    {
                        var i = y * N + x;
                        var noise = (float)random.NextDouble();
                        var distance = MathF.Sqrt((x - mid) * (x - mid) + (y - mid) * (y - mid)) / maxDistance;
                        var spread = noise * 3.17f - MathF.Floor(noise * 3.17f);

                        Dark[i] = values[i];
                        Noise[i] = noise;

                        // The finders come in a little earlier, as anchors.
                        Delay[i] = IsFinder(x, y)
                            ? distance * 0.5f * 0.85f
                            : distance * 0.5f + spread * 0.06f;
                    }
                }
            }

            public string Url { get; }

            public int N { get; }

            public float Cell { get; }

            public bool[] Dark { get; }

            public float[] Noise { get; }

            public float[] Delay { get; }

            public bool[] Spawned { get; }

            public CanvasRenderTarget Mask { get; private set; }

            public bool[] Settled { get; }

            private int _settledCount;
            private CanvasRenderTarget _reveal;
            private AlphaMaskEffect _revealed;

            // The cells the mask holds now, so a frame only draws the ones that changed.
            private bool[] _revealCells;

            public bool IsLogo(int x, int y)
            {
                return x >= _replaceFrom && x < _replaceTill && y >= _replaceFrom && y < _replaceTill;
            }

            public bool IsLogo(int i)
            {
                return IsLogo(i % N, i / N);
            }

            public void ClearSettled()
            {
                Array.Clear(Settled, 0, Settled.Length);
                _settledCount = 0;
            }

            public void Settle(int i)
            {
                if (!Settled[i])
                {
                    Settled[i] = true;
                    _settledCount++;
                }
            }

            // The settled modules as one image: the code drawn whole through a mask of their cells. The mask is
            // filled aliased, so neighbouring cells tile exactly and nothing shows where they meet.
            public void DrawSettled(CanvasControl sender, CanvasDrawingSession ds, float left, float top, float opacity)
            {
                if (_settledCount == 0 || Mask == null)
                {
                    return;
                }

                if (_settledCount == Settled.Length - LogoCells)
                {
                    ds.DrawImage(Mask, left, top, new Rect(0, 0, CodeSize, CodeSize), opacity);
                    return;
                }

                CanvasDrawingSession reveal = null;

                if (_reveal == null)
                {
                    _reveal = new CanvasRenderTarget(sender, CodeSize, CodeSize);
                    _revealCells ??= new bool[Settled.Length];
                    Array.Clear(_revealCells, 0, _revealCells.Length);

                    reveal = BeginReveal();
                    reveal.Clear(Colors.Transparent);
                }

                try
                {
                    for (int i = 0; i < Settled.Length; i++)
                    {
                        if (Settled[i] != _revealCells[i])
                        {
                            _revealCells[i] = Settled[i];

                            reveal ??= BeginReveal();
                            reveal.FillRectangle(i % N * Cell, i / N * Cell, Cell, Cell, Settled[i] ? Colors.White : Colors.Transparent);
                        }
                    }
                }
                finally
                {
                    reveal?.Dispose();
                }

                // Both targets live until Release, which drops the effect with them.
                _revealed ??= new AlphaMaskEffect { Source = Mask, AlphaMask = _reveal };

                ds.DrawImage(_revealed, left, top, new Rect(0, 0, CodeSize, CodeSize), opacity);
            }

            // Copy, so a cleared cell is written transparent rather than drawn over with nothing.
            private CanvasDrawingSession BeginReveal()
            {
                var reveal = _reveal.CreateDrawingSession();
                reveal.Antialiasing = CanvasAntialiasing.Aliased;
                reveal.Blend = CanvasBlend.Copy;

                return reveal;
            }

            private int LogoCells =>(_replaceTill - _replaceFrom) * (_replaceTill - _replaceFrom);

            private bool IsFinder(int x, int y)
            {
                return (x < 7 && y < 7) || (x >= N - 7 && y < 7) || (x < 7 && y >= N - 7);
            }

            public void ResetSpawned()
            {
                Array.Clear(Spawned, 0, Spawned.Length);
            }

            // The code in its ink, rendered once per device and colour.
            public void Prepare(CanvasControl sender, Color ink)
            {
                if (Mask != null && _maskInk == ink)
                {
                    return;
                }

                Mask ??= new CanvasRenderTarget(sender, CodeSize, CodeSize);
                _maskInk = ink;

                using var geometry = QrCode.CreateCanvasGeometry(sender, _data, CodeSize);
                using var ds = Mask.CreateDrawingSession();

                ds.Clear(Colors.Transparent);
                ds.FillGeometry(geometry, ink);
            }

            public void Release()
            {
                Mask?.Dispose();
                Mask = null;

                _reveal?.Dispose();
                _reveal = null;

                _revealed?.Dispose();
                _revealed = null;
            }

            private static int StableHash(string value)
            {
                unchecked
                {
                    var hash = (int)2166136261;

                    foreach (var c in value)
                    {
                        hash = (hash ^ c) * 16777619;
                    }

                    return hash;
                }
            }
        }
    }
}
