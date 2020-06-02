namespace Unigram.Services.Settings
{
    public class DiagnosticsSettings : SettingsServiceBase
    {
        public DiagnosticsSettings()
            : base("Diagnostics")
        {
        }

        private bool? _playStickers;
        public bool PlayStickers
        {
            get
            {
                if (_playStickers == null)
                    _playStickers = GetValueOrDefault("PlayStickers", true);

                return _playStickers ?? true;
            }
            set
            {
                _playStickers = value;
                AddOrUpdateValue("PlayStickers", value);
            }
        }

        private bool? _cacheStickers;
        public bool CacheStickers
        {
            get
            {
                if (_cacheStickers == null)
                    _cacheStickers = GetValueOrDefault("CacheStickers", true);

                return _cacheStickers ?? true;
            }
            set
            {
                _cacheStickers = value;
                AddOrUpdateValue("CacheStickers", value);
            }
        }
    }
}
