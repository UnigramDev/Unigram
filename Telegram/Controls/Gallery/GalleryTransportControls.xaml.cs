using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
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

            // Workaround Slider visual states bug
            var pointer = e.GetCurrentPoint(Slider);
            if (pointer.Position.X >= 0 && pointer.Position.X <= Slider.ActualWidth && pointer.Position.Y >= 0 && pointer.Position.Y <= Slider.ActualHeight)
            {
                VisualStateManager.GoToState(Slider, "PointerOver", false);
            }
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
            _loopingEnabled = item.IsLoopingEnabled;

            Visibility = item.IsVideo && (item.IsVideoNote || !item.IsLoopingEnabled)
                ? Visibility.Visible
                : Visibility.Collapsed;

            CompactButton.Visibility = ConvertCompactVisibility(item)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private bool ConvertCompactVisibility(GalleryMedia item)
        {
            if (item != null && item.IsVideo && !item.IsLoopingEnabled)
            {
                if (item is GalleryMessage message && message.HasProtectedContent)
                {
                    return false;
                }

                return ApplicationView.GetForCurrentView().IsViewModeSupported(ApplicationViewMode.CompactOverlay);
            }

            return false;
        }

        private DisplayRequest _request;

        public void Attach(VideoPlayerBase mediaPlayer)
        {
            if (_player != null)
            {
                _player.Ready += OnReady;
                _player.PositionChanged -= OnPositionChanged;
                _player.BufferedChanged -= OnBufferedChanged;
                _player.DurationChanged -= OnDurationChanged;
                _player.IsPlayingChanged -= OnIsPlayingChanged;
                _player.LevelsChanged -= OnLevelsChanged;
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
                _player.Ready += OnReady;
                _player.PositionChanged += OnPositionChanged;
                _player.BufferedChanged += OnBufferedChanged;
                _player.DurationChanged += OnDurationChanged;
                _player.IsPlayingChanged += OnIsPlayingChanged;
                _player.LevelsChanged += OnLevelsChanged;
                _player.Closed += OnStopped;

                OnPositionChanged(_player, new VideoPlayerPositionChangedEventArgs(_player.Position));
                OnBufferedChanged(_player, new VideoPlayerBufferedChangedEventArgs(_player.Buffered));
                OnDurationChanged(_player, new VideoPlayerDurationChangedEventArgs(_player.Duration));
                OnIsPlayingChanged(_player, new VideoPlayerIsPlayingChangedEventArgs(_player.IsPlaying));
                OnLevelsChanged(_player, new VideoPlayerLevelsChangedEventArgs(_player.Levels, _player.CurrentLevel, _player.IsCurrentLevelAuto));
            }
        }

        private bool _settingsCollapsed = true;

        private void ShowHideSettings(bool show)
        {
            if (_settingsCollapsed != show)
            {
                return;
            }

            _settingsCollapsed = !show;

            SpeedRoot.Visibility = show
                ? Visibility.Collapsed
                : Visibility.Visible;

            SettingsRoot.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnReady(VideoPlayerBase sender, EventArgs args)
        {
            _player.Mute = SettingsService.Current.VolumeMuted;
            _player.Volume = SettingsService.Current.VolumeLevel;
            _player.Rate = SettingsService.Current.Playback.VideoSpeed;
        }

        private void OnLevelsChanged(VideoPlayerBase sender, VideoPlayerLevelsChangedEventArgs args)
        {
            ShowHideSettings(args.Levels.Count > 0);

            if (args.CurrentLevel != null && !args.IsAuto)
            {
                var level = args.CurrentLevel;
                var quality = Math.Min(level.Width, level.Height);

                QualityText.Text = quality switch
                {
                    int n when n < 720 => "SD",
                    int n when n < 1440 => "HD",
                    int n when n < 2160 => "2K",
                    int n when n < 4320 => "4K",
                    _ => "8K"
                };
            }
            else
            {
                QualityText.Text = "A";
            }
        }

        private void OnIsPlayingChanged(VideoPlayerBase sender, VideoPlayerIsPlayingChangedEventArgs args)
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

        private void OnPositionChanged(VideoPlayerBase sender, VideoPlayerPositionChangedEventArgs args)
        {
            if (_scrubbing)
            {
                return;
            }

            Slider.Value = args.Position;
            TimeText.Text = FormatTime(args.Position);
        }

        private void OnBufferedChanged(VideoPlayerBase sender, VideoPlayerBufferedChangedEventArgs args)
        {
            Buffered1.Width = new GridLength(args.Buffered, GridUnitType.Star);
            Buffered2.Width = new GridLength(1 - args.Buffered, GridUnitType.Star);
        }

        private void OnDurationChanged(VideoPlayerBase sender, VideoPlayerDurationChangedEventArgs args)
        {
            Slider.Maximum = args.Duration;
            LengthText.Text = FormatTime(args.Duration);
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_scrubbing)
            {
                Slider.Value = e.NewValue;
                TimeText.Text = FormatTime(e.NewValue);
            }
        }

        private string FormatTime(double time)
        {
            var span = TimeSpan.FromSeconds(time);
            if (span.TotalHours >= 1)
            {
                return span.ToString("h\\:mm\\:ss");
            }
            else
            {
                return span.ToString("mm\\:ss");
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var current = _player.CurrentLevel;
            var auto = _player.IsCurrentLevelAuto;

            var flyout = new MenuFlyout();
            var quality = new MenuFlyoutSubItem
            {
                Text = Strings.Quality,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.Options),
                Style = BootStrapper.Current.Resources["DefaultMenuFlyoutSubItemStyle"] as Style
            };

            var item = new ToggleMenuFlyoutItem();
            item.Text = current != null && auto ? string.Format("{0} ({1})", Strings.QualityAuto, current.ToP()) : Strings.QualityAuto;
            item.IsChecked = _player.IsCurrentLevelAuto;
            item.Click += (s, args) =>
            {
                _player.CurrentLevel = null;
            };

            quality.Items.Add(item);

            foreach (var level in _player.Levels.OrderBy(x => x.Bitrate))
            {
                var option = new ToggleMenuFlyoutItem();
                option.Text = level.ToP();
                option.IsChecked = current?.Index == level.Index && !auto;
                option.Click += (s, args) =>
                {
                    _player.CurrentLevel = level;
                };

                quality.Items.Add(option);
            }

            var speed = new MenuFlyoutSubItem
            {
                Text = Strings.Speed,
                Icon = MenuFlyoutHelper.CreateIcon(Icons.TopSpeed),
                Style = BootStrapper.Current.Resources["DefaultMenuFlyoutSubItemStyle"] as Style
            };

            speed.CreatePlaybackSpeed(_player.Rate, FlyoutPlacementMode.Bottom, UpdatePlaybackSpeed);

            flyout.Items.Add(quality);
            flyout.Items.Add(speed);

            flyout.ShowAt(SettingsButton, FlyoutPlacementMode.TopEdgeAlignedRight);
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
