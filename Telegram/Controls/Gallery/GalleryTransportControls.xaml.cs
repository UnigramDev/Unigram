using LibVLCSharp.Shared;
using System;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Services.Keyboard;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.System;
using Windows.System.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryTransportControls : UserControl
    {
        private readonly DispatcherQueue _dispatcherQueue;
        private long _videoToken;

        private bool _loopingEnabled;
        private bool _playing;
        private bool _unloaded;

        private VideoPlayerBase _player;

        public GalleryTransportControls()
        {
            InitializeComponent();

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            var muted = SettingsService.Current.VolumeMuted;
            var volume = (int)Math.Round(SettingsService.Current.VolumeLevel * 100);
            var speed = SettingsService.Current.Playback.VideoSpeed;

            VolumeSlider.Value = muted ? 0 : volume;
            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;

            VolumeButton.Glyph = muted ? Icons.SpeakerMuteFilled : volume switch
            {
                int n when n > 50 => Icons.Speaker2Filled,
                int n when n > 0 => Icons.Speaker1Filled,
                _ => Icons.SpeakerMuteFilled
            };

            Automation.SetToolTip(VolumeButton, muted ? Strings.PlayerAudioUnmute : Strings.PlayerAudioMute);

            SpeedText.Text = string.Format("{0:N1}x", speed);
            SpeedButton.Badge = string.Format("{0:N1}x", speed);

            Slider.AddHandler(KeyDownEvent, new KeyEventHandler(Slider_KeyDown), true);
            Slider.AddHandler(PointerPressedEvent, new PointerEventHandler(Slider_PointerPressed), true);
            Slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(Slider_PointerReleased), true);
            Slider.AddHandler(PointerCanceledEvent, new PointerEventHandler(Slider_PointerCanceled), true);
            Slider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(Slider_PointerCaptureLost), true);
        }

        public bool IsFullScreen
        {
            get => FullScreenButton.IsChecked == true;
            set
            {
                FullScreenButton.IsChecked = value;
                FullScreenButton.Visibility = ApiInfo.IsXbox
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                Automation.SetToolTip(FullScreenButton, value ? Strings.AccDescrExitFullScreen : Strings.AccDescrFullScreen);
            }
        }

        public event RoutedEventHandler FullScreenClick
        {
            add => FullScreenButton.Click += value;
            remove => FullScreenButton.Click -= value;
        }

        public bool IsCompact
        {
            get => false;
            set
            {
                CompactButton.Glyph = value ? Icons.PictureInPictureExit : Icons.PictureInPictureEnter;
                Automation.SetToolTip(FullScreenButton, value ? Strings.AccDescrExitMiniPlayer : Strings.AccDescrMiniPlayer);

                if (value)
                {
                    LayoutRoot.Background = null;
                    FullScreenButton.Visibility = Visibility.Collapsed;
                }
            }
        }

        public event RoutedEventHandler CompactClick
        {
            add => CompactButton.Click += value;
            remove => CompactButton.Click -= value;
        }

        #region Scrubbing

        private bool _scrubbing;

        private void Slider_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Up)
            {
                _player.AddTime(5);
            }
            else if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Down)
            {
                _player.AddTime(-5);
            }
            else if (e.Key == VirtualKey.PageUp)
            {
                _player.AddTime(30);
            }
            else if (e.Key == VirtualKey.PageDown)
            {
                _player.AddTime(-30);
            }
            else if (e.Key == VirtualKey.Home)
            {
                _player.Position = 0;
            }
            else if (e.Key == VirtualKey.End)
            {
                _player.Position = _player.Duration;
            }
        }

        private void Slider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = true;
            PauseBeforeScrubbing();
        }

        private void Slider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_player != null)
            {
                _player.Position = (long)Slider.Value;
            }

            _scrubbing = false;
            PlayAfterScrubbing();
        }

        private void Slider_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
            PlayAfterScrubbing();
        }

        private void Slider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
            PlayAfterScrubbing();
        }

        #endregion

        public void Attach(GalleryMedia item, File file)
        {
            _loopingEnabled = item.IsLoop;

            Visibility = item.IsVideo && (item.IsVideoNote || !item.IsLoop)
                ? Visibility.Visible
                : Visibility.Collapsed;

            CompactButton.Visibility = ConvertCompactVisibility(item)
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateManager.Subscribe(this, item.ClientService, file, ref _videoToken, UpdateVideo);
            UpdateVideo(item, file);
        }

        private bool ConvertCompactVisibility(GalleryMedia item)
        {
            if (item != null && item.IsVideo && !item.IsLoop)
            {
                if (item is GalleryMessage message && message.IsProtected)
                {
                    return false;
                }

                return ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay);
            }

            return false;
        }

        private void UpdateVideo(object target, File file)
        {
            var offset = file.Local.DownloadOffset + file.Local.DownloadedPrefixSize;

            PositionBar.Maximum = file.Size;
            PositionBar.Value = file.Local.IsDownloadingCompleted || offset == file.Size ? 0 : offset;
        }

        private DisplayRequest _request;

        public void Attach(VideoPlayerBase mediaPlayer)
        {
            if (_player != null)
            {
                _player.PositionChanged -= OnPositionChanged;
                _player.DurationChanged -= OnDurationChanged;
                _player.IsPlayingChanged -= OnIsPlayingChanged;
                _player.VolumeChanged -= OnVolumeChanged;
                _player.Closed -= OnStopped;
            }

            if (_unloaded)
            {
                _player = null;
                return;
            }

            _player = mediaPlayer;

            if (_player != null)
            {
                _player.PositionChanged += OnPositionChanged;
                _player.DurationChanged += OnDurationChanged;
                _player.IsPlayingChanged += OnIsPlayingChanged;
                _player.VolumeChanged += OnVolumeChanged;
                _player.Closed += OnStopped;

                OnPositionChanged(_player, new VideoPlayerPositionChanged(_player.Position));
                OnDurationChanged(_player, new VideoPlayerDurationChanged(_player.Duration));
            }

            PlaybackButton.Glyph = Icons.PlayFilled24;
            Automation.SetToolTip(PlaybackButton, Strings.AccActionPlay);
        }

        private void OnVolumeChanged(VideoPlayerBase sender, VideoPlayerVolumeChanged args)
        {
            if (_player == null)
            {
                return;
            }

            _player.VolumeChanged -= OnVolumeChanged;

            _player.Mute = SettingsService.Current.VolumeMuted;
            _player.Volume = SettingsService.Current.VolumeLevel;
            _player.Rate = SettingsService.Current.Playback.VideoSpeed;
        }

        private void OnIsPlayingChanged(VideoPlayerBase sender, VideoPlayerIsPlayingChanged args)
        {
            if (args.IsPlaying)
            {
                PlaybackButton.Glyph = Icons.PauseFilled24;
                Automation.SetToolTip(PlaybackButton, Strings.AccActionPause);

                if (_request == null)
                {
                    _request = new DisplayRequest();
                    _request.TryRequestActive();
                }
            }
            else
            {
                PlaybackButton.Glyph = Icons.PlayFilled24;
                Automation.SetToolTip(PlaybackButton, Strings.AccActionPlay);

                if (_request != null)
                {
                    _request.TryRequestRelease();
                    _request = null;
                }
            }
        }

        private void OnStopped(VideoPlayerBase sender, EventArgs args)
        {
            //var source = _player == null || _player.State == VLCState.Stopped;
            //Visibility = source
            //    ? Visibility.Collapsed
            //    : Visibility.Visible;

            Visibility = Visibility.Collapsed;
        }

        private void OnPositionChanged(VideoPlayerBase sender, VideoPlayerPositionChanged args)
        {
            if (_player == null || _scrubbing)
            {
                return;
            }

            Slider.Value = args.Position;
            TimeText.Text = FormatTime(args.Position);
        }

        private void OnDurationChanged(VideoPlayerBase sender, VideoPlayerDurationChanged args)
        {
            if (_player == null)
            {
                return;
            }

            Slider.Maximum = args.Duration;
            LengthText.Text = FormatTime(args.Duration);
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_player == null)
            {
                return;
            }

            if (_scrubbing)
            {
                Slider.Value = e.NewValue;
                TimeText.Text = FormatTime(e.NewValue);
            }
        }

        private string FormatTime(double time)
        {
            var span = TimeSpan.FromMilliseconds(time);
            if (span.TotalHours >= 1)
            {
                return span.ToString("h\\:mm\\:ss");
            }
            else
            {
                return span.ToString("mm\\:ss");
            }
        }

        private void Speed_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreatePlaybackSpeed(_player.Rate, FlyoutPlacementMode.Top, UpdatePlaybackSpeed);
            flyout.ShowAt(SpeedButton, FlyoutPlacementMode.TopEdgeAlignedRight);
        }

        private void UpdatePlaybackSpeed(double value)
        {
            value = Math.Clamp(value, 0.5, 2);
            SettingsService.Current.Playback.VideoSpeed = value;

            _player.Rate = value;
            SpeedText.Text = string.Format("{0:N1}x", value);
            SpeedButton.Badge = string.Format("{0:N1}x", value);
        }

        private void ChangePlaybackSpeed(float amount)
        {
            if (_player != null)
            {
                UpdatePlaybackSpeed(_player.Rate + amount);
            }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            TogglePlaybackState();
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            var volume = (int)e.NewValue;
            var muted = false;

            if (volume == 0)
            {
                volume = 100;
                muted = true;
            }

            if (_player != null)
            {
                _player.Volume = volume / 100d;
                _player.Mute = muted;
            }

            SettingsService.Current.VolumeLevel = volume / 100d;
            SettingsService.Current.VolumeMuted = muted;

            VolumeButton.Glyph = muted ? Icons.SpeakerMuteFilled : volume switch
            {
                int n when n > 50 => Icons.Speaker2Filled,
                int n when n > 0 => Icons.Speaker1Filled,
                _ => Icons.SpeakerMuteFilled
            };

            Automation.SetToolTip(VolumeButton, Strings.PlayerAudioMute);
        }

        private void Volume_Click(object sender, RoutedEventArgs e)
        {
            var muted = !SettingsService.Current.VolumeMuted;
            var volume = (int)Math.Round(SettingsService.Current.VolumeLevel * 100);

            if (volume == 0 && !muted)
            {
                volume = 100;
                muted = false;
            }

            if (_player != null)
            {
                _player.Volume = volume / 100d;
                _player.Mute = muted;
            }

            SettingsService.Current.VolumeMuted = muted;

            VolumeSlider.ValueChanged -= VolumeSlider_ValueChanged;
            VolumeSlider.Value = muted ? 0 : volume;
            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;

            VolumeButton.Glyph = muted ? Icons.SpeakerMuteFilled : volume switch
            {
                int n when n > 50 => Icons.Speaker2Filled,
                int n when n > 0 => Icons.Speaker1Filled,
                _ => Icons.SpeakerMuteFilled
            };

            Automation.SetToolTip(VolumeButton, muted ? Strings.PlayerAudioUnmute : Strings.PlayerAudioMute);
        }

        public void TogglePlaybackState()
        {
            _player?.Toggle();
        }

        private void PlayAfterScrubbing()
        {
            if (_player == null || !_playing)
            {
                return;
            }

            _playing = false;
            _player.Play();
        }

        private void PauseBeforeScrubbing()
        {
            if (_player == null || !_player.IsPlaying)
            {
                return;
            }

            _playing = true;
            _player.Pause();
        }

        public void Stop()
        {
            Visibility = Visibility.Collapsed;

            if (_request != null)
            {
                _request.RequestRelease();
                _request = null;
            }
        }

        public void OnAcceleratorKeyActivated(InputKeyDownEventArgs args)
        {
            if (_player == null)
            {
                return;
            }

            var keyCode = (int)args.VirtualKey;

            if (args.VirtualKey is VirtualKey.Space or VirtualKey.K && args.OnlyKey)
            {
                TogglePlaybackState();
                args.Handled = true;
            }
            else if (args.VirtualKey is VirtualKey.Up && args.OnlyKey)
            {
                VolumeSlider.Value += 10;
                args.Handled = true;
            }
            else if (args.VirtualKey is VirtualKey.Down && args.OnlyKey)
            {
                VolumeSlider.Value -= 10;
                args.Handled = true;
            }
            else if ((args.VirtualKey is VirtualKey.J && args.OnlyKey) || (args.VirtualKey is VirtualKey.Left && args.OnlyControl))
            {
                _player.AddTime(-10000);
                args.Handled = true;
            }
            else if ((args.VirtualKey is VirtualKey.L && args.OnlyKey) || (args.VirtualKey is VirtualKey.Right && args.OnlyControl))
            {
                _player.AddTime(10000);
                args.Handled = true;
            }
            else if (keyCode is 188 or 190 && args.OnlyShift)
            {
                ChangePlaybackSpeed(keyCode is 188 ? -0.25f : 0.25f);
                args.Handled = true;
            }
        }

        public void Unload()
        {
            _unloaded = true;
            Attach(null);
        }
    }
}
