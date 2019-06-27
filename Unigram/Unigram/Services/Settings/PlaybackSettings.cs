using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            get
            {
                if (_repeatMode == null)
                    _repeatMode = GetValueOrDefault("RepeatMode", 0);

                return (MediaPlaybackAutoRepeatMode)(_repeatMode ?? 0);
            }
            set
            {
                _repeatMode = (int)value;
                AddOrUpdateValue("RepeatMode", (int)value);
            }
        }
    }
}
