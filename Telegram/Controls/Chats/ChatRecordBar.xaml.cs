﻿using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls.Media;
using Telegram.Td.Api;
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

        private readonly CompositionBlobVisual _blobVisual;

        public ChatRecordBar()
        {
            InitializeComponent();

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

        private void VoiceButton_QuantumProcessed(object sender, float amplitude)
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

            _blobVisual.FillColor = Theme.Accent;

            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _blobVisual.StartAnimating();
            }
            else
            {
                _blobVisual.Clear();
            }

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

            _blobVisual.StopAnimating();

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
}
