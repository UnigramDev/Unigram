using libtgvoip;
using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Entities;
using Unigram.Services;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics.Effects;
using Windows.Phone.Media.Devices;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace Unigram.Views
{
    public sealed partial class PhoneCallPage : Page, IDisposable
    {
        private Visual _descriptionVisual;
        private Visual _largeVisual;
        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;
        private Compositor _compositor;

        private bool _collapsed = true;

        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;

        private VoIPControllerWrapper _controller;
        private Call _call;

        private libtgvoip.CallState _state;
        private IList<string> _emojis;
        private DateTime _started;

        private int _debugTapped;
        private ContentDialog _debugDialog;

        private DispatcherTimer _debugTimer;
        private DispatcherTimer _durationTimer;

        private bool _disposed;

        public ContentDialogBase Dialog { get; set; }

        public PhoneCallPage(IProtoService protoService, ICacheService cacheService, bool extend)
        {
            this.InitializeComponent();

            _protoService = protoService;
            _cacheService = cacheService;

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

            #endregion

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            //coreTitleBar.IsVisibleChanged += CoreBar_IsVisibleChanged;
            coreTitleBar.ExtendViewIntoTitleBar = extend;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.White;

            Window.Current.SetTitleBar(GrabPanel);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1))
            {
                Routing.Visibility = Visibility.Visible;
                AudioRoutingManager.GetDefault().AudioEndpointChanged += AudioEndpointChanged;
            }
            else
            {
                Routing.Visibility = Visibility.Collapsed;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Unloaded");

            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1))
            {
                AudioRoutingManager.GetDefault().AudioEndpointChanged -= AudioEndpointChanged;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _debugTimer.Stop();
            _durationTimer.Stop();

            if (_controller != null)
            {
                _controller.CallStateChanged -= OnCallStateChanged;
                _controller.SignalBarsChanged -= OnSignalBarsChanged;
                _controller = null;
            }

            if (ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1))
            {
                AudioRoutingManager.GetDefault().AudioEndpointChanged -= AudioEndpointChanged;
            }
        }

        public void Connect(VoIPControllerWrapper controller)
        {
            _controller = controller;
            _controller.CallStateChanged += OnCallStateChanged;
            _controller.SignalBarsChanged += OnSignalBarsChanged;
        }

        //private void CoreBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        //{
        //    Debug.WriteLine("TitleBar height: " + sender.Height);

        //    SmallEmojiLabel.Margin = new Thickness(sender.SystemOverlayLeftInset, 20, sender.SystemOverlayRightInset, 0);
        //    OnSizeChanged(null, null);
        //}

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _blurVisual.Size = new Vector2((float)this.ActualWidth, (float)this.ActualHeight);

            if (_collapsed)
            {
                var transform = SmallPanel.TransformToVisual(LargeEmojiLabel);
                var position = transform.TransformPoint(new Point());

                _descriptionVisual.Opacity = 0;
                _largeVisual.Offset = new Vector3(position.ToVector2(), 0);
                _largeVisual.Scale = new Vector3(0.5f);
                _blurBrush.Properties.InsertScalar("Blur.Bluramount", 0);
            }
        }

        public void Update(Call call)
        {
            if (_disposed)
            {
                return;
            }

            _call = call;

            //if (_state != call.State)
            //{
            //    Debug.WriteLine("[{0:HH:mm:ss.fff}] State changed in app: " + tuple.Item1, DateTime.Now);

            //    _state = tuple.Item1;
            //    StateLabel.Content = StateToLabel(tuple.Item1);

            //    if (tuple.Item1 == TLPhoneCallState.Established)
            //    {
            //        SignalBarsLabel.Visibility = Visibility.Visible;
            //        StartUpdatingCallDuration();

            //        if (_emojis != null)
            //        {
            //            for (int i = 0; i < _emojis.Length; i++)
            //            {
            //                var imageLarge = FindName($"LargeEmoji{i}") as Image;
            //                var source = Emoji.BuildUri(_emojis[i]);

            //                imageLarge.Source = new BitmapImage(new Uri(source));
            //            }
            //        }
            //    }
            //}

            //if (tuple.Item2 is TLPhoneCallRequested call)
            //{
            //}

            var user = _cacheService.GetUser(call.UserId);
            if (user != null)
            {
                //if (user.HasPhoto && user.Photo is TLUserProfilePhoto)
                //{
                //    Image.Source = DefaultPhotoConverter.Convert(user, true) as ImageSource;
                //    GrabPanel.Background = new SolidColorBrush(Colors.Transparent);
                //}
                //else
                {
                    Image.Source = null;
                    GrabPanel.Background = PlaceholderHelper.GetBrush(user.Id);
                }

                FromLabel.Text = user.GetFullName();
                DescriptionLabel.Text = string.Format(Strings.Resources.CallEmojiKeyTooltip, user.FirstName);
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
                    StateLabel.Content = call.IsOutgoing
                        ? pending.IsReceived
                        ? Strings.Resources.VoipRinging
                        : pending.IsCreated
                        ? Strings.Resources.VoipWaiting
                        : Strings.Resources.VoipRequesting
                        : Strings.Resources.VoipIncoming;
                    break;
                case CallStateExchangingKeys exchangingKeys:
                    StateLabel.Content = Strings.Resources.VoipExchangingKeys;
                    break;
                case CallStateHangingUp hangingUp:
                    StateLabel.Content = Strings.Resources.VoipHangingUp;
                    break;
            }
        }

        private void OnCallStateChanged(VoIPControllerWrapper sender, libtgvoip.CallState newState)
        {
            _state = newState;

            this.BeginOnUIThread(() =>
            {
                switch (newState)
                {
                    case libtgvoip.CallState.WaitInit:
                    case libtgvoip.CallState.WaitInitAck:
                        StateLabel.Content = Strings.Resources.VoipConnecting;
                        break;
                    case libtgvoip.CallState.Established:
                        StateLabel.Content = "00:00";

                        SignalBarsLabel.Visibility = Visibility.Visible;
                        StartUpdatingCallDuration();
                        break;
                    case libtgvoip.CallState.Failed:
                        StateLabel.Content = Strings.Resources.VoipFailed;
                        break;
                }
            });
        }

        private void OnSignalBarsChanged(VoIPControllerWrapper sender, int newCount)
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
            _started = DateTime.Now;
            _durationTimer.Start();
        }

        private void DurationTimer_Tick(object sender, object e)
        {
            if (DurationLabel.Opacity == 0)
            {
                DurationLabel.Opacity = 1;
                StateLabel.Opacity = 0;
            }

            if (_state == libtgvoip.CallState.Established)
            {
                var duration = DateTime.Now - _started;
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
            _blurBrush.Properties.InsertScalar("Blur.Bluramount", 0);

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

        private void Hangup_Click(object sender, RoutedEventArgs e)
        {
            var call = _call;
            if (call == null)
            {
                return;
            }

            var duration = _state == libtgvoip.CallState.Established ? DateTime.Now - _started : TimeSpan.Zero;
            _protoService.Send(new DiscardCall(call.Id, false, (int)duration.TotalSeconds, 0));
            //await VoIPConnection.Current.SendRequestAsync("phone.DiscardCall", Tuple.Create(duration.TotalSeconds));
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

        private async void Mute_Click(object sender, RoutedEventArgs e)
        {
            var toggle = sender as ToggleButton;
            toggle.IsChecked = !toggle.IsChecked;
            await VoIPConnection.Current.SendRequestAsync(toggle.IsChecked.Value ? "phone.mute" : "phone.unmute");
        }

        private void Routing_Click(object sender, RoutedEventArgs e)
        {
            var routingManager = AudioRoutingManager.GetDefault();

            var toggle = sender as ToggleButton;
            toggle.IsChecked = !toggle.IsChecked;

            if (toggle.IsChecked.Value)
            {
                routingManager.SetAudioEndpoint(AudioRoutingEndpoint.Speakerphone);
            }
            else
            {
                if (routingManager.AvailableAudioEndpoints.HasFlag(AvailableAudioRoutingEndpoints.Bluetooth))
                {
                    routingManager.SetAudioEndpoint(AudioRoutingEndpoint.Bluetooth);
                }
                else if (routingManager.AvailableAudioEndpoints.HasFlag(AvailableAudioRoutingEndpoints.Earpiece))
                {
                    routingManager.SetAudioEndpoint(AudioRoutingEndpoint.Earpiece);
                }
            }
        }

        private async void AudioEndpointChanged(AudioRoutingManager sender, object args)
        {
            if (_disposed)
            {
                return;
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var routingManager = AudioRoutingManager.GetDefault();
                Routing.IsChecked = routingManager.GetAudioEndpoint() == AudioRoutingEndpoint.Speakerphone;
            });
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
            var result = await VoIPConnection.Current.GetDebugStringAsync();
            if (result != null && result.Item1 != null)
            {
                var text = new TextBlock();
                text.Text = result.Item1;
                text.Margin = new Thickness(12, 16, 12, 0);
                text.Style = Application.Current.Resources["BodyTextBlockStyle"] as Style;

                var scroll = new ScrollViewer();
                scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                scroll.VerticalScrollMode = ScrollMode.Auto;
                scroll.Content = text;

                var dialog = new ContentDialog { Style = BootStrapper.Current.Resources["ModernContentDialogStyle"] as Style };
                dialog.Title = $"libtgvoip v{result.Item2}";
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
        }

        private async void DebugTimer_Tick(object sender, object e)
        {
            if (_debugDialog == null)
            {
                _debugTimer.Stop();
                return;
            }

            var result = await VoIPConnection.Current.GetDebugStringAsync();
            if (result != null)
            {
                if (_debugDialog.Content is ScrollViewer scroll && scroll.Content is TextBlock text)
                {
                    text.Text = result.Item1;
                }
            }
        }
    }
}
