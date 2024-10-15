using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Chats;
using Telegram.Controls.Media;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core.Preview;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace Telegram.Views.Calls
{
    public enum VoipVideoActiveState
    {
        None,
        Local,
        Remote,
    }

    public sealed partial class VoipPage : WindowEx, IToastHost
    {
        private static readonly int[] _pendingGradient = new[] { 0x568FD6, 0x626ED5, 0xA667D5, 0x7664DA };
        private static readonly int[] _readyGradient = new[] { 0xACBD65, 0x459F8D, 0x53A4D1, 0x3E917A };
        private static readonly int[] _errorGradient = new[] { 0xC0508D, 0xF09536, 0xCE5081, 0xFC7C4C };

        private readonly VoipCall _call;

        private readonly VoipVideoOutput _localVideo;
        private readonly VoipVideoOutput _remoteVideo;

        private VoipVideoActiveState _maximizedVideo;
        private VoipVideoActiveState _minimizedVideo;

        private readonly CompositionBlobVisual _visual;

        private readonly DispatcherTimer _durationTimer;
        private readonly DispatcherTimer _discardedTimer;

        private readonly DisplayRequest _displayRequest = new();

        private VoipConnectionState _background;

        public VoipPage(VoipCall call)
        {
            InitializeComponent();

            _visual = new CompositionBlobVisual(Blob, 280, 280, 1.5f, ElementComposition.GetElementVisual(Photo))
            {
                FillColor = Colors.White
            };

            InitializeBlob();

            _durationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _durationTimer.Tick += Duration_Tick;

            _discardedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };

            _discardedTimer.Tick += Discarded_Tick;

            _localVideo = new VoipVideoOutput(LocalVideo, true);
            _remoteVideo = new VoipVideoOutput(RemoteVideo, false);

            _localVideo.StateChanged += OnVideoStateChanged;
            _remoteVideo.StateChanged += OnVideoStateChanged;

            _call = call;
            _call.StateChanged += OnStateChanged;
            _call.ConnectionStateChanged += OnConnectionStateChanged;
            _call.RemoteMediaStateChanged += OnRemoteMediaStateChanged;
            _call.RemoteBatteryLevelIsLowChanged += OnRemoteBatteryLevelIsLowChanged;
            _call.AudioLevelUpdated += OnAudioLevelUpdated;
            _call.SignalBarsUpdated += OnSignalBarsUpdated;
            _call.MediaStateChanged += OnMediaStateChanged;
            _call.VideoFailed += OnVideoFailed;
            _call.NeedUpdates();

            if (call.VideoState != VoipVideoState.Inactive)
            {
                _maximizedVideo = VoipVideoActiveState.Local;
                _localVideo.SetState(call.VideoState, !call.IsScreenSharing);

                ShowHideVideoToPlain(true, LocalVideo);
            }
            else if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _visual.StartAnimating();
            }

            if (call.ClientService.TryGetUser(call.UserId, out User user))
            {
                Title.Text = user.FullName();
                Photo.SetUser(call.ClientService, user, 280);

                RemoteAudioOff.Text = string.Format(Strings.VoipUserMicrophoneIsOff, user.FirstName);
                RemoteBatteryOff.Text = string.Format(Strings.VoipUserBatteryIsLow, user.FirstName);
            }

            ElementCompositionPreview.SetIsTranslationEnabled(DetailRoot, true);

            var durationRoot = ElementComposition.GetElementVisual(DurationRoot);
            durationRoot.Opacity = 0;

            var weakNetwork = ElementComposition.GetElementVisual(WeakNetwork);
            weakNetwork.Opacity = 0;

            Window.Current.SetTitleBar(TitleBar);
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += OnCloseRequested;
        }

        private void InitializeBlob()
        {
            var device = ElementComposition.GetSharedDevice();
            var outerClip = CanvasGeometry.CreateRectangle(device, 0, 0, 280, 280);
            var innerClip = CanvasGeometry.CreateEllipse(device, 140, 140, 63, 63);
            var blob = ElementComposition.GetElementVisual(Blob);
            var geometry = blob.Compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(device, new[] { outerClip, innerClip }, CanvasFilledRegionDetermination.Alternate)));

            blob.Clip = blob.Compositor.CreateGeometricClip(geometry);
        }

        private void Duration_Tick(object sender, object e)
        {
            Duration.Text = _call.Duration.ToDuration();
        }

        private void Discarded_Tick(object sender, object e)
        {
            _discardedTimer.Stop();
            Close();
        }

        private void Duration_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var visual = ElementComposition.GetElementVisual(sender as UIElement);
            var point = e.NewSize.ToVector2();

            if (sender == State)
            {
                visual.CenterPoint = new Vector3(point.X / 2, -4, 0);
            }
            else if (sender == DurationRoot)
            {
                visual.CenterPoint = new Vector3(point.X / 2, point.Y + 4, 0);
            }
        }

        #region IToastHost

        public void Connect(TeachingTip toast)
        {
            Resources.Remove("TeachingTip");
            Resources.Add("TeachingTip", toast);
        }

        public void Disconnect(TeachingTip toast)
        {
            if (Resources.TryGetValue("TeachingTip", out object cached))
            {
                if (cached == toast)
                {
                    Resources.Remove("TeachingTip");
                }
            }
        }

        #endregion

        #region Thread switch

        private void OnVideoFailed(VoipCall sender, EventArgs args)
        {
            this.BeginOnUIThread(() => OnVideoFailed());
        }

        private void OnStateChanged(VoipCall sender, VoipCallStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => OnStateChanged(args));
        }

        private void OnConnectionStateChanged(VoipCall sender, VoipCallConnectionStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => OnConnectionStateChanged(args));
        }

        private void OnRemoteMediaStateChanged(VoipCall sender, VoipCallMediaStateChangedEventArgs args)
        {
            Logger.Info(string.Format("Video: {0}, audio: {1}", args.Video, args.Audio));

            _remoteVideo.SetState(args.Video);

            if (args.Video != VoipVideoState.Inactive)
            {
                _call.SetRemoteVideoOutput(_remoteVideo);
            }

            this.BeginOnUIThread(() => OnRemoteMediaStateChanged(args));
        }

        private void OnRemoteBatteryLevelIsLowChanged(VoipCall sender, VoipCallRemoteBatteryLevelIsLowChangedEventArgs args)
        {
            this.BeginOnUIThread(() => OnRemoteBatteryLevelIsLowChanged(args));
        }

        private void OnAudioLevelUpdated(VoipCall sender, VoipCallAudioLevelUpdatedEventArgs args)
        {
            this.BeginOnUIThread(() => OnAudioLevelUpdated(args));
        }

        private void OnSignalBarsUpdated(VoipCall sender, VoipCallSignalBarsUpdatedEventArgs args)
        {
            this.BeginOnUIThread(() => OnSignalBarsUpdated(args));
        }

        private void OnMediaStateChanged(VoipCall sender, VoipCallMediaStateChangedEventArgs args)
        {
            _localVideo.SetState(args.Video, !args.IsScreenSharing);

            if (args.Video != VoipVideoState.Inactive)
            {
                _call.SetLocalVideoOutput(_localVideo);
            }

            this.BeginOnUIThread(() => OnMediaStateChanged(args));
        }

        private void OnVideoStateChanged(VoipVideoOutput sender, VoipVideoStateChangedEventArgs args)
        {
            if (sender == _localVideo)
            {
                this.BeginOnUIThread(() => OnLocalVideoStateChanged(args));
            }
            else
            {
                this.BeginOnUIThread(() => OnRemoteVideoStateChanged(args));
            }
        }

        #endregion

        private void OnStateChanged(VoipCallStateChangedEventArgs args)
        {
            if (args.State == VoipState.Ready)
            {
                Screen.IsEnabled = args.ReadyState == VoipReadyState.Established || _call.Duration > 0;
                Camera.IsEnabled = args.ReadyState == VoipReadyState.Established || _call.Duration > 0;

                if (args.ReadyState is VoipReadyState.Established && _call.State is CallStateReady ready)
                {
                    TransitionToEnstablished(ready);
                }
                else if (args.ReadyState is VoipReadyState.WaitInit or VoipReadyState.WaitInitAck)
                {
                    State.Text = Strings.VoipConnecting;
                }
                else if (args.ReadyState is VoipReadyState.Reconnecting)
                {

                }
            }
            else
            {
                Screen.IsEnabled = false;
                Camera.IsEnabled = false;

                State.Text = args.State switch
                {
                    VoipState.Requesting => Strings.VoipRequesting,
                    VoipState.Waiting => Strings.VoipWaiting,
                    VoipState.Ringing => Strings.VoipRinging,
                    VoipState.Connecting => Strings.VoipConnecting,
                    //VoipState.HangingUp => Strings.VoipHangingUp,
                    _ => _call.Duration.ToDuration(),
                };

                TransitionToEnstablished(null);

                if (_call.Duration > 0)
                {
                    _visual.StopAnimating();
                }
            }

            if (args.State is VoipState.Connecting)
            {
                RestoreWindow();
            }
            else if (args.State == VoipState.HangingUp)
            {
                Title.Text = Strings.VoipCallEnded2;
            }
            else if (args.State is VoipState.Error or VoipState.Discarded)
            {
                TransitionToDiscarded(_call.State as CallStateDiscarded);
            }
        }

        private void TransitionToDiscarded(CallStateDiscarded discarded)
        {
            if (discarded != null && discarded.NeedRating)
            {
                // TODO
                _discardedTimer.Start();
            }
            else
            {
                _discardedTimer.Start();
            }

            if (discarded != null)
            {
                Title.Text = discarded.Reason switch
                {
                    CallDiscardReasonDeclined => Strings.VoipCallBusy2,
                    CallDiscardReasonMissed => Strings.VoipCallBusy2,
                    _ => Strings.VoipCallEnded2
                };
            }
            else
            {
                Title.Text = Strings.VoipCallFailed2;
            }

            _localVideo.SetState(VoipVideoState.Inactive);
            _remoteVideo.SetState(VoipVideoState.Inactive);
        }

        private bool _enstablished;

        private void TransitionToEnstablished(CallStateReady ready)
        {
            var enstablished = ready is not null;
            if (enstablished == _enstablished)
            {
                return;
            }

            _enstablished = enstablished;

            var prevVisual = ElementComposition.GetElementVisual(enstablished ? State : DurationRoot);
            var nextVisual = ElementComposition.GetElementVisual(enstablished ? DurationRoot : State);

            var easing = prevVisual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 0.1f), new Vector2(0.25f, 1));

            var fadeOut = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
            fadeOut.InsertKeyFrame(0, 1);
            fadeOut.InsertKeyFrame(1, 0, easing);
            fadeOut.Duration = Constants.FastAnimation;

            var fadeIn = prevVisual.Compositor.CreateScalarKeyFrameAnimation();
            fadeIn.InsertKeyFrame(0, 0);
            fadeIn.InsertKeyFrame(1, 1, easing);
            fadeIn.Duration = Constants.FastAnimation;

            var slideOut = prevVisual.Compositor.CreateVector3KeyFrameAnimation();
            slideOut.InsertKeyFrame(0, new Vector3(1, 1, 1));
            slideOut.InsertKeyFrame(1, new Vector3(1, 0, 1), easing);
            slideOut.Duration = Constants.FastAnimation;

            var slideIn = prevVisual.Compositor.CreateVector3KeyFrameAnimation();
            slideIn.InsertKeyFrame(0, new Vector3(1, 0, 1));
            slideIn.InsertKeyFrame(1, new Vector3(1, 1, 1), easing);
            slideIn.Duration = Constants.FastAnimation;

            prevVisual.StartAnimation("Opacity", fadeOut);
            nextVisual.StartAnimation("Opacity", fadeIn);
            prevVisual.StartAnimation("Scale", slideOut);
            nextVisual.StartAnimation("Scale", slideIn);

            if (enstablished)
            {
                _durationTimer.Start();
                Duration.Text = _call.Duration.ToDuration();

                Emoji.Content = string.Join(string.Empty, ready.Emojis);
                Emoji.Visibility = Visibility.Visible;

                ToastPopup.Show(Emoji, Icons.LockClosedFilled12 + Icons.Spacing + Strings.VoipHintEncryptionKey, TeachingTipPlacementMode.Bottom, ElementTheme.Light, TimeSpan.FromSeconds(4));
            }
            else
            {
                _durationTimer.Stop();
                State.SkipAnimation = true;
                State.Text = _call.Duration.ToDuration();

                RemoteAudioOff.ShowHide(false, RemoteAudioPanel);
                LocalAudioOff.ShowHide(false, LocalAudioPanel);
                LocalVideoOff.ShowHide(false, LocalVideoPanel);

                Emoji.Visibility = Visibility.Collapsed;

                ShowHideWealNetwork(false);
            }
        }

        private bool _weakNetworkCollapsed = true;

        private void ShowHideWealNetwork(bool show)
        {
            if (_weakNetworkCollapsed != show)
            {
                return;
            }

            _weakNetworkCollapsed = !show;

            var weakNetwork = ElementComposition.GetElementVisual(WeakNetwork);

            var opacity = weakNetwork.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            var scale = weakNetwork.Compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(0, show ? Vector3.Zero : Vector3.One);
            scale.InsertKeyFrame(1, show ? Vector3.One : Vector3.Zero);

            weakNetwork.StartAnimation("Opacity", opacity);
            weakNetwork.StartAnimation("Scale", scale);
        }

        private void OnConnectionStateChanged(VoipCallConnectionStateChangedEventArgs args)
        {
            var gradient = args.State switch
            {
                VoipConnectionState.Pending => _pendingGradient,
                VoipConnectionState.Ready => _readyGradient,
                VoipConnectionState.Error => _errorGradient,
                _ => _pendingGradient
            };

            var background = new Telegram.Td.Api.BackgroundFillFreeformGradient(gradient);

            if (BackgroundNext.ImageSource == null)
            {
                BackgroundNext.ImageSource = ChatBackgroundFreeform.Create(background, 0);
                return;
            }

            BackgroundPrev.ImageSource = BackgroundNext.ImageSource;
            BackgroundNext.ImageSource = ChatBackgroundFreeform.Create(background, 0);

            var visual = ElementComposition.GetElementVisual(BackgroundRoot);
            var compositor = visual.Compositor;

            var show = compositor.CreateScalarKeyFrameAnimation();
            show.InsertKeyFrame(1, 1);
            show.InsertKeyFrame(0, 0);

            visual.StartAnimation("Opacity", show);
        }

        private void OnRemoteMediaStateChanged(VoipCallMediaStateChangedEventArgs args)
        {
            RemoteAudioOff.ShowHide(args.Audio == VoipAudioState.Muted, RemoteAudioPanel);
            LocalVideoOff.ShowHide(args.Video != VoipVideoState.Inactive && _call.VideoState == VoipVideoState.Inactive, LocalVideoPanel);
        }

        private void ShowHideVideoToPlain(bool show, UIElement element)
        {
            if (show)
            {
                element.Visibility = Visibility.Visible;
                BottomShadow.Visibility = Visibility.Visible;

                var visual1 = ElementComposition.GetElementVisual(element);
                var visual2 = ElementComposition.GetElementVisual(PhotoTransform);
                var detail = ElementComposition.GetElementVisual(DetailRoot);
                var title = ElementComposition.GetElementVisual(Title);
                var photo = ElementComposition.GetElementVisual(Photo);
                var shadow = ElementComposition.GetElementVisual(BottomShadow);

                visual1.Scale = Vector3.One;
                visual1.Offset = Vector3.Zero;
                visual1.Opacity = 1;
                visual1.Clip = null;

                visual2.Scale = Vector3.Zero;
                visual2.Offset = Vector3.Zero;
                visual2.Opacity = 0;
                photo.Clip = null;
                shadow.Opacity = 1;

                _visual.StopAnimating();
            }
        }

        private void ShowHideCollapsedToMaximized(bool show, UIElement element, Vector2 frame)
        {
            element.Visibility = Visibility.Visible;
            Photo.Shape = ProfilePictureShape.None;

            var transform = PhotoRoot.TransformToVisual(this);
            var point = transform.TransformVector2(Vector2.Zero);
            var photoSize = new Vector2(126, 126);

            point = new Vector2(point.X + 7, point.Y + 7);

            var visual1 = ElementComposition.GetElementVisual(element);
            var visual2 = ElementComposition.GetElementVisual(PhotoTransform);
            var detail = ElementComposition.GetElementVisual(DetailRoot);
            var title = ElementComposition.GetElementVisual(Title);
            var photo = ElementComposition.GetElementVisual(Photo);

            var center = ActualSize / 2;
            var offset = center - (point + photoSize / 2);

            visual1.CenterPoint = new Vector3(center, 0);
            visual2.CenterPoint = new Vector3(PhotoRoot.ActualSize / 2, 0);

            title.CenterPoint = new Vector3(Title.ActualSize.X / 2, Title.ActualSize.Y, 0);

            var compositor = visual1.Compositor;
            var duration = TimeSpan.FromSeconds(.5); // Constants.SoftAnimation; //TimeSpan.FromSeconds(.5);

            var geometry1 = compositor.CreatePathGeometry();
            var clip1 = compositor.CreateGeometricClip(geometry1);

            var geometry2 = compositor.CreatePathGeometry();
            var clip2 = compositor.CreateGeometricClip(geometry2);

            visual1.Clip = clip1;
            photo.Clip = clip2;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                Photo.Shape = ProfilePictureShape.Ellipse;

                visual1.Clip = null;
                photo.Clip = null;
            };

            var remoteSize = frame * MathF.Min(ActualSize.X / frame.X, ActualSize.Y / frame.Y);
            var remoteMin = MathF.Min(remoteSize.X, remoteSize.Y);

            var ratio1 = photoSize.Y / remoteMin;
            var ratio2 = remoteMin / photoSize.Y;

            var side = (ActualSize.X - ActualSize.Y) / 2;
            var part = (ActualSize.Y - remoteMin) / 2;

            // Background from small to big
            var scale1 = compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, new Vector3(ratio1, ratio1, 0));
            scale1.InsertKeyFrame(show ? 1 : 0, new Vector3(1, 1, 0));
            scale1.Duration = duration;

            var offset1 = compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(-offset, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3(0));
            offset1.Duration = duration;

            var opacity1 = compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration = duration;

            // TODO: corner radius must be scaled relatively

            var path1 = compositor.CreatePathKeyFrameAnimation();
            path1.InsertKeyFrame(show ? 0 : 1, new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, side + part, part, remoteMin, remoteMin, remoteMin, remoteMin)));
            path1.InsertKeyFrame(show ? 1 : 0, new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, 0, 0, ActualSize.X, ActualSize.Y, 0, 0)));
            path1.Duration = duration;

            visual1.StartAnimation("Scale", scale1);
            visual1.StartAnimation("Offset", offset1);
            visual1.StartAnimation("Opacity", opacity1);
            geometry1.StartAnimation("Path", path1);

            // Foreground from small to big
            var scale2 = compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(show ? 0 : 1, new Vector3(1, 1, 0));
            scale2.InsertKeyFrame(show ? 1 : 0, new Vector3(ratio2, ratio2, 0));
            scale2.Duration = duration;

            var offset2 = compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(show ? 0 : 1, new Vector3(0));
            offset2.InsertKeyFrame(show ? 1 : 0, new Vector3(offset, 0));
            offset2.Duration = duration;

            var opacity2 = compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration = duration;

            var path2 = compositor.CreatePathKeyFrameAnimation();
            path2.InsertKeyFrame(show ? 0 : 1, new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, 0, 0, Photo.ActualSize.X, Photo.ActualSize.Y, Photo.ActualSize.Y / 2, Photo.ActualSize.Y / 2)));
            path2.InsertKeyFrame(show ? 1 : 0, new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, 0, 0, Photo.ActualSize.X, Photo.ActualSize.Y, 0, 0)));
            path2.Duration = duration;

            visual2.StartAnimation("Scale", scale2);
            visual2.StartAnimation("Offset", offset2);
            visual2.StartAnimation("Opacity", opacity2);
            geometry2.StartAnimation("Path", path2);

            var detailY = point.Y + PhotoRoot.ActualSize.Y - 20;

            var translation3 = compositor.CreateScalarKeyFrameAnimation();
            translation3.InsertKeyFrame(0, show ? 0 : -detailY);
            translation3.InsertKeyFrame(1, show ? -detailY : 0);
            translation3.Duration = duration;

            var scale3 = compositor.CreateVector3KeyFrameAnimation();
            scale3.InsertKeyFrame(show ? 0 : 1, new Vector3(1));
            scale3.InsertKeyFrame(show ? 1 : 0, new Vector3(14f / 24f));
            scale3.Duration = duration;

            detail.StartAnimation("Translation.Y", translation3);
            title.StartAnimation("Scale", scale3);

            batch.End();

            if (show)
            {
                _visual.StopAnimating();
            }
            else if (_enstablished && PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _visual.StartAnimating();
            }

            // Test
            BottomShadow.Visibility = Visibility.Visible;

            var shadow = ElementComposition.GetElementVisual(BottomShadow);
            var scale4 = compositor.CreateScalarKeyFrameAnimation();
            scale4.InsertKeyFrame(show ? 0 : 1, 0);
            scale4.InsertKeyFrame(show ? 1 : 0, 1);
            scale4.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scale4.DelayTime = show ? duration - Constants.FastAnimation : TimeSpan.Zero;
            scale4.Duration = Constants.FastAnimation;

            shadow.StartAnimation("Opacity", scale4);
        }

        private void ShowHideMinimizedToMaximized(bool show, UIElement element, Vector2 frame, VoipVideoActiveState collapsed)
        {
            element.Visibility = Visibility.Visible;

            Canvas.SetZIndex(LocalVideo, element == LocalVideo ? 0 : -1);
            Canvas.SetZIndex(RemoteVideo, element == LocalVideo ? -1 : 0);

            var photoSize = frame * MathF.Min(146 / frame.X, 146 / frame.Y);
            var point = new Vector2(ActualSize.X - photoSize.X - 12, ActualSize.Y - photoSize.Y - 12);

            var visual1 = ElementComposition.GetElementVisual(element);

            var center = ActualSize / 2;
            var offset = center - (point + photoSize / 2);

            visual1.CenterPoint = new Vector3(center, 0);

            var compositor = visual1.Compositor;
            var duration = TimeSpan.FromSeconds(.5); // Constants.SoftAnimation; //TimeSpan.FromSeconds(.5);

            var geometry1 = compositor.CreatePathGeometry();
            var clip1 = compositor.CreateGeometricClip(geometry1);

            visual1.Clip = clip1;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (collapsed == VoipVideoActiveState.Local)
                {
                    LocalVideo.Visibility = Visibility.Collapsed;
                }
                else if (collapsed == VoipVideoActiveState.Remote)
                {
                    RemoteVideo.Visibility = Visibility.Collapsed;
                }

                return;
                visual1.Clip = null;
            };

            var remoteSize = frame * MathF.Min(ActualSize.X / frame.X, ActualSize.Y / frame.Y);
            var remoteMin = MathF.Min(remoteSize.X, remoteSize.Y);

            var ratio1 = photoSize.Y / remoteMin;
            var ratio2 = remoteMin / photoSize.Y;

            var side = (ActualSize.X - remoteSize.X) / 2;
            var part = (ActualSize.Y - remoteSize.Y) / 2;

            // Background from small to big
            var scale1 = compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, new Vector3(ratio1, ratio1, 0));
            scale1.InsertKeyFrame(show ? 1 : 0, new Vector3(1, 1, 0));
            scale1.Duration = duration;

            var offset1 = compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(-offset, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3(0));
            offset1.Duration = duration;

            var path1 = compositor.CreatePathKeyFrameAnimation();
            path1.InsertKeyFrame(show ? 0 : 1, new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, side, part, ActualSize.X - side * 2, ActualSize.Y - part * 2, 8f * ratio2, 8f * ratio2)));
            path1.InsertKeyFrame(show ? 1 : 0, new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, 0, 0, ActualSize.X, ActualSize.Y, 0, 0)));
            path1.Duration = duration;

            visual1.StartAnimation("Scale", scale1);
            visual1.StartAnimation("Offset", offset1);
            geometry1.StartAnimation("Path", path1);

            batch.End();
        }

        private void ShowHideCollapsedToMinimized(bool show, UIElement element, Vector2 frame)
        {
            element.Visibility = Visibility.Visible;

            Canvas.SetZIndex(LocalVideo, element == LocalVideo ? 0 : -1);
            Canvas.SetZIndex(RemoteVideo, element == LocalVideo ? -1 : 0);

            var photoSize = frame * MathF.Min(146 / frame.X, 146 / frame.Y);
            var point = new Vector2(ActualSize.X - photoSize.X - 12, ActualSize.Y - photoSize.Y - 12);

            var visual1 = ElementComposition.GetElementVisual(element);

            var center = ActualSize / 2;
            var offset = center - (point + photoSize / 2);

            visual1.CenterPoint = new Vector3(center, 0);

            var compositor = visual1.Compositor;
            var duration = TimeSpan.FromSeconds(.5); // Constants.SoftAnimation; //TimeSpan.FromSeconds(.5);

            var geometry1 = compositor.CreatePathGeometry();
            var clip1 = compositor.CreateGeometricClip(geometry1);

            visual1.Clip = clip1;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                return;
                visual1.Clip = null;
            };

            var remoteSize = frame * MathF.Min(ActualSize.X / frame.X, ActualSize.Y / frame.Y);
            var remoteMin = MathF.Min(remoteSize.X, remoteSize.Y);

            var ratio1 = photoSize.Y / remoteMin;
            var ratio2 = remoteMin / photoSize.Y;

            var side = (ActualSize.X - remoteSize.X) / 2;
            var part = (ActualSize.Y - remoteSize.Y) / 2;

            // Background from small to big
            var scale1 = compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 0, 0));
            scale1.InsertKeyFrame(show ? 1 : 0, new Vector3(ratio1, ratio1, 0));
            scale1.Duration = duration;

            visual1.StartAnimation("Scale", scale1);
            visual1.Offset = new Vector3(-offset, 0);
            visual1.Opacity = 1;
            geometry1.Path = new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, side, part, ActualSize.X - side * 2, ActualSize.Y - part * 2, 8f * ratio2, 8f * ratio2));

            batch.End();
        }

        private void OnRemoteBatteryLevelIsLowChanged(VoipCallRemoteBatteryLevelIsLowChangedEventArgs args)
        {
            RemoteBatteryOff.ShowHide(args.IsLow, RemoteBatteryPanel);
        }

        private void OnAudioLevelUpdated(VoipCallAudioLevelUpdatedEventArgs args)
        {

        }

        private void OnMediaStateChanged(VoipCallMediaStateChangedEventArgs args)
        {
            Mute.IsChecked = args.Audio == VoipAudioState.Muted;
            Camera.IsChecked = args.Video != VoipVideoState.Inactive && !_call.IsScreenSharing;
            Screen.IsChecked = args.Video != VoipVideoState.Inactive && _call.IsScreenSharing;

            LocalAudioOff.ShowHide(args.Audio == VoipAudioState.Muted, LocalAudioPanel);
            LocalVideoOff.ShowHide(_call.RemoteVideoState != VoipVideoState.Inactive && args.Video == VoipVideoState.Inactive, LocalVideoPanel);
        }

        private void OnVideoFailed()
        {
            _ = MediaDevicePermissions.CheckAccessAsync(XamlRoot, MediaDeviceAccess.Video, PopupTheme);
        }

        private void OnSignalBarsUpdated(VoipCallSignalBarsUpdatedEventArgs args)
        {
            SignalBars.Count = args.Count;
            ShowHideWealNetwork(args.Count <= 1);
        }

        private void OnLocalVideoStateChanged(VoipVideoStateChangedEventArgs args)
        {
            OnVideoStateChanged(args.IsActive, args.Frame, _remoteVideo.IsActive, _remoteVideo.Frame);
        }

        private void OnRemoteVideoStateChanged(VoipVideoStateChangedEventArgs args)
        {
            OnVideoStateChanged(_localVideo.IsActive, _localVideo.Frame, args.IsActive, args.Frame);
        }

        private void OnVideoStateChanged(bool local, Vector2 localFrame, bool remote, Vector2 remoteFrame)
        {
            var maximized = VoipVideoActiveState.None;
            var minimized = VoipVideoActiveState.None;

            // TODO: Logic to switch minimized and maximized visuals
            if (remote)
            {
                maximized = VoipVideoActiveState.Remote;
                minimized = local
                    ? VoipVideoActiveState.Local
                    : VoipVideoActiveState.None;
            }
            else if (local)
            {
                maximized = VoipVideoActiveState.Local;
                minimized = VoipVideoActiveState.None;
            }

            if (_maximizedVideo == maximized && _minimizedVideo == minimized)
            {
                return;
            }

            if (maximized != VoipVideoActiveState.None && _maximizedVideo != VoipVideoActiveState.None &&
                minimized != VoipVideoActiveState.None && _minimizedVideo != VoipVideoActiveState.None)
            {
                // Switch between minimized and maximized video
            }
            else if (maximized == _maximizedVideo)
            {
                // Maximized video is the same, show/hide minimized
                if (minimized == VoipVideoActiveState.Local || _minimizedVideo == VoipVideoActiveState.Local)
                {
                    ShowHideCollapsedToMinimized(minimized == VoipVideoActiveState.Local, LocalVideo, localFrame);
                }
                else if (minimized == VoipVideoActiveState.Remote || _minimizedVideo == VoipVideoActiveState.Remote)
                {
                    ShowHideCollapsedToMinimized(minimized == VoipVideoActiveState.Remote, RemoteVideo, remoteFrame);
                }
            }
            else if (minimized == _minimizedVideo && _minimizedVideo == VoipVideoActiveState.None)
            {
                // Minimized video is missing, show/hide maximized
                if (maximized == VoipVideoActiveState.Local || _maximizedVideo == VoipVideoActiveState.Local)
                {
                    ShowHideCollapsedToMaximized(maximized == VoipVideoActiveState.Local, LocalVideo, localFrame);
                }
                else if (maximized == VoipVideoActiveState.Remote || _maximizedVideo == VoipVideoActiveState.Remote)
                {
                    ShowHideCollapsedToMaximized(maximized == VoipVideoActiveState.Remote, RemoteVideo, remoteFrame);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if ((minimized == _maximizedVideo && maximized != VoipVideoActiveState.None) || (maximized == _minimizedVideo && minimized == VoipVideoActiveState.None))
            {
                // Maximized video becomes minimized, missing video is maximized
                //if ((minimized == VoipVideoActiveState.Local && maximized == VoipVideoActiveState.Remote) || (maximized == VoipVideoActiveState.Local && _maximizedVideo == VoipVideoActiveState.Remote))
                if (minimized == VoipVideoActiveState.Local || _minimizedVideo == VoipVideoActiveState.Local)
                {
                    ShowHideMinimizedToMaximized(maximized == VoipVideoActiveState.Local, LocalVideo, localFrame, maximized == VoipVideoActiveState.Local ? VoipVideoActiveState.Remote : VoipVideoActiveState.None);
                    ShowHideVideoToPlain(minimized == VoipVideoActiveState.Local, RemoteVideo);
                }
                //else if ((minimized == VoipVideoActiveState.Remote && maximized == VoipVideoActiveState.Local) || (maximized == VoipVideoActiveState.Remote && _maximizedVideo == VoipVideoActiveState.Local))
                else if (minimized == VoipVideoActiveState.Remote || _minimizedVideo == VoipVideoActiveState.Remote)
                {
                    ShowHideMinimizedToMaximized(maximized == VoipVideoActiveState.Remote, RemoteVideo, remoteFrame, maximized == VoipVideoActiveState.Remote ? VoipVideoActiveState.Local : VoipVideoActiveState.None);
                    ShowHideVideoToPlain(minimized == VoipVideoActiveState.Remote, LocalVideo);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else
            {
                throw new InvalidOperationException();
            }

            _maximizedVideo = maximized;
            _minimizedVideo = minimized;
        }

        private void OnCloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            // TODO: only if call is pending?
            _call.Discard();
        }

        private VoipVideoCapture _test;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _displayRequest.TryRequestActive();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _displayRequest.TryRequestRelease();

            _call.StateChanged -= OnStateChanged;
            _call.ConnectionStateChanged -= OnConnectionStateChanged;
            _call.RemoteMediaStateChanged -= OnRemoteMediaStateChanged;
            _call.AudioLevelUpdated -= OnAudioLevelUpdated;
            _call.SignalBarsUpdated -= OnSignalBarsUpdated;
            _call.MediaStateChanged -= OnMediaStateChanged;
            _call.VideoFailed -= OnVideoFailed;

            _call.SetLocalVideoOutput(null);
            _call.SetRemoteVideoOutput(null);

            _localVideo.StateChanged -= OnVideoStateChanged;
            _localVideo.Stop();

            _remoteVideo.StateChanged -= OnVideoStateChanged;
            _remoteVideo.Stop();

            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested -= OnCloseRequested;
            Window.Current.SetTitleBar(null);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_maximizedVideo != VoipVideoActiveState.None)
            {
                var transform = PhotoRoot.TransformToVisual(this);
                var point = transform.TransformVector2(Vector2.Zero);
                var photoSize = new Vector2(126, 126);

                point = new Vector2(point.X + 7, point.Y + 7);

                var detail = ElementComposition.GetElementVisual(DetailRoot);
                var title = ElementComposition.GetElementVisual(Title);

                title.CenterPoint = new Vector3(Title.ActualSize.X / 2, Title.ActualSize.Y, 0);

                var detailY = point.Y + PhotoRoot.ActualSize.Y - 20;

                detail.Properties.InsertVector3("Translation", new Vector3(0, -detailY, 0));
                title.Scale = new Vector3(14f / 24f);
            }

            if (_minimizedVideo != VoipVideoActiveState.None)
            {
                var element = _minimizedVideo == VoipVideoActiveState.Local
                    ? LocalVideo
                    : RemoteVideo;

                var frame = _minimizedVideo == VoipVideoActiveState.Local
                    ? _localVideo.Frame
                    : _remoteVideo.Frame;

                var photoSize = frame * MathF.Min(146 / frame.X, 146 / frame.Y);
                var point = new Vector2(ActualSize.X - photoSize.X - 12, ActualSize.Y - photoSize.Y - 12);

                var visual1 = ElementComposition.GetElementVisual(element);

                var center = ActualSize / 2;
                var offset = center - (point + photoSize / 2);

                var compositor = visual1.Compositor;

                var geometry1 = compositor.CreatePathGeometry();
                var clip1 = compositor.CreateGeometricClip(geometry1);

                visual1.Clip = clip1;

                var remoteSize = frame * MathF.Min(ActualSize.X / frame.X, ActualSize.Y / frame.Y);
                var remoteMin = MathF.Min(remoteSize.X, remoteSize.Y);

                var ratio1 = photoSize.Y / remoteMin;
                var ratio2 = remoteMin / photoSize.Y;

                var side = (ActualSize.X - remoteSize.X) / 2;
                var part = (ActualSize.Y - remoteSize.Y) / 2;

                visual1.CenterPoint = new Vector3(center, 0);
                visual1.Scale = new Vector3(ratio1);
                visual1.Offset = new Vector3(-offset, 0);
                visual1.Opacity = 1;
                geometry1.Path = new CompositionPath(CanvasGeometry.CreateRoundedRectangle(null, side, part, ActualSize.X - side * 2, ActualSize.Y - part * 2, 8f * ratio2, 8f * ratio2));
            }
        }

        private void TempAccept_Click(object sender, RoutedEventArgs e)
        {
            _call.Accept(XamlRoot);
        }

        private void Discard_Click(object sender, RoutedEventArgs e)
        {
            _call.Discard();
        }

        private async void Mute_Click(object sender, RoutedEventArgs e)
        {
            if (Mute.IsChecked == false)
            {
                _call.AudioState = VoipAudioState.Muted;
            }
            else if (await MediaDevicePermissions.CheckAccessAsync(XamlRoot, MediaDeviceAccess.Audio, PopupTheme))
            {
                _call.AudioState = VoipAudioState.Active;
            }
        }

        private async void Camera_Click(object sender, RoutedEventArgs e)
        {
            if (Camera.IsChecked == true)
            {
                _call.VideoState = VoipVideoState.Inactive;
            }
            else if (await MediaDevicePermissions.CheckAccessAsync(XamlRoot, MediaDeviceAccess.Video, PopupTheme))
            {
                _call.VideoState = VoipVideoState.Active;
            }
        }

        private async void Screen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_call.IsScreenSharing)
                {
                    _call.VideoState = VoipVideoState.Inactive;
                }
                else
                {
                    var picker = new GraphicsCapturePicker();

                    var item = await picker.PickSingleItemAsync();
                    if (item == null)
                    {
                        return;
                    }

                    _call.ShareScreen(item);
                }
            }
            catch
            {
                // All bla bla
            }
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (_call.State is CallStateReady ready && _call.ClientService.TryGetUser(_call.UserId, out User user))
            {
                var message = string.Format("**{0}**\n\n{1}", Strings.VoipCallEncryptionEndToEnd, string.Format(Strings.CallEmojiKeyTooltip, user.FirstName));
                var title = string.Join(string.Empty, ready.Emojis);

                var popup = new MessagePopup
                {
                    Title = title,
                    Message = message,
                    PrimaryButtonText = Strings.OK
                };

                var service = ConnectedAnimationService.GetForCurrentView();
                var animation = service.PrepareToAnimate("Emoji", Emoji);
                animation.Configuration = new DirectConnectedAnimationConfiguration();

                void completed(object s, object _)
                {
                    animation.Completed -= completed;
                    Emoji.Opacity = 0;
                }

                animation.Completed += completed;

                void loading(object s, object _)
                {
                    popup.Loading -= loading;

                    var title = popup.GetChild<ContentControl>(x => x.Name == "Title");
                    if (title != null)
                    {
                        title.HorizontalAlignment = HorizontalAlignment.Center;
                        title.Margin = new Thickness(0, 0, 0, 12);
                        title.FontSize = 24;
                        title.FontFamily = new FontFamily("ms-appx:///Assets/Emoji/apple.ttf#Segoe UI Emoji");
                        animation.TryStart(title);
                    }
                }

                void closing(object s, object _)
                {
                    popup.Closing -= closing;
                    Emoji.Opacity = 1;

                    var title = popup.GetChild<ContentControl>(x => x.Name == "Title");
                    if (title != null)
                    {
                        var animation = service.PrepareToAnimate("Emoji", title);
                        animation.Configuration = new DirectConnectedAnimationConfiguration();
                        animation.TryStart(Emoji);
                    }
                }

                popup.Loading += loading;
                popup.Closing += closing;

                popup.RequestedTheme = PopupTheme;

                if (Resources.TryGet("TeachingTip", out TeachingTip cached))
                {
                    cached.IsOpen = false;
                }

                _ = popup.ShowQueuedAsync(XamlRoot);
            }
        }

        private ElementTheme PopupTheme => _maximizedVideo == VoipVideoActiveState.None
            ? ElementTheme.Light
            : ElementTheme.Dark;

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var videoId = _call.VideoInputId;
            var inputId = _call.AudioInputId;
            var outputId = _call.AudioOutputId;

            var video = new MenuFlyoutSubItem();
            video.Text = Strings.VoipDeviceCamera;
            video.Icon = MenuFlyoutHelper.CreateIcon(Icons.Camera);

            var input = new MenuFlyoutSubItem();
            input.Text = Strings.VoipDeviceInput;
            input.Icon = MenuFlyoutHelper.CreateIcon(Icons.MicOn);

            var output = new MenuFlyoutSubItem();
            output.Text = Strings.VoipDeviceOutput;
            output.Icon = MenuFlyoutHelper.CreateIcon(Icons.Speaker3);

            flyout.Items.Add(video);
            flyout.Items.Add(input);
            flyout.Items.Add(output);

            var hasVideo = _call.Devices.HasDevices(MediaDeviceClass.VideoInput);
            if (hasVideo is true)
            {
                foreach (var device in _call.Devices.GetDevices(MediaDeviceClass.VideoInput))
                {
                    var deviceItem = new ToggleMenuFlyoutItem();
                    deviceItem.Text = device.Name;
                    deviceItem.IsChecked = videoId == device.Id;
                    deviceItem.Click += (s, args) =>
                    {
                        _call.VideoInputId = device.Id;
                    };

                    video.Items.Add(deviceItem);
                }
            }
            else
            {
                video.CreateFlyoutItem(null, hasVideo.HasValue ? Strings.NotFoundCamera : Strings.Loading);
            }

            var hasInput = _call.Devices.HasDevices(MediaDeviceClass.AudioInput);
            if (hasInput is true)
            {
                foreach (var device in _call.Devices.GetDevices(MediaDeviceClass.AudioInput))
                {
                    var deviceItem = new ToggleMenuFlyoutItem();
                    deviceItem.Text = device.Name;
                    deviceItem.IsChecked = inputId == device.Id;
                    deviceItem.Click += (s, args) =>
                    {
                        _call.AudioInputId = device.Id;
                    };

                    input.Items.Add(deviceItem);
                }
            }
            else
            {
                input.CreateFlyoutItem(null, hasInput.HasValue ? Strings.NotFoundMicrophone : Strings.Loading);
            }

            var hasOutput = _call.Devices.HasDevices(MediaDeviceClass.AudioOutput);
            if (hasOutput is true)
            {
                foreach (var device in _call.Devices.GetDevices(MediaDeviceClass.AudioOutput))
                {
                    var deviceItem = new ToggleMenuFlyoutItem();
                    deviceItem.Text = device.Name;
                    deviceItem.IsChecked = outputId == device.Id;
                    deviceItem.Click += (s, args) =>
                    {
                        _call.AudioOutputId = device.Id;
                    };

                    output.Items.Add(deviceItem);
                }
            }
            else
            {
                output.CreateFlyoutItem(null, hasOutput.HasValue ? Strings.NotFoundSpeakers : Strings.Loading);
            }

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedLeft);
        }

        // TODO: this feature is not fundamental

        #region Interactions

        //private void Viewfinder_PointerPressed(object sender, PointerRoutedEventArgs e)
        //{
        //    _viewfinderPressed = true;
        //    Viewfinder.CapturePointer(e.Pointer);

        //    var pointer = e.GetCurrentPoint(this);
        //    var point = pointer.Position.ToVector2();
        //    _viewfinderDelta = new Vector2(_viewfinder.Offset.X - point.X, _viewfinder.Offset.Y - point.Y);
        //}

        //private void Viewfinder_PointerMoved(object sender, PointerRoutedEventArgs e)
        //{
        //    if (!_viewfinderPressed)
        //    {
        //        return;
        //    }

        //    var pointer = e.GetCurrentPoint(this);
        //    var delta = _viewfinderDelta + pointer.Position.ToVector2();

        //    _viewfinder.Offset = new Vector3(delta, 0);
        //}

        //private void Viewfinder_PointerReleased(object sender, PointerRoutedEventArgs e)
        //{
        //    _viewfinderPressed = false;
        //    Viewfinder.ReleasePointerCapture(e.Pointer);

        //    var pointer = e.GetCurrentPoint(this);
        //    var offset = _viewfinderDelta + pointer.Position.ToVector2();

        //    // Padding maybe
        //    var p = 8;

        //    var w = (float)ActualWidth - 146 - p * 2;
        //    var h = (float)ActualHeight - 110 - p * 2;

        //    _viewfinderOffset = new Vector2((offset.X - p) / w, (offset.Y - p) / h);

        //    CheckConstraints();
        //}

        //private void CheckConstraints()
        //{
        //    if (ViewfinderPanel.Visibility == Visibility.Collapsed)
        //    {
        //        return;
        //    }

        //    // Padding maybe
        //    var p = 8;

        //    var w = (float)ActualWidth;
        //    var h = (float)ActualHeight;

        //    if (w == 0 || h == 0)
        //    {
        //        return;
        //    }

        //    var x1 = Math.Max(0, Math.Min(w - 146 - p * 2, _viewfinderOffset.X * (w - 146 - p * 2))) + p;
        //    var y1 = Math.Max(0, Math.Min(h - 110 - p * 2, _viewfinderOffset.Y * (h - 110 - p * 2))) + p;

        //    var x2 = x1 + 146;
        //    var y2 = y1 + 110;

        //    if (Math.Min(x1, w - x2) < Math.Min(y1, h - y2))
        //    {
        //        if (x1 < w - x2)
        //        {
        //            x1 = p;
        //        }
        //        else
        //        {
        //            x1 = w - 146 - p;
        //        }
        //    }
        //    else
        //    {
        //        if (y1 < h - y2)
        //        {
        //            y1 = p;
        //        }
        //        else
        //        {
        //            y1 = h - 110 - p;
        //        }
        //    }

        //    var bx1 = (w - 240) / 2;
        //    var bx2 = bx1 + 240;

        //    if (y2 > h / 2 && ((x1 >= bx1 && x1 <= bx2) || (x2 >= bx1 && x2 <= bx2)))
        //    {
        //        y1 = h - 110 - 72 - p;
        //    }

        //    if (x1 != _viewfinder.Offset.X || y1 != _viewfinder.Offset.Y)
        //    {
        //        var anim = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
        //        anim.InsertKeyFrame(0, _viewfinder.Offset);
        //        anim.InsertKeyFrame(1, new Vector3(x1, y1, 0));

        //        var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        //        batch.Completed += (s, args) =>
        //        {
        //            _viewfinder.Offset = new Vector3(x1, y1, 0);
        //            _viewfinderOffset = new Vector2((x1 - p) / (w - 146 - p * 2), (y1 - p) / (h - 110 - p * 2));
        //        };

        //        _viewfinder.StartAnimation("Offset", anim);
        //        batch.End();
        //    }
        //}

        #endregion
    }

    public partial class WindowEx : UserControlEx
    {
        public WindowEx()
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.White;
            titleBar.ButtonHoverBackgroundColor = ColorEx.FromHex(0x19FFFFFF);
            titleBar.ButtonHoverForegroundColor = ColorEx.FromHex(0xCCFFFFFF);
            titleBar.ButtonPressedBackgroundColor = ColorEx.FromHex(0x33FFFFFF);
            titleBar.ButtonPressedForegroundColor = ColorEx.FromHex(0x99FFFFFF);
        }

        public async void Close()
        {
            try
            {
                if (XamlRoot.Content is RootPage root)
                {
                    root.PresentContent(null);
                    return;
                }
            }
            catch
            {
                // XamlRoot.Content seems to throw a NullReferenceException
                // whenever corresponding window has been already closed.
            }

            await WindowContext.Current.ConsolidateAsync();
        }

        protected async void RestoreWindow()
        {
            var applicationView = ApplicationView.GetForCurrentView();
            if (applicationView.ViewMode != ApplicationViewMode.CompactOverlay)
            {
                return;
            }

            var restored = await applicationView.TryEnterViewModeAsync(ApplicationViewMode.Default);
            if (restored)
            {
                applicationView.TryResizeView(new Size(720, 540));
            }
        }
    }
}
