using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Common;
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

            Playback.Session.PlaybackStateChanged += OnPlaybackStateChanged;
            UpdateGlyph();
        }

        private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void UpdateGlyph()
        {
            PlaybackButton.Glyph = Playback.Session.PlaybackState == MediaPlaybackState.Playing ? "\uE103" : "\uE102";
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

        private void View_Click(object sender, RoutedEventArgs e)
        {
            var message = Playback.CurrentItem;
            if (message == null)
            {
                return;
            }

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service == null)
            {
                return;
            }

            service.NavigateToDialog(message.Parent, message.Id);
        }

        #region Binding

        private string ConvertFrom(TLMessage message)
        {
            if (message != null)
            {
                var with = message.Participant;
                return with is TLUser user && user.IsSelf ? "You" : with?.DisplayName;
            }

            return null;
        }

        private string ConvertDate(TLMessage message)
        {
            if (message != null)
            {
                var date = BindConvert.Current.DateTime(message.Date);
                return string.Format("{0} at {1}", date.Date == DateTime.Now.Date ? "Today" : BindConvert.Current.ShortDate.Format(date), BindConvert.Current.ShortTime.Format(date));
            }

            return null;
        }

        #endregion
    }
}
