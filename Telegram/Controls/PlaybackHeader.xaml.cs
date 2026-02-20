//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public sealed partial class PlaybackHeader : UserControl
    {
        private IClientService _clientService;
        private INavigationService _navigationService;

        private readonly Visual _visual1;
        private readonly Visual _visual2;

        private Visual _visual;

        private long _chatId;
        private long _messageId;

        public PlaybackHeader()
        {
            InitializeComponent();

            Slider.AddHandler(KeyDownEvent, new KeyEventHandler(Slider_KeyDown), true);
            Slider.PositionChanged += Slider_PositionChanged;

            _visual1 = ElementComposition.GetElementVisual(Label1);
            _visual2 = ElementComposition.GetElementVisual(Label2);

            _visual = _visual1;
        }

        private bool _collapsed;
        private bool _hidden;

        public bool IsHidden
        {
            get => _hidden;
            set
            {
                _hidden = value;
                Visibility = value
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        public void Update(IClientService clientService, INavigationService navigationService)
        {
            _clientService = clientService;
            _navigationService = navigationService;

            // We unsubscribe first to avoid duplicated notifications
            LifetimeService.Current.Playback.SourceChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged -= OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged -= OnPositionChanged;

            LifetimeService.Current.Playback.SourceChanged += OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged += OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged += OnPositionChanged;

            UpdateGlyph();
        }

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void OnPositionChanged(IPlaybackService sender, PlaybackPositionChangedEventArgs args)
        {
            var position = args.Position;
            var duration = args.Duration;
            var playing = sender.IsPlaying;

            this.BeginOnUIThread(() => UpdatePosition(position, duration, playing));
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration, bool playing)
        {
            if (Slider.IsScrubbing)
            {
                return;
            }

            Slider.UpdateValue(position, duration, playing);
        }

        private void UpdateGlyph()
        {
            UpdatePosition(LifetimeService.Current.Playback.Position, LifetimeService.Current.Playback.Duration, LifetimeService.Current.Playback.IsPlaying);

            var item = LifetimeService.Current.Playback.CurrentItem;
            if (item == null)
            {
                _chatId = 0;
                _messageId = 0;

                _collapsed = true;
                //Visibility = Visibility.Collapsed;

                return;
            }
            else
            {
                if (_collapsed)
                {
                    TitleLabel1.Text = TitleLabel2.Text = string.Empty;
                    SubtitleLabel1.Text = SubtitleLabel2.Text = string.Empty;
                }

                _collapsed = false;
                Visibility = _hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            VolumeButton.Glyph = LifetimeService.Current.Playback.Volume switch
            {
                double n when n > 0.66 => Icons.Speaker3,
                double n when n > 0.33 => Icons.Speaker2,
                double n when n > 0 => Icons.Speaker1,
                _ => Icons.SpeakerOff
            };

            PlaybackButton.Glyph = LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused ? Icons.Play : Icons.Pause;
            Automation.SetToolTip(PlaybackButton, LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused ? Strings.AccActionPlay : Strings.AccActionPause);

            if (item is PlaybackItemMessage message)
            {
                if (item.Performer.Length > 0)
                {
                    UpdateText(message.ChatId, message.Id, item.Title, "- " + item.Performer);
                }
                else
                {
                    UpdateText(message.ChatId, message.Id, item.Title, string.Empty);
                }

                var linkPreview = message.Message.Content is MessageText text ? text.LinkPreview : null;

                if (message.Message.Content is MessageVoiceNote || message.Message.Content is MessageVideoNote || linkPreview?.Type is LinkPreviewTypeVoiceNote or LinkPreviewTypeVideoNote)
                {
                    RepeatButton.Visibility = Visibility.Collapsed;
                    //ShuffleButton.Visibility = Visibility.Collapsed;

                    UpdateSpeed(int.MaxValue);
                }
                else if (message.Message.Content is MessageAudio || linkPreview?.Type is LinkPreviewTypeAudio)
                {
                    RepeatButton.Visibility = Visibility.Visible;
                    //ShuffleButton.Visibility = Visibility.Visible;

                    UpdateSpeed(item.Duration);
                    UpdateRepeat();
                }
            }
            else if (item is PlaybackItemProfileAudio audio)
            {
                if (item.Performer.Length > 0)
                {
                    UpdateText(audio.UserId, audio.Audio.AudioValue.Id, item.Title, "- " + item.Performer);
                }
                else
                {
                    UpdateText(audio.UserId, audio.Audio.AudioValue.Id, item.Title, string.Empty);
                }

                RepeatButton.Visibility = Visibility.Visible;
                //ShuffleButton.Visibility = Visibility.Visible;

                UpdateSpeed(item.Duration);
                UpdateRepeat();
            }
        }

        private void UpdateText(long chatId, long messageId, string title, string subtitle)
        {
            if (_chatId == chatId && _messageId == messageId)
            {
                return;
            }

            var prev = _chatId == chatId && _messageId > messageId;

            _chatId = chatId;
            _messageId = messageId;

            var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            var titleShow = _visual == _visual1 ? TitleLabel2 : TitleLabel1;
            var subtitleShow = _visual == _visual1 ? SubtitleLabel2 : SubtitleLabel1;

            var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(0, new Vector3(0));
            hide1.InsertKeyFrame(1, new Vector3(prev ? -12 : 12, 0, 0));

            var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(0, 1);
            hide2.InsertKeyFrame(1, 0);

            visualHide.StartAnimation("Offset", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            titleShow.Text = title;
            subtitleShow.Text = subtitle;

            var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(0, new Vector3(prev ? 12 : -12, 0, 0));
            show1.InsertKeyFrame(1, new Vector3(0));

            var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(0, 0);
            show2.InsertKeyFrame(1, 1);

            visualShow.StartAnimation("Offset", show1);
            visualShow.StartAnimation("Opacity", show2);

            _visual = visualShow;
        }

        private void UpdateRepeat()
        {
            RepeatButton.IsChecked = LifetimeService.Current.Playback.IsRepeatEnabled;
            Automation.SetToolTip(RepeatButton, LifetimeService.Current.Playback.IsRepeatEnabled == null
                ? Strings.AccDescrRepeatOne
                : LifetimeService.Current.Playback.IsRepeatEnabled == true
                ? Strings.AccDescrRepeatList
                : Strings.AccDescrRepeatOff);
        }

        private void UpdateSpeed(int duration)
        {
            SpeedText.Text = string.Format("{0:N1}x", LifetimeService.Current.Playback.PlaybackSpeed);
            SpeedButton.Badge = string.Format("{0:N1}x", LifetimeService.Current.Playback.PlaybackSpeed);

            SpeedText.Visibility = duration >= 10 * 60
                ? Visibility.Visible
                : Visibility.Collapsed;

            SpeedButton.Visibility = duration >= 10 * 60
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused)
            {
                LifetimeService.Current.Playback.Play();
            }
            else
            {
                LifetimeService.Current.Playback.Pause();
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            LifetimeService.Current.Playback.MoveNext();
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (LifetimeService.Current.Playback.Position.TotalSeconds > 5)
            {
                LifetimeService.Current.Playback.Seek(TimeSpan.Zero);
            }
            else
            {
                LifetimeService.Current.Playback.MovePrevious();
            }
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
        {
            var slider = new MenuFlyoutSlider
            {
                Icon = MenuFlyoutHelper.CreateIcon(Icons.Speaker3),
                TextValueConverter = new TextValueProvider(newValue => string.Format("{0:P0}", newValue / 100)),
                IconValueConverter = new IconValueProvider(newValue => newValue switch
                {
                    double n when n > 66 => Icons.Speaker3,
                    double n when n > 33 => Icons.Speaker2,
                    double n when n > 0 => Icons.Speaker1,
                    _ => Icons.SpeakerOff
                }),
                FontWeight = FontWeights.SemiBold,
                Value = LifetimeService.Current.Playback.Volume * 100
            };

            slider.ValueChanged += VolumeSlider_ValueChanged;

            var flyout = new MenuFlyout();
            flyout.Items.Add(slider);
            flyout.ShowAt(VolumeButton, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            LifetimeService.Current.Playback.Volume = e.NewValue / 100;

            VolumeButton.Glyph = LifetimeService.Current.Playback.Volume switch
            {
                double n when n > 0.66 => Icons.Speaker3,
                double n when n > 0.33 => Icons.Speaker2,
                double n when n > 0 => Icons.Speaker1,
                _ => Icons.SpeakerOff
            };
        }

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            LifetimeService.Current.Playback.IsRepeatEnabled = RepeatButton.IsChecked;
            UpdateRepeat();
        }

        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            //LifetimeService.Current.Playback.IsShuffleEnabled = ShuffleButton.IsChecked == true;
            LifetimeService.Current.Playback.IsReversed = ShuffleButton.IsChecked == true;
        }

        private void Speed_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreatePlaybackSpeed(LifetimeService.Current.Playback.PlaybackSpeed, FlyoutPlacementMode.Bottom, UpdatePlaybackSpeed);
            flyout.ShowAt(SpeedButton, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void UpdatePlaybackSpeed(double value)
        {
            LifetimeService.Current.Playback.PlaybackSpeed = value;
            SpeedText.Text = string.Format("{0:N1}x", value);
            SpeedButton.Badge = string.Format("{0:N1}x", value);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            LifetimeService.Current.Playback?.Clear();
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            var item = LifetimeService.Current.Playback.CurrentItem;
            if (item == null)
            {
                return;
            }

            if (item is PlaybackItemMessage message)
            {
                if (message.Message.Content is MessageAudio)
                {
                    _navigationService.ShowPopup(new PlaybackPopup(_clientService, _navigationService));
                }
                else
                {
                    _navigationService.NavigateToChat(message.ChatId, message.Id);
                }
            }
            else if (item is PlaybackItemProfileAudio)
            {
                _navigationService.ShowPopup(new PlaybackPopup(_clientService, _navigationService));
            }
        }



        private void Slider_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Up)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Position + TimeSpan.FromSeconds(5));
            }
            else if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Down)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Position - TimeSpan.FromSeconds(5));
            }
            else if (e.Key == VirtualKey.PageUp)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Position + TimeSpan.FromSeconds(30));
            }
            else if (e.Key == VirtualKey.PageDown)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Position - TimeSpan.FromSeconds(30));
            }
            else if (e.Key == VirtualKey.Home)
            {
                LifetimeService.Current.Playback?.Seek(TimeSpan.Zero);
            }
            else if (e.Key == VirtualKey.End)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Duration);
            }
        }

        private void Slider_PositionChanged(object sender, PlaybackSliderPositionChanged e)
        {
            LifetimeService.Current.Playback?.Seek(e.NewPosition);
        }

        private void Buttons_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ViewButton.Padding = new Thickness(LeftButtons.ActualWidth + 4, 0, RightButtons.ActualWidth + 4, 0);
        }
    }
}
