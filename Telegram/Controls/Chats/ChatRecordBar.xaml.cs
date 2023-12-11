using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Numerics;
using Telegram.Charts;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Chats
{
    public sealed partial class ChatRecordBar : GridEx
    {
        private readonly DispatcherTimer _elapsedTimer;
        private readonly Visual _ellipseVisual;
        private readonly Visual _elapsedVisual;
        private readonly Visual _slideVisual;
        private readonly Visual _recordVisual;
        private readonly Visual _rootVisual;

        private ChatRecordCircle _drawable;

        private CanvasControl ChatRecordCanvas;

        public ChatRecordBar()
        {
            InitializeComponent();

            ElementCompositionPreview.SetIsTranslationEnabled(Ellipse, true);

            _ellipseVisual = ElementCompositionPreview.GetElementVisual(Ellipse);
            _elapsedVisual = ElementCompositionPreview.GetElementVisual(ElapsedPanel);
            _slideVisual = ElementCompositionPreview.GetElementVisual(SlidePanel);
            _recordVisual = ElementCompositionPreview.GetElementVisual(this);
            _rootVisual = ElementCompositionPreview.GetElementVisual(this);

            _ellipseVisual.CenterPoint = new Vector3(80);
            _ellipseVisual.Scale = new Vector3(0);

            _elapsedTimer = new DispatcherTimer();
            _elapsedTimer.Interval = TimeSpan.FromMilliseconds(100);
            _elapsedTimer.Tick += (s, args) =>
            {
                ElapsedLabel.Text = ControlledButton.Elapsed.ToString("m\\:ss\\.ff");
            };

            _drawable = new ChatRecordCircle();
            _drawable.Update(Theme.Accent);

            Connected += OnLoaded;
            Disconnected += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _drawable.Update(Theme.Accent);

            // TODO: lazy load on start recording?
            ChatRecordCanvas = new CanvasControl
            {
                Width = 180,
                Height = 180
            };

            ChatRecordCanvas.Draw += ChatRecordCanvas_Draw;
            Ellipse.Children.Insert(0, ChatRecordCanvas);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ChatRecordCanvas.Draw -= ChatRecordCanvas_Draw;
            ChatRecordCanvas.RemoveFromVisualTree();
            ChatRecordCanvas = null;
        }

        private void ElapsedPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var point = _elapsedVisual.Offset;
            point.X = (float)-e.NewSize.Width;

            _elapsedVisual.Offset = point;
            _elapsedVisual.Size = e.NewSize.ToVector2();
        }

        private void SlidePanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var point = _slideVisual.Offset;
            point.X = (float)e.NewSize.Width + 36;

            _slideVisual.Opacity = 0;
            _slideVisual.Offset = point;
            _slideVisual.Size = e.NewSize.ToVector2();
        }

        private void ButtonCancelRecording_Click(object sender, RoutedEventArgs e)
        {
            ControlledButton.StopRecording(true);
        }

        private void VoiceButton_QuantumProcessed(object sender, float amplitude)
        {
            //_drawable ??= new AvatarWavesDrawable(true, true);
            _drawable.SetAmplitude(ChatRecordCanvas, amplitude * 1200);
        }

        private void ChatRecordCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            _drawable.Draw(sender, args.DrawingSession);
        }

        private void ChatRecordLocked_Click(object sender, RoutedEventArgs e)
        {
            ControlledButton.Release();
        }

        private ChatRecordButton _controlledButton;
        public ChatRecordButton ControlledButton
        {
            get => _controlledButton;
            set => SetControlledButton(value);
        }

        private void SetControlledButton(ChatRecordButton value)
        {
            if (_controlledButton != null)
            {
                return;
            }

            _controlledButton = value;

            _controlledButton.RecordingStarted += VoiceButton_RecordingStarted;
            _controlledButton.RecordingStopped += VoiceButton_RecordingStopped;
            _controlledButton.RecordingLocked += VoiceButton_RecordingLocked;
            _controlledButton.QuantumProcessed += VoiceButton_QuantumProcessed;
            _controlledButton.ManipulationDelta += VoiceButton_ManipulationDelta;
        }

        private void VoiceButton_RecordingStarted(object sender, EventArgs e)
        {
            // TODO: video message
            Visibility = Visibility.Visible;

            ChatRecordPopup.IsOpen = true;
            ChatRecordGlyph.Text = ControlledButton.Mode == ChatRecordMode.Video
                ? Icons.VideoNoteFilled24
                : Icons.MicOnFilled24;

            var slideWidth = SlidePanel.ActualSize.X;
            var elapsedWidth = ElapsedPanel.ActualSize.X;

            _slideVisual.Opacity = 1;

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Start();
                AttachExpression();
            };

            var slideAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, slideWidth + 36);
            slideAnimation.InsertKeyFrame(1, 0);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var elapsedAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            elapsedAnimation.InsertKeyFrame(0, -elapsedWidth);
            elapsedAnimation.InsertKeyFrame(1, 0);
            elapsedAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var visibleAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            visibleAnimation.InsertKeyFrame(0, 0);
            visibleAnimation.InsertKeyFrame(1, 1);

            var ellipseAnimation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            ellipseAnimation.InsertKeyFrame(0, new Vector3(56f / 96f));
            ellipseAnimation.InsertKeyFrame(1, new Vector3(1));
            ellipseAnimation.Duration = TimeSpan.FromMilliseconds(200);

            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _elapsedVisual.StartAnimation("Offset.X", elapsedAnimation);
            _recordVisual.StartAnimation("Opacity", visibleAnimation);
            _ellipseVisual.StartAnimation("Scale", ellipseAnimation);

            batch.End();

            StartTyping?.Invoke(this, ControlledButton.IsChecked.Value ? new ChatActionRecordingVideoNote() : new ChatActionRecordingVoiceNote());
        }

        public event EventHandler<ChatAction> StartTyping;
        public event EventHandler CancelTyping;

        private void VoiceButton_RecordingStopped(object sender, EventArgs e)
        {
            //if (btnVoiceMessage.IsLocked)
            //{
            //    Poggers.Visibility = Visibility.Visible;
            //    Poggers.UpdateWaveform(btnVoiceMessage.GetWaveform());
            //    return;
            //}

            AttachExpression();

            var slidePosition = ActualSize.X - 48 - 36;
            var difference = slidePosition - ElapsedPanel.ActualSize.X;

            var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Stop();

                DetachExpression();

                ChatRecordPopup.IsOpen = false;

                Visibility = Visibility.Collapsed;
                ButtonCancelRecording.Visibility = Visibility.Collapsed;
                ElapsedLabel.Text = "0:00,0";

                var point = _slideVisual.Offset;
                point.X = _slideVisual.Size.X + 36;

                _slideVisual.Opacity = 0;
                _slideVisual.Offset = point;

                point = _elapsedVisual.Offset;
                point.X = -_elapsedVisual.Size.X;

                _elapsedVisual.Offset = point;

                _ellipseVisual.Properties.TryGetVector3("Translation", out point);
                point.Y = 0;

                _ellipseVisual.Properties.InsertVector3("Translation", point);
            };

            var slideAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, _slideVisual.Offset.X);
            slideAnimation.InsertKeyFrame(1, -slidePosition);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(200);

            var visibleAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            visibleAnimation.InsertKeyFrame(0, 1);
            visibleAnimation.InsertKeyFrame(1, 0);

            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _recordVisual.StartAnimation("Opacity", visibleAnimation);

            batch.End();

            CancelTyping?.Invoke(this, EventArgs.Empty);
        }

        private void VoiceButton_RecordingLocked(object sender, EventArgs e)
        {
            ChatRecordGlyph.Text = Icons.SendFilled;

            DetachExpression();

            var ellipseAnimation = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            ellipseAnimation.InsertKeyFrame(0, -57);
            ellipseAnimation.InsertKeyFrame(1, 0);

            _ellipseVisual.StartAnimation("Translation.Y", ellipseAnimation);

            ButtonCancelRecording.Visibility = Visibility.Visible;
            ControlledButton.Focus(FocusState.Programmatic);

            var point = _slideVisual.Offset;
            point.X = _slideVisual.Size.X + 36;

            _slideVisual.Opacity = 0;
            _slideVisual.Offset = point;
        }

        private void VoiceButton_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            Vector3 point;
            if (ControlledButton.IsLocked || !ControlledButton.IsRecording)
            {
                point = _slideVisual.Offset;
                point.X = 0;

                _slideVisual.Offset = point;

                _ellipseVisual.Properties.TryGetVector3("Translation", out point);
                point.Y = 0;

                _ellipseVisual.Properties.InsertVector3("Translation", point);

                return;
            }

            var cumulative = e.Cumulative.Translation.ToVector2();
            point = _slideVisual.Offset;
            point.X = Math.Min(0, cumulative.X);

            _slideVisual.Offset = point;

            if (point.X < -80)
            {
                e.Complete();
                ControlledButton.StopRecording(true);
                return;
            }

            _ellipseVisual.Properties.TryGetVector3("Translation", out point);
            point.Y = Math.Min(0, cumulative.Y);

            _ellipseVisual.Properties.InsertVector3("Translation", point);

            if (point.Y < -120)
            {
                e.Complete();
                ControlledButton.LockRecording();
            }
        }

        private void AttachExpression()
        {
            var elapsedExpression = Window.Current.Compositor.CreateExpressionAnimation("min(0, slide.Offset.X + ((root.Size.X - 48 - 36 - slide.Size.X) - elapsed.Size.X))");
            elapsedExpression.SetReferenceParameter("slide", _slideVisual);
            elapsedExpression.SetReferenceParameter("elapsed", _elapsedVisual);
            elapsedExpression.SetReferenceParameter("root", _rootVisual);

            var ellipseExpression = Window.Current.Compositor.CreateExpressionAnimation("Vector3(max(0, min(1, 1 + slide.Offset.X / (root.Size.X - 48 - 36))), max(0, min(1, 1 + slide.Offset.X / (root.Size.X - 48 - 36))), 1)");
            ellipseExpression.SetReferenceParameter("slide", _slideVisual);
            ellipseExpression.SetReferenceParameter("elapsed", _elapsedVisual);
            ellipseExpression.SetReferenceParameter("root", _rootVisual);

            _elapsedVisual.StopAnimation("Offset.X");
            _elapsedVisual.StartAnimation("Offset.X", elapsedExpression);

            _ellipseVisual.StopAnimation("Scale");
            _ellipseVisual.StartAnimation("Scale", ellipseExpression);
        }

        private void DetachExpression()
        {
            _elapsedVisual.StopAnimation("Offset.X");
            _ellipseVisual.StopAnimation("Scale");
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _rootVisual.Size = e.NewSize.ToVector2();
        }
    }

    public class ChatRecordCircle
    {
        private float _scale;
        private float _amplitude;
        private float _animateToAmplitude;
        private float _animateAmplitudeDiff;
        private ulong _lastUpdateTime;

        private Color _paint;

        private readonly BlobDrawable _tinyWaveDrawable = new(11/*, LiteMode.FLAGS_CHAT*/);
        private readonly BlobDrawable _bigWaveDrawable = new(12/*, LiteMode.FLAGS_CHAT*/);

        private readonly float _circleRadius = 41;
        private readonly float _circleRadiusAmplitude = 30;

        private float _wavesEnterAnimation = 0f;
        private bool _showWaves = true;

        public ChatRecordCircle()
        {
            _tinyWaveDrawable.minRadius = 47;
            _tinyWaveDrawable.maxRadius = 55;
            _tinyWaveDrawable.GenerateBlob();

            _bigWaveDrawable.minRadius = 47;
            _bigWaveDrawable.maxRadius = 55;
            _bigWaveDrawable.GenerateBlob();
        }

        public void Update(Color color)
        {
            _paint = color;
            _bigWaveDrawable.paint = Color.FromArgb(68, color.R, color.G, color.B);
            _tinyWaveDrawable.paint = Color.FromArgb(68, color.R, color.G, color.B);
        }

        public void SetAmplitude(CanvasControl canvas, double value)
        {
            _bigWaveDrawable.SetValue((float)(Math.Min(WaveDrawable.MAX_AMPLITUDE, value) / WaveDrawable.MAX_AMPLITUDE), true);
            _tinyWaveDrawable.SetValue((float)(Math.Min(WaveDrawable.MAX_AMPLITUDE, value) / WaveDrawable.MAX_AMPLITUDE), false);

            _animateToAmplitude = (float)(Math.Min(WaveDrawable.MAX_AMPLITUDE, value) / WaveDrawable.MAX_AMPLITUDE);
            _animateAmplitudeDiff = (_animateToAmplitude - _amplitude) / (100 + 500.0f * WaveDrawable.animationSpeedCircle);

            canvas.Invalidate();
        }

        public void SetScale(float value)
        {
            //scale = value;
            //invalidate();
        }

        public void Draw(CanvasControl test, CanvasDrawingSession canvas)
        {
            int cx = 80;
            int cy = 80;

            _scale = 1;

            float sc;
            float circleAlpha = 1f;
            if (_scale <= 0.5f)
            {
                sc = _scale / 0.5f;
            }
            else if (_scale <= 0.75f)
            {
                sc = 1.0f - (_scale - 0.5f) / 0.25f * 0.1f;
            }
            else
            {
                sc = 0.9f + (_scale - 0.75f) / 0.25f * 0.1f;
            }
            ulong dt = Logger.TickCount - _lastUpdateTime;
            if (_animateToAmplitude != _amplitude)
            {
                _amplitude += _animateAmplitudeDiff * dt;
                if (_animateAmplitudeDiff > 0)
                {
                    if (_amplitude > _animateToAmplitude)
                    {
                        _amplitude = _animateToAmplitude;
                    }
                }
                else
                {
                    if (_amplitude < _animateToAmplitude)
                    {
                        _amplitude = _animateToAmplitude;
                    }
                }
                test.Invalidate();
            }

            float slideToCancelScale = 1;
            //if (canceledByGesture)
            //{
            //    slideToCancelScale = 0.7f * CubicBezierInterpolator.EASE_OUT.getInterpolation(1f - slideToCancelProgress);
            //}
            //else
            //{
            //    slideToCancelScale = (0.7f + slideToCancelProgress * 0.3f);
            //}
            float radius = (_circleRadius + _circleRadiusAmplitude * _amplitude) * sc * slideToCancelScale;

            //if (LiteMode.isEnabled(LiteMode.FLAGS_CHAT))
            {
                _tinyWaveDrawable.minRadius = 47;
                _tinyWaveDrawable.maxRadius = 47 + 15 * BlobDrawable.FORM_SMALL_MAX;

                _bigWaveDrawable.minRadius = 50;
                _bigWaveDrawable.maxRadius = 50 + 12 * BlobDrawable.FORM_BIG_MAX;

                _bigWaveDrawable.UpdateAmplitude(dt);
                _bigWaveDrawable.Update(_bigWaveDrawable.amplitude, 1.01f);
                _tinyWaveDrawable.UpdateAmplitude(dt);
                _tinyWaveDrawable.Update(_tinyWaveDrawable.amplitude, 1.02f);
            }

            _lastUpdateTime = Logger.TickCount;
            float slideToCancelProgress1 = 1; //slideToCancelProgress > 0.7f ? 1f : slideToCancelProgress / 0.7f;

            //if (LiteMode.isEnabled(LiteMode.FLAGS_CHAT) && progressToSeekbarStep2 != 1 && exitProgress2 < 0.4f && slideToCancelProgress1 > 0 && !canceledByGesture)
            {
                if (_showWaves && _wavesEnterAnimation != 1f)
                {
                    _wavesEnterAnimation += 0.04f;
                    if (_wavesEnterAnimation > 1f)
                    {
                        _wavesEnterAnimation = 1f;
                    }
                }

                float enter = CubicBezierInterpolator.EASE_OUT.getInterpolation(_wavesEnterAnimation);
                float progressToSeekbarStep1 = 0;
                float s = _scale * (1f - progressToSeekbarStep1) * slideToCancelProgress1 * enter * (BlobDrawable.SCALE_BIG_MIN + 1.4f * _bigWaveDrawable.amplitude);
                canvas.Transform = Matrix3x2.CreateScale(s, new Vector2(cx, cy));
                _bigWaveDrawable.Draw(canvas, cx, cy/*, canvas, bigWaveDrawable.paint*/);
                s = _scale * (1f - progressToSeekbarStep1) * slideToCancelProgress1 * enter * (BlobDrawable.SCALE_SMALL_MIN + 1.4f * _tinyWaveDrawable.amplitude);
                canvas.Transform = Matrix3x2.CreateScale(s, new Vector2(cx, cy));
                _tinyWaveDrawable.Draw(canvas, cx, cy/*, canvas, tinyWaveDrawable.paint*/);
            }

            canvas.FillCircle(cx, cy, radius, _paint);
        }

        public void ShowWaves(bool b, bool animated)
        {
            if (!animated)
            {
                _wavesEnterAnimation = b ? 1f : 0.5f;
            }

            _showWaves = b;
        }
    }
}
