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
            _playbackService.Session.PlaybackStateChanged -= OnPlaybackStateChanged;
            _playbackService.Session.PlaybackStateChanged += OnPlaybackStateChanged;
            UpdateGlyph();
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

            PlaybackButton.Glyph = _playbackService.Session.PlaybackState == MediaPlaybackState.Playing ? "\uE103" : "\uE102";

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

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackService.Session.PlaybackState == MediaPlaybackState.Playing)
            {
                _playbackService.Pause();
            }
            else
            {
                _playbackService.Play();
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
