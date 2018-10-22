using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unigram.Common;
using Unigram.Services;
using Unigram.ViewModels;
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

namespace Unigram.Controls.Media
{
    public sealed partial class MusicPlaybackItem : UserControl
    {
        public MessageViewModel ViewModel => DataContext as MessageViewModel;
        public IPlaybackService Playback { get; } = TLContainer.Current.Resolve<IPlaybackService>();

        public MusicPlaybackItem()
        {
            InitializeComponent();

            DataContextChanged += (s, args) =>
            {
                if (ViewModel == null)
                {
                    return;
                }

                Loading?.Invoke(s, null);
                UpdateGlyph();
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Playback.PropertyChanged += OnCurrentItemChanged;
            Playback.PlaybackStateChanged += OnPlaybackStateChanged;

            UpdateGlyph();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Playback.PropertyChanged -= OnCurrentItemChanged;
            Playback.PlaybackStateChanged -= OnPlaybackStateChanged;
        }

        private void OnCurrentItemChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateGlyph();
            UpdateDuration();
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void UpdateGlyph()
        {
            //if (DataContext is TLMessage message && Equals(Playback.CurrentItem, message))
            //{
            //    PlaybackPanel.Visibility = Visibility.Visible;
            //    PlaybackButton.Glyph = Playback.Session.PlaybackState == MediaPlaybackState.Playing ? "\uE103" : "\uE102";
            //    UpdateDuration();
            //}
            //else
            //{
            //    PlaybackPanel.Visibility = Visibility.Collapsed;
            //    UpdateDuration();
            //}
        }

        private void UpdateDuration()
        {
            //if (DataContext is TLMessage message && message.Media is TLMessageMediaDocument mediaDocument && mediaDocument.Document is TLDocument document)
            //{
            //    var audio = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
            //    if (audio == null)
            //    {
            //        return;
            //    }

            //    if (audio.HasPerformer && audio.HasTitle)
            //    {
            //        TitleLabel.Text = audio.Title;
            //        SubtitleLabel.Text = audio.Performer;
            //    }
            //    else if (audio.HasPerformer && !audio.HasTitle)
            //    {
            //        TitleLabel.Text = Strings.Resources.AudioUnknownTitle;
            //        SubtitleLabel.Text = audio.Performer;
            //    }
            //    else if (audio.HasTitle && !audio.HasPerformer)
            //    {
            //        TitleLabel.Text = audio.Title;
            //        SubtitleLabel.Text = Strings.Resources.AudioUnknownArtist;
            //    }
            //    else
            //    {
            //        TitleLabel.Text = Strings.Resources.AudioUnknownTitle;
            //        SubtitleLabel.Text = Strings.Resources.AudioUnknownArtist;
            //    }

            //    //DurationLabel.Text = TimeSpan.FromSeconds(audioAttribute.Duration).ToString("mm\\:ss");
            //}
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (Playback.PlaybackState == MediaPlaybackState.Playing)
            {
                Playback.Pause();
            }
            else
            {
                Playback.Play();
            }
        }

        //private bool Equals(TLMessage x, TLMessage y)
        //{
        //    if (x == null || y == null)
        //    {
        //        return false;
        //    }

        //    return x.Id == y.Id && x.ToId.Equals(y.ToId);
        //}

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;
    }
}
