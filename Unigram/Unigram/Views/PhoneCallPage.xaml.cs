using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Converters;
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

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unigram.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PhoneCallPage : Page
    {
        private Visual _descriptionVisual;
        private Visual _largeVisual;
        private SpriteVisual _blurVisual;
        private CompositionEffectBrush _blurBrush;
        private Compositor _compositor;

        private bool _collapsed = true;

        private TLPhoneCallState _state;
        private string[] _emojis;
        private DateTime _started;

        private DispatcherTimer _durationTimer;

        public PhoneCallPage()
        {
            this.InitializeComponent();

            _durationTimer = new DispatcherTimer();
            _durationTimer.Interval = TimeSpan.FromMilliseconds(500);
            _durationTimer.Tick += DurationTimer_Tick;

            #region Reset

            LargeEmoji0.Source = null;
            LargeEmoji1.Source = null;
            LargeEmoji2.Source = null;
            LargeEmoji3.Source = null;

            #endregion

            #region Routing

            var routing = ApiInformation.IsApiContractPresent("Windows.Phone.PhoneContract", 1);
            if (routing)
            {
                Routing.Visibility = Visibility.Visible;
                AudioRoutingManager.GetDefault().AudioEndpointChanged += AudioEndpointChanged;
            }
            else
            {
                Routing.Visibility = Visibility.Collapsed;
            }

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

            ElementCompositionPreview.SetElementChildVisual(BlurPanel, _blurVisual);

            #endregion

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            //coreTitleBar.IsVisibleChanged += CoreBar_IsVisibleChanged;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.White;

            Window.Current.SetTitleBar(GrabPanel);
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

        public void SetCall(TLTuple<TLPhoneCallState, TLPhoneCallBase, TLUserBase, string> tuple)
        {
            if (_state != tuple.Item1)
            {
                Debug.WriteLine("[{0:HH:mm:ss.fff}] State changed in app: " + tuple.Item1, DateTime.Now);

                _state = tuple.Item1;
                StateLabel.Content = StateToLabel(tuple.Item1);

                if (tuple.Item1 == TLPhoneCallState.Established)
                {
                    StartUpdatingCallDuration();

                    if (_emojis != null)
                    {
                        for (int i = 0; i < _emojis.Length; i++)
                        {
                            var imageLarge = FindName($"LargeEmoji{i}") as Image;
                            var source = Emoji.BuildUri(_emojis[i]);

                            imageLarge.Source = new BitmapImage(new Uri(source));
                        }
                    }
                }
            }

            if (tuple.Item2 is TLPhoneCallRequested call)
            {
            }

            if (tuple.Item3 is TLUser user)
            {
                //try
                //{
                //    Image.Source = DefaultPhotoConverter.Convert(user.Photo, true) as ImageSource;
                //}
                //catch { }

                FromLabel.Text = user.FullName;
                TextBlockHelper.SetMarkdown(DescriptionLabel, string.Format("If these emoji are the same on **{0}**'s screen, this call is 100% secure.", user.FirstName));
            }

            if (tuple.Item4.Length > 0)
            {
                _emojis = tuple.Item4.Split(' ');
            }
        }

        private string StateToLabel(TLPhoneCallState state)
        {
            switch (state)
            {
                case TLPhoneCallState.WaitingIncoming:
                    return "Incoming call";
                case TLPhoneCallState.WaitInit:
                case TLPhoneCallState.WaitInitAck:
                    return "Connecting";
                case TLPhoneCallState.ExchangingKeys:
                    return "Exchanging encryption keys";
                case TLPhoneCallState.Waiting:
                    return "Waiting";
                case TLPhoneCallState.Ringing:
                    return "Ringing";
                case TLPhoneCallState.Requesting:
                    return "Requesting";
                case TLPhoneCallState.HangingUp:
                    return "Hanging up";
                case TLPhoneCallState.Ended:
                    return "Call ended";
                case TLPhoneCallState.Busy:
                    return "Line busy";
                case TLPhoneCallState.Established:
                    return "00:00";
                case TLPhoneCallState.Failed:
                    return "Failed to connect";
            }

            return null;
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

            if (_state == TLPhoneCallState.Established)
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

        private async void Hangup_Click(object sender, RoutedEventArgs e)
        {
            var duration = _state == TLPhoneCallState.Established ? DateTime.Now - _started : TimeSpan.Zero;
            await VoIPConnection.Current.SendRequestAsync("phone.discardCall", TLTuple.Create(duration.TotalSeconds));
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
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var routingManager = AudioRoutingManager.GetDefault();
                Routing.IsChecked = routingManager.GetAudioEndpoint() == AudioRoutingEndpoint.Speakerphone;
            });
        }
    }
}
