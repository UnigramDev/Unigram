using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Td.Api;
using Windows.Media.Capture;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

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

        private readonly CompositionBlobVisual _blobVisual;

        private Popup _videoPopup;
        private CaptureElement _videoElement;

        public ChatRecordBar()
        {
            InitializeComponent();

            var visual = VisualUtilities.DropShadow(ArrowShadow, 2);
            visual.Offset = new Vector3(0, 1, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(Ellipse, true);

            _ellipseVisual = ElementComposition.GetElementVisual(Ellipse);
            _elapsedVisual = ElementComposition.GetElementVisual(ElapsedPanel);
            _slideVisual = ElementComposition.GetElementVisual(SlidePanel);
            _recordVisual = ElementComposition.GetElementVisual(this);
            _rootVisual = ElementComposition.GetElementVisual(this);

            _ellipseVisual.CenterPoint = new Vector3(80);
            _ellipseVisual.Scale = new Vector3(0);

            _elapsedTimer = new DispatcherTimer();
            _elapsedTimer.Interval = TimeSpan.FromMilliseconds(100);
            _elapsedTimer.Tick += (s, args) =>
            {
                ElapsedLabel.Text = ControlledButton.Elapsed.ToString("m\\:ss\\.ff");
            };

            _blobVisual = new CompositionBlobVisual(Blob, 160, 160, 4);

            Disconnected += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _blobVisual.StopAnimating();
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

        private void OnQuantumProcessed(object sender, float amplitude)
        {
            _blobVisual.UpdateLevel(amplitude * 40);
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

            _controlledButton.RecordingStarting += OnRecordingStarting;
            _controlledButton.RecordingStarted += OnRecordingStarted;
            _controlledButton.RecordingStopped += OnRecordingStopped;
            _controlledButton.RecordingLocked += OnRecordingLocked;
            _controlledButton.QuantumProcessed += OnQuantumProcessed;
            _controlledButton.ManipulationDelta += OnManipulationDelta;
        }

        private void OnRecordingStarted(object sender, EventArgs e)
        {
            if (sender is not MediaCapture mediaCapture || mediaCapture.MediaCaptureSettings.StreamingCaptureMode == StreamingCaptureMode.Audio)
            {
                return;
            }

            _videoElement = new CaptureElement
            {
                Source = mediaCapture,
                Stretch = Stretch.UniformToFill,
                Width = 272,
                Height = 272,
                RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
                RenderTransform = new ScaleTransform
                {
                    ScaleX = -1
                }
            };

            _videoPopup = new Popup
            {
                XamlRoot = XamlRoot,
                Child = new Border
                {
                    Width = XamlRoot.Size.Width,
                    Height = XamlRoot.Size.Height,
                    Background = new SolidColorBrush(ActualTheme == ElementTheme.Light
                            ? Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)
                            : Color.FromArgb(0x99, 0x00, 0x00, 0x00)),
                    Child = new Border
                    {
                        Width = 272,
                        Height = 272,
                        Background = new SolidColorBrush(Colors.Black),
                        CornerRadius = new CornerRadius(272 / 2),
                        Child = _videoElement
                    }
                }
            };

            _videoPopup.IsOpen = true;
            _ = mediaCapture.StartPreviewAsync();
        }

        private void OnRecordingStarting(object sender, EventArgs e)
        {
            // TODO: video message
            Visibility = Visibility.Visible;

            _blobVisual.FillColor = Theme.Accent;

            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _blobVisual.StartAnimating();
            }
            else
            {
                _blobVisual.Clear();
            }

            if (_videoPopup != null)
            {
                _videoPopup.IsOpen = false;
                _videoPopup = null;

                _videoElement.Source = null;
                _videoElement = null;
            }

            ChatRecordPopup.IsOpen = true;
            ChatRecordGlyph.Text = ControlledButton.Mode == ChatRecordMode.Video
                ? Icons.VideoNoteFilled24
                : Icons.MicOnFilled24;

            var slideWidth = SlidePanel.ActualSize.X;
            var elapsedWidth = ElapsedPanel.ActualSize.X;

            _slideVisual.Opacity = 1;

            var compositor = BootStrapper.Current.Compositor;

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Start();
                AttachExpression();
            };

            var slideAnimation = compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, slideWidth + 36);
            slideAnimation.InsertKeyFrame(1, 0);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var elapsedAnimation = compositor.CreateScalarKeyFrameAnimation();
            elapsedAnimation.InsertKeyFrame(0, -elapsedWidth);
            elapsedAnimation.InsertKeyFrame(1, 0);
            elapsedAnimation.Duration = TimeSpan.FromMilliseconds(300);

            var visibleAnimation = compositor.CreateScalarKeyFrameAnimation();
            visibleAnimation.InsertKeyFrame(0, 0);
            visibleAnimation.InsertKeyFrame(1, 1);

            var ellipseAnimation = compositor.CreateVector3KeyFrameAnimation();
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

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            //if (btnVoiceMessage.IsLocked)
            //{
            //    Poggers.Visibility = Visibility.Visible;
            //    Poggers.UpdateWaveform(btnVoiceMessage.GetWaveform());
            //    return;
            //}

            _blobVisual.StopAnimating();

            if (_videoPopup != null)
            {
                _videoPopup.IsOpen = false;
                _videoPopup = null;

                _videoElement.Source = null;
                _videoElement = null;
            }

            AttachExpression();

            var slidePosition = ActualSize.X - 48 - 36;
            var difference = slidePosition - ElapsedPanel.ActualSize.X;

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                _elapsedTimer.Stop();

                DetachExpression();

                ChatRecordPopup.IsOpen = false;

                Visibility = Visibility.Collapsed;
                ButtonCancelRecording.Visibility = Visibility.Collapsed;
                PauseRoot.Visibility = Visibility.Collapsed;
                PauseButton.IsChecked = false;
                ElapsedLabel.Text = "0:00,0";

                WaveformLabel.Text = string.Empty;
                Waveform.Visibility = Visibility.Collapsed;

                ElementCompositionPreview.SetElementChildVisual(WaveformBackground, null);
                ChatRecordGlyph.Foreground = new SolidColorBrush(Colors.White);

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

            var slideAnimation = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            slideAnimation.InsertKeyFrame(0, _slideVisual.Offset.X);
            slideAnimation.InsertKeyFrame(1, -slidePosition);
            slideAnimation.Duration = TimeSpan.FromMilliseconds(200);

            var visibleAnimation = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            visibleAnimation.InsertKeyFrame(0, 1);
            visibleAnimation.InsertKeyFrame(1, 0);

            _slideVisual.StartAnimation("Offset.X", slideAnimation);
            _recordVisual.StartAnimation("Opacity", visibleAnimation);

            if (PauseRoot.Visibility == Visibility.Visible)
            {
                var pause = ElementComposition.GetElementVisual(PauseRoot);
                pause.CenterPoint = new Vector3(18);

                var scale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                scale.InsertKeyFrame(0, Vector3.One);
                scale.InsertKeyFrame(1, Vector3.Zero);

                pause.StartAnimation("Scale", scale);
            }

            batch.End();

            ShowHideDelete(false);

            CancelTyping?.Invoke(this, EventArgs.Empty);
        }

        private void OnRecordingLocked(object sender, EventArgs e)
        {
            ChatRecordGlyph.Text = Icons.SendFilled;

            DetachExpression();

            var ellipseAnimation = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            ellipseAnimation.InsertKeyFrame(0, -57);
            ellipseAnimation.InsertKeyFrame(1, 0);

            _ellipseVisual.StartAnimation("Translation.Y", ellipseAnimation);

            ButtonCancelRecording.Visibility = Visibility.Visible;
            ControlledButton.Focus(FocusState.Programmatic);

            var point = _slideVisual.Offset;
            point.X = _slideVisual.Size.X + 36;

            _slideVisual.Opacity = 0;
            _slideVisual.Offset = point;

            PauseRoot.Visibility = Visibility.Visible;

            var pause = ElementComposition.GetElementVisual(PauseRoot);
            pause.CenterPoint = new Vector3(18);

            var scale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, Vector3.Zero);
            scale.InsertKeyFrame(1, Vector3.One);

            pause.StartAnimation("Scale", scale);
        }

        private void OnManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
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
            var elapsedExpression = BootStrapper.Current.Compositor.CreateExpressionAnimation("min(0, slide.Offset.X + ((root.Size.X - 48 - 36 - slide.Size.X) - elapsed.Size.X))");
            elapsedExpression.SetReferenceParameter("slide", _slideVisual);
            elapsedExpression.SetReferenceParameter("elapsed", _elapsedVisual);
            elapsedExpression.SetReferenceParameter("root", _rootVisual);

            var ellipseExpression = BootStrapper.Current.Compositor.CreateExpressionAnimation("Vector3(max(0, min(1, 1 + slide.Offset.X / (root.Size.X - 48 - 36))), max(0, min(1, 1 + slide.Offset.X / (root.Size.X - 48 - 36))), 1)");
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

        public void Pause()
        {
            Pause_Click(null, null);
        }

        private async void Pause_Click(object sender, RoutedEventArgs e)
        {
            _elapsedTimer.Stop();

            var result = await ControlledButton.PauseRecording();
            if (result != null)
            {
                _blobVisual.StopAnimating();

                WaveformLabel.Text = result.Duration.ToString("m\\:ss");
                Waveform.Visibility = Visibility.Visible;
                Waveform.UpdateWaveform(new VoiceNote(0, result.Waveform, string.Empty, null, null));

                var compositor = BootStrapper.Current.Compositor;
                var ellipse = compositor.CreateRoundedRectangleGeometry();
                ellipse.CornerRadius = new Vector2(WaveformBackground.ActualSize.Y / 2);
                ellipse.Size = new Vector2(WaveformBackground.ActualSize.Y, WaveformBackground.ActualSize.Y);

                var shape = compositor.CreateSpriteShape(ellipse);
                shape.FillBrush = compositor.CreateColorBrush(Theme.Accent);

                var visual = compositor.CreateShapeVisual();
                visual.Size = WaveformBackground.ActualSize;
                visual.Shapes.Add(shape);

                var width = compositor.CreateScalarKeyFrameAnimation();
                width.InsertKeyFrame(0, WaveformBackground.ActualSize.Y);
                width.InsertKeyFrame(1, WaveformBackground.ActualSize.X - 48);

                var offset = compositor.CreateScalarKeyFrameAnimation();
                offset.InsertKeyFrame(0, WaveformBackground.ActualSize.X - 44);
                offset.InsertKeyFrame(1, 0);

                ellipse.StartAnimation("Size.X", width);
                ellipse.StartAnimation("Offset.X", offset);

                var root = ElementComposition.GetElementVisual(PauseRoot);
                ElementCompositionPreview.SetIsTranslationEnabled(PauseRoot, true);

                var translate = compositor.CreateScalarKeyFrameAnimation();
                translate.InsertKeyFrame(0, 0);
                translate.InsertKeyFrame(1, 20);

                root.StartAnimation("Translation.Y", translate);

                ElementCompositionPreview.SetElementChildVisual(WaveformBackground, visual);
                ChatRecordGlyph.Foreground = new SolidColorBrush(Theme.Accent);

                ShowHideDelete(true);
            }
            else
            {
                _blobVisual.StartAnimating();
                _elapsedTimer.Start();

                WaveformLabel.Text = string.Empty;
                Waveform.Visibility = Visibility.Collapsed;

                var compositor = BootStrapper.Current.Compositor;
                var ellipse = compositor.CreateRoundedRectangleGeometry();
                ellipse.CornerRadius = new Vector2(WaveformBackground.ActualSize.Y / 2);
                ellipse.Size = new Vector2(WaveformBackground.ActualSize.Y, WaveformBackground.ActualSize.Y);

                var shape = compositor.CreateSpriteShape(ellipse);
                shape.FillBrush = compositor.CreateColorBrush(Theme.Accent);

                var visual = compositor.CreateShapeVisual();
                visual.Size = WaveformBackground.ActualSize;
                visual.Shapes.Add(shape);

                var width = compositor.CreateScalarKeyFrameAnimation();
                width.InsertKeyFrame(1, WaveformBackground.ActualSize.Y);
                width.InsertKeyFrame(0, WaveformBackground.ActualSize.X - 48);

                var offset = compositor.CreateScalarKeyFrameAnimation();
                offset.InsertKeyFrame(1, WaveformBackground.ActualSize.X - 44);
                offset.InsertKeyFrame(0, 0);

                ellipse.StartAnimation("Size.X", width);
                ellipse.StartAnimation("Offset.X", offset);

                var root = ElementComposition.GetElementVisual(PauseRoot);
                ElementCompositionPreview.SetIsTranslationEnabled(PauseRoot, true);

                var translate = compositor.CreateScalarKeyFrameAnimation();
                translate.InsertKeyFrame(1, 0);
                translate.InsertKeyFrame(0, 20);

                root.StartAnimation("Translation.Y", translate);

                ElementCompositionPreview.SetElementChildVisual(WaveformBackground, visual);
                ChatRecordGlyph.Foreground = new SolidColorBrush(Colors.White);

                ShowHideDelete(false);
            }
        }

        private bool _deleteCollapsed = true;

        private void ShowHideDelete(bool show)
        {
            if (_deleteCollapsed != show)
            {
                return;
            }

            _deleteCollapsed = !show;
            DeleteButton.Visibility = Visibility.Visible;

            var visual1 = ElementComposition.GetElementVisual(DeleteButton);
            var visual2 = ElementComposition.GetElementVisual(RecordGlyph);

            visual1.CenterPoint = new Vector3(24);
            visual2.CenterPoint = new Vector3(6);

            var batch = visual1.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                DeleteButton.Visibility = _deleteCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            };

            var scale1 = visual1.Compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale1.InsertKeyFrame(show ? 1 : 0, Vector3.One);

            var scale2 = visual1.Compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(show ? 0 : 1, Vector3.One);
            scale2.InsertKeyFrame(show ? 1 : 0, Vector3.Zero);

            visual1.StartAnimation("Scale", scale1);
            visual2.StartAnimation("Scale", scale2);

            batch.End();
        }
    }
}
