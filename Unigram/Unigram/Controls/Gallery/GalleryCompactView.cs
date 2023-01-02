//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Unigram.Common;
using Unigram.Services.ViewService;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Controls.Gallery
{
    public class GalleryCompactView : MediaPlayerElement
    {
        private readonly ViewLifetimeControl _lifetime;

        private MediaPlayer _mediaPlayer;
        private RemoteFileStream _fileStream;

        public static ViewLifetimeControl Current { get; private set; }

        public GalleryCompactView(ViewLifetimeControl lifetime, MediaPlayer player, RemoteFileStream fileStream)
        {
            _lifetime = lifetime;

            _mediaPlayer = player;
            _fileStream = fileStream;

            Current = lifetime;

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

            lifetime.Closed += OnReleased;
            lifetime.Released += OnReleased;
        }

        public void Update(MediaPlayer player, RemoteFileStream fileStream)
        {
            Dispose();

            _mediaPlayer = player;
            _fileStream = fileStream;

            SetMediaPlayer(player);
        }

        private void OnReleased(object sender, EventArgs e)
        {
            _lifetime.Closed -= OnReleased;
            _lifetime.Released -= OnReleased;

            Current = null;

            Dispose();
        }

        private void Dispose()
        {
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
