//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Gallery;
using Windows.System.Display;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Gallery
{
    public sealed partial class GalleryTransportControls : UserControl
    {
        private readonly DispatcherQueue _dispatcherQueue;

        private bool _loopingEnabled;
        private bool _playing;
        private bool _unloaded;

        private VideoPlayerBase _player;
        private GalleryMedia _item;

        private Border _tooltip;
        private ImageBrush _tooltipSource;

        public GalleryTransportControls()
        {
            InitializeComponent();

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            var muted = SettingsService.Current.VolumeMuted;
            var volume = SettingsService.Current.VolumeLevel;
            var speed = SettingsService.Current.Playback.VideoSpeed;

            VolumeSlider.UpdateValue(muted ? 0 : volume, 1, false);
            VolumeSlider.PositionChanging += VolumeSlider_ValueChanged;
            VolumeSlider.PositionChanged += VolumeSlider_ValueChanged;

            VolumeButton.Glyph = muted ? Icons.SpeakerMuteFilled : volume switch
            {
                double n when n > 0.5 => Icons.Speaker2Filled,
                double n when n > 0 => Icons.Speaker1Filled,
                _ => Icons.SpeakerMuteFilled
            };

            Automation.SetToolTip(VolumeButton, muted ? Strings.PlayerAudioUnmute : Strings.PlayerAudioMute);

            _tooltip = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = _tooltipSource = new ImageBrush
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                }
            };

            Slider.ThumbToolTipContent = _tooltip;
            Slider.AddHandler(KeyDownEvent, new KeyEventHandler(Slider_KeyDown), true);
            Slider.PositionStarted += Slider_PositionStarted;
            Slider.PositionChanging += Slider_PositionChanging;
            Slider.PositionChanged += Slider_PositionChanged;
            Slider.PositionCanceled += Slider_PositionCanceled;
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

        private void Slider_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Up)
            {
                _player.Seek(5);
            }
            else if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Down)
            {
                _player.Seek(-5);
            }
            else if (e.Key == VirtualKey.PageUp)
            {
                _player.Seek(30);
            }
            else if (e.Key == VirtualKey.PageDown)
            {
                _player.Seek(-30);
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

        private void Slider_PositionStarted(PlaybackSlider sender, object e)
        {
            PauseBeforeScrubbing();
        }

        private void Slider_PositionChanged(PlaybackSlider sender, PlaybackSliderPositionChanged e)
        {
            PlayAfterScrubbing();

            _player?.Position = e.NewPosition.TotalSeconds;
        }

        private void Slider_PositionCanceled(PlaybackSlider sender, object e)
        {
            PlayAfterScrubbing();
        }

        #endregion

        private long _storyboardFileToken;
        private long _storyboardMapToken;

        public void Attach(GalleryMedia item, File file)
        {
            _item = item;
            _loopingEnabled = item.IsLoopingEnabled;

            Visibility = item.IsVideo && (item.IsVideoNote || !item.IsLoopingEnabled)
                ? Visibility.Visible
                : Visibility.Collapsed;

            CompactButton.Visibility = ConvertCompactVisibility(item)
                ? Visibility.Visible
                : Visibility.Collapsed;

            UpdateStoryboard(item, true);
        }

        private void UpdateStoryboard(GalleryMedia item, bool download)
        {
            UpdateManager.Unsubscribe(this, ref _storyboardFileToken);
            UpdateManager.Unsubscribe(this, ref _storyboardMapToken);

            if (item is GalleryMessage { Content: MessageVideo video } && video.Storyboards.Count > 0)
            {
                var storyboardFile = video.Storyboards[0].StoryboardFile;
                var storyboardMap = video.Storyboards[0].MapFile;

                if (storyboardFile.Local.IsDownloadingCompleted && storyboardMap.Local.IsDownloadingCompleted)
                {
                    LoadStoryboard(storyboardFile.Local.Path, storyboardMap.Local.Path);
                }
                else
                {
                    if (download)
                    {
                        if (storyboardFile.Local.CanBeDownloaded && !storyboardFile.Local.IsDownloadingActive)
                        {
                            item.ClientService.DownloadFile(storyboardFile.Id, 16);
                        }

                        if (storyboardMap.Local.CanBeDownloaded && !storyboardMap.Local.IsDownloadingActive)
                        {
                            item.ClientService.DownloadFile(storyboardMap.Id, 16);
                        }
                    }

                    UpdateManager.Subscribe(this, item.ClientService, storyboardFile, ref _storyboardFileToken, UpdateStoryboard, true);
                    UpdateManager.Subscribe(this, item.ClientService, storyboardMap, ref _storyboardMapToken, UpdateStoryboard, true);

                    Slider.IsThumbToolTipEnabled = false;
                }
            }
            else
            {
                Slider.IsThumbToolTipEnabled = false;
            }
        }

        private void UpdateStoryboard(object sender, File file)
        {
            UpdateStoryboard(_item, false);
        }

        private SortedList<int, Vector2> _storyboardFrames;
        private double _storyboardScale = 1;

        private void LoadStoryboard(string storyboard, string map)
        {
            _tooltipSource.ImageSource = UriEx.ToBitmap(storyboard);
            Slider.IsThumbToolTipEnabled = true;

            var lines = System.IO.File.ReadAllLines(map);

            var width = 0;
            var height = 0;

            var frames = new SortedList<int, Vector2>(lines.Length - 3);

            foreach (var line in lines)
            {
                var split = line.Split('=');
                if (split.Length == 1)
                {
                    split = line.Split(',');

                    if (split.Length == 3 && int.TryParse(split[0], out int seconds) && int.TryParse(split[1], out int x) && int.TryParse(split[2], out int y))
                    {
                        frames.Add(seconds, new Vector2(x, y));
                    }
                }
                else if (split[0] == "frame_width")
                {
                    int.TryParse(split[1], out width);
                }
                else if (split[0] == "frame_height")
                {
                    int.TryParse(split[1], out height);
                }
            }

            _storyboardFrames = frames;
            _storyboardScale = ImageHelper.ScaleRatioMin(width, height, 144);

            _tooltip.Width = width * _storyboardScale;
            _tooltip.Height = height * _storyboardScale;
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
                _player.Ready -= OnReady;
                _player.PositionChanged -= OnPositionChanged;
                _player.BufferedChanged -= OnBufferedChanged;
                _player.DurationChanged -= OnDurationChanged;
                _player.IsPlayingChanged -= OnIsPlayingChanged;
                _player.LevelsChanged -= OnLevelsChanged;
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

                OnPositionChanged(_player, new VideoPlayerPositionChangedEventArgs(_player.Position));
                OnBufferedChanged(_player, new VideoPlayerBufferedChangedEventArgs(_player.Buffered));
                OnDurationChanged(_player, new VideoPlayerDurationChangedEventArgs(_player.Duration));
                OnIsPlayingChanged(_player, new VideoPlayerIsPlayingChangedEventArgs(_player.IsPlaying));
                OnLevelsChanged(_player, new VideoPlayerLevelsChangedEventArgs(_player.Levels, _player.CurrentLevel, _player.IsCurrentLevelAuto));
            }
        }

        private bool _qualityCollapsed = true;

        private void ShowHideQuality(bool show)
        {
            if (_qualityCollapsed != show)
            {
                return;
            }

            _qualityCollapsed = !show;
            QualityRoot.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnReady(VideoPlayerBase sender, EventArgs args)
        {
            sender.Mute = SettingsService.Current.VolumeMuted;
            sender.Volume = SettingsService.Current.VolumeLevel;
            sender.Rate = SettingsService.Current.Playback.VideoSpeed;
        }

        private void OnLevelsChanged(VideoPlayerBase sender, VideoPlayerLevelsChangedEventArgs args)
        {
            ShowHideQuality(args.Levels.Count > 0);

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

                _request?.TryRequestRelease();
                _request = null;
            }

            if (Slider.IsScrubbing)
            {
                return;
            }

            Slider.UpdateValue(sender.Position, sender.Duration, false);
        }

        private void OnPositionChanged(VideoPlayerBase sender, VideoPlayerPositionChangedEventArgs args)
        {
            if (Slider.IsScrubbing)
            {
                return;
            }

            Slider.UpdateValue(args.Position, sender.Duration, sender.IsPlaying);
            TimeText.Text = FormatTime(args.Position);
        }

        private void OnBufferedChanged(VideoPlayerBase sender, VideoPlayerBufferedChangedEventArgs args)
        {
            Buffered1.Width = new GridLength(args.Buffered, GridUnitType.Star);
            Buffered2.Width = new GridLength(1 - args.Buffered, GridUnitType.Star);
        }

        private void OnDurationChanged(VideoPlayerBase sender, VideoPlayerDurationChangedEventArgs args)
        {
            if (Slider.IsScrubbing)
            {
                return;
            }

            Slider.UpdateValue(sender.Position, args.Duration, false);
            LengthText.Text = FormatTime(args.Duration);

            SkipBackButton.Visibility = args.Duration > 30
                ? Visibility.Visible
                : Visibility.Collapsed;

            SkipForwardButton.Visibility = args.Duration > 30
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Slider_PositionChanging(PlaybackSlider sender, PlaybackSliderPositionChanged e)
        {
            TimeText.Text = FormatTime(e.NewPosition.TotalSeconds);

            _player?.Position = e.NewPosition.TotalSeconds;

            var closest = _storyboardFrames?.LastOrDefault(x => x.Key <= e.NewPosition.TotalSeconds);
            if (closest == null)
            {
                return;
            }

            _tooltipSource.Transform = new CompositeTransform
            {
                TranslateX = -closest.Value.Value.X * _storyboardScale,
                TranslateY = -closest.Value.Value.Y * _storyboardScale,
                ScaleX = _storyboardScale,
                ScaleY = _storyboardScale
            };

            _tooltipSource.Stretch = Stretch.None;
            _tooltipSource.AlignmentX = AlignmentX.Left;
            _tooltipSource.AlignmentY = AlignmentY.Top;
        }

        private string FormatTime(double time)
        {
            try
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
            catch
            {
                // May overflow
                return "00:00";
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (_player == null)
            {
                return;
            }

            var current = _player.CurrentLevel;
            var auto = _player.IsCurrentLevelAuto;

            var flyout = new MenuFlyout();

            if (_player.Levels.Count > 0)
            {
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

                flyout.Items.Add(quality);

                var speed = new MenuFlyoutSubItem
                {
                    Text = Strings.Speed,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.TopSpeed),
                    Style = BootStrapper.Current.Resources["DefaultMenuFlyoutSubItemStyle"] as Style
                };

                speed.CreatePlaybackSpeed(_player.Rate, FlyoutPlacementMode.Bottom, UpdatePlaybackSpeed);

                flyout.Items.Add(speed);
            }
            else
            {
                flyout.CreatePlaybackSpeed(_player.Rate, FlyoutPlacementMode.Bottom, UpdatePlaybackSpeed);
            }

            flyout.ShowAt(SettingsButton, FlyoutPlacementMode.TopEdgeAlignedRight);
        }

        private void UpdatePlaybackSpeed(double value)
        {
            value = Math.Clamp(value, 0.2, 2.5);
            SettingsService.Current.Playback.VideoSpeed = value;

            _player?.Rate = value;
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

        private void VolumeSlider_ValueChanged(PlaybackSlider sender, PlaybackSliderPositionChanged e)
        {
            var volume = e.NewPosition.TotalSeconds;
            var muted = false;

            if (volume == 0)
            {
                volume = 1;
                muted = true;
            }

            if (_player != null)
            {
                _player.Volume = volume;
                _player.Mute = muted;
            }

            SettingsService.Current.VolumeLevel = volume;
            SettingsService.Current.VolumeMuted = muted;

            VolumeButton.Glyph = muted ? Icons.SpeakerMuteFilled : volume switch
            {
                double n when n > 0.5 => Icons.Speaker2Filled,
                double n when n > 0 => Icons.Speaker1Filled,
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

            VolumeSlider.UpdateValue(muted ? 0 : volume, 1, false);

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
            if (_player == null)
            {
                return;
            }

            _playing = _player.IsPlaying;
            _player.Pause();
        }

        public void Stop()
        {
            Visibility = Visibility.Collapsed;

            _request?.TryRequestRelease();
            _request = null;
        }

        public new void ProcessKeyboardAccelerators(KeyRoutedEventArgs args)
        {
            if (_player == null)
            {
                return;
            }

            var modifiers = WindowContext.KeyModifiers();
            var keyCode = (int)args.Key;

            if (args.Key is VirtualKey.K && modifiers == VirtualKeyModifiers.None)
            {
                TogglePlaybackState();
                args.Handled = true;
            }
            else if (args.Key is VirtualKey.M && modifiers == VirtualKeyModifiers.None)
            {
                Volume_Click(null, null);
                args.Handled = true;
            }
            else if (args.Key is VirtualKey.Up && modifiers == VirtualKeyModifiers.None)
            {
                VolumeSlider.SetValue(VolumeSlider.Position.TotalSeconds + 0.1, 1, false);
                args.Handled = true;
            }
            else if (args.Key is VirtualKey.Down && modifiers == VirtualKeyModifiers.None)
            {
                VolumeSlider.SetValue(VolumeSlider.Position.TotalSeconds - 0.1, 1, false);
                args.Handled = true;
            }
            else if ((args.Key is VirtualKey.J && modifiers == VirtualKeyModifiers.None) || (args.Key is VirtualKey.Left && modifiers == VirtualKeyModifiers.Control))
            {
                _player.Seek(-10);
                args.Handled = true;
            }
            else if ((args.Key is VirtualKey.L && modifiers == VirtualKeyModifiers.None) || (args.Key is VirtualKey.Right && modifiers == VirtualKeyModifiers.Control))
            {
                _player.Seek(10);
                args.Handled = true;
            }
            else if (keyCode is 188 or 190 && modifiers == VirtualKeyModifiers.Shift)
            {
                ChangePlaybackSpeed(keyCode is 188 ? -0.25f : 0.25f);
                args.Handled = true;
            }
        }

        private void SkipBackButton_Click(object sender, RoutedEventArgs e)
        {
            _player?.Seek(-10);
        }

        private void SkipForwardButton_Click(object sender, RoutedEventArgs e)
        {
            _player?.Seek(10);
        }

        public void Unload()
        {
            _unloaded = true;
            Attach(null);
        }
    }
}
