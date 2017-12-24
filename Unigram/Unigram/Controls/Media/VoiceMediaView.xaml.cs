using System;
using System.Diagnostics;
using System.Linq;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views;
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace Unigram.Controls.Media
{
    public sealed partial class VoiceMediaView : UserControl
    {
        public TLMessage ViewModel => DataContext as TLMessage;
        public IPlaybackService Playback { get; } = UnigramContainer.Current.ResolveType<IPlaybackService>();

        public VoiceMediaView()
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

            //Slider.AddHandler(PointerPressedEvent, new PointerEventHandler(Slider_PointerPressed), true);
            //Slider.AddHandler(PointerReleasedEvent, new PointerEventHandler(Slider_PointerReleased), true);
            //Slider.ValueChanged += Slider_ValueChanged;
            //Slider.AddHandler(PointerCaptureLostEvent, new PointerEventHandler(Slider_PointerReleased), true);
            //Slider.AddHandler(PointerCanceledEvent, new PointerEventHandler(Slider_PointerReleased), true);
        }

        //private bool _pressed;

        //private void Slider_PointerPressed(object sender, PointerRoutedEventArgs e)
        //{
        //    _pressed = true;
        //}

        //private void Slider_PointerReleased(object sender, PointerRoutedEventArgs e)
        //{
        //    if (Playback.Session.CanSeek)
        //    {
        //        Playback.SetPosition(TimeSpan.FromMilliseconds(Slider.Value));
        //    }

        //    _pressed = false;
        //}

        //private void Slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        //{
        //    if (_pressed)
        //    {
        //        Progress.Value = e.NewValue;
        //    }
        //}

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
                //Slider.IsEnabled = true;

                PlaybackPanel.Visibility = Visibility.Visible;
                PlaybackButton.Glyph = Playback.Session.PlaybackState == MediaPlaybackState.Playing ? "\uE103" : "\uE102";
                UpdatePosition();

                Playback.Session.PositionChanged += OnPositionChanged;
            }
            else
            {
                //Slider.IsEnabled = false;

                PlaybackPanel.Visibility = Visibility.Collapsed;
                UpdateDuration();
            }
        }

        private void UpdatePosition()
        {
            if (DataContext is TLMessage message && Equals(Playback.CurrentItem, message) /*&& !_pressed*/)
            {
                DurationLabel.Text = FormatTime(Playback.Session.Position) + " / " + FormatTime(Playback.Session.NaturalDuration);
                Progress.Maximum = /*Slider.Maximum =*/ Playback.Session.NaturalDuration.TotalMilliseconds;
                Progress.Value = /*Slider.Value =*/ Playback.Session.Position.TotalMilliseconds;
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
                    Progress.Maximum = /*Slider.Maximum =*/ int.MaxValue;
                    Progress.Value = /*Slider.Value =*/ message.IsMediaUnread && !message.IsOut ? int.MaxValue : 0;
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
