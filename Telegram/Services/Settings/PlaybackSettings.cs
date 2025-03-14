//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Windows.Storage;

namespace Telegram.Services.Settings
{
    public partial class PlaybackSettings : SettingsServiceBase
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

        private double? _audioSpeed;
        public double AudioSpeed
        {
            get => _audioSpeed ??= GetValueOrDefault("PlaybackRate", 1.0);
            set => AddOrUpdateValue(ref _audioSpeed, "PlaybackRate", value);
        }

        private double? _videoSpeed;
        public double VideoSpeed
        {
            get => _videoSpeed ??= GetValueOrDefault("VideoSpeed", 1.0);
            set => AddOrUpdateValue(ref _videoSpeed, "VideoSpeed", value);
        }

        private bool? _highQuality;
        public bool HighQuality
        {
            get => _highQuality ??= GetValueOrDefault("HighQuality", true);
            set => AddOrUpdateValue(ref _highQuality, "HighQuality", value);
        }
    }
}
