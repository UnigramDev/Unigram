using Unigram.Common;
using Windows.Storage;

namespace Unigram.Services.Settings
{
    public enum StickersSuggestionMode
    {
        All,
        Installed,
        None
    }

    public enum StickersTab
    {
        Emoji,
        Animations,
        Stickers
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

        private int? _selectedTab;
        public StickersTab SelectedTab
        {
            get
            {
                if (_selectedTab == null)
                    _selectedTab = GetValueOrDefault("SelectedTab", 2);

                return (StickersTab)(_selectedTab ?? 2);
            }
            set
            {
                _selectedTab = (int)value;
                AddOrUpdateValue("SelectedTab", (int)value);
            }
        }

        private int? _skinTone;
        public EmojiSkinTone SkinTone
        {
            get
            {
                if (_skinTone == null)
                    _skinTone = GetValueOrDefault("SkinTone", 0);

                return (EmojiSkinTone)(_skinTone ?? 0);
            }
            set
            {
                _skinTone = (int)value;
                AddOrUpdateValue("SkinTone", (int)value);
            }
        }
    }
}
