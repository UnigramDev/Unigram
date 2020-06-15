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

        private bool? _isLoopingEnabled;
        public bool IsLoopingEnabled
        {
            get
            {
                if (_isLoopingEnabled == null)
                    _isLoopingEnabled = GetValueOrDefault("IsLoopingEnabled", true);

                return _isLoopingEnabled ?? true;
            }
            set
            {
                _isLoopingEnabled = value;
                AddOrUpdateValue("IsLoopingEnabled", value);
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

        private bool? _isSidebarEnabled;
        public bool IsSidebarEnabled
        {
            get
            {
                if (_isSidebarEnabled == null)
                    _isSidebarEnabled = GetValueOrDefault("IsSidebarEnabled", true);

                return _isSidebarEnabled ?? true;
            }
            set
            {
                _isSidebarEnabled = value;
                AddOrUpdateValue("IsSidebarEnabled", value);
            }
        }

        private bool? _isPointerOverEnabled;
        public bool IsPointerOverEnabled
        {
            get
            {
                if (_isPointerOverEnabled == null)
                    _isPointerOverEnabled = GetValueOrDefault("IsPointerOverEnabled", true);

                return _isPointerOverEnabled ?? true;
            }
            set
            {
                _isPointerOverEnabled = value;
                AddOrUpdateValue("IsPointerOverEnabled", value);
            }
        }
    }
}
