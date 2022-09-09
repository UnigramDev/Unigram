using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Numerics;
using Unigram.Charts;
using Unigram.Common;
using Unigram.Native.Calls;
using Unigram.Services;
using Unigram.Views;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Controls
{
    public sealed partial class GroupCallActiveHeader : UserControl
    {
        private FragmentContextViewWavesDrawable _drawable;

        private IGroupCallService _service;

        public GroupCallActiveHeader()
        {
            InitializeComponent();
        }

        public void Update(IGroupCallService service)
        {
#if ENABLE_CALLS
            if (_service?.Manager != null)
            {
                _service.MutedChanged -= OnMutedChanged;
                _service.Manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;
            }

            if (service?.Manager != null && service?.Chat != null && service?.Call != null)
            {
                _service = service;
                _service.MutedChanged += OnMutedChanged;
                _service.Manager.AudioLevelsUpdated += OnAudioLevelsUpdated;

                TitleInfo.Text = service.Call.Title.Length > 0 ? service.Call.Title : service.ClientService.GetTitle(service.Chat);
                Audio.IsChecked = !_service.IsMuted;
                Automation.SetToolTip(Audio, _service.IsMuted ? Strings.Resources.VoipGroupUnmute : Strings.Resources.VoipGroupMute);
            }
        }

        private void OnMutedChanged(object sender, EventArgs e)
        {
            if (sender is VoipGroupManager service && service.IsMuted is bool muted)
            {
                this.BeginOnUIThread(() =>
                {
                    Audio.IsChecked = !muted;
                    Automation.SetToolTip(Audio, muted ? Strings.Resources.VoipGroupUnmute : Strings.Resources.VoipGroupMute);
                });
            }
        }

        private void OnAudioLevelsUpdated(VoipGroupManager sender, IReadOnlyDictionary<int, KeyValuePair<float, bool>> args)
        {
            if (_drawable != null)
            {
                var average = 0f;

                foreach (var level in args)
                {
                    average = MathF.Max(average, level.Key == 0 && sender.IsMuted ? 0 : level.Value.Key);
                }

                _drawable.setAmplitude(MathF.Max(0, MathF.Log(average, short.MaxValue / 4000)));
            }
#endif
        }

        private void CanvasControl_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
#if ENABLE_CALLS
            if (_drawable == null)
            {
                _drawable ??= new FragmentContextViewWavesDrawable(args.DrawingSession);
                _drawable.addParent(sender);
            }

            if (_service?.Manager != null)
            {
                _drawable.setState(_service.IsMuted ? 1 : 0);
                _drawable.draw(0, 40, (float)sender.Size.Width, 40, args.DrawingSession, sender, 2);
            }

            sender.Invalidate();
#endif
        }

        private void Audio_Click(object sender, RoutedEventArgs e)
        {
#if ENABLE_CALLS
            var service = _service;
            if (service != null)
            {
                service.IsMuted = !service.IsMuted;
            }
#endif
        }

        private async void Dismiss_Click(object sender, RoutedEventArgs e)
        {
            var service = _service;
            if (service != null)
            {
                await service.ConsolidateAsync();
                await service.LeaveAsync();
            }
        }

        private async void Title_Click(object sender, RoutedEventArgs e)
        {
            var service = _service;
            if (service != null)
            {
                await service.ShowAsync();
            }
        }
    }

    public class FragmentContextViewWavesDrawable
    {
        private float amplitude;
        private float amplitude2;
        private float animateAmplitudeDiff;
        private float animateAmplitudeDiff2;
        private float animateToAmplitude;
        WeavingState currentState;
        private long lastUpdateTime;
        readonly LineBlobDrawable lineBlobDrawable = new LineBlobDrawable(5);
        readonly LineBlobDrawable lineBlobDrawable1 = new LineBlobDrawable(7);
        readonly LineBlobDrawable lineBlobDrawable2 = new LineBlobDrawable(8);
        Color paint = Colors.Red;
        CanvasRadialGradientBrush shader;
        readonly List<CanvasControl> parents = new List<CanvasControl>();
        WeavingState pausedState;
        WeavingState previousState;
        float progressToState = 1.0f;
        readonly WeavingState[] states = new WeavingState[2];

        CanvasRenderTarget target;

        public FragmentContextViewWavesDrawable(CanvasDrawingSession canvas)
        {
            for (int i = 0; i < 2; i++)
            {
                states[i] = new WeavingState(canvas, i);
            }
        }

        public void draw(float x, float y, float width, float height, CanvasDrawingSession canvas, CanvasControl view, float f5)
        {
            bool z;
            int i;
            float f6;
            CanvasControl view2 = view;
            checkColors();
            if (view2 == null)
            {
                z = false;
            }
            else
            {
                z = parents.Count > 0 && view2 == parents[0];
            }
            //if (f2 <= f4)
            {
                long j = 0;
                if (z)
                {
                    long elapsedRealtime = Environment.TickCount;
                    long j2 = elapsedRealtime - lastUpdateTime;
                    lastUpdateTime = elapsedRealtime;
                    j = j2 > 20 ? 17 : j2;
                    float f7 = animateToAmplitude;
                    float f8 = amplitude;
                    if (f7 != f8)
                    {
                        float f9 = animateAmplitudeDiff;
                        float f10 = f8 + j * f9;
                        amplitude = f10;
                        if (f9 > 0.0f)
                        {
                            if (f10 > f7)
                            {
                                amplitude = f7;
                            }
                        }
                        else if (f10 < f7)
                        {
                            amplitude = f7;
                        }
                        //view.Invalidate();
                    }
                    float f11 = animateToAmplitude;
                    float f12 = amplitude2;
                    if (f11 != f12)
                    {
                        float f13 = animateAmplitudeDiff2;
                        float f14 = f12 + j * f13;
                        amplitude2 = f14;
                        if (f13 > 0.0f)
                        {
                            if (f14 > f11)
                            {
                                amplitude2 = f11;
                            }
                        }
                        else if (f14 < f11)
                        {
                            amplitude2 = f11;
                        }
                        //view.Invalidate();
                    }
                    if (previousState != null)
                    {
                        float f15 = progressToState + j / 250.0f;
                        progressToState = f15;
                        if (f15 > 1.0f)
                        {
                            progressToState = 1.0f;
                            previousState = null;
                        }
                        //view.Invalidate();
                    }
                }
                long j3 = j;
                int i2 = 0;
                while (i2 < 2)
                {
                    if (i2 == 0 && previousState == null)
                    {
                        i = i2;
                    }
                    else
                    {
                        if (i2 == 0)
                        {
                            f6 = 1.0f - progressToState;
                            if (z)
                            {
                                previousState.update((int)(height - y), (int)(width - x), j3, amplitude);
                            }
                            //this.paint.setShader(this.previousState.shader);
                            shader = previousState.shader;
                        }
                        else
                        {
                            WeavingState weavingState = currentState;
                            if (weavingState != null)
                            {
                                f6 = previousState != null ? progressToState : 1.0f;
                                if (z)
                                {
                                    weavingState.update((int)(height - y), (int)(width - x), j3, amplitude);
                                }
                                //this.paint.setShader(this.currentState.shader);
                                shader = currentState.shader;
                            }
                            else
                            {
                                return;
                            }
                        }
                        float f16 = f6;
                        lineBlobDrawable.minRadius = 0.0f;
                        lineBlobDrawable.maxRadius = 2.0f + 2.0f * amplitude;
                        lineBlobDrawable1.minRadius = 0.0f;
                        lineBlobDrawable1.maxRadius = 3.0f + 9.0f * amplitude;
                        lineBlobDrawable2.minRadius = 0.0f;
                        lineBlobDrawable2.maxRadius = 3.0f + 9.0f * amplitude;
                        lineBlobDrawable.update(amplitude, 0.3f);
                        lineBlobDrawable1.update(amplitude, 0.7f);
                        lineBlobDrawable2.update(amplitude, 0.7f);
                        if (i2 == 1)
                        {
                            paint.A = (byte)(255.0f * f16);
                        }
                        else
                        {
                            paint.A = 255;
                        }
                        i = i2;

                        if (target == null || target.Size.Width != width)
                        {
                            target = new CanvasRenderTarget(canvas, width, height);
                        }

                        using (var session = target.CreateDrawingSession())
                        {
                            session.Clear(Colors.Transparent);

                            lineBlobDrawable.Draw(x, y, width, height, session, paint, y, f5);
                            paint.A = (byte)(f16 * 76.0f);
                            float dp = (float)6.0f * amplitude2;
                            float dp2 = (float)6.0f * amplitude2;
                            float f22 = x;
                            lineBlobDrawable1.Draw(f22, y - dp, width, height, session, paint, y, f5);
                            lineBlobDrawable2.Draw(f22, y - dp2, width, height, session, paint, y, f5);
                        }

                        using (var layer = canvas.CreateLayer(new CanvasImageBrush(canvas, target)))
                        {
                            canvas.FillRectangle(0, 0, width, height, shader);
                        }
                    }
                    i2 = i + 1;
                }
            }
        }

        private void checkColors()
        {
            int i = 0;
            while (true)
            {
                WeavingState[] weavingStateArr = states;
                if (i < weavingStateArr.Length)
                {
                    weavingStateArr[i].checkColor();
                    i++;
                }
                else
                {
                    return;
                }
            }
        }

        public void setState(int i)
        {
            WeavingState weavingState = currentState;
            if (weavingState != null && weavingState.currentState == i)
            {
                return;
            }
            //if (VoIPService.getSharedInstance() == null && this.currentState == null)
            //{
            //    this.currentState = this.pausedState;
            //    return;
            //}
            WeavingState weavingState2 = currentState;
            previousState = weavingState2;
            currentState = states[i];
            if (weavingState2 != null)
            {
                progressToState = 0.0f;
            }
            else
            {
                progressToState = 1.0f;
            }
        }

        public void setAmplitude(float f)
        {
            animateToAmplitude = f;
            float f2 = amplitude;
            animateAmplitudeDiff = (f - f2) / 250.0f;
            animateAmplitudeDiff2 = (f - f2) / 120.0f;
        }

        public void addParent(CanvasControl view)
        {
            if (!parents.Contains(view))
            {
                parents.Add(view);
            }
        }

        public void removeParent(CanvasControl view)
        {
            parents.Remove(view);
            if (parents.IsEmpty())
            {
                pausedState = currentState;
                currentState = null;
                previousState = null;
            }
        }

        public class WeavingState
        {
            readonly int color1;
            readonly int color2;
            /* access modifiers changed from: private */
            public readonly int currentState;
            private float duration;
            private readonly Matrix matrix = new Matrix();
            private readonly Random random = new Random();
            public CanvasRadialGradientBrush shader;
            private float startX;
            private float startY;
            private float targetX = -1.0f;
            private float targetY = -1.0f;
            private float time;

            public WeavingState(CanvasDrawingSession canvas, int i)
            {
                currentState = i;
                createGradients(canvas);
            }

            private void createGradients(CanvasDrawingSession canvas)
            {
                if (currentState == 0)
                {
                    shader = new CanvasRadialGradientBrush(canvas, Color.FromArgb(0xFF, 0x00, 0x78, 0xff), Color.FromArgb(0xFF, 0x33, 0xc6, 0x59));
                }
                else
                {
                    shader = new CanvasRadialGradientBrush(canvas, Color.FromArgb(0xFF, 0x59, 0xc7, 0xf8), Color.FromArgb(0xFF, 0x00, 0x78, 0xff));
                }

                shader.RadiusX = MathF.Sqrt(200 * 200 + 200 * 200);
                shader.RadiusY = MathF.Sqrt(200 * 200 + 200 * 200);
                shader.Center = new Vector2(300, 0);

                //if (this.currentState == 0)
                //{
                //    int color = Theme.getColor("voipgroup_muteButton");
                //    this.color1 = color;
                //    int color3 = Theme.getColor("voipgroup_unmuteButton");
                //    this.color2 = color3;
                //    this.shader = new RadialGradient(200.0f, 200.0f, 200.0f, new int[] { color, color3 }, (float[])null, Shader.TileMode.CLAMP);
                //    return;
                //}
                //int color4 = Theme.getColor("voipgroup_unmuteButton2");
                //this.color1 = color4;
                //int color5 = Theme.getColor("voipgroup_unmuteButton");
                //this.color2 = color5;
                //this.shader = new RadialGradient(200.0f, 200.0f, 200.0f, new int[] { color4, color5 }, (float[])null, Shader.TileMode.CLAMP);
            }

            public void update(int i, int i2, long j, float f)
            {
                float f2 = duration;
                if (f2 == 0.0f || time >= f2)
                {
                    duration = random.Next(700) + 500;
                    time = 0.0f;
                    if (targetX == -1.0f)
                    {
                        if (currentState == 0)
                        {
                            targetX = random.Next(100) * 0.2f / 100.0f - 14.4f;
                            targetY = random.Next(100) * 0.3f / 100.0f + 0.7f;
                        }
                        else
                        {
                            targetX = random.Next(100) / 100.0f * 0.2f + 1.1f;
                            targetY = random.Next(100) * 4.0f / 100.0f;
                        }
                    }
                    startX = targetX;
                    startY = targetY;
                    if (currentState == 0)
                    {
                        targetX = random.Next(100) * 0.2f / 100.0f - 14.4f;
                        targetY = random.Next(100) * 0.3f / 100.0f + 0.7f;
                    }
                    else
                    {
                        targetX = random.Next(100) / 100.0f * 0.2f + 1.1f;
                        targetY = random.Next(100) * 2.0f / 100.0f;
                    }
                }
                float f3 = j;
                float f4 = time + (BlobDrawable.GRADIENT_SPEED_MIN + 0.5f) * f3 + f3 * BlobDrawable.GRADIENT_SPEED_MAX * 2.0f * f;
                time = f4;
                float f5 = duration;
                if (f4 > f5)
                {
                    time = f5;
                }
                float interpolation = CubicBezierInterpolator.EASE_OUT.getInterpolation(time / f5);
                float f6 = i2;
                float f7 = startX;
                float f8 = (f7 + (targetX - f7) * interpolation) * f6 - 200.0f;
                float f9 = startY;
                float f10 = i * (f9 + (targetY - f9) * interpolation) - 200.0f;
                float f11 = f6 / 400.0f * (currentState == 0 ? 3.0f : 1.5f);
                //this.matrix.reset();
                //this.matrix.postTranslate(f8, f10);
                //this.matrix.postScale(f11, f11, f8 + 200.0f, f10 + 200.0f);
                shader.Transform = Matrix3x2.Multiply(Matrix3x2.CreateTranslation(f8, f10), Matrix3x2.CreateScale(f11, f11, new Vector2(f8 + 200.0f, f10 + 200.0f)));
            }

            public void checkColor()
            {
                //if (this.currentState == 0)
                //{
                //    if (this.color1 != Theme.getColor("voipgroup_muteButton") || this.color2 != Theme.getColor("voipgroup_unmuteButton"))
                //    {
                //        createGradients();
                //    }
                //}
                //else if (this.color1 != Theme.getColor("voipgroup_unmuteButton2") || this.color2 != Theme.getColor("voipgroup_unmuteButton"))
                //{
                //    createGradients();
                //}
            }
        }
    }


    public class LineBlobDrawable
    {
        private readonly float N;
        public float maxRadius;
        public float minRadius;
        //private Path path = new Path();
        private readonly float[] progress;
        private readonly float[] radius;
        private readonly float[] radiusNext;
        private readonly Random random;
        private readonly float[] speed;

        public LineBlobDrawable(int i)
        {
            random = new Random();
            N = i;
            int i2 = i + 1;
            radius = new float[i2];
            radiusNext = new float[i2];
            progress = new float[i2];
            speed = new float[i2];
            for (int i3 = 0; i3 <= N; i3++)
            {
                generateBlob(radius, i3);
                generateBlob(radiusNext, i3);
                progress[i3] = 0.0f;
            }
        }

        private void generateBlob(float[] fArr, int i)
        {
            float f = maxRadius;
            float f2 = minRadius;
            fArr[i] = f2 + MathF.Abs(random.Next() % 100.0f / 100.0f) * (f - f2);
            float[] fArr2 = speed;
            double abs = MathF.Abs(random.Next() % 100.0f) / 100.0f;
            fArr2[i] = (float)(abs * 0.003d + 0.017d);
        }

        public void update(float f, float f2)
        {
            for (int i = 0; i <= N; i++)
            {
                float[] fArr = progress;
                float f3 = fArr[i];
                float[] fArr2 = speed;
                fArr[i] = f3 + fArr2[i] * BlobDrawable.MIN_SPEED + fArr2[i] * f * BlobDrawable.MAX_SPEED * f2;
                if (fArr[i] >= 1.0f)
                {
                    fArr[i] = 0.0f;
                    float[] fArr3 = radius;
                    float[] fArr4 = radiusNext;
                    fArr3[i] = fArr4[i];
                    generateBlob(fArr4, i);
                }
            }
        }

        public void Draw(float f, float f2, float f3, float f4, CanvasDrawingSession canvas, Color paint, float f5, float f6)
        {
            var builder = new CanvasPathBuilder(canvas);
            builder.BeginFigure(f3, f4);
            builder.AddLine(f, f4);
            int i = 0;
            while (true)
            {
                float f10 = i;
                float f11 = N;
                if (f10 <= f11)
                {
                    if (i == 0)
                    {
                        float f12 = progress[i];
                        builder.AddLine(f, (f2 - (radius[i] * (1.0f - f12) + radiusNext[i] * f12)) * f6 + f5 * (1.0f - f6));
                    }
                    else
                    {
                        float[] fArr = progress;
                        int i2 = i - 1;
                        float f13 = fArr[i2];
                        float[] fArr2 = radius;
                        float f14 = fArr2[i2] * (1.0f - f13);
                        float[] fArr3 = radiusNext;
                        float f15 = fArr[i];
                        float f16 = fArr2[i] * (1.0f - f15) + fArr3[i] * f15;
                        float f17 = f3 - f;
                        float f18 = f17 / f11 * i2;
                        float f19 = f17 / f11 * f10;
                        float f20 = f18 + (f19 - f18) / 2.0f;
                        float f21 = (1.0f - f6) * f5;
                        float f22 = (f2 - f16) * f6 + f21;
                        builder.AddCubicBezier(new Vector2(f20, (f2 - (f14 + fArr3[i2] * f13)) * f6 + f21), new Vector2(f20, f22), new Vector2(f19, f22));
                        if (f10 == N)
                        {
                            builder.AddLine(f3, f4);
                        }
                    }
                    i++;
                }
                else
                {
                    builder.EndFigure(CanvasFigureLoop.Closed);
                    canvas.FillGeometry(CanvasGeometry.CreatePath(builder), paint);
                    return;
                }
            }
        }
    }
}
