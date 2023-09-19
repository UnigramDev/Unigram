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
                _mediaPlayer.Time += 5;
            }
            else if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Down)
            {
                _mediaPlayer.Time -= 5;
            }
            else if (e.Key == VirtualKey.PageUp)
            {
                _mediaPlayer.Time += 30;
            }
            else if (e.Key == VirtualKey.PageDown)
            {
                _mediaPlayer.Time -= 30;
            }
            else if (e.Key == VirtualKey.Home)
            {
                _mediaPlayer.Time = 0;
            }
            else if (e.Key == VirtualKey.End)
            {
                _mediaPlayer.Time = _mediaPlayer.Length;
            }
        }

        private void Slider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = true;
            _mediaPlayer?.SetPause(true);
        }

        private void Slider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Time = (long)Slider.Value;
            }

            _scrubbing = false;
            _mediaPlayer?.SetPause(false);
        }

        private void Slider_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
            _mediaPlayer?.SetPause(false);
        }

        private void Slider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
            _mediaPlayer?.SetPause(false);
        }

        #endregion

        public void Attach(GalleryMedia item, File file)
        {
            _loopingEnabled = item.IsLoop;

            Visibility = item.IsVideo && !item.IsLoop
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

        private MediaPlayer _mediaPlayer;
        private DisplayRequest _request;

        public void Attach(MediaPlayer mediaPlayer)
        {
            if (_mediaPlayer != null)
            {
                //_mediaPlayer.PositionChanged -= OnPositionChanged;
                _mediaPlayer.TimeChanged -= OnTimeChanged;
                _mediaPlayer.LengthChanged -= OnLengthChanged;
                _mediaPlayer.EndReached -= OnEndReached;
                _mediaPlayer.Playing -= OnPlaying;
                _mediaPlayer.Paused -= OnPaused;
                _mediaPlayer.Stopped -= OnStopped;
                _mediaPlayer.VolumeChanged -= OnVolumeChanged;
            }

            _mediaPlayer = mediaPlayer;

            if (_mediaPlayer != null)
            {
                //_mediaPlayer.PositionChanged += OnPositionChanged;
                _mediaPlayer.TimeChanged += OnTimeChanged;
                _mediaPlayer.LengthChanged += OnLengthChanged;
                _mediaPlayer.EndReached += OnEndReached;
                _mediaPlayer.Playing += OnPlaying;
                _mediaPlayer.Paused += OnPaused;
                _mediaPlayer.Stopped += OnStopped;
                _mediaPlayer.VolumeChanged += OnVolumeChanged;

                OnTimeChanged();
                OnLengthChanged();
            }

            PlaybackButton.Glyph = Icons.PlayFilled24;
            Automation.SetToolTip(PlaybackButton, Strings.AccActionPlay);
        }

        private void OnVolumeChanged(object sender, MediaPlayerVolumeChangedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(OnVolumeChanged);
        }

        private void OnEndReached(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(OnEndReached);
        }

        private void OnPlaying(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(OnPlaying);
        }

        private void OnPaused(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(OnPaused);
        }

        private void OnStopped(object sender, EventArgs e)
        {
            _dispatcherQueue.TryEnqueue(OnStopped);
        }

        private void OnTimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(OnTimeChanged);
        }

        private void OnLengthChanged(object sender, MediaPlayerLengthChangedEventArgs e)
        {
            _dispatcherQueue.TryEnqueue(OnLengthChanged);
        }

        private void OnVolumeChanged()
        {
            if (_mediaPlayer == null)
            {
                return;
            }

            _mediaPlayer.VolumeChanged -= OnVolumeChanged;

            _mediaPlayer.Mute = SettingsService.Current.VolumeMuted;
            _mediaPlayer.Volume = (int)Math.Round(SettingsService.Current.VolumeLevel * 100);
            _mediaPlayer.Rate = (float)SettingsService.Current.Playback.VideoSpeed;
        }

        private void OnEndReached()
        {
            if (_mediaPlayer == null)
            {
                return;
            }

            Slider.Value = _mediaPlayer.Length;
            TimeText.Text = FormatTime(_mediaPlayer.Length);

            if (_loopingEnabled)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Play();
            }
        }

        private void OnPlaying()
        {
            PlaybackButton.Glyph = Icons.PauseFilled24;
            Automation.SetToolTip(PlaybackButton, Strings.AccActionPause);

            if (_request == null)
            {
                _request = new DisplayRequest();
                _request.RequestActive();
            }
        }

        private void OnPaused()
        {
            PlaybackButton.Glyph = Icons.PlayFilled24;
            Automation.SetToolTip(PlaybackButton, Strings.AccActionPlay);

            if (_request != null)
            {
                _request.RequestRelease();
                _request = null;
            }
        }

        private void OnStopped()
        {
            var source = _mediaPlayer == null || _mediaPlayer.State == VLCState.Stopped;
            Visibility = source
                ? Visibility.Collapsed
                : Visibility.Visible;

            PlaybackButton.Glyph = Icons.PlayFilled24;
            Automation.SetToolTip(PlaybackButton, Strings.AccActionPlay);

            if (_request != null && source)
            {
                _request.RequestRelease();
                _request = null;
            }
        }

        private void OnTimeChanged()
        {
            if (_mediaPlayer == null || _scrubbing)
            {
                return;
            }

            Slider.Value = _mediaPlayer.Time;
            TimeText.Text = FormatTime(_mediaPlayer.Time);
        }

        private void OnLengthChanged()
        {
            if (_mediaPlayer == null)
            {
                return;
            }

            Slider.Maximum = _mediaPlayer.Length;
            LengthText.Text = FormatTime(_mediaPlayer.Length);
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_mediaPlayer == null)
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
            flyout.CreatePlaybackSpeed(_mediaPlayer.Rate, FlyoutPlacementMode.Top, UpdatePlaybackSpeed);
            flyout.ShowAt(SpeedButton, FlyoutPlacementMode.TopEdgeAlignedRight);
        }

        private void UpdatePlaybackSpeed(double value)
        {
            SettingsService.Current.Playback.VideoSpeed = value;

            _mediaPlayer.SetRate((float)value);
            SpeedText.Text = string.Format("{0:N1}x", value);
            SpeedButton.Badge = string.Format("{0:N1}x", value);
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

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = volume;
                _mediaPlayer.Mute = muted;
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

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Volume = volume;
                _mediaPlayer.Mute = muted;
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
            if (_mediaPlayer == null)
            {
                return;
            }

            switch (_mediaPlayer.State)
            {
                case VLCState.Ended:
                    _mediaPlayer.Stop();
                    goto case VLCState.Stopped;
                case VLCState.Paused:
                case VLCState.Stopped:
                case VLCState.Error:
                    _mediaPlayer.Play();
                    break;
                default:
                    _mediaPlayer.Pause();
                    break;
            }
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
            if (_mediaPlayer == null)
            {
                return;
            }

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
                _mediaPlayer.Time -= 10000;
                args.Handled = true;
            }
            else if ((args.VirtualKey is VirtualKey.L && args.OnlyKey) || (args.VirtualKey is VirtualKey.Right && args.OnlyControl))
            {
                _mediaPlayer.Time += 10000;
                args.Handled = true;
            }
        }

        public void Unload()
        {
            Attach(null);
        }
    }
}
