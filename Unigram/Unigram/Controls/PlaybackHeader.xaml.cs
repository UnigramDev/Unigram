using System;
using System.Numerics;
using System.Windows.Input;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Unigram.Navigation.Services;
using Unigram.Services;
using Windows.Media.Playback;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Unigram.Controls
{
    public sealed partial class PlaybackHeader : UserControl, IHandle<UpdateFile>
    {
        private IProtoService _cacheService;
        private IPlaybackService _playbackService;
        private INavigationService _navigationService;
        private IEventAggregator _aggregator;

        private Visual _visual1;
        private Visual _visual2;

        private Visual _visual;

        private long _chatId;
        private long _messageId;

        public PlaybackHeader()
        {
            InitializeComponent();

            Slider.AddHandler(PointerPressedEvent, new PointerEventHandler(Slider_PointerPressed), true);
            Slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(Slider_PointerReleased), true);
            Slider.AddHandler(PointerCanceledEvent, new PointerEventHandler(Slider_PointerCanceled), true);
            Slider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(Slider_PointerCaptureLost), true);

            _visual1 = ElementCompositionPreview.GetElementVisual(Label1);
            _visual2 = ElementCompositionPreview.GetElementVisual(Label2);

            _visual = _visual1;
        }

        public void Update(IProtoService cacheService, IPlaybackService playbackService, INavigationService navigationService, IEventAggregator aggregator)
        {
            _cacheService = cacheService;
            _playbackService = playbackService;
            _navigationService = navigationService;
            _aggregator = aggregator;

            _playbackService.MediaFailed -= OnMediaFailed;
            _playbackService.MediaFailed += OnMediaFailed;
            _playbackService.PropertyChanged -= OnCurrentItemChanged;
            _playbackService.PropertyChanged += OnCurrentItemChanged;
            _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
            _playbackService.PositionChanged -= OnPositionChanged;
            _playbackService.PositionChanged += OnPositionChanged;
            _playbackService.PlaylistChanged -= OnPlaylistChanged;
            _playbackService.PlaylistChanged += OnPlaylistChanged;
            UpdateGlyph();
            UpdateRate();
        }

        private void OnMediaFailed(MediaPlaybackSession sender, MediaPlayerFailedEventArgs args)
        {
            if (args.Error != MediaPlayerError.SourceNotSupported)
            {
                return;
            }

            this.BeginOnUIThread(async () =>
            {
                var confirm = await MessagePopup.ShowAsync("In order to play voice messages you must install Web Media Extensions from the Microsoft Store.", Strings.Resources.AppName, Strings.Resources.OK, Strings.Resources.Cancel);
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }

                await Launcher.LaunchUriAsync(new Uri("ms-windows-store://pdp/?PFN=Microsoft.WebMediaExtensions_8wekyb3d8bbwe"));
            });
        }

        private void OnCurrentItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            this.BeginOnUIThread(UpdatePosition);
        }

        private void OnPlaylistChanged(object sender, EventArgs e)
        {
            this.BeginOnUIThread(() =>
            {
                Items.ItemsSource = null;
                Items.ItemsSource = _playbackService.Items;
            });
        }

        public void Handle(UpdateFile update)
        {
            UpdateFile(update.File);
        }

        private void UpdateFile(File file)
        {
            foreach (var item in _playbackService.Items)
            {
                if (item.UpdateFile(file))
                {
                    this.BeginOnUIThread(() =>
                    {
                        var container = Items.ContainerFromItem(item) as SelectorItem;
                        if (container == null)
                        {
                            return;
                        }

                        var cell = container.ContentTemplateRoot as SharedAudioCell;
                        if (cell == null)
                        {
                            return;
                        }

                        cell.UpdateFile(item.Message, file);
                    });
                }
            }
        }

        private void UpdatePosition()
        {
            if (_scrubbing)
            {
                return;
            }

            Slider.Maximum = _playbackService.Duration.TotalSeconds;
            Slider.Value = _playbackService.Position.TotalSeconds;
        }

        private void UpdateGlyph()
        {
            var message = _playbackService.CurrentItem;
            if (message == null)
            {
                _chatId = 0;
                _messageId = 0;

                _aggregator.Unsubscribe(this);

                TitleLabel1.Text = TitleLabel2.Text = string.Empty;
                SubtitleLabel1.Text = SubtitleLabel2.Text = string.Empty;
                Visibility = Visibility.Collapsed;

                return;
            }
            else
            {
                _aggregator.Subscribe(this);
                Visibility = Visibility.Visible;
            }

            VolumeSlider.Value = _playbackService.Volume * 100;

            PlaybackButton.Glyph = _playbackService.PlaybackState == MediaPlaybackState.Playing ? "\uE103" : "\uE102";
            Automation.SetToolTip(PlaybackButton, _playbackService.PlaybackState == MediaPlaybackState.Playing ? Strings.Resources.AccActionPause : Strings.Resources.AccActionPlay);

            var webPage = message.Content is MessageText text ? text.WebPage : null;

            if (message.Content is MessageVoiceNote || message.Content is MessageVideoNote || webPage?.VoiceNote != null || webPage?.VideoNote != null)
            {
                var title = string.Empty;
                var date = BindConvert.Current.DateTime(message.Date);

                if (_cacheService.TryGetUser(message.Sender, out Telegram.Td.Api.User senderUser))
                {
                    title = senderUser.Id == _cacheService.Options.MyId ? Strings.Resources.ChatYourSelfName : senderUser.GetFullName();
                }
                else if (_cacheService.TryGetChat(message.Sender, out Chat senderChat))
                {
                    title = _cacheService.GetTitle(senderChat);
                }

                var subtitle = string.Format(Strings.Resources.formatDateAtTime, BindConvert.Current.ShortDate.Format(date), BindConvert.Current.ShortTime.Format(date));

                UpdateText(message.ChatId, message.Id, title, subtitle);

                PreviousButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;

                RepeatButton.Visibility = Visibility.Collapsed;
                //ShuffleButton.Visibility = Visibility.Collapsed;

                UpdateRate();

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

                RateButton.Visibility = Visibility.Collapsed;

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
                ? Strings.Resources.AccDescrRepeatOne
                : _playbackService.IsRepeatEnabled == true
                ? Strings.Resources.AccDescrRepeatList
                : Strings.Resources.AccDescrRepeatOff);
        }

        private void UpdateRate()
        {
            RateButton.Visibility = _playbackService.IsSupportedPlaybackRateRange(2.0, 2.0) ? Visibility.Visible : Visibility.Collapsed;
            RateButton.IsChecked = _playbackService.PlaybackRate == 2.0;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackService.PlaybackState == MediaPlaybackState.Playing)
            {
                _playbackService.Pause();
            }
            else
            {
                _playbackService.Play();
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
                _playbackService.SetPosition(TimeSpan.Zero);
            }
            else
            {
                _playbackService.MovePrevious();
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            _playbackService.Volume = e.NewValue / 100;

            switch (_playbackService.Volume)
            {
                case double n when n >= 1d / 3d * 2d:
                    VolumeButton.Glyph = "\uE995";
                    break;
                case double n when n >= 1d / 3d && n < 1d / 3d * 2d:
                    VolumeButton.Glyph = "\uE994";
                    break;
                case double n when n > 0 && n < 1d / 3d:
                    VolumeButton.Glyph = "\uE993";
                    break;
                default:
                    VolumeButton.Glyph = "\uE74F";
                    break;
            }
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

        private void Rate_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackService.PlaybackRate == 1.0)
            {
                _playbackService.PlaybackRate = 2.0;
                RateButton.IsChecked = true;
            }
            else
            {
                _playbackService.PlaybackRate = 1.0;
                RateButton.IsChecked = false;
            }
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
                if (flyout != null)
                {
                    flyout.ShowAt(ViewButton);
                }
            }
            else
            {
                Command?.Execute(message);
            }
        }

        public ICommand Command { get; set; }



        private bool _scrubbing;

        private void Slider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _scrubbing = true;
        }

        private void Slider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _playbackService?.SetPosition(TimeSpan.FromSeconds(Slider.Value));
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
                cell.UpdateMessage(_playbackService, _cacheService, item.Message);
            }
        }
    }
}
