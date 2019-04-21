using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Input;
using Telegram.Td.Api;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Services;
using Unigram.Views;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Unigram.Controls
{
    public sealed partial class PlaybackHeader : UserControl
    {
        private ICacheService _cacheService;
        private IPlaybackService _playbackService;
        private INavigationService _navigationService;

        public PlaybackHeader()
        {
            InitializeComponent();
        }

        public void Update(ICacheService cacheService, IPlaybackService playbackService, INavigationService navigationService)
        {
            _cacheService = cacheService;
            _playbackService = playbackService;
            _navigationService = navigationService;

            _playbackService.PropertyChanged -= OnCurrentItemChanged;
            _playbackService.PropertyChanged += OnCurrentItemChanged;
            _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
            UpdateGlyph();
            UpdateRate();
        }

        private void OnCurrentItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void UpdateGlyph()
        {
            if (_playbackService.CurrentItem == null)
            {
                Visibility = Visibility.Collapsed;
            }
            else
            {
                Visibility = Visibility.Visible;
            }

            UpdateRate();

            PlaybackButton.Glyph = _playbackService.PlaybackState == MediaPlaybackState.Playing ? "\uE103" : "\uE102";
            Automation.SetToolTip(PlaybackButton, _playbackService.PlaybackState == MediaPlaybackState.Playing ? Strings.Resources.AccActionPause : Strings.Resources.AccActionPlay);

            var message = _playbackService.CurrentItem;
            if (message == null)
            {
                return;
            }

            var webPage = message.Content is MessageText text ? text.WebPage : null;

            if (message.Content is MessageVoiceNote || webPage?.VoiceNote != null)
            {
                var voiceNote = message.Content is MessageVoiceNote messageVoiceNote ? messageVoiceNote?.VoiceNote : webPage?.VoiceNote;
                if (voiceNote == null)
                {
                    return;
                }

                var date = BindConvert.Current.DateTime(message.Date);
                var user = _cacheService.GetUser(message.SenderUserId);
                if (user == null)
                {
                    return;
                }

                TitleLabel.Text = user.Id == _cacheService.Options.MyId ? Strings.Resources.ChatYourSelfName : user.GetFullName();
                SubtitleLabel.Text = string.Format("{0} at {1}", date.Date == DateTime.Now.Date ? "Today" : BindConvert.Current.ShortDate.Format(date), BindConvert.Current.ShortTime.Format(date));
            }

            //        if (audio.HasPerformer && audio.HasTitle)
            //        {
            //            TitleLabel.Text = audio.Title;
            //            SubtitleLabel.Text = "- " + audio.Performer;
            //        }
            //        else if (audio.HasPerformer && !audio.HasTitle)
            //        {
            //            TitleLabel.Text = Strings.Resources.AudioUnknownTitle;
            //            SubtitleLabel.Text = "- " + audio.Performer;
            //        }
            //        else if (audio.HasTitle && !audio.HasPerformer)
            //        {
            //            TitleLabel.Text = audio.Title;
            //            SubtitleLabel.Text = Strings.Resources.AudioUnknownArtist;
            //        }
            //        else
            //        {
            //            TitleLabel.Text = Strings.Resources.AudioUnknownTitle;
            //            SubtitleLabel.Text = Strings.Resources.AudioUnknownArtist;
            //        }
            //    }
            //}
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
            _playbackService.Clear();
        }

        private void View_Click(object sender, RoutedEventArgs e)
        {
            var message = _playbackService.CurrentItem;
            if (message == null)
            {
                return;
            }

            Command?.Execute(message);
        }

        public ICommand Command { get; set; }
    }
}
