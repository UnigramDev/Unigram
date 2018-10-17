using Windows.Storage;

namespace Unigram.Services.Settings
{
    public enum StickersSuggestionMode
    {
        All,
        Installed,
        None
    }

    public class StickersSettings : SettingsServiceBase
    {
        public StickersSettings(ApplicationDataContainer container)
            : base(container)
        {

        }

        private int? _suggestionMode;
        public StickersSuggestionMode SuggestionMode
        {
            get
            {
                if (_suggestionMode == null)
                    _suggestionMode = GetValueOrDefault("SuggestionMode", 0);

                return (StickersSuggestionMode)(_suggestionMode ?? 0);
            }
            set
            {
                _suggestionMode = (int)value;
                AddOrUpdateValue("SuggestionMode", (int)value);
            }
        }
    }
}
