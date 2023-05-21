//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Media.Playback;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls
{
    public sealed partial class PlaybackHeader : UserControl
    {
        private IClientService _clientService;
        private IPlaybackService _playbackService;
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
            Slider.AddHandler(PointerPressedEvent, new PointerEventHandler(Slider_PointerPressed), true);
            Slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(Slider_PointerReleased), true);
            Slider.AddHandler(PointerCanceledEvent, new PointerEventHandler(Slider_PointerCanceled), true);
            Slider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(Slider_PointerCaptureLost), true);

            _visual1 = ElementCompositionPreview.GetElementVisual(Label1);
            _visual2 = ElementCompositionPreview.GetElementVisual(Label2);

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
                Visibility = value || _collapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        public void Update(IClientService clientService, IPlaybackService playbackService, INavigationService navigationService)
        {
            _clientService = clientService;
            _playbackService = playbackService;
            _navigationService = navigationService;

            // We unsubscribe first to avoid duplicated notifications
            _playbackService.SourceChanged -= OnPlaybackStateChanged;
            _playbackService.StateChanged -= OnPlaybackStateChanged;
            _playbackService.PositionChanged -= OnPositionChanged;
            _playbackService.SourceChanged += OnPlaybackStateChanged;

            _playbackService.PlaylistChanged -= OnPlaylistChanged;
            _playbackService.StateChanged += OnPlaybackStateChanged;
            _playbackService.PositionChanged += OnPositionChanged;
            _playbackService.PlaylistChanged += OnPlaylistChanged;

            Items.ItemsSource = _playbackService.Items;

            UpdateGlyph();
        }

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void OnPositionChanged(IPlaybackService sender, object args)
        {
            var position = sender.Position;
            var duration = sender.Duration;

            this.BeginOnUIThread(() => UpdatePosition(position, duration));
        }

        private void OnPlaylistChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(() =>
            {
                Items.ItemsSource = null;
                Items.ItemsSource = _playbackService.Items;
            });
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration)
        {
            if (_scrubbing)
            {
                return;
            }

            Slider.Maximum = duration.TotalSeconds;
            Slider.Value = position.TotalSeconds;
        }

        private void UpdateGlyph()
        {
            var message = _playbackService.CurrentItem;
            if (message == null)
            {
                _chatId = 0;
                _messageId = 0;

                TitleLabel1.Text = TitleLabel2.Text = string.Empty;
                SubtitleLabel1.Text = SubtitleLabel2.Text = string.Empty;

                _collapsed = true;
                Visibility = Visibility.Collapsed;

                return;
            }
            else
            {
                _collapsed = false;
                Visibility = _hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            VolumeButton.Glyph = _playbackService.Volume switch
            {
                double n when n > 0.66 => Icons.Speaker3,
                double n when n > 0.33 => Icons.Speaker2,
                double n when n > 0 => Icons.Speaker1,
                _ => Icons.SpeakerOff
            };

            PlaybackButton.Glyph = _playbackService.PlaybackState == MediaPlaybackState.Paused ? Icons.Play : Icons.Pause;
            Automation.SetToolTip(PlaybackButton, _playbackService.PlaybackState == MediaPlaybackState.Paused ? Strings.AccActionPlay : Strings.AccActionPause);

            var webPage = message.Content is MessageText text ? text.WebPage : null;

            if (message.Content is MessageVoiceNote || message.Content is MessageVideoNote || webPage?.VoiceNote != null || webPage?.VideoNote != null)
            {
                var title = string.Empty;
                var date = Formatter.ToLocalTime(message.Date);

                if (_clientService.TryGetUser(message.SenderId, out Telegram.Td.Api.User senderUser))
                {
                    title = senderUser.Id == _clientService.Options.MyId ? Strings.ChatYourSelfName : senderUser.FullName();
                }
                else if (_clientService.TryGetChat(message.SenderId, out Chat senderChat))
                {
                    title = _clientService.GetTitle(senderChat);
                }

                var subtitle = string.Format(Strings.formatDateAtTime, Formatter.ShortDate.Format(date), Formatter.ShortTime.Format(date));

                UpdateText(message.ChatId, message.Id, title, subtitle);

                PreviousButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;

                RepeatButton.Visibility = Visibility.Collapsed;
                //ShuffleButton.Visibility = Visibility.Collapsed;

                UpdateSpeed(int.MaxValue);

                ViewButton.Padding = new Thickness(48 + 6, 0, 40 * 2 + 48 + 12, 0);
            }
            else if (message.Content is MessageAudio || webPage?.Audio != null)
            {
                var audio = message.Content is MessageAudio messageAudio ? messageAudio.Audio : webPage?.Audio;
                if (audio == null)
                {
                    return;
                }

                if (audio.Performer.Length > 0 && audio.Title.Length > 0)
                {
                    UpdateText(message.ChatId, message.Id, audio.Title, "- " + audio.Performer);
                }
                else
                {
                    UpdateText(message.ChatId, message.Id, audio.FileName, string.Empty);
                }

                PreviousButton.Visibility = Visibility.Visible;
                NextButton.Visibility = Visibility.Visible;

                RepeatButton.Visibility = Visibility.Visible;
                //ShuffleButton.Visibility = Visibility.Visible;

                UpdateSpeed(audio.Duration);
                UpdateRepeat();

                ViewButton.Padding = new Thickness(40 * 3 + 12, 0, 40 * 2 + 48 + 12, 0);
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
            RepeatButton.IsChecked = _playbackService.IsRepeatEnabled;
            Automation.SetToolTip(RepeatButton, _playbackService.IsRepeatEnabled == null
                ? Strings.AccDescrRepeatOne
                : _playbackService.IsRepeatEnabled == true
                ? Strings.AccDescrRepeatList
                : Strings.AccDescrRepeatOff);
        }

        private void UpdateSpeed(int duration)
        {
            SpeedText.Text = string.Format("{0:N1}x", _playbackService.PlaybackSpeed);
            SpeedButton.Badge = string.Format("{0:N1}x", _playbackService.PlaybackSpeed);

            SpeedText.Visibility = duration >= 10 * 60
                ? Visibility.Visible
                : Visibility.Collapsed;

            SpeedButton.Visibility = duration >= 10 * 60
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackService.PlaybackState == MediaPlaybackState.Paused)
            {
                _playbackService.Play();
            }
            else
            {
                _playbackService.Pause();
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            _playbackService.MoveNext();
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackService.Position.TotalSeconds > 5)
            {
                _playbackService.Seek(TimeSpan.Zero);
            }
            else
            {
                _playbackService.MovePrevious();
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
                Value = _playbackService.Volume * 100
            };

            slider.ValueChanged += VolumeSlider_ValueChanged;

            var flyout = new MenuFlyout();
            flyout.Items.Add(slider);
            flyout.ShowAt(VolumeButton, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            _playbackService.Volume = e.NewValue / 100;

            VolumeButton.Glyph = _playbackService.Volume switch
            {
                double n when n > 0.66 => Icons.Speaker3,
                double n when n > 0.33 => Icons.Speaker2,
                double n when n > 0 => Icons.Speaker1,
                _ => Icons.SpeakerOff
            };
        }

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            _playbackService.IsRepeatEnabled = RepeatButton.IsChecked;
            UpdateRepeat();
        }

        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            //_playbackService.IsShuffleEnabled = ShuffleButton.IsChecked == true;
            _playbackService.IsReversed = ShuffleButton.IsChecked == true;
        }

        private void Speed_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreatePlaybackSpeed(_playbackService.PlaybackSpeed, UpdatePlaybackSpeed);
            flyout.ShowAt(SpeedButton, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }

        private void UpdatePlaybackSpeed(double value)
        {
            _playbackService.PlaybackSpeed = value;
            SpeedText.Text = string.Format("{0:N1}x", value);
            SpeedButton.Badge = string.Format("{0:N1}x", value);
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _playbackService?.Clear();
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            var message = _playbackService?.CurrentItem;
            if (message == null)
            {
                return;
            }

            if (message.Content is MessageAudio)
            {
                var flyout = FlyoutBase.GetAttachedFlyout(ViewButton);
                flyout?.ShowAt(ViewButton);
            }
            else
            {
                _navigationService.NavigateToChat(message.ChatId, message.Id);
            }
        }



        private bool _scrubbing;

        private void Slider_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Up)
            {
                Slider.Value += 5;
                _playbackService?.Seek(TimeSpan.FromSeconds(Slider.Value));
            }
            else if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Down)
            {
                Slider.Value -= 5;
                _playbackService?.Seek(TimeSpan.FromSeconds(Slider.Value));
            }
            else if (e.Key == VirtualKey.PageUp)
            {
                Slider.Value += 30;
                _playbackService?.Seek(TimeSpan.FromSeconds(Slider.Value));
            }
            else if (e.Key == VirtualKey.PageDown)
            {
                Slider.Value -= 30;
                _playbackService?.Seek(TimeSpan.FromSeconds(Slider.Value));
            }
            else if (e.Key == VirtualKey.Home)
            {
                Slider.Value = Slider.Minimum;
                _playbackService?.Seek(TimeSpan.FromSeconds(Slider.Value));
            }
            else if (e.Key == VirtualKey.End)
            {
                Slider.Value = Slider.Maximum;
                _playbackService?.Seek(TimeSpan.FromSeconds(Slider.Value));
            }
        }

        private void Slider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = true;
        }

        private void Slider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _playbackService?.Seek(TimeSpan.FromSeconds(Slider.Value));
            _scrubbing = false;
        }

        private void Slider_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
        }

        private void Slider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = false;
        }

        private void Items_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            if (args.Item is PlaybackItem item && args.ItemContainer.ContentTemplateRoot is SharedAudioCell cell)
            {
                cell.UpdateMessage(_playbackService, item.Message);

                var message = item.Message;
                var webPage = message.Content is MessageText text ? text.WebPage : null;

                if (message.Content is MessageVoiceNote || message.Content is MessageVideoNote || webPage?.VoiceNote != null || webPage?.VideoNote != null)
                {
                    if (_clientService.TryGetUser(message.SenderId, out Telegram.Td.Api.User senderUser))
                    {
                        AutomationProperties.SetName(args.ItemContainer, senderUser.Id == _clientService.Options.MyId ? Strings.ChatYourSelfName : senderUser.FullName());
                    }
                    else if (_clientService.TryGetChat(message.SenderId, out Chat senderChat))
                    {
                        AutomationProperties.SetName(args.ItemContainer, _clientService.GetTitle(senderChat));
                    }
                }
                else if (message.Content is MessageAudio || webPage?.Audio != null)
                {
                    var audio = message.Content is MessageAudio messageAudio ? messageAudio.Audio : webPage?.Audio;
                    if (audio == null)
                    {
                        return;
                    }

                    if (audio.Performer.Length > 0 && audio.Title.Length > 0)
                    {
                        AutomationProperties.SetName(args.ItemContainer, $"{audio.Title} - {audio.Performer}");
                    }
                    else
                    {
                        AutomationProperties.SetName(args.ItemContainer, audio.FileName);
                    }
                }
            }
        }

        private void Items_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PlaybackItem item)
            {
                _navigationService.NavigateToChat(item.Message.ChatId, item.Message.Id);
            }

            var flyout = FlyoutBase.GetAttachedFlyout(ViewButton);
            flyout?.Hide();
        }
    }
}
