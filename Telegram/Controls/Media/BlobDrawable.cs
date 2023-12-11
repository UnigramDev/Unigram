using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Numerics;
using Telegram.Common;
using Windows.UI;

namespace Telegram.Controls.Media
{
    public class BlobDrawable
    {
        public static float MAX_SPEED = 8.2f;
        public static float MIN_SPEED = 0.8f;
        public static float AMPLITUDE_SPEED = 0.33f;

        public static float SCALE_BIG = 0.807f;
        public static float SCALE_SMALL = 0.704f;

        public static float SCALE_BIG_MIN = 0.878f;
        public static float SCALE_SMALL_MIN = 0.926f;

        public static float FORM_BIG_MAX = 0.6f;
        public static float FORM_SMALL_MAX = 0.6f;

        public static float GLOBAL_SCALE = 1f;

        public static float FORM_BUTTON_MAX = 0f;

        public static float GRADIENT_SPEED_MIN = 0.5f;
        public static float GRADIENT_SPEED_MAX = 0.01f;

        public static float LIGHT_GRADIENT_SIZE = 0.5f;

        public float minRadius;
        public float maxRadius;

        public Color paint = Colors.Red;

        private readonly float[] _radius;
        private readonly float[] _angle;
        private readonly float[] _radiusNext;
        private readonly float[] _angleNext;
        private readonly float[] _progress;
        private readonly float[] _speed;

        private readonly Vector2[] _pointStart = new Vector2[2];
        private readonly Vector2[] _pointEnd = new Vector2[2];

        private readonly Random _random = new Random();

        private readonly float N;
        private readonly float L;
        public float cubicBezierK = 1f;

        private Matrix3x2 _m;

        private readonly int liteFlag;

        //public BlobDrawable(int n)
        //{
        //    this(n, LiteMode.FLAG_CALLS_ANIMATIONS);
        //}

        public BlobDrawable(int n, int liteFlag = 0)
        {
            N = n;
            L = (float)((4.0 / 3.0) * MathF.Tan(MathF.PI / (2 * N)));
            _radius = new float[n];
            _angle = new float[n];

            _radiusNext = new float[n];
            _angleNext = new float[n];
            _progress = new float[n];
            _speed = new float[n];

            for (int i = 0; i < N; i++)
            {
                GenerateBlob(_radius, _angle, i);
                GenerateBlob(_radiusNext, _angleNext, i);
                _progress[i] = 0;
            }

            this.liteFlag = liteFlag;
        }

        private void GenerateBlob(float[] radius, float[] angle, int i)
        {
            float angleDif = 360f / N * 0.05f;
            float radDif = maxRadius - minRadius;
            radius[i] = minRadius + MathF.Abs(((_random.Next() % 100f) / 100f)) * radDif;
            angle[i] = 360f / N * i + ((_random.Next() % 100f) / 100f) * angleDif;
            _speed[i] = (float)(0.017 + 0.003 * (MathF.Abs(_random.Next() % 100f) / 100f));
        }

        public void Update(float amplitude, float speedScale)
        {
            //if (!LiteMode.isEnabled(liteFlag))
            //{
            //    return;
            //}
            for (int i = 0; i < N; i++)
            {
                _progress[i] += (_speed[i] * MIN_SPEED) + amplitude * _speed[i] * MAX_SPEED * speedScale;
                if (_progress[i] >= 1f)
                {
                    _progress[i] = 0;
                    _radius[i] = _radiusNext[i];
                    _angle[i] = _angleNext[i];
                    GenerateBlob(_radiusNext, _angleNext, i);
                }
            }
        }

        public void Draw(CanvasDrawingSession canvas, float cX, float cY/*, Paint paint*/)
        {
            //if (!LiteMode.isEnabled(liteFlag))
            //{
            //    return;
            //}

            using var path = new CanvasPathBuilder(canvas);

            for (int i = 0; i < N; i++)
            {
                float progress = this._progress[i];
                int nextIndex = i + 1 < N ? i + 1 : 0;
                float progressNext = this._progress[nextIndex];
                float r1 = _radius[i] * (1f - progress) + _radiusNext[i] * progress;
                float r2 = _radius[nextIndex] * (1f - progressNext) + _radiusNext[nextIndex] * progressNext;
                float angle1 = _angle[i] * (1f - progress) + _angleNext[i] * progress;
                float angle2 = _angle[nextIndex] * (1f - progressNext) + _angleNext[nextIndex] * progressNext;

                float l = L * (MathF.Min(r1, r2) + (MathF.Max(r1, r2) - MathF.Min(r1, r2)) / 2f) * cubicBezierK;

                _pointStart[0].X = cX;
                _pointStart[0].Y = cY - r1;
                _pointStart[1].X = cX + l;
                _pointStart[1].Y = cY - r1;

                _m = SetRotate(angle1, cX, cY);
                _pointStart[0] = Vector2.Transform(_pointStart[0], _m);
                _pointStart[1] = Vector2.Transform(_pointStart[1], _m);

                _pointEnd[0].X = cX;
                _pointEnd[0].Y = cY - r2;
                _pointEnd[1].X = cX - l;
                _pointEnd[1].Y = cY - r2;

                _m = SetRotate(angle2, cX, cY);
                _pointEnd[0] = Vector2.Transform(_pointEnd[0], _m);
                _pointEnd[1] = Vector2.Transform(_pointEnd[1], _m);

                if (i == 0)
                {
                    path.BeginFigure(_pointStart[0]);
                }

                path.AddCubicBezier(_pointStart[1], _pointEnd[1], _pointEnd[0]);
            }

            paint.A = 68;

            path.EndFigure(CanvasFigureLoop.Closed);
            canvas.FillGeometry(CanvasGeometry.CreatePath(path), paint);
        }

        private static Matrix3x2 SetRotate(float degree, float px, float py)
        {
            return Matrix3x2.CreateRotation(MathFEx.ToRadians(degree), new Vector2(px, py));
        }

        public void GenerateBlob()
        {
            for (int i = 0; i < N; i++)
            {
                GenerateBlob(_radius, _angle, i);
                GenerateBlob(_radiusNext, _angleNext, i);
                _progress[i] = 0;
            }
        }


        private float animateToAmplitude;
        public float amplitude;
        private float animateAmplitudeDiff;

        private const float ANIMATION_SPEED_WAVE_HUGE = 0.65f;
        private const float ANIMATION_SPEED_WAVE_SMALL = 0.45f;
        private const float animationSpeed = 1f - ANIMATION_SPEED_WAVE_HUGE;
        private const float animationSpeedTiny = 1f - ANIMATION_SPEED_WAVE_SMALL;

        public void SetValue(float value)
        {
            amplitude = value;
        }

        public void SetValue(float value, bool isBig)
        {
            animateToAmplitude = value;
            //if (!LiteMode.isEnabled(liteFlag))
            //{
            //    return;
            //}
            if (isBig)
            {
                if (animateToAmplitude > amplitude)
                {
                    animateAmplitudeDiff = (animateToAmplitude - amplitude) / (100f + 300f * animationSpeed);
                }
                else
                {
                    animateAmplitudeDiff = (animateToAmplitude - amplitude) / (100 + 500f * animationSpeed);
                }
            }
            else
            {
                if (animateToAmplitude > amplitude)
                {
                    animateAmplitudeDiff = (animateToAmplitude - amplitude) / (100f + 400f * animationSpeedTiny);
                }
                else
                {
                    animateAmplitudeDiff = (animateToAmplitude - amplitude) / (100f + 500f * animationSpeedTiny);
                }
            }
        }

        public void UpdateAmplitude(ulong dt)
        {
            if (animateToAmplitude != amplitude)
            {
                amplitude += animateAmplitudeDiff * dt;
                if (animateAmplitudeDiff > 0)
                {
                    if (amplitude > animateToAmplitude)
                    {
                        amplitude = animateToAmplitude;
                    }
                }
                else
                {
                    if (amplitude < animateToAmplitude)
                    {
                        amplitude = animateToAmplitude;
                    }
                }
            }
        }
    }
}
