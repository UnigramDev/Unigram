using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Unigram.Common;
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

namespace Unigram.Controls.Media
{
    public sealed partial class MusicMediaView : UserControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;
        public IPlaybackService Playback { get; } = UnigramContainer.Current.ResolveType<IPlaybackService>();

        public MusicMediaView()
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
            Playback.Session.PlaybackStateChanged += OnPlaybackStateChanged;

            UpdateGlyph();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Playback.PropertyChanged -= OnCurrentItemChanged;
            Playback.Session.PlaybackStateChanged -= OnPlaybackStateChanged;
            Playback.Session.PositionChanged -= OnPositionChanged;
        }

        private void Download_Click(object sender, TransferCompletedEventArgs e)
        {
            if (DataContext is TLMessage message)
            {
                Playback.Enqueue(message);
            }
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

        private void OnPositionChanged(MediaPlaybackSession sender, object args)
        {
            this.BeginOnUIThread(UpdatePosition);
        }

        private void UpdateGlyph()
        {
            Playback.Session.PositionChanged -= OnPositionChanged;

            if (DataContext is TLMessage message && Equals(Playback.CurrentItem, message))
            {
                PlaybackPanel.Visibility = Visibility.Visible;
                PlaybackButton.Glyph = Playback.Session.PlaybackState == MediaPlaybackState.Playing ? "\uE103" : "\uE102";
                UpdatePosition();

                Playback.Session.PositionChanged += OnPositionChanged;
            }
            else
            {
                PlaybackPanel.Visibility = Visibility.Collapsed;
                UpdateDuration();
            }
        }

        private void UpdatePosition()
        {
            if (DataContext is TLMessage message && Equals(Playback.CurrentItem, message))
            {
                DurationLabel.Text = FormatTime(Playback.Session.Position) + " / " + FormatTime(Playback.Session.NaturalDuration);
            }
        }

        private void UpdateDuration()
        {
            if (DataContext is TLMessage message && message.Media is TLMessageMediaDocument mediaDocument && mediaDocument.Document is TLDocument document)
            {
                var audioAttribute = document.Attributes.OfType<TLDocumentAttributeAudio>().FirstOrDefault();
                if (audioAttribute != null)
                {
                    DurationLabel.Text = FormatTime(TimeSpan.FromSeconds(audioAttribute.Duration));
                }
            }
        }

        private string FormatTime(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return span.ToString("h\\:mm\\:ss");
            }
            else
            {
                return span.ToString("mm\\:ss");
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

        private bool Equals(TLMessage x, TLMessage y)
        {
            if (x == null || y == null)
            {
                return false;
            }

            return x.Id == y.Id && x.ToId.Equals(y.ToId);
        }

        /// <summary>
        /// x:Bind hack
        /// </summary>
        public new event TypedEventHandler<FrameworkElement, object> Loading;
    }
}
