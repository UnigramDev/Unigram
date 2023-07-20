//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Storage;

namespace Telegram.Services.Settings
{
    public class PlaybackSettings : SettingsServiceBase
    {
        public PlaybackSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private int? _repeatMode;
        public PlaybackRepeatMode RepeatMode
        {
            get => (PlaybackRepeatMode)(_repeatMode ??= GetValueOrDefault("RepeatMode", 0));
            set => AddOrUpdateValue(ref _repeatMode, "RepeatMode", (int)value);
        }

        private double? _playbackRate;
        public double PlaybackRate
        {
            get => _playbackRate ??= GetValueOrDefault("PlaybackRate", 1.0);
            set => AddOrUpdateValue(ref _playbackRate, "PlaybackRate", value);
        }
    }
}
