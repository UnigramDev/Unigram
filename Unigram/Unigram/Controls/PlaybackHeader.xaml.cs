using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Template10.Common;
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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls
{
    public sealed partial class PlaybackHeader : UserControl
    {
        public IPlaybackService Playback { get; } = UnigramContainer.Current.ResolveType<IPlaybackService>();

        public PlaybackHeader()
        {
            InitializeComponent();

            Playback.PropertyChanged += OnCurrentItemChanged;
            Playback.Session.PlaybackStateChanged += OnPlaybackStateChanged;
            UpdateGlyph();
        }

        private void OnCurrentItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateGlyph();
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void UpdateGlyph()
        {
            PlaybackButton.Glyph = Playback.Session.PlaybackState == MediaPlaybackState.Playing ? "\uE103" : "\uE102";

            if (Playback.CurrentItem is TLMessage message && message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var audio = document.Attributes.FirstOrDefault(x => x is TLDocumentAttributeAudio) as TLDocumentAttributeAudio;
                if (audio == null)
                {
                    return;
                }

                if (audio.IsVoice)
                {
                    var date = BindConvert.Current.DateTime(message.Date);
                    TitleLabel.Text = message.Participant is TLUser user && user.IsSelf ? "You" : message.Participant?.DisplayName;
                    SubtitleLabel.Text = string.Format("{0} at {1}", date.Date == DateTime.Now.Date ? "Today" : BindConvert.Current.ShortDate.Format(date), BindConvert.Current.ShortTime.Format(date));
                }
                else
                {
                    if (audio.HasPerformer && audio.HasTitle)
                    {
                        TitleLabel.Text = audio.Title;
                        SubtitleLabel.Text = "- " + audio.Performer;
                    }
                    else if (audio.HasPerformer && !audio.HasTitle)
                    {
                        TitleLabel.Text = Strings.Android.AudioUnknownTitle;
                        SubtitleLabel.Text = "- " + audio.Performer;
                    }
                    else if (audio.HasTitle && !audio.HasPerformer)
                    {
                        TitleLabel.Text = audio.Title;
                        SubtitleLabel.Text = Strings.Android.AudioUnknownArtist;
                    }
                    else
                    {
                        TitleLabel.Text = Strings.Android.AudioUnknownTitle;
                        SubtitleLabel.Text = Strings.Android.AudioUnknownArtist;
                    }
                }
            }
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (Playback.Session.PlaybackState == MediaPlaybackState.Playing)
            {
                Playback.Pause();
            }
            else
            {
                Playback.Play();
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            Playback.Clear();
        }

        private async void View_Click(object sender, RoutedEventArgs e)
        {
            var message = Playback.CurrentItem;
            if (message == null)
            {
                return;
            }

            if (message.IsVoice())
            {
                var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
                if (service == null)
                {
                    return;
                }

                service.NavigateToDialog(message.Parent, message.Id);
            }
            else
            {
                await PlaybackView.Current.ShowAsync();
            }
        }
    }
}
