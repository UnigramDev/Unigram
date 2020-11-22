using Windows.Media;
using Windows.Storage;

namespace Unigram.Services.Settings
{
    public class PlaybackSettings : SettingsServiceBase
    {
        public PlaybackSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private int? _repeatMode;
        public MediaPlaybackAutoRepeatMode RepeatMode
        {
            get => (MediaPlaybackAutoRepeatMode)(_repeatMode ??= GetValueOrDefault("RepeatMode", 0));
            set => AddOrUpdateValue(ref _repeatMode, "RepeatMode", (int)value);
        }
    }
}
