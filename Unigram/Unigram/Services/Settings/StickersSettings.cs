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
        Stickers,
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
            get => (StickersSuggestionMode)(_suggestionMode ??= GetValueOrDefault("SuggestionMode", 0));
            set => AddOrUpdateValue(ref _suggestionMode, "SuggestionMode", (int)value);
        }

        public bool? _suggestCustomEmoji;
        public bool SuggestCustomEmoji
        {
            get => _suggestCustomEmoji ??= GetValueOrDefault("SuggestCustomEmoji", true);
            set => AddOrUpdateValue(ref _suggestCustomEmoji, "SuggestCustomEmoji", value);
        }

        private bool? _isLoopingEnabled;
        public bool IsLoopingEnabled
        {
            get => _isLoopingEnabled ??= GetValueOrDefault("IsLoopingEnabled", true);
            set => AddOrUpdateValue(ref _isLoopingEnabled, "IsLoopingEnabled", value);
        }

        private int? _selectedTab;
        public StickersTab SelectedTab
        {
            get => (StickersTab)(_selectedTab ??= GetValueOrDefault("SelectedTab", 2));
            set => AddOrUpdateValue(ref _selectedTab, "SelectedTab", (int)value);
        }

        private int? _skinTone;
        public EmojiSkinTone SkinTone
        {
            get => (EmojiSkinTone)(_skinTone ??= GetValueOrDefault("SkinTone", 0));
            set => AddOrUpdateValue(ref _skinTone, "SkinTone", (int)value);
        }

        private bool? _isSidebarEnabled;
        public bool IsSidebarEnabled
        {
            get => _isSidebarEnabled ??= GetValueOrDefault("IsSidebarEnabled", true);
            set => AddOrUpdateValue(ref _isSidebarEnabled, "IsSidebarEnabled", value);
        }

        private bool? _isPointerOverEnabled;
        public bool IsPointerOverEnabled
        {
            get => _isPointerOverEnabled ??= GetValueOrDefault("IsPointerOverEnabled", true);
            set => AddOrUpdateValue(ref _isPointerOverEnabled, "IsPointerOverEnabled", value);
        }
    }
}
