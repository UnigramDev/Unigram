//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Devices.Enumeration;
using Windows.System;
using Windows.System.Display;
using Windows.UI;
using Microsoft.UI.Composition;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Point = Windows.Foundation.Point;

namespace Telegram.Views.Calls
{
    public sealed partial class CallPage : Page, IDisposable
    {
        private readonly Visual _descriptionVisual;
        private readonly Visual _largeVisual;
        private readonly SpriteVisual _blurVisual;
        private readonly CompositionEffectBrush _blurBrush;
        private readonly Compositor _compositor;

        private readonly CompositionBlobVisual _visual;

        private bool _collapsed = true;

        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private readonly IVoipService _service;
        private VoipManager _manager;

        private readonly DispatcherQueue _dispatcherQueue;

        private VoipState _state;

        private int _debugTapped;
        private ContentDialog _debugDialog;

        private readonly DispatcherTimer _debugTimer;
        private readonly DispatcherTimer _durationTimer;

        private readonly DisplayRequest _displayRequest = new();

        private bool _viewfinderPressed;
        private Vector2 _viewfinderDelta;
        private Vector2 _viewfinderOffset = Vector2.One;
        private readonly Visual _viewfinder;

        private bool _disposed;

        public CallPage(IClientService clientService, IEventAggregator aggregator, IVoipService voipService)
        {
            InitializeComponent();
            Logger.Info();

            _clientService = clientService;
            _aggregator = aggregator;

            _service = voipService;
            _service.MutedChanged += OnMutedChanged;
            _service.AudioLevelUpdated += OnAudioLevelUpdated;

            _visual = new CompositionBlobVisual(Blob, 280, 280, 1.5f, ElementComposition.GetElementVisual(Image));
            _visual.FillColor = Colors.White;

            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _visual.StartAnimating();
            }

            //_dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _durationTimer = new DispatcherTimer();
            _durationTimer.Interval = TimeSpan.FromMilliseconds(500);
            _durationTimer.Tick += DurationTimer_Tick;

            _debugTimer = new DispatcherTimer();
            _debugTimer.Interval = TimeSpan.FromMilliseconds(500);
            _debugTimer.Tick += DebugTimer_Tick;

            #region Composition

            _descriptionVisual = ElementComposition.GetElementVisual(DescriptionLabel);
            _largeVisual = ElementComposition.GetElementVisual(LargePanel);
            _compositor = _largeVisual.Compositor;

            var graphicsEffect = new GaussianBlurEffect
            {
                Name = "Blur",
                BlurAmount = 0,
                BorderMode = EffectBorderMode.Hard,
                Source = new CompositionEffectSourceParameter("backdrop")
            };

            var effectFactory = _compositor.CreateEffectFactory(graphicsEffect, new[] { "Blur.BlurAmount" });
            var effectBrush = effectFactory.CreateBrush();
            var backdrop = _compositor.CreateBackdropBrush();
            effectBrush.SetSourceParameter("backdrop", backdrop);

            _blurBrush = effectBrush;
            _blurVisual = _compositor.CreateSpriteVisual();
            _blurVisual.Brush = _blurBrush;

            // Why does this crashes due to an access violation exception on certain devices?
            ElementCompositionPreview.SetElementChildVisual(BlurPanel, _blurVisual);

            VisualUtilities.DropShadow(ViewfinderShadow);

            #endregion

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.White;

            //Window.Current.SetTitleBar(BlurPanel);

            _viewfinder = ElementComposition.GetElementVisual(ViewfinderPanel);

            ViewfinderPanel.PointerPressed += Viewfinder_PointerPressed;
            ViewfinderPanel.PointerMoved += Viewfinder_PointerMoved;
            ViewfinderPanel.PointerReleased += Viewfinder_PointerReleased;

            if (voipService.Call != null)
            {
                Update(voipService.Call);
            }

            if (voipService.Manager != null)
            {
                Connect(voipService.Manager);
            }

            Connect(voipService.Capturer);
        }

        #region Interactions

        private void Viewfinder_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _viewfinderPressed = true;
            Viewfinder.CapturePointer(e.Pointer);

            var pointer = e.GetCurrentPoint(this);
            var point = pointer.Position.ToVector2();
            _viewfinderDelta = new Vector2(_viewfinder.Offset.X - point.X, _viewfinder.Offset.Y - point.Y);
        }

        private void Viewfinder_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_viewfinderPressed)
            {
                return;
            }

            var pointer = e.GetCurrentPoint(this);
            var delta = _viewfinderDelta + pointer.Position.ToVector2();

            _viewfinder.Offset = new Vector3(delta, 0);
        }

        private void Viewfinder_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _viewfinderPressed = false;
            Viewfinder.ReleasePointerCapture(e.Pointer);

            var pointer = e.GetCurrentPoint(this);
            var offset = _viewfinderDelta + pointer.Position.ToVector2();

            // Padding maybe
            var p = 8;

            var w = (float)ActualWidth - 146 - p * 2;
            var h = (float)ActualHeight - 110 - p * 2;

            _viewfinderOffset = new Vector2((offset.X - p) / w, (offset.Y - p) / h);

            CheckConstraints();
        }

        private void CheckConstraints()
        {
            if (ViewfinderPanel.Visibility == Visibility.Collapsed)
            {
                return;
            }

            // Padding maybe
            var p = 8;

            var w = (float)ActualWidth;
            var h = (float)ActualHeight;

            if (w == 0 || h == 0)
            {
                return;
            }

            var x1 = Math.Max(0, Math.Min(w - 146 - p * 2, _viewfinderOffset.X * (w - 146 - p * 2))) + p;
            var y1 = Math.Max(0, Math.Min(h - 110 - p * 2, _viewfinderOffset.Y * (h - 110 - p * 2))) + p;

            var x2 = x1 + 146;
            var y2 = y1 + 110;

            if (Math.Min(x1, w - x2) < Math.Min(y1, h - y2))
            {
                if (x1 < w - x2)
                {
                    x1 = p;
                }
                else
                {
                    x1 = w - 146 - p;
                }
            }
            else
            {
                if (y1 < h - y2)
                {
                    y1 = p;
                }
                else
                {
                    y1 = h - 110 - p;
                }
            }

            var bx1 = (w - 240) / 2;
            var bx2 = bx1 + 240;

            if (y2 > h / 2 && ((x1 >= bx1 && x1 <= bx2) || (x2 >= bx1 && x2 <= bx2)))
            {
                y1 = h - 110 - 72 - p;
            }

            if (x1 != _viewfinder.Offset.X || y1 != _viewfinder.Offset.Y)
            {
                var anim = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, _viewfinder.Offset);
                anim.InsertKeyFrame(1, new Vector3(x1, y1, 0));

                var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
                batch.Completed += (s, args) =>
                {
                    _viewfinder.Offset = new Vector3(x1, y1, 0);
                    _viewfinderOffset = new Vector2((x1 - p) / (w - 146 - p * 2), (y1 - p) / (h - 110 - p * 2));
                };

                _viewfinder.StartAnimation("Offset", anim);
                batch.End();
            }
        }

        #endregion

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _displayRequest.RequestActive();
            }
            catch { }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Dispose();

            BackgroundPanel.RemoveFromVisualTree();
            Viewfinder.RemoveFromVisualTree();

            try
            {
                _displayRequest.RequestRelease();
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _debugTimer.Stop();
            _durationTimer.Stop();

            _incomingToken?.Stop();
            _outgoingToken?.Stop();

            _service.MutedChanged -= OnMutedChanged;
            _service.AudioLevelUpdated -= OnAudioLevelUpdated;

            if (_manager != null)
            {
                _manager.StateUpdated -= OnStateUpdated;
                _manager.SignalBarsUpdated -= OnSignalBarsUpdated;
                _manager.RemoteMediaStateUpdated -= OnRemoteMediaStateUpdated;

                _manager.SetIncomingVideoOutput(null);
                _manager = null;
            }

            var capturer = _service.Capturer;
            capturer?.SetOutput(null);
        }

        private void OnMutedChanged(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(() => Audio.IsChecked = !_service.IsMuted);
        }

        private void OnAudioLevelUpdated(object sender, float average)
        {
            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _visual.UpdateLevel(average);
            }
        }

        public void Connect(VoipManager controller)
        {
            if (_disposed)
            {
                return;
            }

            if (_manager != null)
            {
                // Let's avoid duplicated events
                _manager.StateUpdated -= OnStateUpdated;
                _manager.SignalBarsUpdated -= OnSignalBarsUpdated;
                _manager.RemoteMediaStateUpdated -= OnRemoteMediaStateUpdated;

                if (_manager != controller)
                {
                    _manager.SetIncomingVideoOutput(BackgroundPanel);
                }
            }

            if (_manager != controller)
            {
                _incomingToken?.Stop();
                _incomingToken = controller.SetIncomingVideoOutput(BackgroundPanel);
            }

            controller.StateUpdated += OnStateUpdated;
            controller.SignalBarsUpdated += OnSignalBarsUpdated;
            controller.RemoteMediaStateUpdated += OnRemoteMediaStateUpdated;

            _manager = controller;

            //controller.SetMuteMicrophone(_isMuted);

            //OnStateUpdated(controller, controller.GetConnectionState());
            //OnSignalBarsUpdated(controller, controller.GetSignalBarsCount());
        }

        private bool _capturerWasNull = true;

        public void Connect(VoipCaptureBase capturer)
        {
            if (_disposed)
            {
                return;
            }

            if (capturer != null && _capturerWasNull)
            {
                _capturerWasNull = false;

                Screen.IsChecked = capturer is VoipScreenCapture;
                Video.IsChecked = capturer is VoipVideoCapture;
                ViewfinderPanel.Visibility = Visibility.Visible;

                _outgoingToken?.Stop();
                _outgoingToken = capturer.SetOutput(Viewfinder, false);
            }
            else if (capturer == null && !_capturerWasNull)
            {
                _capturerWasNull = true;

                Screen.IsChecked = false;
                Video.IsChecked = false;
                ViewfinderPanel.Visibility = Visibility.Collapsed;
            }

            CheckConstraints();
        }

        private void OnRemoteMediaStateUpdated(VoipManager sender, RemoteMediaStateUpdatedEventArgs args)
        {
            this.BeginOnUIThread(() =>
            {
                AudioOff.Visibility = args.Audio == VoipAudioState.Muted
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                VideoOff.Visibility = args.Video == VoipVideoState.Inactive && _service.Capturer != null
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                BackgroundPanel.Visibility = args.Video == VoipVideoState.Inactive
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                if (args.Video == VoipVideoState.Inactive)
                {
                    if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
                    {
                        _visual.StartAnimating();
                    }
                    else
                    {
                        _visual.StopAnimating();
                        _visual.Clear();
                    }
                }
                else
                {
                    _visual.StopAnimating();
                }
            });
        }

        //private void CoreBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        //{
        //    Debug.WriteLine("TitleBar height: " + sender.Height);

        //    SmallEmojiLabel.Margin = new Thickness(sender.SystemOverlayLeftInset, 20, sender.SystemOverlayRightInset, 0);
        //    OnSizeChanged(null, null);
        //}

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _blurVisual.Size = e.NewSize.ToVector2();

            if (_collapsed)
            {
                var transform = SmallPanel.TransformToVisual(LargeEmojiLabel);
                var position = transform.TransformPoint(new Point());

                _descriptionVisual.Opacity = 0;
                _largeVisual.Offset = new Vector3(position.ToVector2(), 0);
                _largeVisual.Scale = new Vector3(0.5f);
                _blurBrush.Properties.InsertScalar("Blur.BlurAmount", 0);
            }

            CheckConstraints();
        }

        public void Update(Call call)
        {
            if (_disposed)
            {
                return;
            }

            var user = _clientService.GetUser(call.UserId);
            if (user != null)
            {
                Image.SetUser(_clientService, user, 280);

                FromLabel.Text = user.FullName();
                DescriptionLabel.Text = string.Format(Strings.CallEmojiKeyTooltip, user.FirstName);

                AudioOffText.Text = string.Format(Strings.VoipUserMicrophoneIsOff, user.FirstName);
                VideoOffText.Text = string.Format(Strings.VoipUserCameraIsOff, user.FirstName);
            }

            if (call.State is CallStateReady ready)
            {
                for (int i = 0; i < ready.Emojis.Count; i++)
                {
                    var textLarge = FindName($"LargeEmoji{i}") as TextBlock;
                    textLarge.Text = ready.Emojis[i] + "\uFE0F";
                }
            }

            switch (call.State)
            {
                case CallStatePending:
                    if (call.IsOutgoing)
                    {
                        ResetUI();
                    }
                    else
                    {
                        Audio.Visibility = Visibility.Collapsed;
                        Video.Visibility = Visibility.Collapsed;

                        Close.Visibility = Visibility.Collapsed;
                        Close.Margin = new Thickness();

                        Accept.IsChecked = false;
                        Accept.Margin = new Thickness(8, 0, 0, 0);
                        Accept.Visibility = Visibility.Visible;

                        Discard.Margin = new Thickness(0, 0, 8, 0);
                        Discard.Visibility = Visibility.Visible;
                    }
                    break;
                case CallStateDiscarded discarded:
                    if (call.IsOutgoing && discarded.Reason is CallDiscardReasonDeclined)
                    {
                        Audio.Visibility = Visibility.Collapsed;
                        Video.Visibility = Visibility.Collapsed;

                        Close.Margin = new Thickness(8, 0, 0, 0);
                        Close.Visibility = Visibility.Visible;

                        Accept.IsChecked = false;
                        Accept.Margin = new Thickness(0, 0, 8, 0);
                        Accept.Visibility = Visibility.Visible;

                        Discard.Visibility = Visibility.Collapsed;
                        Discard.Margin = new Thickness();
                    }
                    break;
                default:
                    ResetUI();
                    break;
            }

            switch (call.State)
            {
                case CallStatePending pending:
                    StateLabel.Content = call.IsOutgoing
                        ? pending.IsReceived
                        ? Strings.VoipRinging
                        : pending.IsCreated
                        ? Strings.VoipWaiting
                        : Strings.VoipRequesting
                        : Strings.VoipIncoming;
                    break;
                case CallStateExchangingKeys:
                    StateLabel.Content = Strings.VoipConnecting;
                    break;
                case CallStateHangingUp:
                    StateLabel.Content = Strings.VoipHangingUp;
                    break;
                case CallStateDiscarded discarded:
                    StateLabel.Content = discarded.Reason is CallDiscardReasonDeclined
                        ? Strings.VoipBusy
                        : Strings.VoipCallEnded;
                    break;
            }
        }

        private void ResetUI()
        {
            Audio.Visibility = Visibility.Visible;
            Video.Visibility = Visibility.Visible;

            Close.Visibility = Visibility.Collapsed;
            Close.Margin = new Thickness();

            Accept.IsChecked = true;
            Accept.Visibility = Visibility.Visible;
            Accept.Margin = new Thickness();

            Discard.Margin = new Thickness();
            Discard.Visibility = Visibility.Collapsed;
        }

        private void OnStateUpdated(VoipManager sender, VoipState newState)
        {
            this.BeginOnUIThread(() =>
            {
                switch (newState)
                {
                    case VoipState.WaitInit:
                    case VoipState.WaitInitAck:
                        _state = newState;
                        StateLabel.Content = Strings.VoipConnecting;
                        break;
                    case VoipState.Established:
                        _state = newState;
                        StateLabel.Content = "00:00";

                        SignalBarsLabel.Visibility = Visibility.Visible;
                        StartUpdatingCallDuration();

                        _incomingToken?.Stop();
                        _incomingToken = sender.SetIncomingVideoOutput(BackgroundPanel);
                        break;
                    case VoipState.Failed:
                        //switch (sender.GetLastError())
                        //{
                        //    case libtgvoip.Error.Incompatible:
                        //    case libtgvoip.Error.Timeout:
                        //    case libtgvoip.Error.Unknown:
                        //        _state = newState;
                        //        StateLabel.Content = Strings.VoipFailed;
                        //        break;
                        //}
                        break;
                }
            });
        }

        private void OnSignalBarsUpdated(VoipManager sender, int newCount)
        {
            this.BeginOnUIThread(() =>
            {
                SetSignalBars(newCount);
            });
        }

        public void SetSignalBars(int count)
        {
            for (int i = 1; i < 5; i++)
            {
                ((Rectangle)FindName($"Signal{i}")).Fill = Resources[count >= i ? "SignalBarForegroundBrush" : "SignalBarForegroundDisabledBrush"] as SolidColorBrush;
            }
        }

        private void StartUpdatingCallDuration()
        {
            //_started = _started == DateTime.MinValue ? DateTime.Now : _started;
            _durationTimer.Start();
        }

        private void DurationTimer_Tick(object sender, object e)
        {
            if (DurationLabel.Opacity == 0)
            {
                DurationLabel.Opacity = 1;
                StateLabel.Opacity = 0;
            }

            if (_state == VoipState.Established && _service.CallStarted is DateTime callStarted)
            {
                var duration = DateTime.Now - callStarted;
                DurationLabel.Text = duration.ToString(duration.TotalHours >= 1 ? "hh\\:mm\\:ss" : "mm\\:ss");
            }
            else
            {
                _durationTimer.Stop();
            }
        }

        private void SmallEmojiLabel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var transform = SmallPanel.TransformToVisual(LargeEmojiLabel);
            var position = transform.TransformPoint(new Point());

            _descriptionVisual.Opacity = 0;
            _largeVisual.Offset = new Vector3(position.ToVector2(), 0);
            _largeVisual.Scale = new Vector3(0.5f);
            _blurBrush.Properties.InsertScalar("Blur.BlurAmount", 0);

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            var opacityAnimation = _compositor.CreateScalarKeyFrameAnimation();
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            var scaleAnimation = _compositor.CreateVector3KeyFrameAnimation();
            var blurAnimation = _compositor.CreateScalarKeyFrameAnimation();

            opacityAnimation.Duration = TimeSpan.FromMilliseconds(300);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(300);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(300);
            blurAnimation.Duration = TimeSpan.FromMilliseconds(300);

            opacityAnimation.InsertKeyFrame(1, 1);
            offsetAnimation.InsertKeyFrame(1, new Vector3(0));
            scaleAnimation.InsertKeyFrame(1, new Vector3(1));
            blurAnimation.InsertKeyFrame(1, 20);

            _descriptionVisual.StartAnimation("Opacity", opacityAnimation);
            _largeVisual.StartAnimation("Offset", offsetAnimation);
            _largeVisual.StartAnimation("Scale", scaleAnimation);
            _blurBrush.Properties.StartAnimation("Blur.BlurAmount", blurAnimation);
            _collapsed = false;

            batch.End();

            //var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("EmojiAnimation", SmallEmojiLabel);
            //if (animation != null)
            //{
            //    EmojifyPanel.Visibility = Visibility.Visible;
            //    animation.TryStart(LargeEmojiLabel);
            //}
        }

        private void LargeEmojiLabel_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_descriptionVisual.Opacity == 0)
            {
                SmallEmojiLabel_Tapped(null, null);
                return;
            }

            var transform = SmallPanel.TransformToVisual(LargeEmojiLabel);
            var position = transform.TransformPoint(new Point());

            var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            var opacityAnimation = _compositor.CreateScalarKeyFrameAnimation();
            var offsetAnimation = _compositor.CreateVector3KeyFrameAnimation();
            var scaleAnimation = _compositor.CreateVector3KeyFrameAnimation();
            var blurAnimation = _compositor.CreateScalarKeyFrameAnimation();

            opacityAnimation.Duration = TimeSpan.FromMilliseconds(300);
            offsetAnimation.Duration = TimeSpan.FromMilliseconds(300);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(300);
            blurAnimation.Duration = TimeSpan.FromMilliseconds(300);

            opacityAnimation.InsertKeyFrame(1, 0);
            offsetAnimation.InsertKeyFrame(1, new Vector3(position.ToVector2(), 0));
            scaleAnimation.InsertKeyFrame(1, new Vector3(0.5f));
            blurAnimation.InsertKeyFrame(1, 0);

            _descriptionVisual.StartAnimation("Opacity", opacityAnimation);
            _largeVisual.StartAnimation("Offset", offsetAnimation);
            _largeVisual.StartAnimation("Scale", scaleAnimation);
            _blurBrush.Properties.StartAnimation("Blur.BlurAmount", blurAnimation);
            _collapsed = true;

            batch.End();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _aggregator.Publish(new UpdateCall(new Call { State = new CallStateDiscarded { Reason = new CallDiscardReasonEmpty() } }));
        }

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            var call = _service.Call;
            if (call == null)
            {
                return;
            }

            if (call.IsOutgoing && call.State is CallStateDiscarded discarded && discarded.Reason is CallDiscardReasonDeclined)
            {
                _clientService.Send(new CreateCall(call.UserId, _service.Protocol, false));
            }
            else if (call.State is CallStateReady || (call.State is CallStatePending && call.IsOutgoing))
            {
                var relay = 0L;
                if (_service.Manager != null)
                {
                    relay = _service.Manager.GetPreferredRelayId();
                }

                var duration = _service.CallStarted is DateTime callStarted ? DateTime.Now - callStarted : TimeSpan.Zero;
                _clientService.Send(new DiscardCall(call.Id, false, (int)duration.TotalSeconds, _service.Capturer != null, relay));
            }
            else
            {
                var permissions = await MediaDeviceWatcher.CheckAccessAsync(XamlRoot, call.IsVideo);
                if (permissions == false)
                {
                    _clientService.Send(new DiscardCall(call.Id, false, 0, call.IsVideo, 0));
                }
                else
                {
                    _clientService.Send(new AcceptCall(call.Id, _service.Protocol));
                }
            }
        }

        private void Hangup_Click(object sender, RoutedEventArgs e)
        {
            var call = _service.Call;
            if (call == null)
            {
                return;
            }

            var relay = 0L;
            if (_service.Manager != null)
            {
                relay = _service.Manager.GetPreferredRelayId();
            }

            var duration = _service.CallStarted is DateTime callStarted ? DateTime.Now - callStarted : TimeSpan.Zero;
            _clientService.Send(new DiscardCall(call.Id, false, (int)duration.TotalSeconds, _service.Capturer != null, relay));
        }

        private async void Screen_Click(object sender, RoutedEventArgs e)
        {
            await ToggleCapturingAsync(VoipCaptureType.Screencast);
        }

        private async void Video_Click(object sender, RoutedEventArgs e)
        {
            await ToggleCapturingAsync(VoipCaptureType.Video);
        }

        private async Task ToggleCapturingAsync(VoipCaptureType type)
        {
            var capturer = await _service.ToggleCapturingAsync(_service.CaptureType == type
                ? VoipCaptureType.None
                : type);

            if (capturer != null)
            {
                ViewfinderPanel.Visibility = Visibility.Visible;

                _outgoingToken?.Stop();
                _outgoingToken = capturer.SetOutput(Viewfinder, false);
            }
            else
            {
                ViewfinderPanel.Visibility = Visibility.Collapsed;
            }

            CheckConstraints();
        }

        private void Audio_Click(object sender, RoutedEventArgs e)
        {
            if (_service != null)
            {
                _service.IsMuted = Audio.IsChecked == false;
            }
        }

        private VoipVideoRendererToken _outgoingToken;
        private VoipVideoRendererToken _incomingToken;

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var videoId = _service.CurrentVideoInput;
            var inputId = _service.CurrentAudioInput;
            var outputId = _service.CurrentAudioOutput;

            var video = new MenuFlyoutSubItem();
            video.Text = Strings.VoipDeviceCamera;
            video.Icon = MenuFlyoutHelper.CreateIcon(Icons.Camera);

            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                var videoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                foreach (var device in videoDevices)
                {
                    var deviceItem = new ToggleMenuFlyoutItem();
                    deviceItem.Text = device.Name;
                    deviceItem.IsChecked = videoId == device.Id;
                    deviceItem.Click += (s, args) =>
                    {
                        _outgoingToken?.Stop();
                        _outgoingToken = _service.SetVideoInput(device.Id, Viewfinder);
                    };

                    video.Items.Add(deviceItem);
                }
            });

            var defaultInput = new ToggleMenuFlyoutItem();
            defaultInput.Text = Strings.Default;
            defaultInput.IsChecked = inputId == string.Empty;
            defaultInput.Click += (s, args) =>
            {
                _service.CurrentAudioInput = string.Empty;
            };

            var input = new MenuFlyoutSubItem();
            input.Text = Strings.VoipDeviceInput;
            input.Icon = MenuFlyoutHelper.CreateIcon(Icons.MicOn);
            input.Items.Add(defaultInput);

            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                var inputDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
                foreach (var device in inputDevices)
                {
                    var deviceItem = new ToggleMenuFlyoutItem();
                    deviceItem.Text = device.Name;
                    deviceItem.IsChecked = inputId == device.Id;
                    deviceItem.Click += (s, args) =>
                    {
                        _service.CurrentAudioInput = device.Id;
                    };

                    input.Items.Add(deviceItem);
                }
            });

            var defaultOutput = new ToggleMenuFlyoutItem();
            defaultOutput.Text = Strings.Default;
            defaultOutput.IsChecked = outputId == string.Empty;
            defaultOutput.Click += (s, args) =>
            {
                _service.CurrentAudioOutput = string.Empty;
            };

            var output = new MenuFlyoutSubItem();
            output.Text = Strings.VoipDeviceOutput;
            output.Icon = MenuFlyoutHelper.CreateIcon(Icons.Speaker3);
            output.Items.Add(defaultOutput);

            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
            {
                var outputDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
                foreach (var device in outputDevices)
                {
                    var deviceItem = new ToggleMenuFlyoutItem();
                    deviceItem.Text = device.Name;
                    deviceItem.IsChecked = outputId == device.Id;
                    deviceItem.Click += (s, args) =>
                    {
                        _service.CurrentAudioOutput = device.Id;
                    };

                    output.Items.Add(deviceItem);
                }
            });

            flyout.Items.Add(video);
            flyout.Items.Add(input);
            flyout.Items.Add(output);

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedLeft);
            }
        }

        private void DebugString_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_debugTapped == 9)
            {
                _debugTapped = 0;
                ShowDebugString();
            }
            else
            {
                _debugTapped++;
            }
        }

        private async void ShowDebugString()
        {
            if (_service.Manager == null)
            {
                return;
            }

            var debug = _service.Manager.GetDebugInfo();
            var version = "VoIPControllerWrapper.GetVersion()";

            var text = new TextBlock();
            text.Text = debug;
            text.Margin = new Thickness(12, 16, 12, 0);
            text.Style = Application.Current.Resources["BodyTextBlockStyle"] as Style;

            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.VerticalScrollMode = ScrollMode.Auto;
            scroll.Content = text;

            var dialog = new ContentPopup();
            dialog.Title = $"libtgvoip v{version}";
            dialog.Content = scroll;
            dialog.PrimaryButtonText = "OK";
            dialog.Closed += (s, args) =>
            {
                _debugDialog = null;
                _debugTimer.Stop();
            };

            _debugDialog = dialog;
            _debugTimer.Start();

            await dialog.ShowQueuedAsync(XamlRoot);
        }

        private void DebugTimer_Tick(object sender, object e)
        {
            if (_debugDialog == null || _service.Manager == null)
            {
                _debugTimer.Stop();
                return;
            }

            if (_debugDialog.Content is ScrollViewer scroll && scroll.Content is TextBlock text)
            {
                text.Text = _service.Manager.GetDebugInfo();
            }
        }
    }
}
