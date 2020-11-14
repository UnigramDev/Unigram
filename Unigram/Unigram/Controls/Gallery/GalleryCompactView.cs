using System;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.ViewService;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Gallery
{
    public class GalleryCompactView : MediaPlayerElement, IHandle<UpdateFile>
    {
        private readonly IEventAggregator _aggregator;
        private readonly ViewLifetimeControl _lifetime;

        private MediaPlayer _mediaPlayer;
        private RemoteFileStream _fileStream;

        public GalleryCompactView(IEventAggregator aggregator, ViewLifetimeControl lifetime, MediaPlayer player, RemoteFileStream fileStream)
        {
            _aggregator = aggregator;
            _lifetime = lifetime;

            _mediaPlayer = player;
            _fileStream = fileStream;

            _aggregator.Subscribe(this);

            RequestedTheme = ElementTheme.Dark;
            TransportControls = new MediaTransportControls
            {
                IsCompact = true,
                IsCompactOverlayButtonVisible = false,
                IsFastForwardButtonVisible = false,
                IsFastRewindButtonVisible = false,
                IsFullWindowButtonVisible = false,
                IsNextTrackButtonVisible = false,
                IsPlaybackRateButtonVisible = false,
                IsPreviousTrackButtonVisible = false,
                IsRepeatButtonVisible = false,
                IsSkipBackwardButtonVisible = false,
                IsSkipForwardButtonVisible = false,
                IsVolumeButtonVisible = false,
                IsStopButtonVisible = false,
                IsZoomButtonVisible = false,
            };
            AreTransportControlsEnabled = true;

            SetMediaPlayer(player);

            lifetime.Released += OnReleased;
        }

        public void Handle(UpdateFile update)
        {
            _fileStream?.UpdateFile(update.File);
        }

        private void OnReleased(object sender, EventArgs e)
        {
            _aggregator.Unsubscribe(this);

            if (_mediaPlayer != null)
            {
                _mediaPlayer.Source = null;
                //_mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            if (_fileStream != null)
            {
                _fileStream.Dispose();
                _fileStream = null;
            }
        }
    }
}
