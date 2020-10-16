using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Native.Calls;
using Unigram.Services;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views
{
    public sealed partial class VoIPPage : Page, IDisposable
    {
        private Visual _descriptionVisual;
        private Visual _largeVisual;
        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;
        private Compositor _compositor;

        private bool _collapsed = true;

        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly IEventAggregator _aggregator;

        private IVoipService _service;

        private VoipState _state;
        private IList<string> _emojis;

        private int _debugTapped;
        private ContentDialog _debugDialog;

        private DispatcherTimer _debugTimer;
        private DispatcherTimer _durationTimer;

        private bool _viewfinderPressed;
        private Vector2 _viewfinderDelta;
        private Vector2 _viewfinderOffset = Vector2.One;
        private Visual _viewfinder;

        private bool _disposed;

        public OverlayPage Dialog { get; set; }

        public VoIPPage(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, IVoipService voipService)
        {
            InitializeComponent();

            _protoService = protoService;
            _cacheService = cacheService;
            _aggregator = aggregator;

            _service = voipService;

            _durationTimer = new DispatcherTimer();
            _durationTimer.Interval = TimeSpan.FromMilliseconds(500);
            _durationTimer.Tick += DurationTimer_Tick;

            _debugTimer = new DispatcherTimer();
            _debugTimer.Interval = TimeSpan.FromMilliseconds(500);
            _debugTimer.Tick += DebugTimer_Tick;

            #region Reset

            LargeEmoji0.Source = null;
            LargeEmoji1.Source = null;
            LargeEmoji2.Source = null;
            LargeEmoji3.Source = null;

            #endregion

            #region Composition

            _descriptionVisual = ElementCompositionPreview.GetElementVisual(DescriptionLabel);
            _largeVisual = ElementCompositionPreview.GetElementVisual(LargePanel);
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

            var viewfinder = DropShadowEx.Attach(ViewfinderShadow, 20, 0.25f);
            viewfinder.RelativeSizeAdjustment = Vector2.One;

            #endregion

            //var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            //titleBar.ButtonBackgroundColor = Colors.Transparent;
            //titleBar.ButtonForegroundColor = Colors.White;
            //titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            //titleBar.ButtonInactiveForegroundColor = Colors.White;

            //Window.Current.SetTitleBar(BlurPanel);

            _viewfinder = ElementCompositionPreview.GetElementVisual(ViewfinderPanel);

            ViewfinderPanel.PointerPressed += Viewfinder_PointerPressed;
            ViewfinderPanel.PointerMoved += Viewfinder_PointerMoved;
            ViewfinderPanel.PointerReleased += Viewfinder_PointerReleased;

            if (voipService.Call != null)
            {
                Update(voipService.Call, voipService.CallStarted);
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
            var point = pointer.Position.AsVector2();
            _viewfinderDelta = new Vector2(_viewfinder.Offset.X - point.X, _viewfinder.Offset.Y - point.Y);
        }

        private void Viewfinder_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_viewfinderPressed)
            {
                return;
            }

            var pointer = e.GetCurrentPoint(this);
            var delta = _viewfinderDelta + pointer.Position.AsVector2();

            _viewfinder.Offset = new Vector3(delta, 0);
        }

        private void Viewfinder_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _viewfinderPressed = false;
            Viewfinder.ReleasePointerCapture(e.Pointer);

            var pointer = e.GetCurrentPoint(this);
            var offset = _viewfinderDelta + pointer.Position.AsVector2();

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
                var anim = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                anim.InsertKeyFrame(0, _viewfinder.Offset);
                anim.InsertKeyFrame(1, new Vector3(x1, y1, 0));

                var batch = Window.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
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
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        }

        public void Dispose()
        {
            _disposed = true;
            _debugTimer.Stop();
            _durationTimer.Stop();

            var controller = _service.Manager;
            if (controller != null)
            {
                controller.StateUpdated -= OnStateUpdated;
                controller.SignalBarsUpdated -= OnSignalBarsUpdated;
                controller.RemoteMediaStateUpdated -= OnRemoteMediaStateUpdated;

                controller.SetIncomingVideoOutput(null);
                //_controller = null;
            }

            var capturer = _service.Capturer;
            if (capturer != null)
            {
                capturer.SetOutput(null);
            }
        }

        public void Connect(VoipManager controller)
        {
            //_controller = controller;

            //_controller.SetIncomingVideoOutput(BackgroundPanel);

            // Let's avoid duplicated events
            controller.StateUpdated -= OnStateUpdated;
            controller.SignalBarsUpdated -= OnSignalBarsUpdated;
            controller.RemoteMediaStateUpdated -= OnRemoteMediaStateUpdated;

            controller.StateUpdated += OnStateUpdated;
            controller.SignalBarsUpdated += OnSignalBarsUpdated;
            controller.RemoteMediaStateUpdated += OnRemoteMediaStateUpdated;

            //controller.SetMuteMicrophone(_isMuted);

            //OnStateUpdated(controller, controller.GetConnectionState());
            //OnSignalBarsUpdated(controller, controller.GetSignalBarsCount());
        }

        private bool _capturerWasNull = true;

        public void Connect(VoipVideoCapture capturer)
        {
            if (capturer != null && _capturerWasNull)
            {
                _capturerWasNull = false;

                Video.IsChecked = true;
                ViewfinderPanel.Visibility = Visibility.Visible;

                capturer.SetOutput(Viewfinder);
            }
            else if (capturer == null && !_capturerWasNull)
            {
                _capturerWasNull = true;

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
            _blurVisual.Size = e.NewSize.AsVector2();

            if (_collapsed)
            {
                var transform = SmallPanel.TransformToVisual(LargeEmojiLabel);
                var position = transform.TransformPoint(new Point());

                _descriptionVisual.Opacity = 0;
                _largeVisual.Offset = new Vector3(position.AsVector2(), 0);
                _largeVisual.Scale = new Vector3(0.5f);
                _blurBrush.Properties.InsertScalar("Blur.BlurAmount", 0);
            }

            CheckConstraints();
        }

        public void Update(Call call, DateTime started)
        {
            if (_disposed)
            {
                return;
            }

            var user = _cacheService.GetUser(call.UserId);
            if (user != null)
            {
                Image.Source = PlaceholderHelper.GetUser(_protoService, user, 144);

                //if (user.ProfilePhoto != null)
                //{
                //    var file = user.ProfilePhoto.Big;
                //    if (file.Local.IsDownloadingCompleted)
                //    {
                //        Image.Source = new BitmapImage(new Uri("file:///" + file.Local.Path));
                //        BackgroundPanel.Background = new SolidColorBrush(Colors.Transparent);
                //    }
                //    else if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive)
                //    {
                //        Image.Source = null;
                //        BackgroundPanel.Background = PlaceholderHelper.GetBrush(user.Id);

                //        _protoService?.DownloadFile(file.Id, 1, 0);
                //    }
                //}
                //else
                //{
                //    Image.Source = null;
                //    BackgroundPanel.Background = PlaceholderHelper.GetBrush(user.Id);
                //}

                FromLabel.Text = user.GetFullName();
                DescriptionLabel.Text = string.Format(Strings.Resources.CallEmojiKeyTooltip, user.FirstName);

                AudioOffText.Text = string.Format(Strings.Resources.VoipUserMicrophoneIsOff, user.FirstName);
                VideoOffText.Text = string.Format(Strings.Resources.VoipUserCameraIsOff, user.FirstName);
            }

            if (call.State is CallStateReady ready)
            {
                _emojis = ready.Emojis;

                for (int i = 0; i < ready.Emojis.Count; i++)
                {
                    var imageLarge = FindName($"LargeEmoji{i}") as Image;
                    var source = Emoji.BuildUri(_emojis[i]);

                    imageLarge.Source = new BitmapImage(new Uri(source));
                }
            }

            switch (call.State)
            {
                case CallStatePending pending:
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
                        ? Strings.Resources.VoipRinging
                        : pending.IsCreated
                        ? Strings.Resources.VoipWaiting
                        : Strings.Resources.VoipRequesting
                        : Strings.Resources.VoipIncoming;
                    break;
                case CallStateExchangingKeys exchangingKeys:
                    StateLabel.Content = Strings.Resources.VoipConnecting;
                    break;
                case CallStateHangingUp hangingUp:
                    StateLabel.Content = Strings.Resources.VoipHangingUp;
                    break;
                case CallStateDiscarded discarded:
                    StateLabel.Content = discarded.Reason is CallDiscardReasonDeclined
                        ? Strings.Resources.VoipBusy
                        : Strings.Resources.VoipCallEnded;
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
                        StateLabel.Content = Strings.Resources.VoipConnecting;
                        break;
                    case VoipState.Established:
                        _state = newState;
                        StateLabel.Content = "00:00";

                        SignalBarsLabel.Visibility = Visibility.Visible;
                        StartUpdatingCallDuration();

                        sender.SetIncomingVideoOutput(BackgroundPanel);
                        break;
                    case VoipState.Failed:
                        //switch (sender.GetLastError())
                        //{
                        //    case libtgvoip.Error.Incompatible:
                        //    case libtgvoip.Error.Timeout:
                        //    case libtgvoip.Error.Unknown:
                        //        _state = newState;
                        //        StateLabel.Content = Strings.Resources.VoipFailed;
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

            if (_state == VoipState.Established)
            {
                var duration = DateTime.Now - _service.CallStarted;
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
            _largeVisual.Offset = new Vector3(position.AsVector2(), 0);
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
            offsetAnimation.InsertKeyFrame(1, new Vector3(position.AsVector2(), 0));
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
                _protoService.Send(new CreateCall(call.UserId, _service.GetProtocol(), false));
            }
            else if (call.State is CallStateReady || (call.State is CallStatePending && call.IsOutgoing))
            {
                var relay = 0L;
                if (_service.Manager != null)
                {
                    relay = _service.Manager.GetPreferredRelayId();
                }

                var duration = _state == VoipState.Established ? DateTime.Now - _service.CallStarted : TimeSpan.Zero;
                _protoService.Send(new DiscardCall(call.Id, false, (int)duration.TotalSeconds, _service.Capturer != null, relay));
            }
            else
            {
                var permissions = await _service.CheckAccessAsync(call.IsVideo);
                if (permissions == false)
                {
                    _protoService.Send(new DiscardCall(call.Id, false, 0, call.IsVideo, 0));
                }
                else
                {
                    _protoService.Send(new AcceptCall(call.Id, _service.GetProtocol()));
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

            var duration = _state == VoipState.Established ? DateTime.Now - _service.CallStarted : TimeSpan.Zero;
            _protoService.Send(new DiscardCall(call.Id, false, (int)duration.TotalSeconds, _service.Capturer != null, relay));
        }

        private void Video_Click(object sender, RoutedEventArgs e)
        {
            if (_service.Manager != null)
            {
                if (_service.Capturer != null)
                {
                    ViewfinderPanel.Visibility = Visibility.Collapsed;

                    _service.Capturer.SetOutput(null);
                    _service.Manager.SetVideoCapture(null);

                    _service.Capturer.Dispose();
                    _service.Capturer = null;
                }
                else
                {
                    ViewfinderPanel.Visibility = Visibility.Visible;

                    _service.Capturer = new VoipVideoCapture(string.Empty);

                    _service.Capturer.SetOutput(Viewfinder);
                    _service.Manager.SetVideoCapture(_service.Capturer);
                }

                CheckConstraints();
            }
        }

        private void Audio_Click(object sender, RoutedEventArgs e)
        {
            if (_service.Manager != null)
            {
                _service.Manager.SetMuteMicrophone(Audio.IsChecked == false);
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

            await dialog.ShowQueuedAsync();
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
